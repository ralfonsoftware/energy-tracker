---
baseline_commit: 336495dde96e028177583efbd72b1c1061ec6f39
---

# Story 11.14: Scope Default Insights Read to Most-Recent-Per-Identity

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want the Insights tab to show only the current, most relevant finding per device/type,
so that the tab doesn't accumulate an ever-growing list of stale historical findings while no dismiss/history feature exists yet to manage them.

## Acceptance Criteria

1. **Given** `GetInsightsFunction.cs:49-53` currently returns `db.Insights.Where(i => i.FlatId == flatGuid)` unfiltered — every row ever written for the flat — **when** implemented, **then** the query is changed to return only the single most-recently-stored row (by `CreatedAt`, tie-broken by `InsightId` descending — same tie-break `InsightDeduplication.cs:33-34` already uses) per distinct `(Type, DeviceId)` identity for the flat. No `RunId` filtering (a `RunId` filter would incorrectly hide a type that didn't fire in the latest run but is still current). No schema change, no migration, and no `Insight` row is ever deleted or modified by this read.
2. **Given** `GetInsightsFunctionTests.cs:74-91`'s `RunAsync_MultipleInsights_ReturnsSortedByCreatedAtDescending` currently seeds three `Insight` rows via `MakeInsight(flatId, createdAt)` — a helper that hardcodes `Type = InsightType.Standby` and an implicit `DeviceId = null` for every call, meaning **all three seeded rows share the same `(Type, DeviceId)` identity** — this test locks in the old all-time-unscoped contract and will fail under AC #1 as written today (it currently asserts all three are returned; under the new contract only the newest of the three should be). **When** implemented, **then** this test is renamed/rewritten to reflect the new contract, and `MakeInsight` gains optional `type`/`deviceId` parameters (defaulting to today's `Standby`/`null`) so both same-identity and distinct-identity scenarios can be seeded without duplicating the helper.
3. **Given** the new scoping, **when** tested, **then** new/rewritten test cases in `GetInsightsFunctionTests.cs` cover: (a) 3 distinct `(Type, DeviceId)` identities each with 1 row → all 3 returned; (b) 1 identity with 2 historical rows at different `CreatedAt` → only the newer returned, older excluded; (c) 2 rows for the same identity sharing the exact same `CreatedAt` → the tie-break (`InsightId` descending) determines which one is returned, matching `InsightDeduplication`'s own tie-break rule; all pre-existing tests in this file other than the one rewritten in AC #2 continue to pass unmodified.
4. **Given** this changes only which rows are returned, not what they contain, **when** implemented, **then** `InsightDto`/`InsightsResponse` (`InsightModels.cs`) are unchanged, no frontend file changes (verified: `InsightsTab.tsx` renders `insightsData.insights` 1:1 with no assumption about count or identity uniqueness — it already renders correctly whether it receives 1 or N rows), and the full backend suite passes with no regressions in `ProcessInsightsFunctionTests`, the four detector test files, or `InsightDeduplicationTests`.

## Tasks / Subtasks

- [x] Task 1: Change `GetInsightsFunction.cs`'s query to most-recent-per-identity (AC: #1)
  - [x] 1.1 Keep the existing `db.Insights.AsNoTracking().Where(i => i.FlatId == flatGuid).OrderByDescending(i => i.CreatedAt).Select(i => new { i.InsightId, i.Type, i.DeviceId, i.Data, i.CreatedAt }).ToListAsync(ct)` fetch exactly as-is (lines 49-53) — do **not** try to push the grouping into the SQL query.
  - [x] 1.2 Add `.ThenByDescending(i => i.InsightId)` to the existing `OrderByDescending(i => i.CreatedAt)` (matching `InsightDeduplication.cs:33-34`'s tie-break) so the fetched list is already ordered most-recent-first with a deterministic tie-break.
  - [x] 1.3 After materializing the list (`ToListAsync`), apply `.GroupBy(i => (i.Type, i.DeviceId)).Select(g => g.First())` **in LINQ-to-Objects** (i.e. on the already-fetched `List<T>`, not on the `IQueryable`) to reduce to one row per identity — this works because the list is already sorted by the required tie-break, so `g.First()` per group is exactly the row AC #1 wants, with zero extra sorting logic needed.
  - [x] 1.4 Do not add a `.GroupBy()` directly onto the EF Core query before materialization — see "Critical implementation warning" in Dev Notes for why.
- [x] Task 2: Update `GetInsightsFunctionTests.cs` (AC: #2, #3)
  - [x] 2.1 Extend `MakeInsight` to `MakeInsight(Guid flatId, DateTimeOffset createdAt, InsightType type = InsightType.Standby, Guid? deviceId = null)`, passing the new parameters through to the constructed `Insight`. All existing call sites keep compiling unchanged (defaults match today's hardcoded values).
  - [x] 2.2 Rewrite `RunAsync_MultipleInsights_ReturnsSortedByCreatedAtDescending` — split it into cases matching AC #3(a)/(b)/(c): distinct-identities-all-returned, same-identity-only-newest-returned, and the `CreatedAt`-tie-break case. Reuse the existing sorted-descending assertion style (`.Select(i => i.InsightId).ShouldBe([...])`).
  - [x] 2.3 Run `dotnet test api.Tests --filter FullyQualifiedName~GetInsightsFunctionTests` first to confirm the new/rewritten cases pass in isolation before the full suite run in Task 3.
- [x] Task 3: Full regression pass (AC: #4)
  - [x] 3.1 Run `dotnet test api.Tests` from repo root; confirm the full suite passes with no regressions (baseline going into this story: 474/474 per Story 11.13's completion notes — expect the same count minus/plus whatever net test-case delta Task 2.2 produces).
  - [x] 3.2 Confirm no other file references `GetInsightsFunction`'s old all-rows contract (grep `db.Insights` usage in `ProcessInsightsFunction.cs`/detectors to confirm they're unaffected — they write, not read via this Function, so this should be a no-op check).

### Review Findings

- [x] [Review][Patch] Malformed JSON on the selected newest row silently drops the entire identity, with no fallback to an older valid row in the same group — `GroupBy(...).Select(g => g.First())` (`GetInsightsFunction.cs:56-59`) ran *before* the JSON-parse/skip loop. **Fixed:** identity grouping now iterates each group's rows newest-first and falls back to the next-newest row if the current one's `Data` fails to parse, before dropping the identity entirely.

- [x] [Review][Patch] No log line when identity-grouping drops a row [api/Features/Insights/GetInsightsFunction.cs:56-59] — the malformed-JSON path logs via `logger.LogError`, but rows discarded by grouping were dropped silently. **Fixed:** added a `LogDebug` line noting the selected `InsightId` and historical-row count whenever a group has more than one row.

- [x] [Review][Patch] Misleading test variable names `lowerInsightId`/`higherInsightId` [api.Tests/Features/Insights/GetInsightsFunctionTests.cs:117-118] — both hold full `Insight` entities, not scalar IDs. **Fixed:** renamed to `insightWithLowerId`/`insightWithHigherId`.

- [x] [Review][Defer] `InsightId` (GUID) tie-break may resolve differently on SQL Server (`uniqueidentifier` ordering) vs. .NET/InMemory (`Guid.CompareTo`) on an exact `CreatedAt` tie [api/Features/Insights/GetInsightsFunction.cs:52] — deferred, pre-existing pattern inherited from `InsightDeduplication.cs`'s identical tie-break (spec-mandated for consistency per AC #1); low-probability (exact-timestamp collision), low-impact (both rows are legitimately current for that identity). The new tie-break test (`RunAsync_SameIdentitySameCreatedAt_TieBreaksOnInsightIdDescending`) only verifies InMemory/.NET ordering semantics, so the production (SQL Server) winner on a true tie is unverified by the suite.

- [x] [Review][Defer] `(Type, DeviceId)` identity collapses all history to a single row for insight types without a device dimension (e.g. `Budget`, `DeviceId = null`) [api/Features/Insights/GetInsightsFunction.cs:56-59] — deferred, intentional per spec (AC #1 explicitly requires matching `InsightDeduplication.cs`'s identity for write/read consistency), but worth confirming this is the desired long-term product behavior for device-less types (e.g. one `Budget` insight per billing period would still collapse to just the latest).

- [x] [Review][Defer] Whole flat history is fetched and materialized before filtering to distinct identities in memory [api/Features/Insights/GetInsightsFunction.cs:49-59] — deferred, pre-existing convention (Dev Notes explicitly mandate "load once, resolve in-memory" citing `ResolveTariff`/`InsightDeduplication.cs` precedent and an assumed small/bounded dataset). Worth revisiting if the project's existing insight-growth investigation (`_bmad-output/implementation-artifacts/investigations/insights-duplicated-across-runs-investigation.md`) finds the per-flat row count is not actually bounded in practice.

- [x] [Review][Defer] Test coverage gaps beyond AC #3's required cases [api.Tests/Features/Insights/GetInsightsFunctionTests.cs] — deferred, not required by spec: no test for multiple identities each with 3+ duplicate rows interleaved, and no boundary test distinguishing `DeviceId = null` vs. a non-null `DeviceId` under the same `Type` (confirms they're treated as separate identities).

## Dev Notes

### Why this story exists

Sourced from `bmad-correct-course`'s second same-day Sprint Change Proposal (`_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-27-fr51-amendment.md`), which amended FR-51 (PRD §4.11) after Story 11.13 shipped. FR-51's original wording chose unlimited historical retention with **all** distinct findings staying **visible** forever — an explicit, deliberate tradeoff at the time, made "to support a future 'dismiss a finding' feature without further schema work now." That dismiss feature doesn't exist yet, so today there's no way to manage the growing list. The amendment keeps the retention guarantee (no row is ever deleted — unchanged) but narrows the **visibility** guarantee: the default read now returns only the most-recently-stored row per `(Type, DeviceId)` identity.

**This is not a revert of Story 11.13.** 11.13's write-time dedup guard (`InsightDeduplication.IsNearDuplicateOfMostRecentAsync`) remains necessary and unchanged — it's what keeps this story's "most recent row per identity" from itself being a near-duplicate of a slightly older one. 11.13 explicitly left `GetInsightsFunction.cs` untouched at the time ("the read path was already correct for whatever rows exist" — true under the *old* FR-51, no longer true under the *amended* FR-51). This story is that read-side complement, now that the amendment calls for it.

### Critical implementation warning: do not `GroupBy` directly on the EF Core query

The obvious-looking one-liner —
```csharp
var insights = await db.Insights.AsNoTracking()
    .Where(i => i.FlatId == flatGuid)
    .GroupBy(i => new { i.Type, i.DeviceId })
    .Select(g => g.OrderByDescending(i => i.CreatedAt).First())
    .ToListAsync(ct);
```
— is the pattern an LLM developer will likely reach for first, and it is a trap for two independent reasons:
1. **EF Core InMemory provider (used by every test in this project) has historically weak/inconsistent translation support for `GroupBy(...).Select(g => g.OrderBy(...).First())`.** This project's entire test suite (`GetInsightsFunctionTests.cs` and everything else) runs against `UseInMemoryDatabase`, not real SQL Server — a query shape that happens to work against SQL Server can still throw or silently misbehave against InMemory, and you will not find out until running tests.
2. **This project's own established convention is "load once, resolve in-memory"** for exactly this class of problem — see `ResolveTariff`/`TariffResolution.cs` (project-context.md: "load the flat's Tariff list once, then resolve each day in-memory ... avoids an N+1 per-day DB round-trip") and `InsightDeduplication.cs` itself (fetches one row, then does all value-extraction/comparison in C#). `Insight` rows per flat are a small, bounded dataset (a handful of detector types × devices) — there is no performance reason to push this to SQL.

**The correct approach (Task 1.1-1.3):** fetch the flat's `Insight` rows exactly as today (unfiltered, just add the `InsightId` tie-break to the existing `OrderByDescending`), materialize with `ToListAsync()`, then do the identity-grouping as plain LINQ-to-Objects on the resulting `List<T>` (`.GroupBy(...).Select(g => g.First())`). Because the list is already sorted most-recent-first with the tie-break applied, `g.First()` needs no further ordering inside the group — this is simpler than the query-side version, not just safer.

### `GetInsightsFunction.cs` — current state (before this story)

[Source: api/Features/Insights/GetInsightsFunction.cs:1-79 — read in full during story creation]

```csharp
var insights = await db.Insights.AsNoTracking()
    .Where(i => i.FlatId == flatGuid)
    .OrderByDescending(i => i.CreatedAt)
    .Select(i => new { i.InsightId, i.Type, i.DeviceId, i.Data, i.CreatedAt })
    .ToListAsync(ct);

var insightDtos = new List<InsightDto>(insights.Count);
foreach (var i in insights)
{
    // ... JsonDocument.Parse(i.Data) into a JsonElement, skip on JsonException (logs + continue) ...
    insightDtos.Add(new InsightDto(i.InsightId, i.Type, i.DeviceId, data, i.CreatedAt));
}

return new OkObjectResult(new InsightsResponse(runStatus, insightDtos));
```
The malformed-JSON skip-and-log behavior (lines ~60-72) is unrelated to this story and must not be touched. The grouping (Task 1.3) should happen on the anonymous-typed `insights` list (`{ InsightId, Type, DeviceId, Data, CreatedAt }`) *before* the `foreach` that builds `insightDtos` — group first, then only JSON-parse the rows that survive grouping (avoids wasted parsing work on rows that will be discarded anyway, though at this data scale this is a clarity choice, not a perf-critical one).

`mostRecentRun`/`runStatus` (lines ~38-43) are entirely unrelated to the `Insights` list and must not be touched — `RunStatusDto` is about the most recent `InsightRun`, independent of which `Insight` rows are returned.

### `InsightConfiguration.cs` — existing index already supports this query, no migration needed

[Source: api/Data/Configurations/InsightConfiguration.cs]

```csharp
builder.HasIndex(i => new { i.FlatId, i.Type, i.CreatedAt })
    .HasDatabaseName("IX_Insights_FlatId_Type_CreatedAt")
    .IsDescending(false, false, true);
```
This index already covers `Where(FlatId).OrderByDescending(CreatedAt)` efficiently (`Type` in the index doesn't hurt, just isn't the primary filter here). `DeviceId` isn't indexed, but the grouping happens in-memory after fetch (per the warning above), so this is irrelevant to query performance — no new index, no migration, do not touch `InsightConfiguration.cs`.

### `InsightDeduplication.cs` — the identity/tie-break definition this story must match exactly

[Source: api/Shared/InsightDeduplication.cs:31-34]
```csharp
var mostRecent = await db.Insights.AsNoTracking()
    .Where(i => i.FlatId == flatId && i.Type == type && i.DeviceId == deviceId)
    .OrderByDescending(i => i.CreatedAt)
    .ThenByDescending(i => i.InsightId)
    .FirstOrDefaultAsync(ct);
```
This is the exact identity (`Type` + `DeviceId`) and exact tie-break (`CreatedAt` desc, then `InsightId` desc) the write-time guard already uses to decide "what's the current representative for this finding." This story's read-side grouping must use the identical identity and tie-break — not a coincidence, a deliberate consistency requirement from the FR-51 amendment (both the write guard and the read scope must agree on what "current" means).

### `GetInsightsFunctionTests.cs` — current state and exact test to rewrite

[Source: api.Tests/Features/Insights/GetInsightsFunctionTests.cs:51-58, 74-91 — read in full during story creation]

```csharp
private static Insight MakeInsight(Guid flatId, DateTimeOffset createdAt) => new()
{
    InsightId = Guid.NewGuid(),
    FlatId = flatId,
    Type = InsightType.Standby,
    Data = """{"deviceName":"Fridge","standbyWatts":12.5}""",
    CreatedAt = createdAt
};

[Fact]
public async Task RunAsync_MultipleInsights_ReturnsSortedByCreatedAtDescending()
{
    var (flat, db) = await SeedFlatAsync();
    var oldest = MakeInsight(flat.FlatId, DateTimeOffset.UtcNow.AddDays(-2));
    var middle = MakeInsight(flat.FlatId, DateTimeOffset.UtcNow.AddDays(-1));
    var newest = MakeInsight(flat.FlatId, DateTimeOffset.UtcNow);
    db.Insights.AddRange(oldest, middle, newest);
    await db.SaveChangesAsync();
    // ... asserts all three are returned, newest-first ...
}
```
Note `DeviceId` is never set in the object initializer, so it defaults to `null` (the C# default for `Guid?`) — combined with the hardcoded `Type = InsightType.Standby`, **all three seeded rows in this existing test share the exact same `(Type, DeviceId)` identity**. Under this story's new contract, only `newest` should be returned — the test as currently written will fail once Task 1 is implemented, which is expected and exactly what AC #2 requires you to fix (not a regression to work around).

The other four tests in this file (`RunAsync_NoInsightRunYet_...`, `RunAsync_MostRecentRunStatus_...`, `RunAsync_ForeignFlatId_Returns403`, `RunAsync_InvalidFlatIdFormat_Returns400`) don't seed multiple same-identity rows and are unaffected — confirm they still pass unmodified (AC #3's final clause).

### What NOT to touch

- `InsightDeduplication.cs` and its tests — the write-time guard is correct and unchanged by this story; do not modify its identity/tie-break logic (this story *reads* that same logic as a spec to match, not a file to edit).
- The four detectors (`StandbyDetector.cs`, `ReplacementDetector.cs`, `BudgetAlertDetector.cs`, `InvoiceDeviationDetector.cs`) and their tests — write-side, entirely unrelated to this read-side story.
- `ProcessInsightsFunction.cs`, `ScheduledInsightsFunction.cs`, `TriggerInsightsFunction.cs` — orchestration/run-creation, unaffected by which rows a later read returns.
- `InsightConfiguration.cs` / migrations — no schema change (see above).
- `InsightModels.cs` (`InsightDto`, `InsightsResponse`) — response shape is unchanged; only which rows populate it changes.
- Frontend (`InsightsTab.tsx`, `InsightCard.tsx`, `useInsights.ts`, `insightsApi.ts`) — confirmed during story creation that the render path is a direct 1:1 map over `insightsData.insights` with no count/identity assumptions; zero frontend changes required.

### Testing Rules (from project context)

- xUnit + Shouldly, EF Core InMemory provider — matches every existing test in this file.
- Do not test `InsightConfiguration.cs` itself (EF Core config classes are trusted, per project rules).
- `MakeInsight`'s new optional parameters must default to today's values so no other test in the file needs to change just because the signature grew.

### Previous Story Intelligence (Story 11.13)

- Story 11.13's review found and fixed a missing deterministic tie-break on `OrderByDescending(CreatedAt)` in `InsightDeduplication.cs` (`.ThenByDescending(i => i.InsightId)` was added during review, not the first pass). **Apply that lesson directly here** — Task 1.2 bakes the same tie-break in from the start rather than waiting for a review round to catch its absence.
- Story 11.13 verified the full backend suite (474/474) before marking done, given it touched four files simultaneously. This story touches fewer files (one Function + one test file) but changes a load-bearing read path every other Insights consumer depends on — run the full suite (Task 3.1), not just the touched file's tests, before considering this done.
- Story 11.13's `InsightDeduplicationTests.cs` and detector test extensions used small `Make*` factory-method helpers with optional parameters added incrementally (not new helper classes) — Task 2.1 follows the identical pattern for `MakeInsight`.

### Project Structure Notes

- No new files. Two existing files modified: `api/Features/Insights/GetInsightsFunction.cs`, `api.Tests/Features/Insights/GetInsightsFunctionTests.cs`.
- No migration, no entity/config changes, no API contract changes (response shape identical), no frontend changes.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-11-post-epic-10-hardening-and-technical-debt-resolution.md#Story 11.14] — epic-level AC and rationale
- [Source: _bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md#FR-51] — the amended FR this story implements
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-27-fr51-amendment.md] — the correct-course proposal that created this story and amended FR-51
- [Source: _bmad-output/implementation-artifacts/investigations/insights-duplicated-across-runs-investigation.md] — original root-cause investigation (Findings 1-3, Deduction 1) whose read-side recommendation this story finally implements
- [Source: _bmad-output/implementation-artifacts/11-13-insight-deduplication-skip-writing-near-identical-findings.md] — previous story; write-time guard this story's read-side must stay consistent with
- [Source: api/Features/Insights/GetInsightsFunction.cs] — file to modify
- [Source: api.Tests/Features/Insights/GetInsightsFunctionTests.cs] — test file to modify
- [Source: api/Shared/InsightDeduplication.cs] — identity/tie-break definition to match
- [Source: api/Data/Configurations/InsightConfiguration.cs] — confirms no migration needed
- [Source: client/src/features/insights/components/InsightsTab.tsx] — confirmed no frontend change needed

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- Confirmed RED state before implementing: ran the two rewritten same-identity test cases (`RunAsync_SameIdentityMultipleRows_ReturnsOnlyNewest`, `RunAsync_SameIdentitySameCreatedAt_TieBreaksOnInsightIdDescending`) against the pre-change `GetInsightsFunction.cs` — both failed as expected (returned all rows for the shared identity instead of just the most recent), confirming the tests exercise the new contract correctly before the fix landed.
- Post-implementation: `dotnet test api.Tests --filter FullyQualifiedName~GetInsightsFunctionTests` → 7/7 passed.
- Full suite: `dotnet test api.Tests` → 476/476 passed (474 baseline from Story 11.13 + net +2 from Task 2.2's test rewrite: one test split into three, i.e. -1 +3).
- Grepped `db.Insights` usage repo-wide to confirm all other consumers (detectors' `.Add`, `ProcessInsightsFunction`'s stale-row cleanup by `RunId`, `InsightDeduplication`'s single-identity lookup, `AppDbContextExtensions`'s cascade-load) are write-side or unrelated read paths, unaffected by this story's read-side scoping change.

### Completion Notes List

- Implemented AC #1: `GetInsightsFunction.cs`'s `Insights` query now adds `.ThenByDescending(i => i.InsightId)` to the existing `OrderByDescending(i => i.CreatedAt)` (matching `InsightDeduplication.cs`'s tie-break exactly), then after `ToListAsync()` applies `.GroupBy(i => (i.Type, i.DeviceId)).Select(g => g.First())` in LINQ-to-Objects on the materialized list to keep only the most-recent row per `(Type, DeviceId)` identity. No `RunId` filtering, no schema change, no row deletion/modification — a pure read-side scoping change per the Dev Notes' explicit warning against pushing the `GroupBy` into the EF Core query (InMemory provider translation risk + established "load once, resolve in-memory" project convention).
- Implemented AC #2/#3: `MakeInsight` gained optional `type`/`deviceId` parameters defaulting to today's `Standby`/`null` values (existing call sites unaffected). The old `RunAsync_MultipleInsights_ReturnsSortedByCreatedAtDescending` (which seeded 3 same-identity rows, an obsolete premise under the new contract) was replaced with three tests: `RunAsync_DistinctIdentities_ReturnsAllSortedByCreatedAtDescending` (3 distinct identities → all 3 returned, newest-first), `RunAsync_SameIdentityMultipleRows_ReturnsOnlyNewest` (3 same-identity rows → only newest returned), and `RunAsync_SameIdentitySameCreatedAt_TieBreaksOnInsightIdDescending` (2 rows, identical `CreatedAt` → higher `InsightId` wins, matching `InsightDeduplication`'s tie-break). The four pre-existing unrelated tests in the file pass unmodified.
- Implemented AC #4: `InsightModels.cs` untouched (response shape unchanged), no frontend files touched, full backend suite green (476/476) with the only two test-file changes being the ones this story specifies.

### File List

- api/Features/Insights/GetInsightsFunction.cs
- api.Tests/Features/Insights/GetInsightsFunctionTests.cs

## Change Log

- 2026-07-27: Scoped `GetInsightsFunction`'s default read to the most-recently-stored row per `(Type, DeviceId)` identity (FR-51 amendment). Rewrote `GetInsightsFunctionTests.cs`'s multi-insight test into three cases covering distinct identities, same-identity dedup, and the `CreatedAt` tie-break. Full suite: 476/476 passing.
