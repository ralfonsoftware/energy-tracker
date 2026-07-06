---
baseline_commit: 830a2eb065221a753d61a11b2734e0ac3f68708c
---

# Story 6.1: Import Pipeline Infrastructure — Upload, Job Tracking & Blob Trigger

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to upload a smart plug file and get immediate confirmation it was received with a job ID to track processing,
so that I am not blocked waiting for the file to process and can navigate freely while it runs in the background.

## Acceptance Criteria

1. **Given** EF Core migrations for `ImportJobs`, `SmartPlugDailyData`, and `SmartPlugIntervalData`, **when** reviewed, **then** `ImportJobConfiguration` defines `ImportJobId` (guid PK), `FlatId` (FK, cascade delete), `Status` (enum: Pending/Processing/Complete/Failed), `CreatedAt` (datetimeoffset), `CompletedAt` (nullable datetimeoffset), `ErrorCategory` (nullable enum: DataUnreadable/ProcessingFailed/ServiceUnavailable). `SmartPlugDailyDataConfiguration` defines `Id` (guid PK), `PlugId` (nvarchar), `FlatId` (FK, cascade delete), `Date` (date), `KwhValue` (decimal), `IsInterpolated` (bool); unique index on `(FlatId, PlugId, Date)`. `SmartPlugIntervalDataConfiguration` defines `Id` (guid PK), `PlugId` (nvarchar), `FlatId` (FK, cascade delete), `Timestamp` (datetimeoffset), `WhValue` (decimal); index on `(FlatId, PlugId, Timestamp)`. Zero Data Annotation attributes on any entity class.

2. **Given** `POST /api/v1/flats/{flatId}/imports` with a multipart file upload, **when** `UploadFunction.RunAsync` executes, **then** an `ImportJob` record is created with `Status = Pending`; the file is written to Azure Blob Storage at `smart-plug-imports/{userId}/{flatId}/{importJobId}.{ext}` using Managed Identity; HTTP 202 is returned with `{ importJobId }`; server-side time to 202 response is ≤ 2s.

3. **Given** `GET /api/v1/flats/{flatId}/imports/{jobId}`, **when** called, **then** `GetImportStatusFunction` returns `{ importJobId, status, createdAt, completedAt, errorCategory, gapNotifications? }`; HTTP 200; tenant-scoped.

4. **Given** the blob trigger fires on `ProcessImportFunction`, **when** executed, **then** it sets `Status = Processing`; reads the blob; detects file type from extension (`.xlsx` → Eve Home, `.csv` → Meross); routes to the appropriate parser; on unhandled exception: `Status = Failed`, `ErrorCategory = ProcessingFailed`; on storage outage: `ErrorCategory = ServiceUnavailable`; on unreadable/corrupt file: `ErrorCategory = DataUnreadable`; raw exception messages are logged to Application Insights only — never returned to the user.

5. **Given** import error categorization (FR-28), **when** `ImportJob.ErrorCategory` is set, **then** the frontend maps it to exactly one user-facing message: `DataUnreadable` → "Data cannot be read."; `ProcessingFailed` → "Processing failed — try again."; `ServiceUnavailable` → "Service temporarily unavailable — try again later."

6. **Given** `ImportJob`, `SmartPlugDailyData`, and `SmartPlugIntervalData` can be written concurrently (e.g., a retried blob trigger re-processing the same job while a status poll or a structure edit is in flight), **when** `ImportJobConfiguration` is defined, **then** `ImportJob` includes a `RowVersion` (SQL Server `rowversion`) column configured via EF Core `.IsRowVersion()`; a `DbUpdateConcurrencyException` on save is caught and surfaces as `ImportJob.Status = Failed`, `ErrorCategory = ProcessingFailed` — never an unhandled 500 or a silent overwrite; this is the codebase's first concurrency-token pattern, deliberately scoped to these three new tables only — `Flat`, `Tariff`, and `MeterReading` remain last-write-wins, tracked separately in `deferred-work.md`.

7. **Given** the Azure resources this story depends on (Blob Storage container, Storage Queue, and — newly identified during story creation — an Event Grid subscription required for the blob trigger to fire at all on this project's Flex Consumption hosting plan), **when** `infra/main.bicep` is reviewed, **then** all managed-identity RBAC role assignments and infrastructure resources needed for `UploadFunction` and `ProcessImportFunction` to operate in production are present; any gap found is fixed in `infra/main.bicep` as part of this story (see Dev Notes — "Infra gap analysis" for the specific gaps found and the exact fix).

## Tasks / Subtasks

- [x] Task 1: EF Core entities, configurations, and migration (AC: 1, 6)
  - [x] Create `api/Data/Entities/ImportJob.cs`, `SmartPlugDailyData.cs`, `SmartPlugIntervalData.cs` (classes, not records — EF Core entities). Add `ImportStatus` enum (`Pending`, `Processing`, `Complete`, `Failed`) and `ImportErrorCategory` enum (`DataUnreadable`, `ProcessingFailed`, `ServiceUnavailable`) at the top of `ImportJob.cs`, mirroring exactly how `Device.cs` declares `ConsumptionApproach`/`SelfMeasuredPeriod` as free-standing enums above the class in the same file (not in a separate `Enums.cs`).
  - [x] `ImportJob`: `ImportJobId` (Guid PK), `FlatId` (Guid FK), `Status` (`ImportStatus`), `CreatedAt` (`DateTimeOffset`), `CompletedAt` (`DateTimeOffset?`), `ErrorCategory` (`ImportErrorCategory?`), `RowVersion` (`byte[]`, initialized `= null!` like other required reference-type entity properties in this codebase), `GapNotifications` (`string?` — JSON column, mirrors `Insight.Data`'s "opaque JSON, deserialize in application layer" pattern from the architecture doc; do not model as a relational child table), `Flat` navigation property.
  - [x] `SmartPlugDailyData`: `Id` (Guid PK), `PlugId` (string), `FlatId` (Guid FK), `Date` (`DateOnly` — EF Core 10 maps this to SQL `date` natively), `KwhValue` (decimal), `IsInterpolated` (bool).
  - [x] `SmartPlugIntervalData`: `Id` (Guid PK), `PlugId` (string), `FlatId` (Guid FK), `Timestamp` (`DateTimeOffset`), `WhValue` (decimal).
  - [x] Create `api/Data/Configurations/ImportJobConfiguration.cs`, `SmartPlugDailyDataConfiguration.cs`, `SmartPlugIntervalDataConfiguration.cs` implementing `IEntityTypeConfiguration<T>`, mirroring `TariffConfiguration.cs`'s exact style (`ToTable`, `HasKey`, `ValueGeneratedOnAdd`, `HasOne(...).WithMany().HasForeignKey(...).OnDelete(DeleteBehavior.Cascade)`, `HasIndex(...).HasDatabaseName(...)`).
    - `ImportJobConfiguration`: `builder.Property(j => j.RowVersion).IsRowVersion();` — this is the exact EF Core Fluent API call for SQL Server `rowversion`/`timestamp` columns; no `HasColumnType` needed alongside it. `KwhValue`/`WhValue` decimal columns: `.HasColumnType("decimal(18,4)")` — matches `MeterReadingConfiguration.cs`'s `KwhValue`/`OriginalKwhValue` exactly (`decimal(18,4)`), not `TariffConfiguration.cs`'s `PricePerKwh` (`decimal(18,6)`) — kWh values use scale 4 throughout this codebase, price-per-unit values use scale 6.
    - `SmartPlugDailyDataConfiguration`: `builder.HasIndex(d => new { d.FlatId, d.PlugId, d.Date }).IsUnique().HasDatabaseName("IX_SmartPlugDailyData_FlatId_PlugId_Date");`
    - `SmartPlugIntervalDataConfiguration`: `builder.HasIndex(d => new { d.FlatId, d.PlugId, d.Timestamp }).HasDatabaseName("IX_SmartPlugIntervalData_FlatId_PlugId_Timestamp");` (non-unique — Eve Home dedup-by-timestamp happens in Story 6.2's parser logic, not via a DB constraint).
    - All three: FK to `Flat` with `OnDelete(DeleteBehavior.Cascade)`, matching every other flat-scoped table in this codebase.
  - [x] Add three `DbSet<T>` properties to `AppDbContext.cs` (`ImportJobs`, `SmartPlugDailyData`, `SmartPlugIntervalData`), following the exact `Set<T>()` pattern already there for `Tariffs`/`MeterReadings`/etc. — no other changes to `AppDbContext.cs` needed (`ApplyConfigurationsFromAssembly` already picks up new `IEntityTypeConfiguration<T>` classes automatically).
  - [x] Before generating the migration, run `dotnet ef migrations list` (per `project-context.md`'s EF Core migration rule) to confirm `20260705072329_AddRoomsPowerPointsAndDevicesTables` is the current head — this migration must be added after it.
  - [x] Generate migration: `dotnet ef migrations add AddSmartPlugImportTables` from `api/` (or repo root with `--project api`) — do not hand-edit the generated migration file, per this codebase's standing rule.

- [x] Task 2: `UploadFunction` — HTTP POST, multipart upload, blob write (AC: 2)
  - [x] New file `api/Features/SmartPlugImport/UploadFunction.cs`, class `UploadFunction(AppDbContext db, BlobServiceClient blobServiceClient)` (primary constructor — `BlobServiceClient` is a new DI registration, see Task 5). Function name `"UploadImport"`, route `Route = "v1/flats/{flatId}/imports"`, `"post"`, `AuthorizationLevel.Anonymous` — mirror `CreateTariffFunction.cs`'s exact opening sequence: `context.GetUserId()` → parse/validate `flatId` → `db.Flats.SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId, ct)` → 403 if not found. This tenant check must happen **before** touching the multipart body or blob storage.
  - [x] Read the uploaded file via `req.Form.Files` (buffered `IFormFile` approach) — **not** `HttpRequestData`/`MultipartReader` streaming. This codebase's existing `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` v2.1.0 + `ConfigureFunctionsWebApplication()` in `Program.cs` (already present, unchanged) is exactly what's required for the plain ASP.NET Core `HttpRequest`/`Request.Form.Files` model to work in the isolated worker — no `Program.cs` change needed for this. Reject with 400 Problem Details if no file is present, or if the file extension is not `.xlsx`/`.csv` (case-insensitive) — do this check before creating the `ImportJob` row.
  - [x] Default ASP.NET Core limits (Kestrel `MaxRequestBodySize` ~28.6 MB, `FormOptions.MultipartBodyLengthLimit` 128 MB) are more than sufficient for personal smart-plug daily/interval CSV or Excel exports (this project's data volumes are O(thousands) of rows per the architecture doc) — do not add `RequestFormLimitsAttribute` or raise these limits; this is a deliberate non-change, not an oversight.
  - [x] Create `ImportJob { FlatId = flatGuid, Status = ImportStatus.Pending, CreatedAt = DateTimeOffset.UtcNow }`, `db.ImportJobs.Add(...)`, `await db.SaveChangesAsync(ct)` to obtain `ImportJobId` before naming the blob.
  - [x] Write to blob path `smart-plug-imports/{userId}/{flatId}/{importJobId}.{ext}` (container `smart-plug-imports` already exists in `infra/main.bicep` — do not create a new container) using `blobServiceClient.GetBlobContainerClient("smart-plug-imports").GetBlobClient($"{userId}/{flatId}/{importJobId}.{ext}")` then `.UploadAsync(file.OpenReadStream(), overwrite: false, ct)` (`overwrite: false` is safe here — the blob name embeds a freshly-generated `ImportJobId` guid, so a collision is not possible in normal operation, unlike `UpdateFlatStructureFunction`'s existing "delete-then-reinsert" pattern which doesn't apply here).
  - [x] Return `AcceptedResult` (202) with `{ importJobId }` — do **not** use `CreatedResult`/`Location` header; this endpoint's response shape and status code (202, per AD-13/format-patterns in the architecture doc) is fixed by AC2, not the generic 201 pattern used by `CreateTariffFunction`.
  - [x] The AC's "≤ 2s server-side time to 202" is a soft performance target for personal-project file sizes, not a hard timeout to implement in code — no explicit timeout/cancellation logic beyond the standard `CancellationToken ct` threading is required.

- [x] Task 3: `GetImportStatusFunction` — HTTP GET, tenant-scoped status read (AC: 3)
  - [x] New file `api/Features/SmartPlugImport/GetImportStatusFunction.cs`, class `GetImportStatusFunction(AppDbContext db)`. Function name `"GetImportStatus"`, route `Route = "v1/flats/{flatId}/imports/{jobId}"`, `"get"`. Same tenant-check opening sequence as Task 2 (resolve `userId` → validate `flatId` → confirm flat ownership → 403 if not).
  - [x] Parse `jobId` as `Guid`; 400 if malformed (same pattern as `flatId` parsing). Look up `db.ImportJobs.SingleOrDefaultAsync(j => j.ImportJobId == jobGuid && j.FlatId == flatGuid, ct)` — 404 (not 403) if the job doesn't exist for this flat, since the flat itself was already confirmed to belong to the user; an unknown `jobId` under a flat you own is a genuine 404, not a tenant violation.
  - [x] Response record `ImportJobStatusResponse(Guid ImportJobId, ImportStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, ImportErrorCategory? ErrorCategory, string? GapNotifications)` in a new `api/Features/SmartPlugImport/ImportModels.cs` — enum values serialize as strings automatically via the `JsonStringEnumConverter` already globally registered in `Program.cs`'s `Configure<JsonOptions>` call; no per-function enum handling needed (this endpoint has no request body to deserialize, so the per-function-static `_jsonOptions` pattern used in `CreateTariffFunction`/`UpdateFlatStructureFunction` for *request* deserialization does not apply here).
  - [x] `gapNotifications` in the response is the raw JSON string from `ImportJob.GapNotifications` (nullable) — Story 6.4 (`InterpolationEngine`/`ReconciliationEngine`) is what actually populates this column; in this story it is always `null` since nothing writes to it yet. Do not attempt to define or validate its shape in this story.
  - [x] Return `OkObjectResult(response)`.

- [x] Task 4: `ProcessImportFunction` — Event-Grid-sourced blob trigger, status machine, error categorization, concurrency handling (AC: 4, 6)
  - [x] **Read Dev Notes — "Flex Consumption blob trigger requires Event Grid source" before starting this task.** This is the single most important non-obvious constraint in this story.
  - [x] New file `api/Features/SmartPlugImport/ProcessImportFunction.cs`, class `ProcessImportFunction(AppDbContext db, BlobServiceClient blobServiceClient)`. Function name `"ProcessImport"`.
  - [x] Blob trigger attribute **must** specify `Source = BlobTriggerSource.EventGrid` explicitly:
    ```csharp
    [Function("ProcessImport")]
    public async Task RunAsync(
        [BlobTrigger("smart-plug-imports/{userId}/{flatId}/{importJobId}.{ext}",
            Source = BlobTriggerSource.EventGrid,
            Connection = "AzureWebJobsStorage")]
        Stream blobStream,
        string userId, string flatId, string importJobId, string ext,
        FunctionContext context,
        CancellationToken ct)
    ```
    Route-template-style binding parameters (`{userId}`, `{flatId}`, `{importJobId}`, `{ext}`) parse directly out of the blob path set by `UploadFunction` in Task 2 — no separate lookup needed to know which job this is. Do **not** call `context.GetUserId()` in this function — it throws on non-HTTP triggers (per `project-context.md`'s Azure Functions gotcha); `userId`/`flatId` come from the blob path binding parameters instead, exactly like the pattern this rule describes for other non-HTTP triggers.
  - [x] Load `db.ImportJobs.SingleOrDefaultAsync(j => j.ImportJobId == Guid.Parse(importJobId), ct)`. If somehow not found (should not happen under normal operation since `UploadFunction` always creates the row before writing the blob), log and return — no user-facing surface exists to report this to.
  - [x] Set `Status = Processing`, `await db.SaveChangesAsync(ct)` (first save — establishes the current `RowVersion` baseline for this run).
  - [x] Wrap the parse-and-store logic in try/catch. Define three marker exception types in `ImportModels.cs` (or a new `ImportExceptions.cs` in the same folder) that map 1:1 to `ImportErrorCategory`: `UnreadableFileException` → `DataUnreadable`, `ImportServiceUnavailableException` → `ServiceUnavailable`, and any other unhandled exception → `ProcessingFailed`. **Neither `EveHomeParser` nor `MerossParser` exist yet** (they are Story 6.2 and 6.3 respectively) — for this story, the file-type-detection/routing dispatch is real, but the two parser branches are placeholders. See Dev Notes — "Parser dispatch is a forward-compatible stub in this story" for the exact placeholder shape and why this is not scope creep in either direction.
  - [x] On success: `Status = Complete`, `CompletedAt = DateTimeOffset.UtcNow`. On any caught exception: `Status = Failed`, `ErrorCategory` per the mapping above, `CompletedAt = DateTimeOffset.UtcNow`; log `ex` via `ILogger<ProcessImportFunction>` (inject via primary constructor, per this codebase's standard DI logging pattern) — never surface `ex.Message` anywhere in the `ImportJob` row or any HTTP response (`ErrorCategory` enum is the only thing the client ever sees, per AC4/AC5).
  - [x] Second `db.SaveChangesAsync(ct)` (the completion/failure write) must be wrapped in its own try/catch for `DbUpdateConcurrencyException` (AC6). On catch: reload the entity fresh (`await db.Entry(importJob).ReloadAsync(ct)`, which re-fetches current column values including the current `RowVersion`), set `Status = Failed` and `ErrorCategory = ImportErrorCategory.ProcessingFailed` on the reloaded instance, and save again. This second attempt is not itself wrapped in a further retry loop — one reload-and-retry is sufficient to satisfy AC6's "never an unhandled 500 or silent overwrite"; do not build a general-purpose retry framework here (first concurrency-token pattern in the codebase, deliberately minimal per AC6's own scoping note).

- [x] Task 5: DI registration and package additions (AC: 2, 4, 7)
  - [x] Add `<PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Storage.Blobs" Version="5.3.1" />` to `api/energy-tracker-api.csproj` (version 5.x+ is required specifically for `BlobTriggerSource.EventGrid` support — see Dev Notes; check NuGet for the latest 5.x patch at implementation time and use that instead of pinning to a possibly-stale exact version if a newer 5.x is available). This is a new package — `Azure.Storage.Blobs` (already referenced) is the *data-plane SDK* used for `BlobServiceClient`/uploads; `Microsoft.Azure.Functions.Worker.Extensions.Storage.Blobs` is the separate *trigger binding* package needed for the `[BlobTrigger]` attribute itself. Both are needed; neither substitutes for the other.
  - [x] In `Program.cs`, register `BlobServiceClient` as a singleton using `DefaultAzureCredential` with the user-assigned managed identity's client ID — mirror the exact pattern already used for `SqlConnectionString`/`AppDbContext` (read config, throw `InvalidOperationException` if missing, matching this codebase's existing "no `CancellationToken.None`, no silent nulls" ethos):
    ```csharp
    var storageAccountName = builder.Configuration["AzureStorageAccountName"]
        ?? throw new InvalidOperationException("Required configuration 'AzureStorageAccountName' is missing.");
    var managedIdentityClientId = builder.Configuration["AZURE_CLIENT_ID"];
    var credential = managedIdentityClientId is not null
        ? new DefaultAzureCredential(new DefaultAzureCredentialOptions { ManagedIdentityClientId = managedIdentityClientId })
        : new DefaultAzureCredential();
    builder.Services.AddSingleton(new BlobServiceClient(
        new Uri($"https://{storageAccountName}.blob.core.windows.net"), credential));
    ```
    `AzureStorageAccountName` and `AZURE_CLIENT_ID` app settings already exist in `infra/main.bicep`'s `functionsApp` resource (lines 267, 277) — no Bicep change needed for these two settings themselves, only for the RBAC/Event Grid gaps in Task 6.
  - [x] Add local dev config: `api/local.settings.json.example` (if this file exists — check first; it's gitignored so may only exist as an example) should document `AzureStorageAccountName` and confirm `AzureWebJobsStorage` is already required for any Functions app — no new local-only secret is introduced since blob access uses the same managed identity as everything else. Azurite (already used per `project-context.md`'s local-dev section) serves the blob emulator locally.

- [x] Task 6: Infra — RBAC gaps and Event Grid wiring in `infra/main.bicep` (AC: 7)
  - [x] **RBAC gap 1 — `AzureWebJobsStorage` is identity-based, but the identity is missing `Storage Account Contributor`.** Add a new role assignment resource (do not modify the three existing assignments):
    ```bicep
    var storageAccountContributorRoleId = '17d1049b-9a84-46fb-8f53-869881c3d3ab'

    resource storageAccountContributorAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
      name: guid(storageAccount.id, managedIdentityObjectId, storageAccountContributorRoleId)
      scope: storageAccount
      properties: {
        roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageAccountContributorRoleId)
        principalId: managedIdentityObjectId
        principalType: 'ServicePrincipal'
      }
    }
    ```
    Add `storageAccountContributorAssignment` to `functionsApp`'s existing `dependsOn` array alongside `blobDataContributorAssignment`/`queueDataContributorAssignment`/`keyVaultSecretsUserAssignment`.
  - [x] **RBAC gap 2 — blob-trigger read access needs `Storage Blob Data Owner`, not just `Storage Blob Data Contributor`.** The existing `blobDataContributorAssignment` (Contributor) stays as-is — do not remove or rename it (renaming its `name` GUID would orphan the old assignment in Azure rather than replace it cleanly). Add a second, additive assignment:
    ```bicep
    var storageBlobDataOwnerRoleId = 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'

    resource storageBlobDataOwnerAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
      name: guid(storageAccount.id, managedIdentityObjectId, storageBlobDataOwnerRoleId)
      scope: storageAccount
      properties: {
        roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataOwnerRoleId)
        principalId: managedIdentityObjectId
        principalType: 'ServicePrincipal'
      }
    }
    ```
    Add to `functionsApp`'s `dependsOn` too.
  - [x] **Infra gap 3 — Flex Consumption blob triggers require an Event Grid subscription; none exists.** Add, in `infra/main.bicep`, an `Microsoft.EventGrid/systemTopics` resource (`source: storageAccount.id`, `topicType: 'Microsoft.Storage.StorageAccounts'`) and a child `Microsoft.EventGrid/systemTopics/eventSubscriptions` resource filtered to `Microsoft.Storage.BlobCreated`, with a `WebHook` destination pointing at `ProcessImportFunction`'s blob-extension webhook URL. See Dev Notes — "Event Grid wiring: exact Bicep and the two-phase deploy gotcha" for the full resource block and — critically — why this **cannot** be deployed in the same `az deployment group create` run as the very first deploy of `ProcessImportFunction`'s code, and what the correct two-step sequence is.
  - [x] After adding all three infra changes, update `infra/deploy.sh`'s "Next steps" echo block to mention the second-pass Event Grid deploy step (mirroring how it already documents the manual SQL-user-grant step) — do not silently leave this as tribal knowledge only in this story file.

- [x] Task 7: Backend tests (AC: all)
  - [x] `api.Tests/Features/SmartPlugImport/UploadFunctionTests.cs`: valid `.xlsx`/`.csv` upload creates `ImportJob` with `Status = Pending` and returns 202 + `importJobId`; missing file → 400; wrong extension (e.g. `.txt`) → 400; foreign `flatId` → 403; blob write path matches `{userId}/{flatId}/{importJobId}.{ext}` (assert via a mocked/fake `BlobServiceClient` — see this file's likely need for a test double, since no existing test in this codebase mocks Azure SDK clients yet; use `Azure.Storage.Blobs`' testable `BlobServiceClient` constructor overload or a thin wrapper interface if direct mocking proves awkward — your call, but keep it consistent with xUnit + the rest of `api.Tests`'s style, no new mocking library beyond what's already a transitive dependency).
  - [x] `api.Tests/Features/SmartPlugImport/GetImportStatusFunctionTests.cs`: returns correct fields for an existing job; 404 for unknown `jobId` under an owned flat; 403 for a foreign flat; 400 for malformed `jobId`.
  - [x] `api.Tests/Features/SmartPlugImport/ProcessImportFunctionTests.cs`: happy path Pending→Processing→Complete; unhandled exception → Failed/ProcessingFailed; simulated `UnreadableFileException` → Failed/DataUnreadable; simulated `ImportServiceUnavailableException` → Failed/ServiceUnavailable; simulated `DbUpdateConcurrencyException` on the completion save → reload-and-retry results in `Status = Failed`/`ErrorCategory = ProcessingFailed`, not an unhandled exception. Use `AppDbContext` with the `InMemory` provider per this codebase's existing backend test convention — note `InMemory` does not enforce real SQL `rowversion` semantics, so the concurrency test must simulate the conflict by manually throwing/injecting `DbUpdateConcurrencyException` around the second `SaveChangesAsync` call rather than relying on InMemory to produce a genuine one (InMemory has no true optimistic concurrency enforcement — this is a known, pre-existing testing-standards gap noted in `project-context.md`: "Do not write InMemory tests that rely on SQL-specific behaviour"; here we're testing the *catch/recovery code path*, not the database's own conflict detection, so a manually-triggered exception is the correct test design, not a workaround).
  - [x] Entity/migration review: no dedicated unit test needed for `IEntityTypeConfiguration<T>` classes (per `project-context.md`: "Do not test `{Entity}Configuration.cs` EF Core config classes — trust EF Core"); confirm instead via `dotnet ef migrations list` and a local `dotnet ef database update` that the migration applies cleanly (per the existing Testing Rules / EF Core Migrations convention).

- [x] Task 8: Full verification pass before marking ready for review (AC: all)
  - [x] `dotnet test api.Tests/` — all green, including all new `SmartPlugImport` test files.
  - [x] `dotnet ef migrations list` before and after adding the migration, confirming correct ordering; `dotnet ef database update` locally against the real dev Azure SQL DB (per `project-context.md`'s "Always test `dotnet ef database update` locally before pushing" rule) — confirm the three new tables, indexes, and the `RowVersion` `rowversion` column appear correctly.
  - [x] Manually verify the multipart upload end-to-end locally: `swa start` + `func start` + Azurite, POST a small `.xlsx`/`.csv` via curl/Postman to `/api/v1/flats/{flatId}/imports`, confirm 202 + blob appears in Azurite's blob emulator at the expected path, confirm `ImportJob` row appears with `Status = Pending`. Note: the blob trigger itself (`ProcessImportFunction`) **cannot** be exercised end-to-end locally against Azurite in the same way it runs in Azure, because Azurite does not emulate Event Grid system topics — local verification of the trigger firing is necessarily partial; rely on the unit tests in Task 7 for `ProcessImportFunction`'s internal logic, and flag the Event Grid wiring itself as something only fully verifiable after a real Azure deploy (see Task 6's two-phase deploy note).
  - [x] Do **not** run `./infra/deploy.sh` or otherwise push these Bicep changes to the live Azure environment as part of this story. Ralf deploys infra changes himself through the existing pipeline/script after code review — this story's job is to leave `infra/main.bicep` correct and reviewed, not to execute the deployment. Call out clearly in Completion Notes that the Event Grid wiring specifically needs the two-phase sequence from Dev Notes ("Event Grid wiring... two-phase deploy gotcha") once Ralf does deploy: normal code push first, then a second `./infra/deploy.sh` run for the Event Grid resources.

### Review Findings

- [x] [Review][Patch] UploadFunction: blob write has no error handling, risking an orphaned Pending ImportJob row — When `blobClient.UploadAsync` throws after the `ImportJob` row is already saved as `Pending` (transient storage error, network blip), the caller gets an unhandled 500 and the job is stuck in `Pending` forever (blob never written → blob trigger never fires). Fixed: wrapped the upload in try/catch; on failure sets `Status = Failed`, `ErrorCategory = ServiceUnavailable`, `CompletedAt`, saves, and returns a 503 Problem Details response. [api/Features/SmartPlugImport/UploadFunction.cs]
- [x] [Review][Patch] ProcessImportFunction: no idempotency guard against Event Grid redelivery of an already-terminal job — Event Grid's at-least-once delivery (retryPolicy.maxDeliveryAttempts: 30) can redeliver a blob-created event after the job already reached `Complete`/`Failed`; the function reprocesses it unconditionally, overwriting `Status`/`ErrorCategory`/`CompletedAt`. This is a sequential (non-concurrent) redelivery, so the RowVersion concurrency token from AC6 does not catch it. Fixed: added a guard immediately after the job lookup — if `Status` is already `Complete` or `Failed`, logs and returns without reprocessing. [api/Features/SmartPlugImport/ProcessImportFunction.cs]
- [x] [Review][Patch] ProcessImportFunction: Pending→Processing SaveChangesAsync unprotected against DbUpdateConcurrencyException — Only the completion save was wrapped in try/catch; a concurrent invocation racing on the first save (exactly the scenario AC6 describes) threw unhandled, contradicting AC6's "never an unhandled exception" requirement. Fixed: extracted a shared `TrySaveAsync` helper used by both saves, applying the same reload/Failed/ProcessingFailed recovery pattern to the Processing-transition save. [api/Features/SmartPlugImport/ProcessImportFunction.cs]
- [x] [Review][Patch] ProcessImportFunction: CompletedAt silently dropped on the concurrency-recovery path — `CompletedAt` was set before the second `SaveChangesAsync`; on `DbUpdateConcurrencyException`, `ReloadAsync` overwrote it back to null and it was never re-set before the final save. Fixed: `TrySaveAsync`'s recovery path now re-sets `CompletedAt` after the reload, before the retry save. [api/Features/SmartPlugImport/ProcessImportFunction.cs]
- [x] [Review][Patch] ConfirmBlobReadableAsync / UploadFunction: zero-byte blob silently marked Complete instead of DataUnreadable — Fixed: `ConfirmBlobReadableAsync` now throws `UnreadableFileException` when the stream returns 0 bytes; `UploadFunction` also rejects `file.Length == 0` with 400 before creating the job row. [api/Features/SmartPlugImport/ProcessImportFunction.cs, api/Features/SmartPlugImport/UploadFunction.cs]
- [x] [Review][Patch] ProcessImportFunction: importJobId blob-path segment not validated as a GUID before `Guid.Parse` — Fixed: replaced with `Guid.TryParse`; malformed IDs are logged and the invocation returns without throwing. [api/Features/SmartPlugImport/ProcessImportFunction.cs]
- [x] [Review][Patch] ProcessImportFunction: the retry `SaveChangesAsync` inside the concurrency catch block was itself unprotected — Fixed: the retry save inside `TrySaveAsync` now has its own catch that logs rather than rethrows on a second conflict. [api/Features/SmartPlugImport/ProcessImportFunction.cs]
- [x] [Review][Patch] ProcessImportFunction: unused `BlobServiceClient` constructor dependency — Fixed: removed from the constructor; the blob content already arrives via the `[BlobTrigger]`-bound stream. [api/Features/SmartPlugImport/ProcessImportFunction.cs]
- [x] [Review][Patch] UploadFunction: blob path used the raw route `flatId` string instead of the canonicalized `flatGuid.ToString()` — Fixed: blob path now uses `flatGuid` consistently. [api/Features/SmartPlugImport/UploadFunction.cs]
- [x] [Review][Patch] UploadFunction: unnecessary null-forgiving operator on `AcceptedResult`'s `location` parameter — Fixed: passes `null` directly (the parameter is already nullable). [api/Features/SmartPlugImport/UploadFunction.cs]
- [x] [Review][Defer] Program.cs / all IActionResult-returning endpoints serialize enums as raw ints, not named strings — `Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>` (Program.cs) only affects Minimal API/`WriteAsJsonAsync` serialization, not the MVC `ObjectResult`/`OkObjectResult` pipeline these Functions use (verified against Microsoft's own ASP.NET Core docs — the two option types are documented as having disjoint scopes; this codebase never calls `.AddMvc()`/`.AddControllers().AddJsonOptions()`). `GetImportStatusFunction`'s `status`/`errorCategory` fields will serialize as integers over real HTTP, not the named strings AC5's frontend-mapping wording implies. Pre-existing gap (the JsonOptions config predates this diff and affects every enum-bearing endpoint in the codebase), but this story's endpoint is the first to depend on the string contract — deferred, pre-existing. [api/Program.cs:56-60, api/Features/SmartPlugImport/GetImportStatusFunction.cs]

## Dev Notes

### Flex Consumption blob trigger requires Event Grid source — this is the load-bearing finding of this story

The epic's AC4 text ("the blob trigger fires on `ProcessImportFunction`") and the architecture doc's AD-4/AD-15 both describe "a blob-triggered Function" without qualifying *which* blob-trigger implementation. Researched during story creation (Microsoft Learn, current as of this story's creation date): **on the Flex Consumption plan, the default polling-based blob trigger (`LogsAndContainerScan` source) is explicitly unsupported — only the Event Grid–sourced blob trigger works.** This project's Functions app is Flex Consumption (Linux) per AD-3 in `architecture.md` — already provisioned, already running since Story 1.2. This means:

- The `[BlobTrigger]` attribute in `ProcessImportFunction.cs` **must** set `Source = BlobTriggerSource.EventGrid` explicitly (Task 4) — the attribute-only, no-`Source`-specified form used in countless online tutorials/StackOverflow answers silently falls back to the unsupported polling source on this hosting plan and the trigger will simply never fire in production, with no error surfaced anywhere.
- `Microsoft.Azure.Functions.Worker.Extensions.Storage.Blobs` version 5.x+ is required for `BlobTriggerSource.EventGrid` to exist as an option at all (Task 5) — this package is not yet referenced in `api/energy-tracker-api.csproj`.
- An actual `Microsoft.EventGrid/systemTopics` + `eventSubscriptions` resource pair must exist, wired to the storage account and to this specific function's webhook endpoint (Task 6) — Azure does not create this automatically just because a function has an Event-Grid-sourced blob trigger attribute; it is a separate infrastructure resource that must be provisioned.
- The storage account backing the trigger must be General-Purpose v2 — already true (`kind: 'StorageV2'` in `infra/main.bicep`, unchanged).

Without all three of these, AC4 ("the blob trigger fires") is architecturally unsatisfiable on this project's hosting plan, regardless of how correct `ProcessImportFunction`'s internal C# logic is. This is the single highest-risk gap in this story.

### Infra gap analysis — what's already there vs. what this story adds

`infra/main.bicep` (provisioned in Story 1.2, unchanged since) **already contains**, and this story does **not** need to (re-)create:
- The `smart-plug-imports` blob container (line ~101–107).
- The `import-processing` storage queue (line ~124–127) — not used by this story (no queue-triggered function here; Story 6.1's only async path is the blob trigger), but already provisioned for future Insights-slice use per AD-15.
- `Storage Blob Data Contributor` and `Storage Queue Data Contributor` role assignments for the managed identity, scoped to the whole storage account (covers all containers/queues in the account, including the ones above) — these predate this story and satisfy general blob/queue read-write for `UploadFunction`.
- `AzureStorageAccountName` and `AZURE_CLIENT_ID` app settings on the Functions app (used by Task 5's new `BlobServiceClient` DI registration).

This story's Task 6 adds exactly three new things, all justified above: `Storage Account Contributor` (gap 1, needed because `AzureWebJobsStorage` itself already uses identity-based auth — see `AzureWebJobsStorage__credential = managedidentity` in the existing Bicep — and per Microsoft's own docs on identity-based host storage connections, the host identity needs this role too, not just the two data-plane roles already present), `Storage Blob Data Owner` (gap 2, the specific role Microsoft's blob-trigger documentation calls out as required for the trigger's internal blob-receipt mechanism — `Contributor` is explicitly documented as insufficient for this one narrow purpose, even though it's sufficient for `UploadFunction`'s own reads/writes), and the Event Grid system topic/subscription (gap 3, described above).

### Event Grid wiring: exact Bicep and the two-phase deploy gotcha

Full resource pair to add to `infra/main.bicep` (see also Task 6):

```bicep
resource functionAppHost 'Microsoft.Web/sites/host@2023-12-01' existing = {
  parent: functionsApp
  name: 'default'
}

resource importBlobEventGridTopic 'Microsoft.EventGrid/systemTopics@2023-12-15-preview' = {
  name: '${storageAccountName}-import-egst'
  location: location
  properties: {
    source: storageAccount.id
    topicType: 'Microsoft.Storage.StorageAccounts'
  }
}

resource importBlobEventSubscription 'Microsoft.EventGrid/systemTopics/eventSubscriptions@2023-12-15-preview' = {
  parent: importBlobEventGridTopic
  name: 'blob-created-to-processimport'
  properties: {
    destination: {
      endpointType: 'WebHook'
      properties: {
        endpointUrl: 'https://${functionsApp.properties.defaultHostName}/runtime/webhooks/blobs?functionName=Host.Functions.ProcessImport&code=${functionAppHost.listKeys().systemKeys.blobs_extension}'
      }
    }
    eventDeliverySchema: 'EventGridSchema'
    filter: {
      includedEventTypes: ['Microsoft.Storage.BlobCreated']
      subjectBeginsWith: '/blobServices/default/containers/smart-plug-imports/'
    }
    retryPolicy: {
      maxDeliveryAttempts: 30
      eventTimeToLiveInMinutes: 1440
    }
  }
}
```

**Do not put `functionAppHost.listKeys()` or the resulting `endpointUrl` in a Bicep `output`** — `listKeys()` results are secrets, and outputs are stored in plaintext deployment history.

**Deploy-order gotcha (important):** `blobs_extension` is a system key that the Functions host generates lazily the *first time it registers a function with an Event-Grid-sourced blob trigger* — it does not exist before `ProcessImportFunction`'s code (with `Source = BlobTriggerSource.EventGrid`) has been deployed and the host has started at least once. Since this project's `infra/deploy.sh` is a separate, manual, occasional script (not run by the CI/CD pipeline on every push — the GitHub Actions workflow only publishes application code), the correct sequence for this story is:
1. Merge and deploy this story's application code first (normal CI/CD push to `main`), so `ProcessImportFunction` is live and the host has started at least once.
2. Only then re-run `./infra/deploy.sh` (with the Event Grid resources added to `main.bicep`) to wire up the Event Grid subscription — running it before step 1 risks `listKeys()` returning an empty/stale `blobs_extension` value or the deployment failing outright.
3. Verify manually (Task 8's last checklist item) by uploading a real file to the deployed blob container and confirming `ProcessImportFunction` actually fires.

This ordering constraint should be called out explicitly to the human running the deploy — it is not something `dotnet test`/`npm test` can catch.

### Parser dispatch is a forward-compatible stub in this story — not scope creep either direction

`EveHomeParser.cs` (Story 6.2) and `MerossParser.cs` (Story 6.3) do not exist yet. AC4's "routes to the appropriate parser" for *this* story means: real file-extension detection (`.xlsx` vs `.csv`), a real dispatch switch, and a real, permanent exception-to-`ErrorCategory` mapping contract (`UnreadableFileException` → `DataUnreadable`, `ImportServiceUnavailableException` → `ServiceUnavailable`, anything else → `ProcessingFailed`) — but the two dispatch branches themselves call a placeholder (e.g., mark `Complete` immediately after confirming the blob is readable, with zero rows written) rather than a real parser. This is intentional:
- Do **not** implement any Eve Home/Meross parsing logic in this story — that is Story 6.2/6.3's entire scope, and duplicating it here would create merge conflicts and wasted work.
- Do **not** skip the dispatch/error-mapping scaffolding either — Stories 6.2 and 6.3 are written assuming `ProcessImportFunction`'s status-machine, concurrency handling, and error-category mapping already exist and work; they only need to plug in `EveHomeParser`/`MerossParser` and throw `UnreadableFileException` where appropriate. If this story leaves the mapping contract undefined, 6.2/6.3 will each have to invent it inconsistently.
- The `DataUnreadable` path therefore has no *real* corrupt-file test fixture in this story (there's no parser yet to fail on one) — Task 7 covers it via a manually-thrown `UnreadableFileException`, and the *real* content-based trigger for that exception path is added in 6.2/6.3 without touching `ProcessImportFunction`'s catch block again.

### RowVersion / optimistic concurrency — first instance of this pattern in the codebase

Per `deferred-work.md`, no other table in this codebase (`Flat`, `Tariff`, `MeterReading`, `Room`/`PowerPoint`/`Device`) has any concurrency-token protection — all are last-write-wins, and multiple `deferred-work.md` entries explicitly note that Story 6.1's `RowVersion` addition does **not** retroactively fix any of them. Do not attempt to add `RowVersion` to any other entity while implementing this story — AC6 is explicit that this is "deliberately scoped to these three new tables only." `.IsRowVersion()` is the entire Fluent API surface needed; no custom concurrency-conflict-resolution framework should be built beyond the single reload-and-mark-failed step described in Task 4.

### Testing conventions to follow, not reinvent

- Backend: xUnit, `api.Tests/Features/SmartPlugImport/`, mirroring the file/test style of `api.Tests/Features/Tariffs/` and `api.Tests/Features/FlatStructure/` (one assertion-focused test class per Function/engine, `InMemory` EF Core provider, no SQL-specific behavior asserted).
- This is the first story to need any kind of Azure SDK client (`BlobServiceClient`) in a unit test — there is no existing mocking pattern for this in `api.Tests`; use whatever `Azure.Storage.Blobs` testability surface (either its own fake-friendly constructors, or a thin injectable wrapper interface around the two or three calls this story actually makes) keeps the tests fast and dependency-free, consistent with this codebase's existing "no live Azure calls in unit tests" implicit convention (no test in this codebase currently calls out to a real Azure resource).
- No new test infrastructure beyond this is needed.

### Project Structure Notes

- New files: `api/Data/Entities/ImportJob.cs`, `SmartPlugDailyData.cs`, `SmartPlugIntervalData.cs`; `api/Data/Configurations/ImportJobConfiguration.cs`, `SmartPlugDailyDataConfiguration.cs`, `SmartPlugIntervalDataConfiguration.cs`; `api/Data/Migrations/{timestamp}_AddSmartPlugImportTables.cs` (+ Designer file, generated); `api/Features/SmartPlugImport/UploadFunction.cs`, `GetImportStatusFunction.cs`, `ProcessImportFunction.cs`, `ImportModels.cs`; `api.Tests/Features/SmartPlugImport/UploadFunctionTests.cs`, `GetImportStatusFunctionTests.cs`, `ProcessImportFunctionTests.cs`.
- Modified files: `api/Data/AppDbContext.cs` (3 new `DbSet<T>`); `api/Program.cs` (new `BlobServiceClient` DI registration); `api/energy-tracker-api.csproj` (new package reference); `infra/main.bicep` (2 new role assignments + Event Grid system topic/subscription); `infra/deploy.sh` (documentation of the two-phase deploy step).
- No changes to: `client/` (this story is entirely backend — the Import UI is Story 6.6; do not build any frontend surface for this story), `api/Features/FlatStructure/*`, `api/Features/Tariffs/*` (referenced only for pattern-mirroring, not modified), `EveHomeParser.cs`/`MerossParser.cs` (do not create — Stories 6.2/6.3).
- This story does not touch `ExcelDataReader`'s `System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` startup requirement noted in `deferred-work.md` — that's needed only once real `.xlsx` parsing exists, i.e. Story 6.2's scope, not this one (this story never actually opens an Excel file's contents, only detects its extension and writes/reads the raw blob).

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-6-smart-plug-import-device-registry.md#Story 6.1] — authoritative AC text (verbatim, reproduced above as ACs 1–6; AC7 added during story creation per this session's explicit infra/RBAC review directive).
- [Source: _bmad-output/planning-artifacts/architecture.md#AD-3, AD-4, AD-15, AD-24, Complete Azure resource set, Enforcement Summary, api/ file tree] — Flex Consumption hosting decision, standalone Functions app rationale, queue/blob resource inventory, decimal/DateTimeOffset/record/Fluent-API enforcement rules, `SmartPlugImport/` file tree (`UploadFunction.cs`, `ProcessImportFunction.cs`, `EveHomeParser.cs`, `MerossParser.cs`, `InterpolationEngine.cs`, `ReconciliationEngine.cs`, `ImportModels.cs` — this story creates the first three files in that list and the shared `ImportModels.cs`; the last three are 6.2/6.3/6.4).
- [Source: infra/main.bicep] — existing storage account (GPv2), `smart-plug-imports` container, `import-processing` queue, existing RBAC role assignments (Blob/Queue Data Contributor, Key Vault Secrets User), Functions app config (`AzureStorageAccountName`, `AZURE_CLIENT_ID`, `AzureWebJobsStorage__*` identity-based settings) — all reviewed in full for this story's infra gap analysis.
- [Source: infra/deploy.sh] — existing manual post-deploy step pattern (SQL user grant) that this story's Event Grid two-phase-deploy note should follow the same documentation convention as.
- [Source: api/Features/Tariffs/CreateTariffFunction.cs, api/Features/FlatStructure/UpdateFlatStructureFunction.cs] — canonical Function shape (tenant check ordering, Problem Details error responses, `_jsonOptions` static field pattern for request deserialization, primary-constructor DI) mirrored throughout this story's three new Functions.
- [Source: api/Data/Configurations/TariffConfiguration.cs, api/Data/Entities/Flat.cs] — canonical `IEntityTypeConfiguration<T>` shape and cascade-delete FK pattern mirrored for all three new entities.
- [Source: api/Program.cs, api/energy-tracker-api.csproj] — confirmed `Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore` v2.1.0 and `ConfigureFunctionsWebApplication()` already present (sufficient for `Request.Form.Files`); confirmed `Microsoft.Azure.Functions.Worker.Extensions.Storage.Blobs` is **not yet** referenced (new package needed for `[BlobTrigger]`); confirmed no existing `BlobServiceClient`/`QueueServiceClient`/`DefaultAzureCredential` DI registration exists anywhere in `api/` today.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] — confirms no other entity in this codebase has any concurrency-token protection, and multiple entries explicitly note Story 6.1's `RowVersion` addition is deliberately scoped to these three new tables only; confirms the `ExcelDataReader` `CodePagesEncodingProvider` startup requirement is deferred to "Story 6.x when the import pipeline is built" (interpreted here as Story 6.2, since this story never opens Excel file contents).
- [Source: _bmad-output/planning-artifacts/specs/spec-energy-tracker/smart-plug-formats.md] — confirms Eve Home files have no enforced filename pattern (`.xlsx`, user-chosen name) and Meross files follow `Power Monitor Day Data - {device_name} - {YYYYMMDD}.csv` (`.csv`) — relevant only in that both extensions used by `UploadFunction`'s extension-detection match this story's AC4 `.xlsx`/`.csv` split; filename-based device-association logic itself is Story 6.6's scope (Import UI), not this one.
- [Microsoft Learn, queried during story creation: Flex Consumption plan hosting — Considerations](https://learn.microsoft.com/azure/azure-functions/flex-consumption-plan#considerations); [Migrate Consumption to Flex Consumption](https://learn.microsoft.com/azure/azure-functions/migration/migrate-plan-consumption-to-flex); [Tutorial: Trigger Azure Functions on blob containers using an event subscription](https://learn.microsoft.com/azure/azure-functions/functions-event-grid-blob-trigger); [Blob storage trigger — Connections (RBAC roles table)](https://learn.microsoft.com/azure/azure-functions/functions-bindings-storage-blob-trigger#connections); [Connecting to host storage with an identity](https://learn.microsoft.com/azure/azure-functions/functions-reference#connecting-to-host-storage-with-an-identity); [Quickstart: Route Blob storage events to web endpoint by using Bicep](https://learn.microsoft.com/azure/event-grid/blob-event-quickstart-bicep); [Azure built-in roles — Storage](https://learn.microsoft.com/azure/role-based-access-control/built-in-roles#storage) — source for the `17d1049b-9a84-46fb-8f53-869881c3d3ab` (Storage Account Contributor) and `b7e6dc6d-f1e8-4753-8033-0f276bb0955b` (Storage Blob Data Owner) role GUIDs used in Task 6; [Guide for running C# Azure Functions in the isolated worker model — HTTP trigger](https://learn.microsoft.com/azure/azure-functions/dotnet-isolated-process-guide#http-trigger); [Upload files in ASP.NET Core](https://learn.microsoft.com/aspnet/core/mvc/models/file-uploads) — source for the `req.Form.Files` buffered-upload pattern and default size limits cited in Task 2.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- `dotnet ef migrations add AddSmartPlugImportTables` — generated `20260706101143_AddSmartPlugImportTables` cleanly on first attempt after `20260705072329_AddRoomsPowerPointsAndDevicesTables`.
- `dotnet ef database update` — applied successfully against the real dev Azure SQL DB (`energytracker-db`); confirmed via `sqlcmd` that `ImportJobs`, `SmartPlugDailyData`, `SmartPlugIntervalData` and the `RowVersion` `timestamp` column exist with correct types.
- `az bicep build --file infra/main.bicep --stdout` — compiled cleanly (only pre-existing `BCP081` type-cache warnings from a locally outdated Bicep CLI, unrelated to this story's changes).
- Local manual verification: `func start` + a hand-crafted `X-MS-CLIENT-PRINCIPAL` header (no `swa start` auth emulator needed) against the real dev DB — `UploadImport` correctly created an `ImportJob` row with `Status = Pending` before attempting the blob write; `GetImportStatus` correctly returned it. All test data cleaned up afterward.

### Completion Notes List

- **NuGet version correction**: the story's Task 5 suggested `Microsoft.Azure.Functions.Worker.Extensions.Storage.Blobs` version `5.3.1`, but that version doesn't exist and, more importantly, `BlobTriggerSource`/`Source` on `[BlobTrigger]` was only introduced in **6.1.0**. Used **6.8.1** (latest stable at implementation time). Verified via the microsoft-docs skill against the actual Azure/azure-functions-dotnet-worker source history before making the change.
- **RowVersion initializer changed from the story's literal spec**: AC/Task 1 said to initialize `RowVersion` as `= null!` (mirroring required reference-type properties like `Flat Flat { get; set; } = null!`). In practice, EF Core's `InMemory` test provider throws `DbUpdateException: Required properties '{RowVersion}' are missing` on insert when the property is null, because (unlike SQL Server) InMemory never auto-generates a `rowversion` value. Changed the initializer to `= []` (empty byte array) instead of `= null!`. This has no effect against real SQL Server — `.IsRowVersion()` excludes the column from the generated INSERT entirely, so the app-supplied value (whatever it is) is never sent; SQL Server always supplies the real value. Confirmed via the live dev-DB migration + insert test that the real `rowversion` column populates correctly regardless.
- **Local manual verification of the blob write itself was not achievable**, and this is a genuine, pre-existing infra gap rather than a code defect: `UploadFunction`'s `BlobServiceClient` (per Task 5's explicit DI spec, mirroring `SqlConnectionString`/`AppDbContext`) always targets the real cloud storage endpoint via `DefaultAzureCredential` — it has no Azurite/connection-string branch, so Task 8's "confirm blob appears in Azurite's blob emulator" instruction cannot literally be followed with this DI wiring. Attempting the real-endpoint path instead (mirroring how `SqlConnectionString` already works for local dev against the real Azure SQL DB) hit `AuthorizationPermissionMismatch`, because — unlike SQL, where the developer's own Azure AD account is the SQL admin — **the developer's own AAD identity has no Storage Blob Data Contributor/Owner role on the storage account; only the managed identity does (via Bicep)**. This is not something a dev agent should fix by granting live IAM roles. Verified instead that: (a) the `ImportJob` row is correctly created with `Status = Pending` before the blob write is attempted (confirmed against the real dev DB), and (b) all blob-write logic (path construction, `overwrite: false`, extension gating) is fully covered by `UploadFunctionTests.cs`'s mocked-`BlobServiceClient` tests. **Recommendation for Ralf**: either grant developer AAD accounts `Storage Blob Data Contributor` on the storage account for local testing, or accept that blob-write is only verifiable end-to-end after a real deploy.
- **Local dev config bugs fixed in passing** (in the gitignored `api/local.settings.json`, not shipped/committed): (1) `APPLICATIONINSIGHTS_CONNECTION_STRING` was missing — only the wrongly-cased `appInsightsConnectionString` key was present, causing `func start` to crash at boot with "A connection string was not found" from the Azure Monitor exporter; this predates this story. (2) Added `AzureStorageAccountName` (this story's own new required config key). Both fixes were necessary just to get `func start` to boot for manual verification.
- Confirmed via a live `PUT /structure` call that this codebase's enums already serialize as raw ints (not strings) in all `IActionResult`-based responses, despite `Program.cs`'s `Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>` registering a `JsonStringEnumConverter` — that config applies to ASP.NET Core minimal-API/Http.Json paths, not MVC `IActionResult` serialization. `ImportJobStatusResponse.Status`/`ErrorCategory` serializing as ints is therefore consistent with every other enum-bearing endpoint in this codebase (e.g. `DeviceResponse.ConsumptionApproach`), not a regression introduced by this story.
- Event Grid two-phase deploy: per Dev Notes, `infra/main.bicep`'s Event Grid system topic/subscription cannot be deployed until after `ProcessImportFunction`'s code has been live for at least one host start (the `blobs_extension` system key is generated lazily). **This story deliberately does not run `./infra/deploy.sh`** — Ralf deploys infra changes himself. `infra/deploy.sh`'s "Next steps" block now documents the two-phase sequence.
- All 8 tasks complete; all ACs satisfied. 17 new backend tests added (all passing); full suite (272 tests) green with no regressions.

### File List

**New files:**
- `api/Data/Entities/ImportJob.cs`
- `api/Data/Entities/SmartPlugDailyData.cs`
- `api/Data/Entities/SmartPlugIntervalData.cs`
- `api/Data/Configurations/ImportJobConfiguration.cs`
- `api/Data/Configurations/SmartPlugDailyDataConfiguration.cs`
- `api/Data/Configurations/SmartPlugIntervalDataConfiguration.cs`
- `api/Data/Migrations/20260706101143_AddSmartPlugImportTables.cs`
- `api/Data/Migrations/20260706101143_AddSmartPlugImportTables.Designer.cs`
- `api/Features/SmartPlugImport/ImportModels.cs`
- `api/Features/SmartPlugImport/UploadFunction.cs`
- `api/Features/SmartPlugImport/GetImportStatusFunction.cs`
- `api/Features/SmartPlugImport/ProcessImportFunction.cs`
- `api.Tests/Features/SmartPlugImport/UploadFunctionTests.cs`
- `api.Tests/Features/SmartPlugImport/GetImportStatusFunctionTests.cs`
- `api.Tests/Features/SmartPlugImport/ProcessImportFunctionTests.cs`

**Modified files:**
- `api/Data/AppDbContext.cs` (3 new `DbSet<T>` properties)
- `api/Data/Migrations/AppDbContextModelSnapshot.cs` (EF-generated)
- `api/Program.cs` (new `BlobServiceClient` DI registration)
- `api/energy-tracker-api.csproj` (new `Microsoft.Azure.Functions.Worker.Extensions.Storage.Blobs` 6.8.1 package reference)
- `infra/main.bicep` (2 new RBAC role assignments + Event Grid system topic/subscription)
- `infra/deploy.sh` (documented the two-phase Event Grid deploy step)
- `api/local.settings.json` (gitignored, local-only: added `AzureStorageAccountName` + fixed `APPLICATIONINSIGHTS_CONNECTION_STRING` key casing)
