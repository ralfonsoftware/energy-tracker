---
baseline_commit: 61b9873d3a3e198fc9701d1ccaca4d1c28678a0c
---

# Story 9.10: Optimistic-Concurrency Hardening — Flat, Structure, Reading & Tariff

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the team maintaining this app,
I want concurrent edits to Flat structure, meter readings, and tariffs to fail loudly instead of silently overwriting each other,
so that a race between two sessions/tabs never loses data without anyone noticing.

## Acceptance Criteria

1. **Given** no optimistic-concurrency control exists for `Flat`, Room/PowerPoint/Device structure edits, `MeterReading` corrections, or `Tariff` PATCH updates — a recurring gap across `DeleteFlatFunction.cs`, `UpdateFlatStructureFunction.cs`, `PatchReadingFunction.cs`, `PatchTariffFunction.cs`, `flatStructureApi.ts` — explicitly **not** resolved by Story 6.1's `RowVersion` addition, which was deliberately scoped to `ImportJob` only (see Dev Notes — `SmartPlugDailyData`/`SmartPlugIntervalData` were named in that story's AC but never actually implemented; only `ImportJob` has a real `RowVersion` column today), **when** this story is implemented, **then** a `RowVersion` concurrency-token column (`byte[]`, EF Core `.IsRowVersion()`, mirroring `ImportJobConfiguration.cs:21`'s exact pattern) is added to `Flat`, `Room`, `PowerPoint`, `Device`, `MeterReading`, and `Tariff` entities.
2. **Given** the new `RowVersion` columns, **when** a PATCH/PUT/DELETE request's `rowVersion` doesn't match the current DB value, **then** the request fails with a `409 Conflict` Problem Details response instead of silently last-write-wins, across `DeleteFlatFunction`, `UpdateFlatStructureFunction`, `PatchReadingFunction`, `PatchTariffFunction`, **and `PatchFlatFunction`** (see AC4 — gap found during story creation: `PatchFlatFunction.cs` writes to `Flat` and was not named in the epic's Function list, but the epic's own catch-all clause — "any other Function writing to these entities" — requires it).
3. **Given** this is a cross-cutting hardening pass, **when** implemented, **then** one consolidated migration adds all six new `RowVersion` columns together (not six separate migrations), and regression tests cover at least one concurrent-conflict scenario per entity type.
4. **Gap found during story creation:** two design gaps in the epic's AC text would silently break the feature if not resolved explicitly here:
   - **(a) `PatchFlatFunction.cs` is in scope.** It mutates `Flat.Name`/`AnnualKwhBaseline`/`PlannedAnnualSpend` (`api/Features/Flats/PatchFlatFunction.cs:68-70`) but is not named in the epic's Function list. Per AC2's own "any other Function writing to these entities" clause, it must get the identical 409-on-mismatch treatment as the other four Functions, or `Flat` edits remain silently last-write-wins despite AC1 adding the column.
   - **(b) Room/PowerPoint/Device do not get individually-checked `RowVersion`s.** `UpdateFlatStructureFunction.cs` fully replaces a Flat's entire room tree on every save (`db.Rooms.RemoveRange(existingRooms)` then `db.Rooms.AddRange(newRooms)`, `:86,113`) — every save deletes all existing Room/PowerPoint/Device rows and inserts brand-new ones with fresh PKs and fresh `RowVersion`s. There is no stable child row for a client to hold a `RowVersion` against between two PUTs. The concurrency check for the whole structure-edit therefore checks the **parent `Flat`'s `RowVersion`** — see Dev Notes "Structure-edit concurrency check" for why this requires an explicit forced-touch of the `Flat` row, and why Room/PowerPoint/Device still get the schema-level column (for AC1 literalness and future per-row editing) without an individual check being meaningful today.

## Tasks / Subtasks

- [x] **Task 1: Add `RowVersion` to all six entities + configurations (AC: 1)**
  - [x] Add `public byte[] RowVersion { get; set; } = [];` to `Flat.cs`, `Room.cs`, `PowerPoint.cs`, `Device.cs`, `MeterReading.cs`, `Tariff.cs` — **use `= []`, not `= null!`.** Story 6.1's dev notes record that EF Core's `InMemory` test provider throws `DbUpdateException: Required properties '{RowVersion}' are missing` on insert when a `RowVersion` property is null, because InMemory never auto-generates a value the way real SQL Server does (`_bmad-output/implementation-artifacts/6-1-import-pipeline-infrastructure-upload-job-tracking-and-blob-trigger.md`, Completion Notes). `ImportJob.cs:28` already uses `= []` for this exact reason — copy it verbatim.
  - [x] Add `builder.Property(x => x.RowVersion).IsRowVersion();` to `FlatConfiguration.cs`, `RoomConfiguration.cs`, `PowerPointConfiguration.cs`, `DeviceConfiguration.cs`, `MeterReadingConfiguration.cs`, `TariffConfiguration.cs` — one line each, identical to `ImportJobConfiguration.cs:21`. No `HasColumnType` call alongside it — `.IsRowVersion()` is the entire Fluent API surface needed (confirmed by the only existing instance in this codebase).
  - [x] Run `dotnet ef migrations list` first to confirm current order, then generate **one** migration (e.g. `AddOptimisticConcurrencyRowVersions`) containing all six `ALTER TABLE ... ADD RowVersion rowversion NOT NULL` statements. Do not split into six migrations — AC3 requires one consolidated migration.
  - [x] `dotnet ef database update` against the local dev Azure SQL DB and confirm all six `RowVersion` columns exist with type `rowversion`/`timestamp`, per this project's "always test locally before pushing" rule.

- [x] **Task 2: Add a shared concurrency-token helper (`api/Shared/`)**
  - [x] Create `api/Shared/ConcurrencyExtensions.cs` following the exact style of `api/Shared/DecimalPrecisionValidatorExtensions.cs` / `FunctionContextExtensions.cs` (static class, static extension methods, no DI). Provide two members:
    ```csharp
    public static class ConcurrencyExtensions
    {
        public static bool TryParseRowVersion(string? base64, out byte[] rowVersion)
        {
            rowVersion = [];
            if (string.IsNullOrEmpty(base64)) return false;
            try { rowVersion = Convert.FromBase64String(base64); return true; }
            catch (FormatException) { return false; }
        }

        public static void ApplyRowVersionCheck<TEntity>(this Microsoft.EntityFrameworkCore.DbContext db, TEntity entity, byte[] rowVersion)
            where TEntity : class =>
            db.Entry(entity).Property("RowVersion").OriginalValue = rowVersion;
    }
    ```
    This is intentionally the smallest possible shared surface — parsing + applying — not a general concurrency framework, matching Story 6.1's explicit note that no general-purpose retry/conflict framework should be built beyond the minimal pattern. The 409-response construction and the `try { SaveChangesAsync } catch (DbUpdateConcurrencyException)` block stay inline in each Function (this codebase's established per-Function duplication convention for response shaping — see `TariffResolver`/`KpiCalculator`'s per-engine tariff-resolution duplication in `project-context.md`), not folded into the helper.
  - [x] Every Function below returns this exact 409 body on a caught `DbUpdateConcurrencyException`: `new ObjectResult(new { title = "Conflict", status = 409, detail = "This record was modified by another request. Reload and try again." }) { StatusCode = 409 }` — matches this project's anonymous-object Problem Details convention (no typed `ProblemDetails` class, per-Function literal, exactly like every existing 400/403/404/422 in these files).
  - [x] Missing/unparseable `rowVersion` in the request is a **400**, not a silent skip-the-check: `new BadRequestObjectResult(new { title = "Bad Request", status = 400, detail = "rowVersion is required." })`. Deliberate design decision (not stated explicitly in the epic AC): if `rowVersion` were optional, a caller could bypass the entire concurrency check by omitting it, defeating AC2's "never silently last-write-wins" intent. Every write path touched by this story must now always send it.

- [x] **Task 3: `PatchFlatFunction.cs` — add concurrency check (AC: 2, 4a)**
  - [x] Add `RowVersion` (`byte[]`) to `PatchFlatRequest` and `RowVersion` (`byte[]`) to `FlatResponse` in `FlatModels.cs`. Extract `rowVersion` from the incoming `JsonObject obj` the same way `annualKwhBaseline` is extracted today (`obj["rowVersion"]?.GetValue<string>()` → `ConcurrencyExtensions.TryParseRowVersion`), **before** the validator call — return 400 immediately if missing/unparseable (Task 2's rule).
  - [x] Right before `await db.SaveChangesAsync(ct);` (`PatchFlatFunction.cs:72`), call `db.ApplyRowVersionCheck(flat, request.RowVersion);`. Wrap the existing `SaveChangesAsync` call in `try { ... } catch (DbUpdateConcurrencyException) { return 409; }`.
  - [x] Return `flat.RowVersion` in the `FlatResponse` constructor call (`:74`) so the caller has the new current version for its next write.
  - [x] Do not touch the existing `Name`/`AnnualKwhBaseline`/`PlannedAnnualSpend` extraction/validation logic (`:38-66`) — this task only adds the `rowVersion` field and the save-time check around it.

- [x] **Task 4: `DeleteFlatFunction.cs` — add concurrency check (AC: 2)**
  - [x] `DeleteFlatFunction` currently has no request body at all (`DELETE` with no reader). Add JSON body parsing identical in shape to `PatchFlatFunction.cs:30-36` (read body, `JsonNode.Parse`, expect a `JsonObject` with a `rowVersion` field) — return 400 for a missing/invalid body or missing/unparseable `rowVersion`.
  - [x] Call `db.ApplyRowVersionCheck(flat, rowVersion);` after `await db.LoadFlatCascadeChildrenAsync(flatGuid, ct);` and before `db.Flats.Remove(flat)` (`:36-38`) — order relative to `LoadFlatCascadeChildrenAsync` doesn't matter, but it must be set on the tracked `flat` entity before `SaveChangesAsync`.
  - [x] Wrap `await db.SaveChangesAsync(ct);` (`:39`) in the same try/catch → 409 pattern as Task 3.

- [x] **Task 5: `UpdateFlatStructureFunction.cs` — structure-edit concurrency check via parent `Flat` (AC: 2, 4b)**
  - [x] Add `RowVersion` (`byte[]`) to `UpdateFlatStructureRequest` and `RowVersion` (`byte[]`) to `FlatStructureResponse` in `FlatStructureModels.cs`. This is deserialized automatically by the existing `JsonSerializer.DeserializeAsync<UpdateFlatStructureRequest>` call (`:46`) — `byte[]` round-trips through base64 with zero extra code, unlike the `JsonNode`-based Functions. **Do not** add `RowVersion` to `RoomInput`/`PowerPointInput`/`DeviceInput` — per AC4b, every save deletes and recreates the entire room tree, so there is no stable child row across two PUTs to compare a per-room version against.
  - [x] After the existing ownership check (`:35-41`) and before the validator call, apply `db.ApplyRowVersionCheck(flat, request.RowVersion);`.
  - [x] **Critical, non-obvious step:** a SQL Server `rowversion` column only advances when its *own row* is UPDATEd — deleting/inserting child `Room` rows does **not** bump the parent `Flat` row's `RowVersion`. Without an explicit touch, this check would never actually catch a structure-only conflict (two tabs both PUT the same stale structure would both succeed). Force EF Core to include the `Flat` row in the generated UPDATE by marking it modified: `db.Entry(flat).State = EntityState.Modified;` right after the `ApplyRowVersionCheck` call, before `SaveChangesAsync`. This produces a real (if functionally no-op) `UPDATE Flats SET ... WHERE FlatId = @p AND RowVersion = @rv`, which both performs the concurrency check *and* advances `Flat.RowVersion` so the next load/save cycle has a fresh token.
  - [x] Wrap `await db.SaveChangesAsync(ct);` (`:114`) in the try/catch → 409 pattern.
  - [x] Return `flat.RowVersion` (now the freshly-bumped value, post-save) in the `FlatStructureResponse` constructor (`:116-141`).
  - [x] `GetFlatStructureFunction.cs` (a **new** UPDATE file for this story, not previously touched) must also add `flat.RowVersion` to its own `FlatStructureResponse` construction (`:45-71`) — this is the initial-load path the frontend uses to obtain the token before the *first* save, so it must expose the same field the PUT response does.

- [x] **Task 6: `PatchReadingFunction.cs` — add concurrency check (AC: 2)**
  - [x] Add `RowVersion` (`byte[]`) to `PatchReadingRequest` and to `ReadingResponse` in `ReadingModels.cs` — deserializes automatically via the existing `JsonSerializer.DeserializeAsync<PatchReadingRequest>` call (`:54`), same as Task 5.
  - [x] Apply `db.ApplyRowVersionCheck(reading, request.RowVersion);` after the existing "unchanged value" early-return (`:83-88`, which must stay a no-op — no version check needed when nothing is actually being written) and before the mutation lines (`:90-93`).
  - [x] Wrap `await db.SaveChangesAsync(ct);` (`:95`) in the try/catch → 409 pattern.
  - [x] Return `reading.RowVersion` in both `ReadingResponse` construction sites (`:85-86` unchanged-value path, and `:97-98` mutated path).

- [x] **Task 7: `PatchTariffFunction.cs` — add concurrency check (AC: 2)**
  - [x] Add `RowVersion` (`byte[]`) to `PatchTariffRequest` and to `TariffResponse` in `TariffModels.cs`. Extract from the `JsonObject` the same way `pricePerKwh` etc. are extracted today (`:61-89`) — 400 on missing/unparseable, before the validator call.
  - [x] Apply `db.ApplyRowVersionCheck(tariff, request.RowVersion);` after the existing `lockBlocksPriceUpdate` gate (`:116-123`, which must still short-circuit with 422 before any version check — a locked tariff is rejected regardless of version) and before the mutation lines (`:125-132`).
  - [x] Wrap `await db.SaveChangesAsync(ct);` (`:134`) in the try/catch → 409 pattern.
  - [x] Return `tariff.RowVersion` in the `TariffResponse` construction (`:136-143`).

- [x] **Task 8: Backend tests — one conflict scenario per entity type (AC: 3)**
  - [x] For each of `PatchFlatFunctionTests.cs`, `DeleteFlatFunctionTests.cs`, `UpdateFlatStructureFunctionTests.cs`, `PatchReadingFunctionTests.cs`, `PatchTariffFunctionTests.cs`: add a private nested `ConcurrencyConflictDbContext : AppDbContext` that overrides `SaveChangesAsync` to throw `DbUpdateConcurrencyException` on the Nth call — copy the exact pattern already in `ProcessImportFunctionTests.cs:88-101` (`_saveCount` counter, throw on the save that represents "someone else already saved"). **Do not** try to make `InMemory` detect a real mismatch by passing a wrong `rowVersion` byte array — `InMemory` does not enforce rowversion semantics at all (confirmed by Story 6.1's identical finding), so a genuinely mismatched `OriginalValue` will silently succeed under `InMemory`, not throw. The override-`SaveChangesAsync` trick is the only way to exercise the catch/409 code path in these tests.
  - [x] One new `[Fact]` per file asserting: `result.ShouldBeOfType<ObjectResult>()` with `StatusCode == 409`, and (where applicable) that the entity's DB value is unchanged from the pre-conflict seed (proving the write did not silently apply).
  - [x] `UpdateFlatStructureFunctionTests.cs`'s single new conflict test covers Room/PowerPoint/Device collectively (AC3's "per entity type" requirement for these three is satisfied by this one test, since they share the one write path per Task 5/AC4b — do not attempt separate Room/PowerPoint/Device-level conflict tests, there is no code path that would exercise them independently).
  - [x] Every **existing** passing test in these five files that currently sends a valid PATCH/PUT/DELETE request must be updated to include a matching `rowVersion` in its request body (base64 of whatever byte array the test's seed helper assigns to the entity — e.g. seed with `RowVersion = [1,2,3]` and send `Convert.ToBase64String([1,2,3])`). This is the single largest source of test breakage in this story — run the full suite early and often, not just at the end.
  - [x] Add one new negative test per file for the missing/malformed `rowVersion` → 400 case (Task 2's rule).

- [x] **Task 9: Frontend — thread `rowVersion` through every touched write path**
  - [x] `client/src/lib/apiClient.ts`: extend `delete` to accept an optional body, matching `patch`/`put`'s shape: `delete: <T>(path: string, body?: unknown) => request<T>(path, { method: 'DELETE', body: body !== undefined ? JSON.stringify(body) : undefined })`.
  - [x] `flatStructureApi.ts`: add `rowVersion: string` to `FlatStructureResponse` and `UpdateFlatStructureRequest`.
  - [x] `readingApi.ts`: add `rowVersion: string` to `ReadingResponse` and `PatchReadingRequest`.
  - [x] `tariffApi.ts`: add `rowVersion: string` to `TariffResponse` and `PatchTariffRequest`.
  - [x] `settingsApi.ts`: add `flatRowVersion: string | undefined` to `UserSettings`, and `rowVersion: string` to `PatchFlatBody` and `FlatData`. Change `deleteFlat` to `deleteFlat(flatId: string, rowVersion: string) => apiClient.delete<void>(\`/flats/${flatId}\`, { rowVersion })`.
  - [x] Backend counterpart: add `FlatRowVersion` (`byte[]`) to `UserSettingsResponse` (`SettingsModels.cs`), populate it in both `GetUserSettingsFunction.cs:43-51` and `UpdateUserSettingsFunction.cs:134-142` from `flat?.RowVersion`.
  - [x] `useUpdateFlatStructure.ts` / `usePatchReading.ts` / `usePatchTariff.ts` / `usePatchFlat.ts` / `useDeleteFlat.ts`: thread the new field through their `mutationFn` signatures without changing invalidation/error-handling logic. For `usePatchReading` and `useDeleteFlat`, which take narrow positional args rather than a spread `body`, add `rowVersion` as an explicit new parameter.
  - [x] Call sites — each must pass the `rowVersion` already present on the entity/query data it's already holding (no new fetches needed):
    - `FlatStructureEditor.tsx`: all three `mutate(payload, ...)` call sites (`:105`, `:137`, `:157`) need `rowVersion` merged into the payload from `data.rowVersion` (the currently-loaded `useFlatStructure` result) — since this component sends multiple sequential PUTs per session (per-room autosave + page-level save), track the *current* version in a ref/state updated from each mutation's own response (`onSuccess`) so the second save in a session uses the version returned by the first, not the stale initial-load value.
    - `TariffForm.tsx:152` (`patchMutateAsync`): source from the `tariff: TariffResponse` prop already in scope — `tariff.rowVersion`.
    - `ReadingHistorySheet.tsx` (`mutate` call, inside the editing-row component holding `reading: ReadingResponse`): source from `reading.rowVersion`.
    - `FlatBaselineEdit.tsx:83`, `FlatSettingsCard.tsx:38`, `SettingsPage.tsx:39` (all three call `patchFlat({ flatId, body })`): source from `settings.flatRowVersion` (all three already read `settings` via `useUserSettings()`).
    - `FlatDeleteConfirm.tsx:22` (`deleteFlat(flatId, ...)`): needs a new `flatRowVersion` prop threaded from its only caller, `AccountSettings.tsx:57-61`, sourced from `settings.flatRowVersion` (same `useUserSettings()` data already in scope there).
  - [x] No new UI component or error-message copy is required for the 409 case — `apiClient.ts`'s existing `!res.ok` handling already surfaces `problem.detail` via `mutation.error`, and every touched component already renders an inline error banner keyed off `isError`/`mutation.error` per this project's established mutation-error convention. A 409's `detail` string ("This record was modified by another request. Reload and try again.") will display there unchanged.

- [x] **Task 10: Full verification**
  - [x] `dotnet test api.Tests/api.Tests.csproj` — full suite green, including the 5 new conflict tests, 5 new malformed-`rowVersion` tests, and every pre-existing test updated per Task 8.
  - [x] `npx vitest run` (from `client/`) — full suite green; if any existing frontend test mocks `flatStructureApi`/`readingApi`/`tariffApi`/`settingsApi` with a response object missing the new `rowVersion`/`flatRowVersion` field, add it to the mock (TypeScript will flag these as compile errors first).
  - [x] `dotnet ef migrations has-pending-model-changes` (or re-run `dotnet ef migrations list`) after the migration to confirm the model and migration are back in sync.

### Review Findings

- [x] [Review][Patch] No frontend save flow refreshes its tracked `rowVersion` after a real 409 conflict — `FlatStructureEditor`'s ref, `TariffForm`/`ReadingHistorySheet`'s prop-sourced value, and `FlatDeleteConfirm`'s `flatRowVersion` prop are never re-fetched on `onError`, so a user who retries after a genuine conflict resubmits the identical stale value and gets another 409 indefinitely. Resolved via user decision (2026-07-19): add a refetch-on-error at each of the 4 call sites so a subsequent retry can succeed. [client/src/features/flat-structure/components/FlatStructureEditor.tsx, client/src/features/readings/components/ReadingHistorySheet.tsx, client/src/features/tariffs/components/TariffForm.tsx, client/src/features/settings/components/FlatDeleteConfirm.tsx]
- [x] [Review][Patch] A non-string `rowVersion` JSON value (number/bool/array/object) crashes with an unhandled 500 instead of a 400 — `obj["rowVersion"]?.GetValue<string>()` throws `InvalidOperationException` on a type mismatch instead of returning null/failing gracefully, in the three `JsonNode`-based Functions. [api/Features/Flats/PatchFlatFunction.cs:54, api/Features/Flats/DeleteFlatFunction.cs:49, api/Features/Tariffs/PatchTariffFunction.cs:91]
- [x] [Review][Patch] `ConcurrencyExtensions.TryParseRowVersion` accepts a whitespace-only string as a valid token — `Convert.FromBase64String` ignores whitespace and returns an empty `byte[]` instead of throwing `FormatException`, so a `" "` rowVersion silently passes as "provided" and produces a spurious 409 instead of the correct 400 in the three Functions that don't separately check `Length > 0`. [api/Shared/ConcurrencyExtensions.cs:7-13]
- [x] [Review][Patch] Task 8's "malformed rowVersion → 400" negative test was never actually written — every new test across all 5 files is named `..._MissingRowVersion_...` and sends `rowVersion: null`; none sends a non-empty, non-decodable value, so `TryParseRowVersion`'s `catch (FormatException)` branch has zero coverage despite Task 10's checklist claiming "5 new malformed-`rowVersion` tests" exist. [api.Tests/Features/Flats/PatchFlatFunctionTests.cs, api.Tests/Features/Flats/DeleteFlatFunctionTests.cs, api.Tests/Features/FlatStructure/UpdateFlatStructureFunctionTests.cs, api.Tests/Features/Readings/PatchReadingFunctionTests.cs, api.Tests/Features/Tariffs/PatchTariffFunctionTests.cs]
- [x] [Review][Patch] Inconsistent/silent failure when `flatRowVersion` is momentarily unavailable client-side — `FlatBaselineEdit`/`FlatSettingsCard` silently no-op with zero user feedback (unlike their own sibling validation-failure branches, which do call `setSubmitError`/`setEditError`), and `SettingsPage`'s `isPlannedAnnualSpendSaveError` condition (`missingFlatIdError && !settings?.flatId`) can never actually surface for this case — the stale `!settings?.flatId` clause is `false` whenever `flatId` is present and only `flatRowVersion` is missing. [client/src/features/settings/components/FlatBaselineEdit.tsx:61, client/src/features/settings/components/FlatSettingsCard.tsx:36, client/src/features/settings/SettingsPage.tsx:34-42]

## Dev Notes

### Story 6.1's `RowVersion` precedent — and a correction to the epic's own AC text

- AC1's epic text says Story 6.1 added `RowVersion` to `ImportJob`, `SmartPlugDailyData`, and `SmartPlugIntervalData`. **Verified false as written** — only `ImportJob.cs:28` / `ImportJobConfiguration.cs:21` actually have a `RowVersion` property/`.IsRowVersion()` call today (confirmed by full-repo grep: `SmartPlugDailyDataConfiguration.cs`/`SmartPlugIntervalDataConfiguration.cs` have no such property). Story 6.1's own AC6 text named all three tables aspirationally, but the implementation only touched `ImportJob`. This doesn't change this story's scope (Flat/Room/PowerPoint/Device/MeterReading/Tariff, per the epic's actual list), but don't be surprised if you go looking for a second/third precedent example and only find one — `ImportJobConfiguration.cs:21` is it.
- The exact EF Core mechanics you're replicating: `.IsRowVersion()` on a `byte[]` property with no `HasColumnType` alongside it. A `DbUpdateConcurrencyException` on `SaveChangesAsync` is the only signal — there is no separate "check first" query, the check happens transactionally as part of the UPDATE/DELETE's `WHERE RowVersion = @p` clause.

### Structure-edit concurrency check — why it targets `Flat`, not `Room`

`UpdateFlatStructureFunction` is a full-tree replace, not an in-place edit (`RemoveRange(existingRooms)` + `AddRange(newRooms)` every single save — confirmed at `UpdateFlatStructureFunction.cs:84-113`). This means:
- Room/PowerPoint/Device get fresh PKs and fresh `RowVersion`s on literally every save, including a save that changes nothing about that specific room. A client can never hold a stable child-row `RowVersion` across two PUTs.
- The only entity whose identity is stable across saves is the `Flat` itself. So the concurrency check for "did the structure change under me since I loaded it" has to be: does the `Flat`'s `RowVersion` at save-time match what I loaded?
- But `Flat`'s own row is otherwise untouched by a structure edit (only its children change) — so its `rowversion` column would never advance on its own. The `db.Entry(flat).State = EntityState.Modified` trick in Task 5 is what forces SQL Server to actually bump it. Skipping this step is the single most likely way this story silently fails to protect anything for the structure-edit path specifically — the code will compile, the happy path will work, and the conflict test (which manually throws via `ConcurrencyConflictDbContext`, not via real detection) will even pass, but two real concurrent tabs would both succeed against real SQL Server.

### Base64 wire format

`byte[]` round-trips through `System.Text.Json` as a base64 string automatically in both directions — this applies to every `JsonSerializer.DeserializeAsync<T>`-based Function (`UpdateFlatStructureFunction`, `PatchReadingFunction`) with zero extra code once the record field is typed `byte[]`. The three `JsonNode`-based manual parsers (`PatchFlatFunction`, `DeleteFlatFunction`, `PatchTariffFunction`) must extract the field as `string` (`obj["rowVersion"]?.GetValue<string>()`) and convert it themselves via `ConcurrencyExtensions.TryParseRowVersion` (Task 2) — `JsonNode`/`JsonObject` indexing doesn't know about the target C# type the way the serializer-based path does.

### Previous story (9.9) — conventions confirmed, no direct carryover

Story 9.9 (`PatchFlatFunction.cs`'s explicit-null handling) is unrelated in feature area but confirms conventions this story must also follow: anonymous-object Problem Details literals (not a typed class), xUnit + Shouldly + the `MakeDb`/`MakeFunctionContext`/`SeedFlatAsync`/`MakeRequest` test helper shape already present in every touched test file, and the discipline of explicitly verifying "does this backend contract change actually require a frontend change" rather than assuming — this story's own Task 9 is the frontend answer to that same question, and the answer here is yes (unlike 9.9's "no frontend change needed" conclusion), because every touched write path becomes stricter, not more permissive.

### Testing Requirements Summary

- Backend: xUnit + Shouldly, `InMemory` EF Core provider — this provider **does not** enforce SQL Server's real `rowversion` semantics (per `project-context.md`: "Do not write InMemory tests that rely on SQL-specific behaviour"). Every conflict test must use the `ConcurrencyConflictDbContext`-override-`SaveChangesAsync` technique already proven in `ProcessImportFunctionTests.cs:88-101`, not a mismatched-byte-array approach.
- Frontend: Vitest + Testing Library; any existing test that mocks one of the five touched API modules with a literal response object will fail TypeScript compilation once the new required fields are added to the response types — this is expected and is how you'll find every mock that needs updating (compiler-driven, not runtime-driven).

### Project Structure Notes

- No new feature folders. Six entity files, six configuration files, one migration, five Function files, one additional Function file (`GetFlatStructureFunction.cs`) touched for response-shape parity, one new shared helper (`api/Shared/ConcurrencyExtensions.cs`), four DTO/model files, five test files, one `apiClient.ts` signature extension, four frontend API modules, five mutation hooks, and roughly eight component call sites (per Task 9's list) — all within existing files/folders, matching this project's VSA slice conventions. No new backend or frontend feature slice is created by this story.
- `api/Shared/ConcurrencyExtensions.cs` follows the exact precedent of `api/Shared/DecimalPrecisionValidatorExtensions.cs` (Story 9.7) — a narrow, single-purpose static extension class, not a general framework, consistent with this codebase's stated aversion to premature abstraction.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-9-unified-save-affordance-fix-and-pre-epic-10-hardening.md#Story 9.10] — verbatim epic AC1-AC3.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md:207,226,269,278] — the four deferred-work entries this story resolves (`PatchReadingFunction.cs`, `PatchTariffFunction.cs:102`, `DeleteFlatFunction.cs`/`UpdateFlatStructureFunction.cs`, `flatStructureApi.ts`), each explicitly promoted to Story 9.10 and each confirming the "not resolved by Story 6.1" scoping.
- [Source: _bmad-output/implementation-artifacts/6-1-import-pipeline-infrastructure-upload-job-tracking-and-blob-trigger.md] — the only prior `RowVersion`/optimistic-concurrency implementation in this codebase; source of the `= []` InMemory-initializer pitfall and the `ConcurrencyConflictDbContext` test-simulation technique this story reuses directly.
- [Source: api/Data/Entities/ImportJob.cs:28, api/Data/Configurations/ImportJobConfiguration.cs:21] — the exact pattern being replicated across six new entities.
- [Source: api/Features/Flats/PatchFlatFunction.cs, DeleteFlatFunction.cs, FlatModels.cs] — current state of the two Flat-writing Functions this story modifies (Task 3, 4).
- [Source: api/Features/FlatStructure/UpdateFlatStructureFunction.cs, GetFlatStructureFunction.cs, FlatStructureModels.cs] — current full-replace structure-edit implementation (Task 5); confirms no per-room update path exists.
- [Source: api/Features/Readings/PatchReadingFunction.cs, ReadingModels.cs] — current reading-correction implementation (Task 6).
- [Source: api/Features/Tariffs/PatchTariffFunction.cs, TariffModels.cs] — current tariff-patch implementation, including the pre-existing lock-check gate that must still run before the version check (Task 7).
- [Source: api/Features/Settings/GetUserSettingsFunction.cs, UpdateUserSettingsFunction.cs, SettingsModels.cs] — confirms all four `patchFlat`/`deleteFlat` frontend call sites source their Flat data from `UserSettingsResponse` via `useUserSettings()`, not from `GetFlatsFunction`'s `FlatSummary` — scoping Task 9's backend DTO changes to `UserSettingsResponse` only, not `FlatSummary`.
- [Source: client/src/lib/apiClient.ts, flatStructureApi.ts, readingApi.ts, tariffApi.ts, settingsApi.ts] — current frontend API layer shapes.
- [Source: client/src/features/flat-structure/components/FlatStructureEditor.tsx:43,95-161, tariffs/components/TariffForm.tsx:46,152, readings/components/ReadingHistorySheet.tsx, settings/components/{FlatBaselineEdit,FlatSettingsCard,FlatDeleteConfirm,AccountSettings}.tsx, settings/SettingsPage.tsx] — every UI call site identified for Task 9, confirmed by direct grep of every `usePatchReading`/`usePatchTariff`/`usePatchFlat`/`useDeleteFlat`/`useUpdateFlatStructure` usage in `client/src`.
- [Source: api.Tests/Features/SmartPlugImport/ProcessImportFunctionTests.cs:88-101] — the `ConcurrencyConflictDbContext` test-double pattern this story's Task 8 replicates in five more test files.
- [Source: _bmad-output/project-context.md#EF Core, #Error responses — Problem Details only, #Code Quality & Style Rules] — Problem Details anonymous-object convention, `Scoped` DI lifetime rules (unaffected — no new services), decimal/DateTimeOffset invariants (unaffected — no new decimal/date fields).

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- `dotnet build` (api) — green after each of Tasks 1–7.
- `dotnet ef migrations add AddOptimisticConcurrencyRowVersions` + `dotnet ef database update` — one consolidated migration adding `RowVersion` to `Flats`, `Rooms`, `PowerPoints`, `Devices`, `MeterReadings`, `Tariffs`; applied successfully against the real dev Azure SQL DB.
- `dotnet test api.Tests/api.Tests.csproj` — RED after Tasks 3–7 (39 pre-existing tests failed, all because they no longer sent a `rowVersion`); GREEN after Task 8 fixes: 375/375 (365 pre-existing + 10 new: 5 conflict tests + 5 malformed/missing-`rowVersion` tests).
- `npx vitest run` (client) — RED after Task 9's type changes (10 runtime failures across 6 test files: stale mocks missing `rowVersion`/`flatRowVersion`); GREEN after fixes: 395/395 (385 pre-existing + a net +10 from `FlatStructureEditor`'s two-sequential-save regression case needing an explicit `rowVersion` assertion, offset by no new test files).
- `npx tsc --noEmit` (client) — 0 errors both before and after Task 9; note that `vi.mocked()`-typed mutate mocks did **not** catch the now-wrong call-site shapes at compile time (e.g. `useDeleteFlat`'s old single-string `mutate('flat-1')` call) — all 10 frontend breakages were caught only by the actual vitest run, not `tsc`. Do not rely on a clean `tsc` alone as proof a mutation-hook signature change is fully propagated.
- `dotnet ef migrations has-pending-model-changes` — "No changes have been made to the model since the last migration." (confirms model/migration sync per Task 10).
- `npm run lint` (client) — clean; only pre-existing unrelated warnings in `router.tsx`.

### Completion Notes List

- Added `RowVersion` (`byte[]`, `.IsRowVersion()`) to `Flat`, `Room`, `PowerPoint`, `Device`, `MeterReading`, `Tariff` via one consolidated migration (`20260719122743_AddOptimisticConcurrencyRowVersions`), mirroring `ImportJobConfiguration.cs`'s existing pattern exactly, including the `= []` (not `= null!`) initializer needed for the `InMemory` test provider.
- Added `api/Shared/ConcurrencyExtensions.cs` (`TryParseRowVersion`, `ApplyRowVersionCheck`) as the shared parsing/application helper; each Function keeps its own inline 409 response and `try/catch (DbUpdateConcurrencyException)` block, per the story's explicit "no general framework" scoping.
- Implemented the 409-on-mismatch check in `PatchFlatFunction`, `DeleteFlatFunction`, `UpdateFlatStructureFunction`, `PatchReadingFunction`, `PatchTariffFunction` — all five now require a `rowVersion` field (base64) and reject a missing/unparseable one with 400.
- `UpdateFlatStructureFunction` checks the parent `Flat`'s `RowVersion` (not per-Room) and explicitly forces `db.Entry(flat).State = EntityState.Modified` before saving, per AC4b — without this the full-tree-replace pattern would never actually advance or check the Flat's row version, silently defeating the protection while all tests still passed.
- Response DTOs (`FlatResponse`, `FlatStructureResponse`, `ReadingResponse`, `TariffResponse`, `UserSettingsResponse`) all now carry `RowVersion`/`FlatRowVersion` so every read path gives the client a fresh token; this required also touching `SubmitReadingFunction`, `GetReadingHistoryFunction`, `CreateTariffFunction`, `GetTariffsFunction`, `GetFlatStructureFunction`, `GetUserSettingsFunction` — none of which were named in the epic's Function list, but all construct one of the now-extended shared response records and wouldn't compile otherwise.
- **Correction to the story's own Dev Notes, discovered empirically during Task 8**: EF Core's `InMemory` provider *does* enforce concurrency-token comparison generically (throws `DbUpdateConcurrencyException` on a genuine `OriginalValue` mismatch against the stored value) — it just never auto-*generates* a new value the way SQL Server's real `rowversion` does. This first surfaced as an unexpected 409 in `RunAsync_SamePlugIdAcrossDifferentFlats_Succeeds` (a second, unrelated seeded `Flat` had never had its `RowVersion` explicitly set, so it defaulted to `[]` while the test's request claimed `[1,2,3]` as the original — a real mismatch). Fixed by always seeding a matching `RowVersion` on every test entity, not by changing production code. The `ConcurrencyConflictDbContext`-override technique (per Story 6.1 precedent) remains the correct, deterministic way to test the 409 code path — this finding doesn't invalidate it, it just means an *accidental* mismatch in unrelated tests is a real failure mode to watch for, not silently ignored by `InMemory` as the story's Dev Notes assumed.
- Frontend: extended `apiClient.delete` to accept an optional JSON body; added `rowVersion`/`flatRowVersion` to all five touched API modules' request/response types; updated `usePatchReading` and `useDeleteFlat` (positional-arg hooks) to accept it explicitly, while `useUpdateFlatStructure`/`usePatchTariff`/`usePatchFlat` needed no hook changes since their generic `body` parameter already carries the new field.
- `FlatStructureEditor.tsx` tracks the current `rowVersion` in a `useRef`, seeded from the initial GET and updated from each mutation's own response — necessary because this component fires multiple sequential PUTs per session (per-room autosave + page-level save), and using a stale initial-load version on the second save would cause a false 409 against the app's own prior save.
- `FlatDeleteConfirm` gained a new required `flatRowVersion` prop threaded from `AccountSettings` (its only caller); all three `patchFlat` call sites (`FlatBaselineEdit`, `FlatSettingsCard`, `SettingsPage`) source `rowVersion` from `settings.flatRowVersion` (all three already read the same `useUserSettings()` data) and now guard on its presence alongside the existing `flatId` guard.
- No new UI component or copy was added for the 409 case — it surfaces through each screen's existing `mutation.error`-driven inline error banner unchanged, per the story's Dev Notes.

### File List

**Backend — entities, configuration, migration**
- `api/Data/Entities/Flat.cs` (modified)
- `api/Data/Entities/Room.cs` (modified)
- `api/Data/Entities/PowerPoint.cs` (modified)
- `api/Data/Entities/Device.cs` (modified)
- `api/Data/Entities/MeterReading.cs` (modified)
- `api/Data/Entities/Tariff.cs` (modified)
- `api/Data/Configurations/FlatConfiguration.cs` (modified)
- `api/Data/Configurations/RoomConfiguration.cs` (modified)
- `api/Data/Configurations/PowerPointConfiguration.cs` (modified)
- `api/Data/Configurations/DeviceConfiguration.cs` (modified)
- `api/Data/Configurations/MeterReadingConfiguration.cs` (modified)
- `api/Data/Configurations/TariffConfiguration.cs` (modified)
- `api/Data/Migrations/20260719122743_AddOptimisticConcurrencyRowVersions.cs` (new)
- `api/Data/Migrations/20260719122743_AddOptimisticConcurrencyRowVersions.Designer.cs` (new)
- `api/Data/Migrations/AppDbContextModelSnapshot.cs` (modified, EF-generated)

**Backend — shared helper**
- `api/Shared/ConcurrencyExtensions.cs` (new)

**Backend — Functions and models**
- `api/Features/Flats/FlatModels.cs` (modified)
- `api/Features/Flats/PatchFlatFunction.cs` (modified)
- `api/Features/Flats/DeleteFlatFunction.cs` (modified)
- `api/Features/FlatStructure/FlatStructureModels.cs` (modified)
- `api/Features/FlatStructure/UpdateFlatStructureFunction.cs` (modified)
- `api/Features/FlatStructure/GetFlatStructureFunction.cs` (modified)
- `api/Features/Readings/ReadingModels.cs` (modified)
- `api/Features/Readings/PatchReadingFunction.cs` (modified)
- `api/Features/Readings/SubmitReadingFunction.cs` (modified)
- `api/Features/Readings/GetReadingHistoryFunction.cs` (modified)
- `api/Features/Tariffs/TariffModels.cs` (modified)
- `api/Features/Tariffs/PatchTariffFunction.cs` (modified)
- `api/Features/Tariffs/CreateTariffFunction.cs` (modified)
- `api/Features/Tariffs/GetTariffsFunction.cs` (modified)
- `api/Features/Settings/SettingsModels.cs` (modified)
- `api/Features/Settings/GetUserSettingsFunction.cs` (modified)
- `api/Features/Settings/UpdateUserSettingsFunction.cs` (modified)

**Backend — tests**
- `api.Tests/Features/Flats/PatchFlatFunctionTests.cs` (modified)
- `api.Tests/Features/Flats/PatchFlatValidatorTests.cs` (modified)
- `api.Tests/Features/Flats/DeleteFlatFunctionTests.cs` (modified)
- `api.Tests/Features/FlatStructure/UpdateFlatStructureFunctionTests.cs` (modified)
- `api.Tests/Features/Readings/PatchReadingFunctionTests.cs` (modified)
- `api.Tests/Features/Tariffs/PatchTariffFunctionTests.cs` (modified)

**Frontend — API layer and hooks**
- `client/src/lib/apiClient.ts` (modified)
- `client/src/features/flat-structure/api/flatStructureApi.ts` (modified)
- `client/src/features/flat-structure/components/draftModel.ts` (modified)
- `client/src/features/readings/api/readingApi.ts` (modified)
- `client/src/features/readings/hooks/usePatchReading.ts` (modified)
- `client/src/features/tariffs/api/tariffApi.ts` (modified)
- `client/src/features/settings/api/settingsApi.ts` (modified)
- `client/src/features/settings/hooks/useDeleteFlat.ts` (modified)
- `client/src/features/settings/hooks/useUserSettings.ts` (modified — code review fix, exposes `refetch`)

**Frontend — components**
- `client/src/features/flat-structure/components/FlatStructureEditor.tsx` (modified)
- `client/src/features/readings/components/ReadingHistorySheet.tsx` (modified)
- `client/src/features/tariffs/components/TariffForm.tsx` (modified)
- `client/src/features/tariffs/components/TariffList.tsx` (modified — code review fix, threads `onSaveConflict`)
- `client/src/features/settings/components/FlatDeleteConfirm.tsx` (modified)
- `client/src/features/settings/components/AccountSettings.tsx` (modified)
- `client/src/features/settings/components/FlatBaselineEdit.tsx` (modified)
- `client/src/features/settings/components/FlatSettingsCard.tsx` (modified)
- `client/src/features/settings/SettingsPage.tsx` (modified)

**Frontend — tests**
- `client/src/features/flat-structure/components/FlatStructureEditor.test.tsx` (modified)
- `client/src/features/settings/hooks/useDeleteFlat.test.ts` (modified)
- `client/src/features/settings/components/FlatDeleteConfirm.test.tsx` (modified)
- `client/src/features/settings/components/AccountSettings.test.tsx` (modified)
- `client/src/features/settings/components/FlatSettingsCard.test.tsx` (modified)
- `client/src/features/settings/SettingsPage.test.tsx` (modified)
- `client/src/features/readings/components/ReadingHistorySheet.test.tsx` (modified — code review fix, `refetch` mock)
- `client/src/features/tariffs/components/TariffList.test.tsx` (modified — code review fix, `refetch` mock)

**Other**
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified)

## Change Log

- 2026-07-19: Implemented cross-cutting optimistic-concurrency hardening across Flat, Room/PowerPoint/Device structure, MeterReading, and Tariff — one consolidated `RowVersion` migration, a shared `ConcurrencyExtensions` helper, and 409-on-mismatch handling in `PatchFlatFunction`, `DeleteFlatFunction`, `UpdateFlatStructureFunction`, `PatchReadingFunction`, `PatchTariffFunction`. Every read/write response DTO touching these entities now carries the current `RowVersion`, and the frontend threads it through all five affected write paths (including a new `flatRowVersion`-bearing `UserSettingsResponse` field and a ref-tracked current version in `FlatStructureEditor` for its multi-save-per-session flow). Backend 375/375, frontend 395/395, zero regressions. Status → review.
- 2026-07-19: Code review — 3-layer adversarial review (Blind Hunter, Edge Case Hunter, Acceptance Auditor), 32 raw findings triaged to 1 decision-needed + 4 patch (27 dismissed as noise, pre-existing convention, or already covered by an explicit spec decision). Decision resolved by Ralf: add refetch-on-error to the 4 frontend save flows rather than deferring. All 5 patches applied: (1) non-string `rowVersion` no longer crashes with 500 in the three `JsonNode`-based Functions, (2) whitespace-only `rowVersion` no longer silently passes as valid, (3) added the missing malformed-`rowVersion` negative test to all 5 backend test files, (4) fixed silent/inconsistent client-side feedback when `flatRowVersion` is momentarily unavailable (including a stale boolean-AND bug in `SettingsPage.tsx` that could never actually surface its own error banner), (5) all four frontend save flows now refetch and refresh their tracked `rowVersion` after a 409 so a retry can succeed. Backend 380/380 (+5), frontend 395/395, `tsc` and lint clean. Status → done.
