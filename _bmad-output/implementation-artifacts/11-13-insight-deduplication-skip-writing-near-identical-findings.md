---
baseline_commit: 4ac39001fe2ed518c7eb5cd78dd963293778a687
---

# Story 11.13: Insight De-duplication — Skip Writing Near-Identical Findings

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want the Insights tab to show one card per distinct finding instead of near-identical repeats from every discovery run,
So that the tab stays trustworthy and a future ability to dismiss a specific finding has a stable, non-noisy set of rows to act on.

## Acceptance Criteria

1. **Given** a new shared utility `api/Shared/InsightDeduplication.cs` does not yet exist, **when** implemented, **then** it exposes `public static async Task<bool> IsNearDuplicateOfMostRecentAsync(AppDbContext db, Guid flatId, InsightType type, Guid? deviceId, decimal newPrimaryValue, CancellationToken ct)` that queries `db.Insights.AsNoTracking().Where(i => i.FlatId == flatId && i.Type == type && i.DeviceId == deviceId).OrderByDescending(i => i.CreatedAt).FirstOrDefaultAsync(ct)`, extracts that row's primary quantified figure from its `Data` JSON via `JsonDocument.Parse` (per-`Type` property name: `estimatedMonthlyCost` for Standby, `estimatedSavingsEur` for Replacement, `overspendEur` for Budget, `impliedDeltaEur` for InvoiceDeviation), and returns `true` when `Math.Abs(newPrimaryValue - existingValue) <= 0.05m * Math.Max(Math.Abs(newPrimaryValue), Math.Abs(existingValue))`; returns `false` when no prior row exists for that identity or the JSON property is missing/non-numeric (parse-failure is not-a-duplicate, never a thrown exception).
2. **Given** the four detectors' unconditional `db.Insights.Add(...)` calls, **when** implemented, **then** each call site first awaits `InsightDeduplication.IsNearDuplicateOfMostRecentAsync(...)` with its own computed primary value and skips the write when it returns `true` — no other behavior in these detectors changes (thresholds, candidate selection, and all other logic are untouched).
3. **Given** the fix, **when** tested, **then** each of the four detectors' existing test files gains a case asserting a finding within 5% of the most recently stored Insight for the same Type/Device does not create a new row, and a finding beyond 5% does create a new row alongside the untouched prior one; a new `InsightDeduplicationTests.cs` in `api.Tests/Shared/` covers the utility directly (no prior row, within tolerance, beyond tolerance, zero-value symmetry, missing/malformed JSON property); all existing detector and `ProcessInsightsFunctionTests`/`GetInsightsFunctionTests` tests continue to pass unmodified.
4. **Given** this changes real write behavior across all four detectors, **when** implemented, **then** no `Insight` row is ever deleted or modified by this change — only whether a *new* row gets written; `GetInsightsFunction.cs` and its existing tests require no changes, since the read path was already correct for whatever rows exist; no EF Core migration is needed (no schema change — the existing `IX_Insights_FlatId_Type_CreatedAt` index already supports the new query's access pattern).

## Tasks / Subtasks

- [x] Task 1: Create the shared `InsightDeduplication` utility (AC: #1)
  - [x] 1.1 Create `api/Shared/InsightDeduplication.cs` with the exact signature from AC #1, following `api/Shared/TariffResolution.cs`'s static-utility style (XML doc comment, `namespace EnergyTracker.Api.Shared`)
  - [x] 1.2 Implement the per-`Type` JSON property lookup and the symmetric relative-tolerance formula exactly as specified in AC #1 — do not special-case zero values; the `Math.Max(Abs(a), Abs(b))` reference already makes `0 == 0` a duplicate and any nonzero-vs-zero comparison a non-duplicate without extra branching
  - [x] 1.3 Use `JsonDocument.Parse` + `TryGetProperty` (matching `GetInsightsFunction.cs`'s existing JSON-handling pattern) — swallow a missing/wrong-kind property by returning `null` from the extraction helper, never throw
- [x] Task 2: Wire the guard into `StandbyDetector.cs` (AC: #2)
  - [x] 2.1 In the `foreach (var pp in eligible)` loop, after `estimatedMonthlyCost` is computed (currently line 79) and before `db.Insights.Add(...)` (currently line 82), insert: `if (await InsightDeduplication.IsNearDuplicateOfMostRecentAsync(db, flatId, InsightType.Standby, device.DeviceId, estimatedMonthlyCost, ct)) continue;`
- [x] Task 3: Wire the guard into `ReplacementDetector.cs` (AC: #2)
  - [x] 3.1 In the `foreach (var candidate in topBand)` loop, after `estimatedSavingsEur` is computed (currently line 93) and before `db.Insights.Add(...)` (currently line 98), insert: `if (await InsightDeduplication.IsNearDuplicateOfMostRecentAsync(db, flatId, InsightType.Replacement, candidate.Device.DeviceId, estimatedSavingsEur, ct)) continue;`
- [x] Task 4: Wire the guard into `BudgetAlertDetector.cs` (AC: #2)
  - [x] 4.1 Inside the `if (projectedAnnualCost > flat.PlannedAnnualSpend.Value)` block (currently lines 61-76), extract `var overspendEur = projectedAnnualCost - flat.PlannedAnnualSpend.Value;` before constructing `data`, and wrap the existing `db.Insights.Add(...)` in `if (!await InsightDeduplication.IsNearDuplicateOfMostRecentAsync(db, flatId, InsightType.Budget, null, overspendEur, ct)) { ... }` — pass `overspendEur` (not `data.OverspendEur`) as `newPrimaryValue` to avoid constructing `data` before the check
- [x] Task 5: Wire the guard into `InvoiceDeviationDetector.cs` (AC: #2)
  - [x] 5.1 After `impliedDeltaEur` is computed (currently inline at line 78 as part of the `data` constructor call — extract it to its own `var impliedDeltaEur = (projectedAnnualKwh - baselineKwh) * tariff.PricePerKwh;` line first) and before `db.Insights.Add(...)` (currently line 80), insert an early-return guard: `if (await InsightDeduplication.IsNearDuplicateOfMostRecentAsync(db, flatId, InsightType.InvoiceDeviation, null, impliedDeltaEur, ct)) { await db.SaveChangesAsync(ct); return; }` (this detector returns early rather than looping, unlike Standby/Replacement — match the existing early-return style already used earlier in this same method for the null-tariff/null-flat/insufficient-window cases)
- [x] Task 6: Test coverage (AC: #3)
  - [x] 6.1 Create `api.Tests/Shared/InsightDeduplicationTests.cs` (mirror `TariffResolutionTests.cs`'s style: xUnit + Shouldly, no mocking, real `AppDbContext` against `UseInMemoryDatabase`). Cover: no prior row for the identity → `false`; prior row within 5% → `true`; prior row beyond 5% → `false`; both values exactly `0` → `true`; one value `0` and the other nonzero → `false`; prior row's JSON missing the expected property → `false` (not a thrown exception); two different `DeviceId`s with the same `Type` are independent identities (a near-duplicate for Device A does not suppress a write for Device B)
  - [x] 6.2 Extend `StandbyDetectorTests.cs`: a second `DetectAsync` run producing a standby cost within 5% of an already-stored Standby Insight for the same device does not add a new `Insight` row; a run producing a cost beyond 5% adds a new row alongside the untouched prior one
  - [x] 6.3 Extend `ReplacementDetectorTests.cs`: same pattern as 6.2, keyed on savings amount and device
  - [x] 6.4 Extend `BudgetAlertDetectorTests.cs`: same pattern as 6.2, keyed on overspend amount (no device — flat-level identity)
  - [x] 6.5 Extend `InvoiceDeviationDetectorTests.cs`: same pattern as 6.2, keyed on implied delta (no device — flat-level identity); include one case where deviation direction flips sign (e.g. prior `+50€` above baseline, new `-48€` below) to confirm the signed-magnitude formula treats this as a distinct finding, not a near-duplicate
  - [x] 6.6 Run `dotnet test api.Tests` from repo root and confirm all tests pass, including the full pre-existing suite (`ProcessInsightsFunctionTests`, `GetInsightsFunctionTests`, `TriggerInsightsFunctionTests`, `ScheduledInsightsFunctionTests` must all pass unmodified — none of these files are touched by this story)

### Review Findings

- [x] [Review][Patch] `ExtractPrimaryValue` violates AC #1's "never throws" guarantee on a valid-but-non-object JSON root [api/Shared/InsightDeduplication.cs:47-64] — fixed: added a `doc.RootElement.ValueKind != JsonValueKind.Object` guard before `TryGetProperty`, and switched the `PrimaryValueProperty[type]` indexer to `TryGetValue` for defensiveness. `dotnet test api.Tests`: 474/474 passing.
- [x] [Review][Patch] Locale-dependent decimal interpolation in new test JSON fixtures [api.Tests/Features/Insights/BudgetAlertDetectorTests.cs, InvoiceDeviationDetectorTests.cs, ReplacementDetectorTests.cs, StandbyDetectorTests.cs] — fixed: each `SeedExistingInsightAsync` helper now formats with `.ToString(CultureInfo.InvariantCulture)`. `dotnet test api.Tests`: 474/474 passing.
- [x] [Review][Patch] No deterministic tiebreaker on `OrderByDescending(i => i.CreatedAt)` in `IsNearDuplicateOfMostRecentAsync` [api/Shared/InsightDeduplication.cs:31-35] — fixed: added `.ThenByDescending(i => i.InsightId)` as a secondary sort key. `dotnet test api.Tests`: 474/474 passing.
- [x] [Review][Defer] Read-then-write dedup check has a TOCTOU race across concurrent runs [api/Features/Insights/BudgetAlertDetector.cs, InvoiceDeviationDetector.cs, ReplacementDetector.cs, StandbyDetector.cs] — deferred, pre-existing architectural tradeoff: closing this would need a DB-level uniqueness constraint or transaction, which AC #4 explicitly rules out for this story (no schema change / no migration). Low real-world likelihood given this app's single-user, cron-triggered usage pattern (the root-cause investigation describes overlapping runs a day apart, not concurrently).

**Reviewed and dismissed as noise/by-design (6):** indefinite suppression once within tolerance (full-retention write-time-skip is the explicit design intent); the ~5.26%-vs-5% asymmetry in the symmetric-tolerance formula (implements AC #1's formula verbatim — a property of the specified algorithm, not a code defect); `RunId` not recorded when a write is skipped (no downstream consumer depends on it); N+1 query per candidate in the detector loops (explicitly accepted at this project's data scale per Dev Notes); "beyond tolerance" tests not asserting the new row's value (AC #3 only requires a new row exists); four different control-flow idioms across the detectors (each matches its own file's pre-existing style, exactly as prescribed by Tasks 2-5).

**Acceptance Auditor:** no AC violations found — all four ACs verified against the diff; `dotnet test api.Tests` confirmed 474/474 passing.

## Dev Notes

### Why this story exists

Sourced from `bmad-correct-course`'s Sprint Change Proposal (`_bmad-output/planning-artifacts/sprint-change-proposal-2026-07-27.md`), itself sourced from a production investigation (`_bmad-output/implementation-artifacts/investigations/insights-duplicated-across-runs-investigation.md`, Confidence: High). Root cause: `ScheduledInsightsFunction` creates a new `InsightRun` for every flat every night unconditionally, and none of the four detectors check whether their finding already exists before writing — so two runs a day apart (a manual trigger + the next night's scheduled run) each write their own near-identical `Insight` row, and `GetInsightsFunction`'s (correct, by original Story 10.1 design) "return all Insight rows for the flat" then displays both. This story implements FR-51 (PRD §4.11): write-time de-duplication with full retention (materially different findings — beyond 5% — are never suppressed or deleted, preserving history for a future dismiss feature).

**Important design correction from the original proposal draft:** the fix is a **write-time skip**, not a read-time collapse. The older `Insight` row is kept; a new near-duplicate is simply never persisted. This means `GetInsightsFunction.cs` needs **zero changes** — confirm you are not touching that file or its tests.

### The four detectors — current write pattern (exact, before this story)

All four follow the same shape: compute a value, construct a private `record` DTO, `db.Insights.Add(new Insight { ... Data = JsonSerializer.Serialize(data, InsightsConstants.MessageJsonOptions) ... })`, then `await db.SaveChangesAsync(ct)` once at the end of the method (Standby/Replacement: once after their loop; Budget/InvoiceDeviation: at several early-return points plus the end).

[Source: api/Features/Insights/StandbyDetector.cs:74-94]
```csharp
var estimatedMonthlyKwh = (meanWatts / 1000m) * OutOfUseHoursPerDay * 30m;
var estimatedMonthlyCost = estimatedMonthlyKwh * tariff.PricePerKwh;

var data = new StandbyInsightData(device.Name, meanWatts, estimatedMonthlyKwh, estimatedMonthlyCost);
db.Insights.Add(new Insight { InsightId = Guid.NewGuid(), FlatId = flatId, RunId = runId, Type = InsightType.Standby, DeviceId = device.DeviceId, Data = JsonSerializer.Serialize(data, InsightsConstants.MessageJsonOptions), CreatedAt = DateTimeOffset.UtcNow });
```

[Source: api/Features/Insights/ReplacementDetector.cs:92-107] — same shape, `InsightType.Replacement`, `candidate.Device.DeviceId`, primary value `estimatedSavingsEur`.

[Source: api/Features/Insights/BudgetAlertDetector.cs:61-76] — single-shot inside an `if`, `InsightType.Budget`, `DeviceId = null`, primary value is the overspend amount (`projectedAnnualCost - flat.PlannedAnnualSpend.Value`, currently computed inline as the third constructor argument — Task 4.1 requires extracting it to a named variable first).

[Source: api/Features/Insights/InvoiceDeviationDetector.cs:76-89] — single-shot, `InsightType.InvoiceDeviation`, `DeviceId = null`, primary value is `impliedDeltaEur` (currently computed inline as the fourth constructor argument — Task 5.1 requires extracting it first). This value is **signed** (`direction` is `"above"` or `"below"`) — the tolerance formula's `Math.Abs` handles sign correctly without special-casing (see Task 6.5).

`InsightType` enum values: `Standby`, `Replacement`, `Budget`, `InvoiceDeviation` [Source: api/Data/Entities/Insight.cs:3-9]. JSON field names are camelCase (via `InsightsConstants.MessageJsonOptions`'s `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` [Source: api/Features/Insights/InsightModels.cs:20-24]) even though the C# `record` properties are PascalCase — the utility's `PrimaryValueProperty` lookup must use the camelCase JSON names (`estimatedMonthlyCost`, not `EstimatedMonthlyCost`).

### No schema change needed

`Insight` already has an index `IX_Insights_FlatId_Type_CreatedAt` (descending on `CreatedAt`) [Source: api/Data/Configurations/InsightConfiguration.cs] that supports the new `Where(FlatId, Type).OrderByDescending(CreatedAt).First()` query pattern — `DeviceId` isn't part of the index, but at this project's personal-scale data volume (architecture.md: "O(thousands) of data points") this is not a performance concern and does not warrant a migration. Do not add a new index or touch `InsightConfiguration.cs`.

### `Insights.Data` is opaque JSON — deserialize in application layer

Per project-context.md's non-negotiable rule: "no LINQ predicates against its properties." The new utility parses `Data` in C# via `JsonDocument.Parse` after fetching the row — it does **not** attempt any SQL-side JSON query. This matches `GetInsightsFunction.cs`'s own existing pattern (`JsonDocument.Parse(i.Data)` at line 64 of that file).

### What NOT to touch

- `GetInsightsFunction.cs` and `GetInsightsFunctionTests.cs` — the read path is already correct; this story is entirely write-side.
- `ProcessInsightsFunction.cs` — it only orchestrates the four detectors via `RunDetectorSafelyAsync`; it does not itself write `Insight` rows and needs no changes.
- `ScheduledInsightsFunction.cs` / `TriggerInsightsFunction.cs` — creating a new `InsightRun` per night/trigger is correct, expected behavior (FR-38); this story does not reduce how often runs happen, only how often they produce redundant rows.
- Detector thresholds, candidate-selection logic, and window calculations — entirely unrelated to this story; do not modify beyond the single guard insertion per file.
- `InsightConfiguration.cs` / migrations — no schema change (see above).

### Testing Rules (from project context)

- xUnit + Shouldly (`.ShouldBe(...)`), matching every existing test in this project
- `EF Core InMemory` provider — no real SQL Server needed
- New shared-utility tests go in `api.Tests/Shared/` (matches `TariffResolutionTests.cs`, `LocaleResolverTests.cs` placement) — mirror `TariffResolutionTests.cs`'s helper style (small `Make*` factory methods, no shared fixture class)
- Detector test files already have `MakeDb()`/`SeedFlatAsync()`/`SeedTariffAsync()`-style helpers (see `BudgetAlertDetectorTests.cs:12-49` for the exact pattern) — reuse them, do not invent new seeding helpers
- Do not test `InsightConfiguration.cs` itself (EF Core config classes are trusted, per project rules)

### Project Structure Notes

- One new file: `api/Shared/InsightDeduplication.cs`
- One new test file: `api.Tests/Shared/InsightDeduplicationTests.cs`
- Four existing files modified with a single guard insertion each: `StandbyDetector.cs`, `ReplacementDetector.cs`, `BudgetAlertDetector.cs`, `InvoiceDeviationDetector.cs`
- Four existing test files extended (not replaced): `StandbyDetectorTests.cs`, `ReplacementDetectorTests.cs`, `BudgetAlertDetectorTests.cs`, `InvoiceDeviationDetectorTests.cs`
- No migration, no entity/config changes, no API contract changes, no frontend changes

### Previous Story Intelligence (Story 11.2)

- Story 11.2's code review found the dev agent's first implementation had a subtle correctness gap (a guard that looked right but didn't actually prevent the target race under a specific state — an already-`Processing`/`Complete` row). Lesson applied here: verify the tolerance formula against edge cases explicitly (zero values, sign flips — see Task 6.1/6.5) rather than assuming the "obvious" implementation is correct; write the edge-case tests, don't just eyeball the formula.
- Story 11.2's review also flagged dangling blank lines and stale comments after moving code blocks. When extracting `overspendEur`/`impliedDeltaEur` into named variables (Tasks 4.1/5.1), check the surrounding code reads cleanly — no leftover blank lines or comments referencing the old inline-computation shape.
- Story 11.2 verified the full backend suite (454/454) before marking done. Do the same here (Task 6.6) — this story touches four detector files simultaneously, higher-than-usual regression surface for a single story.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-11-post-epic-10-hardening-and-technical-debt-resolution.md#Story 11.13] — epic-level AC and rationale
- [Source: _bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md#FR-51] — the FR this story implements
- [Source: _bmad-output/implementation-artifacts/investigations/insights-duplicated-across-runs-investigation.md] — root-cause investigation
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-27.md] — the correct-course proposal that created this story
- [Source: api/Shared/TariffResolution.cs, api.Tests/Shared/TariffResolutionTests.cs] — shared-utility style precedent (Story 11.1)
- [Source: api/Features/Insights/StandbyDetector.cs, ReplacementDetector.cs, BudgetAlertDetector.cs, InvoiceDeviationDetector.cs] — the four files to modify
- [Source: api/Features/Insights/GetInsightsFunction.cs] — confirmed unmodified read path; do not touch
- [Source: api/Data/Entities/Insight.cs, api/Data/Configurations/InsightConfiguration.cs] — entity/index confirming no migration needed
- [Source: api/Features/Insights/InsightModels.cs] — `InsightsConstants.MessageJsonOptions` camelCase serialization

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

One real bug found and fixed during test-writing (Task 6.1): `JsonElement.TryGetDecimal` throws `InvalidOperationException` (not a `false` return) when the property exists but its `ValueKind` is not `Number` (e.g. a string). AC #1 requires malformed/non-numeric JSON to be treated as not-a-duplicate without throwing. Fixed `InsightDeduplication.ExtractPrimaryValue` to check `property.ValueKind == JsonValueKind.Number` before calling `TryGetDecimal`. Caught immediately by the new `IsNearDuplicateOfMostRecentAsync_PriorRowPropertyIsNonNumeric_ReturnsFalse` test; all 474 tests pass after the fix.

### Completion Notes List

- Created `api/Shared/InsightDeduplication.cs` with `public static async Task<bool> IsNearDuplicateOfMostRecentAsync(AppDbContext db, Guid flatId, InsightType type, Guid? deviceId, decimal newPrimaryValue, CancellationToken ct)` exactly per AC #1: queries the most recent `Insight` for the `FlatId`/`Type`/`DeviceId` identity, extracts the per-`Type` primary JSON property (`estimatedMonthlyCost`/`estimatedSavingsEur`/`overspendEur`/`impliedDeltaEur`), and applies the symmetric relative-tolerance formula (`Math.Abs(diff) <= 0.05m * Math.Max(Abs(new), Abs(existing))`). No prior row, missing property, or non-numeric property all return `false` without throwing.
- Wired the guard into all four detectors per AC #2, exactly as specified in Tasks 2-5: `StandbyDetector`/`ReplacementDetector` use `continue` inside their loops; `BudgetAlertDetector` extracts `overspendEur` before constructing `data` and wraps the write in the guard; `InvoiceDeviationDetector` extracts `impliedDeltaEur` and uses an early-return guard (`SaveChangesAsync` + `return`) matching its existing early-return style. No other detector logic (thresholds, candidate selection, window calculations) was touched.
- Wrote `api.Tests/Shared/InsightDeduplicationTests.cs` (9 tests) covering: no prior row, within/beyond 5% tolerance, zero-value symmetry (both zero → duplicate, one zero → not), missing property, non-numeric property (the bug above), independent `DeviceId` identities, and most-recent-row-wins ordering with multiple prior rows.
- Extended all four detector test files with a within-5%-skips / beyond-5%-writes-alongside-untouched-prior pair each (`StandbyDetectorTests`, `ReplacementDetectorTests`, `BudgetAlertDetectorTests`, `InvoiceDeviationDetectorTests`), plus a sign-flip case in `InvoiceDeviationDetectorTests` (prior `+131.4€` above baseline vs. new `-131.4€` below) confirming the signed-magnitude formula treats a direction flip as a distinct finding, not a near-duplicate.
- `GetInsightsFunction.cs`, `ProcessInsightsFunction.cs`, `ScheduledInsightsFunction.cs`, `TriggerInsightsFunction.cs`, and `InsightConfiguration.cs` were not touched, per the story's explicit scope boundary — no migration needed.
- Ran the full backend suite (`dotnet test` from repo root): 474/474 passed (456 pre-existing + 18 new), no regressions.

### File List

- `api/Shared/InsightDeduplication.cs` (new)
- `api.Tests/Shared/InsightDeduplicationTests.cs` (new)
- `api/Features/Insights/StandbyDetector.cs` (modified)
- `api/Features/Insights/ReplacementDetector.cs` (modified)
- `api/Features/Insights/BudgetAlertDetector.cs` (modified)
- `api/Features/Insights/InvoiceDeviationDetector.cs` (modified)
- `api.Tests/Features/Insights/StandbyDetectorTests.cs` (modified)
- `api.Tests/Features/Insights/ReplacementDetectorTests.cs` (modified)
- `api.Tests/Features/Insights/BudgetAlertDetectorTests.cs` (modified)
- `api.Tests/Features/Insights/InvoiceDeviationDetectorTests.cs` (modified)
