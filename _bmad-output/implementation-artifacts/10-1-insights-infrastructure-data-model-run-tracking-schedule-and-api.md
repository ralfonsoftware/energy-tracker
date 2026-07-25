---
baseline_commit: e45af936f0745c1a33100f29c958dfb75dc56384
---

# Story 10.1: Insights Infrastructure — Data Model, Run Tracking, Schedule & API

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want the app to automatically discover insights every night and let me trigger a refresh manually, with prior insights staying visible while a new run completes,
So that I always see the most recent findings and never land on an empty screen while a run is in progress.

## Acceptance Criteria

1. **EF Core migrations for `InsightRuns` and `Insights`.** `InsightRunConfiguration` defines `RunId` (guid PK), `FlatId` (FK, cascade delete), `Status` (enum: Pending/Processing/Complete/Failed), `StartedAt` (datetimeoffset), `CompletedAt` (nullable datetimeoffset). `InsightConfiguration` defines `InsightId` (guid PK), `FlatId` (FK, cascade delete), `RunId` (FK, set-null on run delete), `Type` (enum: Standby/Replacement/Budget/InvoiceDeviation), `DeviceId` (nullable guid FK), `Data` (nvarchar(max) JSON column), `CreatedAt` (datetimeoffset); index on `(FlatId, Type, CreatedAt desc)`. Zero Data Annotation attributes on any entity class.

2. **`ScheduledInsightsFunction.cs`** with `[TimerTrigger("0 0 2 * * *")]`. When it fires at 02:00 UTC, it queries all `FlatId` values for active users; for each flat it enqueues a discovery message containing `{ flatId, runId }` onto the insights Azure Storage queue using Managed Identity; no HTTP response — fire-and-forget.

3. **`POST /api/v1/flats/{flatId}/insights/trigger`.** `TriggerInsightsFunction.RunAsync` creates a new `InsightRun` with `Status = Pending` and saves it; enqueues a discovery message `{ flatId, runId }`; returns HTTP 202 with `{ runId }`; tenant check enforces flatId belongs to authenticated userId (HTTP 403 on mismatch); if a run with `Status = Pending` or `Processing` already exists for this flatId, the existing `runId` is returned with HTTP 202 (no duplicate runs).

4. **`ProcessInsightsFunction.cs`** with `[QueueTrigger]`. When a discovery message is dequeued, `InsightRun.Status` is set to `Processing`; all four detectors are called in sequence: `StandbyDetector`, `ReplacementDetector`, `BudgetAlertDetector`, `InvoiceDeviationDetector`; each detector's findings are written as `Insight` rows; on successful completion, `InsightRun.Status = Complete` and `CompletedAt` is set; on unhandled exception, `InsightRun.Status = Failed`; detector errors are logged to Application Insights but do not suppress other detectors — each runs independently.

5. **`GET /api/v1/flats/{flatId}/insights`.** `GetInsightsFunction.RunAsync` returns HTTP 200 with `{ runStatus: { status, startedAt, completedAt? }, insights: [...] }` where `insights` is all `Insight` rows for the flat sorted by `CreatedAt desc`; the most recent run's status is included regardless of whether it is still running; tenant check applied; TanStack Query cache key (for Story 10.4): `['insights', flatId]`.

6. **`InsightModels.cs`.** `InsightsResponse` (C# record) has `RunStatus` (`RunStatusDto`: status, startedAt, completedAt) and `Insights` (list of `InsightDto`). `InsightDto` has: `insightId` (guid), `type` (string enum), `deviceId` (nullable guid), `data` (raw JSON passthrough — serialized as-is to client), `createdAt` (datetimeoffset). No Data Annotation attributes.

7. **Gap found during story creation — cascade-delete extension.** `LoadFlatCascadeChildrenAsync` (`api/Shared/AppDbContextExtensions.cs`) is extended to load `InsightRuns` and `Insights` for the flat before a `Flat` is deleted, exactly like every other Flat-scoped child table. `DeleteFlatFunctionTests.cs` gains assertions that both tables are empty after a flat delete. Per the Epic 9 retrospective, this must land in this story — `InsightRuns`/`Insights` didn't exist before Epic 10, so it could not have been done earlier, and every other Flat-scoped table already has this coverage. Without it, deleting a Flat with insight history throws an unhandled FK-constraint `DbUpdateException` under the real SQL Server provider (the InMemory test provider used elsewhere in this codebase would not even catch the gap, since it doesn't enforce FK constraints — this is a real-provider-only failure mode).

## Tasks / Subtasks

- [x] Task 1: Data model (AC: #1, #7)
  - [x] Add `InsightRun` and `Insight` entity classes to `api/Data/Entities/` (classes, not records — EF Core convention)
  - [x] Add `InsightRunConfiguration.cs` and `InsightConfiguration.cs` to `api/Data/Configurations/`
  - [x] Add `DbSet<InsightRun> InsightRuns` and `DbSet<Insight> Insights` to `AppDbContext.cs`
  - [x] Generate migration `AddInsightsTables` (run `dotnet ef migrations list` first to confirm it lands after `20260719122743_AddOptimisticConcurrencyRowVersions`)
  - [x] Extend `LoadFlatCascadeChildrenAsync` in `api/Shared/AppDbContextExtensions.cs` for both new tables
  - [x] Extend `DeleteFlatFunctionTests.cs` with an `InsightRun`+`Insight` cascade-delete assertion, following the file's existing per-table pattern
- [x] Task 2: DTOs and stub detectors (AC: #4, #6)
  - [x] Add `InsightModels.cs` with `InsightsResponse`, `RunStatusDto`, `InsightDto`, and the internal queue message record
  - [x] Add four detector classes (`StandbyDetector.cs`, `ReplacementDetector.cs`, `BudgetAlertDetector.cs`, `InvoiceDeviationDetector.cs`) in `api/Features/Insights/` with the agreed `DetectAsync(Guid flatId, Guid runId, CancellationToken ct)` signature; bodies are no-ops in this story (Stories 10.2/10.3 fill in the real algorithms against this exact signature — do not change it later without checking `ProcessInsightsFunction`'s call sites)
  - [x] Register all four detectors `AddScoped` in `Program.cs` (they take `AppDbContext`, matching `EveHomeParser`/`MerossParser`/`InterpolationEngine`/`ReconciliationEngine`)
- [x] Task 3: Queue infrastructure (AC: #2, #3, #4)
  - [x] Register a `QueueServiceClient` singleton in `Program.cs`, mirroring the existing `BlobServiceClient` registration (same `storageAccountName`/`storageCredential` variables) — **must** set `QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 }` (see Dev Notes — SDK v12 default does not match what the Functions QueueTrigger binding expects)
  - [x] Use the literal queue name `"insight-discovery"` everywhere (see Dev Notes — the epic text says `insights-discovery`, but the already-provisioned Bicep resource is `insight-discovery`)
  - [x] `TriggerInsightsFunction.cs`: `POST v1/flats/{flatId}/insights/trigger`
  - [x] `ScheduledInsightsFunction.cs`: `[TimerTrigger("0 0 2 * * *")]`
  - [x] `ProcessInsightsFunction.cs`: `[QueueTrigger("insight-discovery", Connection = "AzureWebJobsStorage")]`
- [x] Task 4: Read API (AC: #5, #6)
  - [x] `GetInsightsFunction.cs`: `GET v1/flats/{flatId}/insights`
- [x] Task 5: Backend tests
  - [x] `TriggerInsightsFunctionTests.cs`: 202 + runId on first trigger; 202 + same runId returned on duplicate trigger while Pending/Processing; 403 on foreign flatId
  - [x] `ProcessInsightsFunctionTests.cs`: Status transitions Pending→Processing→Complete on success; →Failed on unhandled exception; a single detector throwing does not stop the other three from running (assert via mocked/spy detector or a detector that always throws)
  - [x] `ScheduledInsightsFunctionTests.cs`: enqueues one message per existing Flat
  - [x] `GetInsightsFunctionTests.cs`: returns insights sorted `CreatedAt desc`; returns `runStatus: null` when no `InsightRun` exists yet for the flat (see Dev Notes); 403 on foreign flatId
  - [x] Update `DeleteFlatFunctionTests.cs` per Task 1

### Review Findings

- [x] [Review][Patch] Stuck-forever `Pending` run when queue enqueue fails — `TriggerInsightsFunction` and `ScheduledInsightsFunction` both commit the `InsightRun` row (`Status = Pending`) before calling `SendMessageAsync`. If the send throws, the run is left permanently `Pending`, and `TriggerInsightsFunction`'s dedup check then returns that same dead `runId` forever. **Resolution (2026-07-25):** wrap `SendMessageAsync` in try/catch in both functions; on failure, set `Status = Failed` (+`CompletedAt`) before rethrowing/returning, symmetric with `ProcessInsightsFunction`'s existing failure handling. [api/Features/Insights/TriggerInsightsFunction.cs:56-63, api/Features/Insights/ScheduledInsightsFunction.cs:26-34]
- [x] [Review][Patch] TOCTOU race in `TriggerInsightsFunction`'s duplicate-run dedup — the "check for existing Pending/Processing run, else create" sequence has no DB-level uniqueness or transaction isolation. Two concurrent trigger requests for the same flat can both pass the existence check before either commits, producing two `InsightRun` rows and two queue messages. **Resolution (2026-07-25):** add a SQL Server filtered unique index on `(FlatId)` where `Status IN (Pending, Processing)` via `InsightRunConfiguration`, generate a migration for it, and handle the resulting `DbUpdateException` in `TriggerInsightsFunction` as a fallback dedup path (re-query for the now-existing run and return its `runId`). [api/Features/Insights/TriggerInsightsFunction.cs:42-49, api/Data/Configurations/InsightRunConfiguration.cs]
- [x] [Review][Patch] `ProcessInsightsFunction` silently swallows malformed/null queue messages and missing-run lookups — on `JsonException`, null `discoveryMessage`, or `run is null`, the function logs and returns without any DB update. Azure Functions treats a normal return as success, so the message is dequeued and never retried or sent to a poison queue. **Resolution (2026-07-25):** rethrow after logging on `JsonException` and on a null/empty deserialized message (lets Azure's built-in retry + poison-queue mechanism handle genuine producer bugs); keep the existing log-and-return behavior for `run is null` (expected — e.g. the Flat was cascade-deleted while the run was queued — there is no row to update). [api/Features/Insights/ProcessInsightsFunction.cs:19-35]
- [x] [Review][Patch] `ProcessInsightsFunction`'s final `SaveChangesAsync` sits outside the try/catch — `run.CompletedAt = ...; await db.SaveChangesAsync(ct);` runs after the try/catch that sets `Status` to `Complete`/`Failed`. If this save throws, the exception is unhandled, the in-memory status transition is lost, and the Functions runtime retries the whole invocation (re-running all detectors). [api/Features/Insights/ProcessInsightsFunction.cs:75-76]
- [x] [Review][Patch] `RunDetectorSafelyAsync` catches `Exception` unconditionally, including `OperationCanceledException` — on host shutdown/cancellation, it keeps calling the remaining detectors and further DB calls with an already-cancelled token instead of unwinding promptly. [api/Features/Insights/ProcessInsightsFunction.cs:79-88]
- [x] [Review][Patch] `ScheduledInsightsFunction`'s per-flat enqueue loop has no per-iteration isolation — one flat's `SaveChangesAsync`/`SendMessageAsync` failure throws out of the loop, aborting the scheduled run for every subsequent flat in the list instead of just that one. [api/Features/Insights/ScheduledInsightsFunction.cs:23-34]
- [x] [Review][Patch] `GetInsightsFunction`'s `JsonDocument.Parse(i.Data)` is unguarded — a single malformed `Data` row (corruption, future schema drift) throws and 500s the entire insights list, hiding every other insight for that flat too. [api/Features/Insights/GetInsightsFunction.cs:55]
- [x] [Review][Patch] `JsonDocument` returned by `JsonDocument.Parse` in `GetInsightsFunction` is never disposed — a per-request resource leak (pooled buffers not returned promptly) on a list endpoint. [api/Features/Insights/GetInsightsFunction.cs:55]
- [x] [Review][Patch] Queue name literal `"insight-discovery"` is duplicated across three files (`ProcessInsightsFunction`'s `[QueueTrigger]` attribute, `TriggerInsightsFunction`, `ScheduledInsightsFunction`) with no shared constant — a typo in any one silently breaks the pipeline with no compile-time signal. [api/Features/Insights/ProcessInsightsFunction.cs:19, api/Features/Insights/TriggerInsightsFunction.cs:61, api/Features/Insights/ScheduledInsightsFunction.cs:25]
- [x] [Review][Patch] `JsonSerializerOptions` camelCase instance is defined independently in three files (`ProcessInsightsFunction`, `ScheduledInsightsFunction`, `TriggerInsightsFunction`) instead of a single shared constant for `InsightDiscoveryMessage` (de)serialization. [api/Features/Insights/ProcessInsightsFunction.cs:16, api/Features/Insights/ScheduledInsightsFunction.cs:14, api/Features/Insights/TriggerInsightsFunction.cs:17]
- [x] [Review][Patch] `ProcessInsightsFunctionTests.RunAsync_AllDetectorsSucceed_TransitionsPendingToProcessingToComplete` only asserts the final `Complete` status — the test name promises verification of the intermediate `Processing` transition that isn't actually checked. [api.Tests/Features/Insights/ProcessInsightsFunctionTests.cs:496-513]
- [x] [Review][Patch] `TriggerInsightsFunction`'s read-only `Flats`/`InsightRuns` queries omit `AsNoTracking()`, unlike the equivalent queries in `GetInsightsFunction` — minor unnecessary change-tracking overhead. [api/Features/Insights/TriggerInsightsFunction.cs:36-49]
- [x] [Review][Defer] No idempotency guard against duplicate detector execution on queue-message redelivery — `run.Status = Processing` is committed before detectors run; if the Functions host is killed at that point, the queue-trigger retry mechanism re-invokes `RunAsync` with the same message and nothing prevents the (currently no-op) detectors from re-running and, once real logic lands, re-inserting duplicate `Insight` rows. Not yet an active bug since all four detectors are no-ops in this story — the guard can't be correctly designed until the real detector write pattern from Stories 10.2/10.3 exists. `blocks: Story 10.2, Story 10.3` [api/Features/Insights/ProcessInsightsFunction.cs:43-46] — deferred, no detector currently writes data so no duplication is possible yet; revisit when Story 10.2/10.3 add real `Insight` inserts.

## Dev Notes

### Critical corrections to the epic text (verified against current code/infra)

- **Queue name mismatch:** The epic's code snippet says `[QueueTrigger("insights-discovery")]`, but `infra/main.bicep:54` already provisions `var insightQueueName = 'insight-discovery'` (no "s" on "insight"). The queue is real infrastructure, already deployed — use `"insight-discovery"` literally in `TriggerInsightsFunction`, `ScheduledInsightsFunction`, and `ProcessInsightsFunction`'s `[QueueTrigger]` attribute. If the enqueue side and the trigger side don't use the identical literal string, `ProcessInsightsFunction` silently never fires.
- **Message encoding gotcha (Azure.Storage.Queues v12 vs Functions QueueTrigger):** `QueueClient`/`QueueServiceClient` v12 (this project's version, 12.27.1) does **not** base64-encode messages by default — that was v11-only behavior. The Azure Functions QueueTrigger binding, however, still expects a base64-encoded message body and decodes it before binding to your `string` parameter. If the SDK client sends plain text, the trigger will fail to process it correctly. Construct the `QueueServiceClient` with `new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 }` — sub-`QueueClient`s obtained via `GetQueueClient(name)` inherit this option automatically, so `SendMessageAsync(jsonString)` just works. [Source: Azure Queue Storage trigger for Azure Functions docs, Azure.Storage.Queues v12.27.1 README "Message encoding" section]
- **`TariffResolver.cs` no longer exists.** Do not recreate it and do not follow `architecture.md`'s file tree (line 789), which still lists it — it was deleted during the Epic 9 retrospective cleanup (confirmed: `find api -iname "*TariffResolver*"` returns nothing today). Not relevant to detector *logic* in this story (that's Stories 10.2/10.3's concern), but relevant if you're tempted to follow the stale architecture doc's file tree literally.

### Data model — nullable FK delete-behavior (not spelled out by the epic)

- `Insight.RunId` → `InsightRun`: `DeleteBehavior.SetNull` (epic-specified).
- `Insight.DeviceId` → `Device`: **must also be `DeleteBehavior.SetNull`, not `Cascade`.** `Device` already has a cascade path from `Flat` (`Flat`→`Room`→`PowerPoint`→`Device`, all `Cascade` per `RoomConfiguration.cs`/`PowerPointConfiguration.cs`/`DeviceConfiguration.cs`), and `Insight` already has a *direct* cascade path from `Flat` via `Insight.FlatId`. If `Insight.DeviceId` were also `Cascade`, deleting a `Flat` would reach the `Insights` table via two different cascade paths — SQL Server rejects this at migration/deploy time with "may cause cycles or multiple cascade paths." `SetNull` avoids it (mirrors the same reasoning the epic already applied to `RunId`).
- Both `RunId` and `DeviceId` are the first nullable-FK-with-`SetNull` relationships in this codebase (everything else uses `Cascade`) — there's no existing example file to copy verbatim; the `.OnDelete(DeleteBehavior.SetNull)` call itself follows the identical builder pattern as every other `.HasOne(...).WithMany().HasForeignKey(...).OnDelete(...)` block in `api/Data/Configurations/`.
- `Type` enums stored as `int` by convention (no `HasConversion<string>`) — same as `ImportJob.Status`/`ImportErrorCategory` in `ImportJobConfiguration.cs`.
- Composite index `(FlatId, Type, CreatedAt desc)`: EF Core 10 supports descending indexes via `.HasIndex(...).IsDescending(false, false, true)`. Name it `IX_Insights_FlatId_Type_CreatedAt` per this codebase's `IX_{Table}_{Column(s)}` convention.

### Detector stub contract (necessary sequencing decision, not stated by the epic)

Story 10.1's own AC (#4) requires `ProcessInsightsFunction` to call all four detectors, but the detectors' actual algorithms are Story 10.2's (`StandbyDetector`, `ReplacementDetector`) and Story 10.3's (`BudgetAlertDetector`, `InvoiceDeviationDetector`) deliverables — they don't exist yet. This codebase has **zero existing custom interfaces** (`grep` confirms — everything is concrete classes with primary-constructor DI, per the project's own "don't add abstraction beyond what's needed" convention), so don't introduce an `IInsightDetector` interface. Instead:
- Create the four detector classes now as concrete classes with primary-constructor DI (`AppDbContext db`, plus whatever else `ProcessInsightsFunction` needs to pass), each exposing `public async Task DetectAsync(Guid flatId, Guid runId, CancellationToken ct)`.
- Bodies are no-ops in this story (write zero `Insight` rows, return immediately). Stories 10.2/10.3 replace the bodies with real detection logic against this same signature.
- `ProcessInsightsFunction` calls each detector's `DetectAsync` inside its own try/catch (per AC #4: one detector throwing must not stop the others) and logs failures via `ILogger<ProcessInsightsFunction>` to Application Insights.
- Detectors persist directly via their injected `AppDbContext` (add `Insight` rows + `SaveChangesAsync`) — this matches the established per-engine pattern (`InterpolationEngine.cs` calls `SaveChangesAsync` itself rather than returning data for the caller to persist).
- Register all four `AddScoped` in `Program.cs` (they take `AppDbContext`, same lifetime as `EveHomeParser`/`MerossParser`/`InterpolationEngine`/`ReconciliationEngine`/`DecompositionEngine`).

### "Active users" (AC #2 — not defined elsewhere in the schema)

There is no `IsActive` flag anywhere on `User` or `Flat`. Every `Flat` row belongs to a user who completed onboarding — interpret "active users" as **all `Flat` rows in the database**; enqueue one discovery message per `FlatId`, full stop. Don't invent a new activity-tracking concept for this.

### `GetInsightsFunction` — no-run-yet case (needed for Story 10.4 to work, not spelled out by AC #5)

A brand-new `Flat` (never manually triggered, before the first 02:00 UTC run) has **zero** `InsightRun` rows. `RunStatusDto` must be nullable (`RunStatusDto?`) in `InsightsResponse`, and `GetInsightsFunction` must return `{ runStatus: null, insights: [] }` for this case rather than throwing on a missing "most recent run." Story 10.4's own AC anticipates this exact state ("no completed run exists and fewer than 30 days of readings"). Get the DTO shape right now — this is exactly the kind of contract Story 10.4 will build directly against without revisiting this story's code.

### Standard patterns to follow (established elsewhere in this codebase)

- Tenant check: `context.GetUserId()` first line, then `db.Flats.SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId, ct)` → 403 Problem Details if null. `ProcessInsightsFunction`/`ScheduledInsightsFunction` are non-HTTP triggers — `context.GetUserId()` throws on these (per `FunctionContextExtensions.cs`); `flatId`/`runId` come from the queue message payload instead, exactly like the blob-triggered `ProcessImportFunction` gets `flatId` from its trigger route template instead of calling `GetUserId()`.
- Route templates: `Route = "v1/..."` (never `api/v1/...` — SWA strips `/api`).
- Error responses: anonymous Problem Details objects (`{ title, status, detail }`), no typed class — copy `GetDashboardFunction.cs`/`UploadFunction.cs` verbatim in shape.
- JSON body parsing/serialization: `private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }` for the queue message, matching `ProcessImportFunction.cs`'s `_jsonOptions` field. Response body serialization goes through the global `JsonSerializationDefaults` already wired in `Program.cs` — don't build a second ad-hoc JSON path for `InsightsResponse`.
- Every async method: `CancellationToken ct` last parameter, threaded through every `Async` call.
- `SingleOrDefaultAsync` for the flat-ownership lookup (unique by PK); `FirstOrDefaultAsync`/ordered query for "most recent `InsightRun`" (multiple rows expected, take newest by `StartedAt desc`).
- `GetInsightsFunction` is read-only: use `.AsNoTracking()` on the flat/insight-run/insight queries, matching `GetDashboardFunction.cs`'s convention for GET endpoints.

### Local dev — known, accepted gap (don't try to fix it here)

Same pattern already documented for `UploadFunction`'s blob writes (`deferred-work.md`, "6-6" entry): the `QueueServiceClient` you register uses Managed Identity against the **real** `energytrackerstorage` account, while `[QueueTrigger(..., Connection = "AzureWebJobsStorage")]` on `ProcessInsightsFunction` reads from **Azurite** locally. A local `func start` run can enqueue a message that the local trigger will never see (and vice versa) unless a developer temporarily grants themselves `Storage Queue Data Contributor` on the real account. This is a pre-existing project-wide asymmetry, not something to solve in this story.

### Project Structure Notes

New files (all under existing, already-scaffolded `api/Features/Insights/` and `api.Tests/Features/Insights/` — both currently contain only a `.gitkeep`):
- `api/Data/Entities/InsightRun.cs`, `Insight.cs`
- `api/Data/Configurations/InsightRunConfiguration.cs`, `InsightConfiguration.cs`
- `api/Features/Insights/InsightModels.cs`, `TriggerInsightsFunction.cs`, `ScheduledInsightsFunction.cs`, `ProcessInsightsFunction.cs`, `GetInsightsFunction.cs`, `StandbyDetector.cs`, `ReplacementDetector.cs`, `BudgetAlertDetector.cs`, `InvoiceDeviationDetector.cs`
- `api.Tests/Features/Insights/TriggerInsightsFunctionTests.cs`, `ProcessInsightsFunctionTests.cs`, `ScheduledInsightsFunctionTests.cs`, `GetInsightsFunctionTests.cs`
- `api/Data/Migrations/{timestamp}_AddInsightsTables.cs` (generated, never hand-edited)

Modified files:
- `api/Data/AppDbContext.cs` (two new `DbSet`s)
- `api/Shared/AppDbContextExtensions.cs` (cascade-delete extension, AC #7)
- `api/Program.cs` (QueueServiceClient registration + 4 detector DI registrations)
- `api.Tests/Features/Flats/DeleteFlatFunctionTests.cs` (AC #7 coverage)

No frontend changes in this story — `InsightsTab.tsx`, `useInsights`, `InsightCard.tsx` etc. are Story 10.4's scope. `client/src/features/insights/InsightsPage.tsx` (currently a placeholder `<div>Insights</div>`) is untouched here.

Follows `api/Features/{Feature}/` VSA slice convention exactly like every prior epic — no deviation.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-10-actionable-insights.md#Story 10.1] — epic ACs (verbatim basis for ACs #1–#6 above)
- [Source: _bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md#FR-38, #FR-39] — scheduled/manual discovery + progress indicator requirements
- [Source: _bmad-output/planning-artifacts/architecture.md:736-745] — `Insights/` file tree (note: still lists `TariffResolver.cs` at line 789, which is stale — see Dev Notes)
- [Source: _bmad-output/planning-artifacts/architecture.md:853-862] — UJ-3 Insight Discovery data-flow scenario
- [Source: infra/main.bicep:53-134] — already-provisioned `insight-discovery` queue and `insightQueue` resource (verified queue name against actual Bicep, not the epic's prose)
- [Source: api/Program.cs] — `BlobServiceClient` DI registration pattern to mirror for `QueueServiceClient`
- [Source: api/Features/SmartPlugImport/ProcessImportFunction.cs, UploadFunction.cs] — blob-trigger/Tier-3-async patterns to mirror for the queue/timer triggers
- [Source: api/Features/Flats/DeleteFlatFunction.cs, api/Shared/AppDbContextExtensions.cs] — cascade-delete pattern for AC #7
- [Source: _bmad-output/implementation-artifacts/deferred-work.md, "6-6" entry] — pre-existing local-dev Managed-Identity-vs-Azurite asymmetry, replicated (not newly introduced) by this story's queue client
- [Memory: Epic 9 retro / Epic 10 prep — cascade-delete extension must land in Story 10.1, TariffResolver already removed]

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- `dotnet ef migrations list` (api/) confirmed `20260719122743_AddOptimisticConcurrencyRowVersions` as the prior head before generating `AddInsightsTables`.
- `dotnet build` clean after each task; final full-suite `dotnet test api.Tests` run: 393/393 passed, 0 regressions.
- Added two new NuGet packages required to implement the ACs' Timer/Queue triggers (not previously referenced in `energy-tracker-api.csproj`): `Microsoft.Azure.Functions.Worker.Extensions.Storage.Queues` 5.5.4 and `Microsoft.Azure.Functions.Worker.Extensions.Timer` 4.3.1 — both are the latest versions compatible with this project's `Microsoft.Azure.Functions.Worker` 2.52.0.
- Used reflection against the installed `Azure.Storage.Queues`/`Microsoft.Azure.Functions.Worker.Extensions.Timer` assemblies to confirm `QueueClient`/`QueueServiceClient`/`TimerInfo` all expose protected parameterless constructors and virtual members, making them Moq-mockable exactly like the existing `BlobServiceClient` pattern in `UploadFunctionTests.cs`.

### Completion Notes List

- Data model (AC #1, #7): `InsightRun`/`Insight` entities and configurations added; migration `AddInsightsTables` generated and verified against the model snapshot. `Insight.RunId` and `Insight.DeviceId` both use `DeleteBehavior.SetNull` per Dev Notes (avoids SQL Server's multiple-cascade-path rejection). `LoadFlatCascadeChildrenAsync` extended for both new tables; `DeleteFlatFunctionTests` gained a cascade-delete assertion following the file's existing seed-via-separate-context pattern.
- DTOs/stub detectors (AC #4, #6): `InsightModels.cs` added. `InsightDto.Data` is typed `JsonElement` (not `string`) so the detector's stored JSON is embedded as-is in the response rather than double-encoded — required by AC #6's "raw JSON passthrough." `RunStatusDto.Status`/`InsightDto.Type` use the entity enums directly (not `string`), consistent with `ImportJobStatusResponse`'s convention — the app's global `JsonStringEnumConverter` (wired in `Program.cs`) renders them as strings on the wire. Four detector stubs added with the agreed `DetectAsync(flatId, runId, ct)` signature, bodies are no-ops; `DetectAsync` is `virtual` (not part of an interface — codebase has none) purely so `ProcessInsightsFunctionTests` can subclass one detector to force a failure for the "one detector throws" test. All four registered `AddScoped` in `Program.cs`.
- Queue infrastructure (AC #2, #3, #4): `QueueServiceClient` singleton registered in `Program.cs` mirroring the `BlobServiceClient` pattern, with `QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 }` per the Dev Notes SDK-v12-vs-Functions-trigger gotcha. Queue name `"insight-discovery"` used literally everywhere (matches provisioned Bicep resource, not the epic's prose). `TriggerInsightsFunction` creates a `Pending` `InsightRun` and enqueues, with the Pending/Processing dedup check per AC #3. `ScheduledInsightsFunction` fires nightly, treats every `Flat` row as an "active user" (no `IsActive` flag exists in the schema), and creates one `Pending` `InsightRun` + one queue message per flat. `ProcessInsightsFunction` transitions `Processing` → `Complete`/`Failed`; each of the four detector calls is wrapped in its own try/catch so one detector's failure never blocks the others (AC #4) — the run only transitions to `Failed` on an exception *outside* those four guarded calls (e.g. persisting a status transition), which is a distinct, deliberately separate failure path from detector isolation.
- Read API (AC #5, #6): `GetInsightsFunction` returns `runStatus: null` + `insights: []` for a flat with zero `InsightRun` rows (the pre-first-run state Story 10.4 depends on), otherwise the most recent run's status regardless of whether it's still running, plus all `Insight` rows sorted `CreatedAt desc`. Uses `.AsNoTracking()` throughout per the codebase's GET-endpoint convention.
- All 393 backend tests pass (16 new for this story's four Functions + the extended `DeleteFlatFunctionTests`); no regressions in the existing suite.

### File List

**New:**
- `api/Data/Entities/InsightRun.cs`
- `api/Data/Entities/Insight.cs`
- `api/Data/Configurations/InsightRunConfiguration.cs`
- `api/Data/Configurations/InsightConfiguration.cs`
- `api/Data/Migrations/20260725130437_AddInsightsTables.cs`
- `api/Data/Migrations/20260725130437_AddInsightsTables.Designer.cs`
- `api/Features/Insights/InsightModels.cs`
- `api/Features/Insights/StandbyDetector.cs`
- `api/Features/Insights/ReplacementDetector.cs`
- `api/Features/Insights/BudgetAlertDetector.cs`
- `api/Features/Insights/InvoiceDeviationDetector.cs`
- `api/Features/Insights/TriggerInsightsFunction.cs`
- `api/Features/Insights/ScheduledInsightsFunction.cs`
- `api/Features/Insights/ProcessInsightsFunction.cs`
- `api/Features/Insights/GetInsightsFunction.cs`
- `api.Tests/Features/Insights/TriggerInsightsFunctionTests.cs`
- `api.Tests/Features/Insights/ProcessInsightsFunctionTests.cs`
- `api.Tests/Features/Insights/ScheduledInsightsFunctionTests.cs`
- `api.Tests/Features/Insights/GetInsightsFunctionTests.cs`

**Modified:**
- `api/Data/AppDbContext.cs` (two new `DbSet`s)
- `api/Data/Migrations/AppDbContextModelSnapshot.cs` (regenerated by `dotnet ef migrations add`)
- `api/Shared/AppDbContextExtensions.cs` (`LoadFlatCascadeChildrenAsync` extended for `InsightRuns`/`Insights`)
- `api/Program.cs` (`QueueServiceClient` DI registration + four detector DI registrations)
- `api/energy-tracker-api.csproj` (added `Microsoft.Azure.Functions.Worker.Extensions.Storage.Queues` 5.5.4, `Microsoft.Azure.Functions.Worker.Extensions.Timer` 4.3.1)
- `api.Tests/Features/Flats/DeleteFlatFunctionTests.cs` (AC #7 cascade-delete coverage for `InsightRun`/`Insight`)

## Change Log

| Date | Change |
|---|---|
| 2026-07-25 | Story implemented: Insights data model, run tracking, nightly schedule, manual trigger, and read API (Tasks 1–5, all ACs). |
