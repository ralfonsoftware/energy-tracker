---
baseline_commit: 45ee0a54ae7f80782d260868741a56d0a6453ab3
---

# Story 11.2: Insights Discovery Redelivery — DB-Level Idempotency Guard

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the team maintaining this app,
I want overlapping redelivery of the same insight-discovery queue message to be safe rather than racy,
so that a slow discovery run doesn't produce duplicate or corrupted `Insight` rows under Azure Storage Queue's visibility-timeout retry behavior.

## Acceptance Criteria

1. **Given** `ProcessInsightsFunction.cs`'s current guard only checks for and clears *existing* `Insight` rows, with no mechanism preventing two concurrent invocations for the same `RunId` from both passing that check, **when** implemented, **then** the function acquires an exclusive claim on the `InsightRun` row before proceeding — a `RowVersion` optimistic-concurrency column (following the exact pattern already used on `Flat`/`Tariff`/`Room`/`PowerPoint`/`MeterReading`/`Device` from Story 9.10) is added to `InsightRun`, and the transition `run.Status = InsightRunStatus.Processing` followed by `SaveChangesAsync` is attempted **before** any stale-`Insight` cleanup or detector work — such that only one concurrent invocation can win the transition; the other's `SaveChangesAsync` throws `DbUpdateConcurrencyException`.
2. **Given** a second, redelivered invocation loses the claim (its `SaveChangesAsync` throws `DbUpdateConcurrencyException` because the first invocation already changed the row's `RowVersion`), **when** it detects this, **then** it catches `DbUpdateConcurrencyException` specifically, logs the redelivery via `ILogger<ProcessInsightsFunction>` at `LogInformation` (this is a normal, expected outcome, not an error — do not log at `LogError` or `LogWarning`), and returns without running any detector or touching `Insight` rows.
3. **Given** the fix, **when** tested, **then** a new test in `ProcessInsightsFunctionTests.cs` simulates two concurrent invocations for the same `RunId` using two separate `AppDbContext` instances pointed at the same `UseInMemoryDatabase` name (a single shared context won't reproduce the race — see Dev Notes) and asserts exactly one set of detector writes results, with no duplicate or partial `Insight` rows, and the loser's `AppDbContext` shows zero `Insight` rows written by it.

## Tasks / Subtasks

- [x] Task 1: Add `RowVersion` to `InsightRun` (AC: #1)
  - [x] 1.1 Add `public byte[] RowVersion { get; set; } = [];` to `api/Data/Entities/InsightRun.cs`
  - [x] 1.2 Add `builder.Property(r => r.RowVersion).IsRowVersion();` to `api/Data/Configurations/InsightRunConfiguration.cs` (same line shape as `TariffConfiguration.cs`/`FlatConfiguration.cs`)
  - [x] 1.3 Generate the migration via `dotnet ef migrations add AddRowVersionToInsightRun` from `api/` (do NOT hand-write the migration file — `Data/Migrations/` is generated). Model it after `20260719122743_AddOptimisticConcurrencyRowVersions.cs`'s single-column `AddColumn<byte[]>(..., type: "rowversion", rowVersion: true, nullable: false, defaultValue: new byte[0])` shape, scoped to just the `InsightRuns` table this time.
  - [x] 1.4 Run `dotnet ef database update` locally to verify the migration applies cleanly (per this project's EF Core migration rule)
- [x] Task 2: Reorder `ProcessInsightsFunction.RunAsync` to claim before touching data (AC: #1, #2)
  - [x] 2.1 In `api/Features/Insights/ProcessInsightsFunction.cs`, move the `run.Status = InsightRunStatus.Processing; await db.SaveChangesAsync(ct);` transition to **before** the existing stale-`Insight` cleanup block (currently lines 59-64 run first, then the Processing transition at lines 66-67 — swap this order)
  - [x] 2.2 Wrap the (now-first) `Processing` transition's `SaveChangesAsync` call in its own `try`/`catch (DbUpdateConcurrencyException)`. On catch: `logger.LogInformation("InsightRun {RunId} redelivery lost the processing claim to a concurrent invocation.", discoveryMessage.RunId);` then `return;` immediately — before the outer try block that currently wraps stale-cleanup + Processing + detectors, so a lost claim never enters that block at all
  - [x] 2.3 Move the stale-`Insight` cleanup block (currently lines 59-64) to run immediately after the claim succeeds, still inside the existing outer `try` that already handles `OperationCanceledException` / generic `Exception` → `Failed` status
  - [x] 2.4 Update the existing inline comment above the stale-cleanup block (currently explains "Guards against Azure's at-least-once queue delivery re-invoking RunAsync...") to reflect that it now runs only after this invocation has won the exclusive claim
- [x] Task 3: Test coverage (AC: #3)
  - [x] 3.1 Add a new test to `api.Tests/Features/Insights/ProcessInsightsFunctionTests.cs`: seed a `Flat` + `InsightRun` (status `Pending`) via one `AppDbContext`, then create **two separate** `AppDbContext` instances against the **same** `UseInMemoryDatabase(name)` string, each loading the same `RunId`. Construct two `ProcessInsightsFunction` instances, one per context (each needs its own detector instances constructed against its own context, per the existing `MakeDb()`/detector-construction pattern). Run both `RunAsync` calls (e.g. via `Task.WhenAll`) against the same message.
  - [x] 3.2 Assert: exactly one of the two invocations reaches `InsightRunStatus.Complete` and writes detector output; the other returns early. Use a `WritingStandbyDetector`-style stand-in (already exists in the test file) on both invocations' detector sets to make the "did it write" outcome observable, and assert via a third, fresh verification `AppDbContext` against the same DB name that `db.Insights.Where(i => i.RunId == run.RunId).ToListAsync()` contains exactly the count one successful run would produce (not double).
  - [x] 3.3 Run the full `ProcessInsightsFunctionTests` suite plus the full backend suite (`dotnet test` from repo root) and confirm all existing tests still pass unmodified — the reordering in Task 2 must not change the outcome of any of the four existing tests (`AllDetectorsSucceed`, `OneDetectorThrows`, `UnhandledExceptionOutsideDetectors`, `RedeliveredMessage_ClearsStaleInsights`).

### Review Findings

- [x] [Review][Patch] Redelivery-while-Processing claim is a silent no-op — the guard doesn't actually stop the race it was built to fix [api/Features/Insights/ProcessInsightsFunction.cs:56-57] — `run.Status = InsightRunStatus.Processing;` re-assigns the *same* enum value when a redelivered invocation loads a row a still-running first invocation already flipped to `Processing`. EF Core's change tracker does not mark an unchanged property as `Modified`, so `SaveChangesAsync` issues no UPDATE, throws no `DbUpdateConcurrencyException`, and the second invocation silently falls through the claim block as if it had won — proceeding straight into stale-`Insight` cleanup and a full detector re-run concurrently with the still-active first invocation. This is exactly the "visibility timeout expires while the first attempt is still running" scenario the story exists to close (per Dev Notes), and it is not actually closed. Separately, a redelivery arriving after the run already reached `Complete` *is* a genuine value change (`Complete` → `Processing`) that succeeds without conflict, silently reopening and reprocessing an already-finished run. The new race test only exercises two invocations racing from a shared `Pending` start (both pre-load before either commits) — it never exercises redelivery against an already-`Processing` or already-`Complete` row, so it would not have caught either failure mode. Fix: guard the claim on the freshly-loaded `run.Status` — only attempt the `Pending` → `Processing` transition when the loaded status is actually `Pending`; treat any other status as "already claimed or already finished" and return early (mirroring the existing `DbUpdateConcurrencyException` catch's log-and-return shape). Add test coverage for both the already-`Processing` and already-`Complete` starting states. Update the stale-cleanup comment's "won the exclusive Processing claim" wording once the guard is genuinely exclusive.
- [x] [Review][Defer] `RowVersionSimulatingDbContext` only overrides the `SaveChangesAsync(CancellationToken)` overload [api.Tests/Features/Insights/ProcessInsightsFunctionTests.cs:43-51] — deferred, pre-existing test-double pattern (mirrors `FailingOnFirstSaveDbContext` in the same file); harmless today since no code path in this diff calls a different `SaveChanges` overload, but would silently stop simulating the row-version bump if one ever did.
- [x] [Review][Defer] Race test's forced pre-load of both contexts before either commits proves the mechanics work but doesn't exercise genuine async-scheduler interleaving [api.Tests/Features/Insights/ProcessInsightsFunctionTests.cs:114-119] — deferred; the InMemory provider's concurrency-token enforcement across independently-tracked contexts sharing one store is being trusted without a citation, and true interleaving (claim committing between the two loads) is the more realistic timing but isn't what's tested.
- [x] [Review][Defer] `catch (DbUpdateConcurrencyException)` around the claim save isn't scoped to the `InsightRun` entity specifically [api/Features/Insights/ProcessInsightsFunction.cs:54-70] — deferred; correct today since `run` is the only tracked-dirty entity at that point, but the surrounding comment asserts a strong "never touches Insight rows" guarantee that would silently stop being true if a future change makes another entity dirty before this save.
- [x] [Review][Defer] No timeout/reaper mechanism for a run stuck in `Processing` forever (e.g. process killed after the claim commits but before `Complete`/`Failed`, with no further redelivery) — deferred, pre-existing risk category not introduced by this diff; worth revisiting once the no-op guard above is fixed, since a stale `Processing` claim would then correctly block all future redelivery attempts rather than being silently bypassed.

## Dev Notes

### Why this story exists

Flagged by the Epic 10 retrospective (Action Item #3). Story 10.2 added a guard to `ProcessInsightsFunction.cs` that deletes pre-existing `Insight` rows for the `RunId` before detectors run — this closes the case where a message is redelivered *after* the first attempt fully finished. It does **not** close the case where Azure Storage Queue's visibility timeout expires *while the first attempt is still running*: Azure redelivers the same message to a second concurrent invocation, and both invocations can pass the "no stale rows yet" check before either has written anything, then both proceed to write detector output concurrently. No DB-level lease or lock serializes this today. This story adds that lock via the codebase's existing `RowVersion` optimistic-concurrency convention.

### Current `ProcessInsightsFunction.RunAsync` control flow (exact, before this story)

[Source: api/Features/Insights/ProcessInsightsFunction.cs:19-106]

```
1. Deserialize message → discoveryMessage
2. Load run = db.InsightRuns.SingleOrDefaultAsync(r => r.RunId == ...)
3. If run is null → log warning, return (unrelated to this story — a deleted Flat's cascade-deleted run)
4. try {
     a. staleInsights = db.Insights.Where(RunId == ...).ToList(); if any, RemoveRange + SaveChangesAsync   <-- MOVE to after (c)
     b. run.Status = Processing; SaveChangesAsync                                                          <-- MOVE to before (a), guard with try/catch(DbUpdateConcurrencyException)
     c. RunDetectorSafelyAsync x4 (each independently swallows its own exception)
     d. run.Status = Complete
   } catch (OperationCanceledException) { throw }
     catch (Exception ex) { log error; run.Status = Failed }
5. try { run.CompletedAt = now; SaveChangesAsync } catch swallow-and-log
```

**This story's reordering:** step (b) — the `Processing` claim — must execute **first**, wrapped in its own concurrency-specific catch that returns early on loss, before step (a)'s stale-Insight cleanup runs. This is the only way to guarantee AC #2's "returns without... touching Insight rows" — as written today, (a) runs unconditionally before any claim exists, so a losing invocation would still delete/query `Insight` rows before ever attempting the claim.

### The `RowVersion` claim mechanism — no manual WHERE clause needed

Because `run` is loaded via `db.InsightRuns.SingleOrDefaultAsync(...)` on a tracked `DbContext`, EF Core automatically captures the row's original `RowVersion` value at load time. Once `InsightRunConfiguration.cs` marks `RowVersion` with `.IsRowVersion()`, a plain `await db.SaveChangesAsync(ct)` after `run.Status = InsightRunStatus.Processing` already performs the equivalent of `UPDATE InsightRuns SET Status = @new WHERE RunId = @id AND RowVersion = @originalRowVersion` under the hood, and throws `DbUpdateConcurrencyException` if another writer changed the row first. **No manual SQL or `ExecuteUpdateAsync` is needed** — this matches the existing pattern used implicitly everywhere `RowVersion`-tracked entities are saved in this codebase (e.g. `PatchFlatFunction.cs`, `PatchTariffFunction.cs`), just without those files' explicit client-supplied-`RowVersion` header check (`ConcurrencyExtensions.ApplyRowVersionCheck`) since this is a queue-triggered function with no HTTP caller supplying an expected version — the two racing invocations' own DB-loaded original values naturally diverge once either one writes.

**Do not use `ConcurrencyExtensions.ApplyRowVersionCheck`** — that helper is for HTTP PATCH endpoints where the client supplies an expected `RowVersion` via request body/header. Here, both invocations load their own original value directly from the DB; no client-supplied value is involved.

### Why the two-`AppDbContext` test setup is required

A single shared `AppDbContext` instance across two `RunAsync` calls will **not** reproduce this race: the change tracker returns the same tracked entity instance for both calls (same key, same context), so the second call would see the first call's already-applied in-memory mutation rather than a stale original value — no concurrency conflict is possible. The existing four tests in `ProcessInsightsFunctionTests.cs` all use a single shared `db` from `SeedFlatAndRunAsync()`/`MakeDb()`. The new race test must instead point **two separate `AppDbContext` instances** (each a `new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(sameName).Options)`) at the **same** in-memory database name — EF Core's InMemory provider does enforce concurrency-token checks (including `IsRowVersion()`-configured properties) across independently-tracked contexts sharing one backing store, which is exactly what's needed here.

### File-by-file changes

- `api/Data/Entities/InsightRun.cs` — add `RowVersion` property (currently has `RunId`, `FlatId`, `Status`, `StartedAt`, `CompletedAt`, `Flat`; no `RowVersion` today)
- `api/Data/Configurations/InsightRunConfiguration.cs` — add `.IsRowVersion()` line; the existing filtered unique index `IX_InsightRuns_FlatId_ActiveOnly` (enforcing "at most one active run per **Flat**") is unrelated to this story and must not change — that index solves a different problem (preventing `TriggerInsightsFunction` from creating two concurrent runs for the same flat), not this story's per-`RunId` redelivery race
- `api/Features/Insights/ProcessInsightsFunction.cs` — reorder per Task 2; no signature changes, no new constructor dependencies
- `api.Tests/Features/Insights/ProcessInsightsFunctionTests.cs` — add the new race test; existing four tests must pass unmodified

### What NOT to touch

- `TriggerInsightsFunction.cs` — its own concurrency handling (the `IX_InsightRuns_FlatId_ActiveOnly` unique-index + `DbUpdateException` catch) is a separate, already-correct mechanism for a different race (two HTTP triggers for the same flat), out of scope here.
- `ScheduledInsightsFunction.cs` (if present) — not part of this story's scope; only `ProcessInsightsFunction.cs` is touched.
- The per-detector `RunDetectorSafelyAsync` isolation (each of the four detectors independently swallowing its own exception) — unrelated to this story, do not modify.
- `architecture.md`'s `InsightRuns` table schema line (`docs`/planning-artifacts, line ~225: `RunId (guid), FlatId FK, Status (enum), StartedAt, CompletedAt (nullable)`) does not list `RowVersion` — this is informational staleness only (same pattern Story 11.1 flagged for `TariffResolver.cs`); no doc update is in scope for this story.

### Testing Rules (from project context)

- xUnit + Shouldly (`.ShouldBe(...)`), matching every existing test in this file
- `EF Core InMemory` provider — no real SQL Server needed; concurrency-token checks work across independently-tracked contexts sharing one in-memory database name (see above)
- Test placement: extend the existing `api.Tests/Features/Insights/ProcessInsightsFunctionTests.cs` — do not create a new file
- Do not test `InsightRunConfiguration.cs` itself (EF Core config classes are trusted, per project rules)
- `dotnet ef` commands run from `api/` (matches how the existing `AddOptimisticConcurrencyRowVersions` migration was almost certainly generated — confirm working directory before running)

### Project Structure Notes

- No new files — this story only modifies three existing files (`InsightRun.cs`, `InsightRunConfiguration.cs`, `ProcessInsightsFunction.cs`) plus its test file, and adds one generated migration file pair (`.cs` + `.Designer.cs`) under `api/Data/Migrations/`
- Matches this codebase's established `RowVersion` optimistic-concurrency convention (Story 9.10) exactly — no new pattern introduced

### Previous Story Intelligence (Story 11.1)

- Story 11.1 (centralizing `ResolveTariff`) is unrelated in subject matter but confirms this epic's working rhythm: exact line numbers and current-state snippets in Dev Notes were verified correct and the dev agent needed zero deviation from the plan — same standard applied here.
- Story 11.1's review found and fixed: missing null-guard on new public surface, missing XML doc comments, a missing boundary-case test, and dangling blank lines after deleting code. Apply the same rigor here — this story also introduces a new public-ish surface (the `RowVersion` claim pattern) and deletes/reorders existing code blocks (watch for dangling blank lines after moving the stale-cleanup block in Task 2.3).
- Story 11.1's dev agent verified all six affected test suites and the full backend suite passed before marking done (451/451). Do the same here: run the full `dotnet test` suite, not just the modified file's tests.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-11-post-epic-10-hardening-and-technical-debt-resolution.md#Story 11.2] — epic-level AC and rationale
- [Source: api/Features/Insights/ProcessInsightsFunction.cs] — current implementation being modified
- [Source: api/Data/Entities/InsightRun.cs, api/Data/Configurations/InsightRunConfiguration.cs] — entity/config to modify
- [Source: api/Data/Configurations/TariffConfiguration.cs, api/Data/Configurations/FlatConfiguration.cs] — `.IsRowVersion()` pattern to replicate
- [Source: api/Data/Migrations/20260719122743_AddOptimisticConcurrencyRowVersions.cs] — migration shape to replicate (scoped to one table)
- [Source: api/Shared/ConcurrencyExtensions.cs] — existing `RowVersion` helpers; note `ApplyRowVersionCheck` does NOT apply to this story (see Dev Notes)
- [Source: api.Tests/Features/Insights/ProcessInsightsFunctionTests.cs] — existing test patterns and fixtures to extend
- [Source: api/Features/Insights/TriggerInsightsFunction.cs] — the *other* InsightRun concurrency mechanism (`IX_InsightRuns_FlatId_ActiveOnly`), out of scope but useful context for why that index doesn't already solve this story's problem

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- `dotnet ef migrations add AddRowVersionToInsightRun` from `api/` — generated `20260726182406_AddRowVersionToInsightRun.cs`/`.Designer.cs`, matching the single-column shape of `20260719122743_AddOptimisticConcurrencyRowVersions.cs` scoped to `InsightRuns`.
- `dotnet ef database update` from `api/` — applied cleanly to local dev DB.
- `dotnet test api.Tests` — 454/454 passing (full backend suite, run from repo root).

### Completion Notes List

- Task 1: Added `RowVersion` to `InsightRun`/`InsightRunConfiguration`/migration exactly per spec; migration applies cleanly locally.
- Task 2: Reordered `ProcessInsightsFunction.RunAsync` per spec, with one deliberate structural deviation from the Dev Notes' literal wording: the `DbUpdateConcurrencyException`-specific catch is nested *inside* the outer try block (wrapping just the claim's `SaveChangesAsync`) rather than physically preceding it. A first attempt placing it before the outer try broke the existing `UnhandledExceptionOutsideDetectors` test — a non-concurrency exception on that same first `SaveChangesAsync` call (the test's simulated transient failure) needs to still fall through to the outer generic `catch (Exception)` and set `Failed` status, per Task 3.3's explicit requirement that all four existing tests must pass unmodified. The nested structure satisfies both: a `DbUpdateConcurrencyException` is caught and returns early before stale-cleanup/detectors (AC #2), while any other exception on that same call still reaches the outer handler (existing test's expected behavior, AC unaffected).
- Task 3: Added the concurrent-invocation race test. Discovered that EF Core 10's InMemory provider does **not** auto-generate a new `byte[]` value for `IsRowVersion()`-configured properties on save (unlike a real SQL Server `rowversion` column) — verified via a throwaway diagnostic test before writing the real one. Two independently-tracked contexts alone do not naturally conflict under InMemory because the concurrency-token value never actually changes between saves. Worked around this **test-only** (no production code touched) with a `RowVersionSimulatingDbContext` subclass — analogous to this file's existing `FailingOnFirstSaveDbContext` pattern — that assigns a fresh `Guid`-derived byte array to any modified `InsightRun.RowVersion` immediately before delegating to `base.SaveChangesAsync()`, simulating what a real rowversion column does automatically. Combined with pre-loading the `InsightRun` into both contexts before either invocation runs (so both capture the same pre-claim original value), this reliably reproduces the race regardless of whether the two `RunAsync` calls are scheduled with genuine thread-level concurrency. Verified non-flaky across 10 consecutive runs. Full backend suite: 454/454 passing.

### File List

- `api/Data/Entities/InsightRun.cs` — added `RowVersion` property
- `api/Data/Configurations/InsightRunConfiguration.cs` — added `.IsRowVersion()` configuration
- `api/Data/Migrations/20260726182406_AddRowVersionToInsightRun.cs` — generated migration
- `api/Data/Migrations/20260726182406_AddRowVersionToInsightRun.Designer.cs` — generated migration designer file
- `api/Data/Migrations/AppDbContextModelSnapshot.cs` — updated by EF Core migration generation
- `api/Features/Insights/ProcessInsightsFunction.cs` — reordered claim-before-cleanup, added concurrency-specific catch
- `api.Tests/Features/Insights/ProcessInsightsFunctionTests.cs` — added `RowVersionSimulatingDbContext` test double and the new concurrent-redelivery race test

## Change Log

- 2026-07-26: Implemented DB-level idempotency guard for insight-discovery redelivery via `RowVersion` optimistic concurrency on `InsightRun`. Full backend suite: 454/454 passing.
