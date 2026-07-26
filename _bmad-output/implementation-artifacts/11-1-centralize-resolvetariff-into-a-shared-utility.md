---
baseline_commit: c64211d91cf50917009ab0c9242f106f0d7faa84
---

# Story 11.1: Centralize `ResolveTariff` Into a Shared Utility

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the team maintaining this app,
I want the tariff-resolution logic to exist in one place instead of six independent copies,
so that a correctness fix only needs to be made once, and a future change to the resolution rule can't silently drift across files.

## Acceptance Criteria

1. **Given** six identical private `ResolveTariff` methods, all containing the same tie-break defect (`t.ContractStartDate > best.ContractStartDate` — a strict comparison that silently favors whichever tariff the unordered `db.Tariffs` query happens to enumerate first when two tariffs share the exact same `ContractStartDate`), **when** implemented, **then** a single shared static utility (`api/Shared/TariffResolution.cs`, a pure function taking an already-loaded `IReadOnlyList<Tariff>` and a `DateTimeOffset` — no DB access, preserving every call site's existing in-memory-resolution performance characteristic) replaces all six duplicated methods, and adds a deterministic secondary sort key (`TariffId`) so two tariffs sharing a `ContractStartDate` resolve consistently regardless of query enumeration order.
2. **Given** the six call sites (`KpiCalculator`, `DecompositionEngine`, `StandbyDetector`, `ReplacementDetector`, `BudgetAlertDetector`, `InvoiceDeviationDetector`), **when** migrated to the shared utility, **then** each call site's existing tests continue to pass unmodified except where a test specifically exercised the old non-deterministic tie-break (none currently do — verified, see Dev Notes), and a new dedicated test file for the shared utility covers: no tariff active on the date (returns null), single active tariff, multiple tariffs with the target date landing between two contract starts, and the tie-break case (two tariffs sharing a `ContractStartDate`).

## Tasks / Subtasks

- [x] Task 1: Create the shared utility (AC: #1)
  - [x] 1.1 Create `api/Shared/TariffResolution.cs`, `namespace EnergyTracker.Api.Shared;`, `public static class TariffResolution` with `public static Tariff? Resolve(IReadOnlyList<Tariff> tariffs, DateTimeOffset date)`
  - [x] 1.2 Port the existing logic verbatim (`t.ContractStartDate <= date && (best is null || t.ContractStartDate > best.ContractStartDate)`), then add the deterministic secondary sort key: when `t.ContractStartDate == best.ContractStartDate`, prefer the tariff with the higher `TariffId` (`Guid.CompareTo`) — pick one consistent direction and apply it uniformly; do not change resolution order for tariffs with distinct `ContractStartDate` values
- [x] Task 2: Migrate all six call sites (AC: #2)
  - [x] 2.1 `api/Features/Dashboard/KpiCalculator.cs` — delete the private `ResolveTariff` (lines ~160-169), add `using EnergyTracker.Api.Shared;`, replace both call sites (`ResolveTariff(tariffs, readings[i].ReadingDate)` at line 82, `ResolveTariff(tariffs, now)` at line 115) with `TariffResolution.Resolve(...)`
  - [x] 2.2 `api/Features/Decomposition/DecompositionEngine.cs` — delete the private `ResolveTariff` (lines ~248-257), add `using EnergyTracker.Api.Shared;`, replace the call site at line 67 (`ResolveTariff(tariffs, ToLocalMidnight(date))`)
  - [x] 2.3 `api/Features/Insights/StandbyDetector.cs` — delete the private `ResolveTariff` (lines ~98-107, including the "duplicated verbatim... don't recreate it" comment, now obsolete), add `using EnergyTracker.Api.Shared;`, replace the call site at line 73
  - [x] 2.4 `api/Features/Insights/ReplacementDetector.cs` — delete the private `ResolveTariff` (lines ~144-153), add `using EnergyTracker.Api.Shared;`, replace the call site at line 37
  - [x] 2.5 `api/Features/Insights/BudgetAlertDetector.cs` — delete the private `ResolveTariff` (lines ~108-117), add `using EnergyTracker.Api.Shared;`, replace the call site at line 43
  - [x] 2.6 `api/Features/Insights/InvoiceDeviationDetector.cs` — delete the private `ResolveTariff` (lines ~117-126), add `using EnergyTracker.Api.Shared;`, replace the call site at line 68
- [x] Task 3: Test coverage (AC: #2)
  - [x] 3.1 Create `api.Tests/Shared/TariffResolutionTests.cs`, `namespace api.Tests.Shared;`, covering: empty/no-active-tariff-on-date → null; single active tariff → returns it; multiple tariffs with target date between two contract starts → returns the most recent one that started on/before the date; tie-break — two tariffs sharing the same `ContractStartDate` → asserts the deterministic winner (by `TariffId`), and run the same two tariffs in both enumeration orders to prove the result doesn't depend on list order
  - [x] 3.2 Run the full existing suite for the six migrated files (`dotnet test` filtered to `KpiCalculatorTests|DecompositionEngineTests|StandbyDetectorTests|ReplacementDetectorTests|BudgetAlertDetectorTests|InvoiceDeviationDetectorTests`) and confirm all pass unmodified — no test in these six files currently exercises the tie-break case, so none should need updating (see Dev Notes verification)
  - [x] 3.3 Run the full backend test suite (`dotnet test` from `api.Tests/`) to confirm no regression elsewhere

### Review Findings

- [x] [Review][Patch] Add a null-guard to public `TariffResolution.Resolve` — it's now a public static utility with a bare `foreach (var t in tariffs)` and no check. Unreachable today (all six current call sites pass an already-loaded, non-null `List<Tariff>`), but per user decision (2026-07-26), harden the new public surface with `ArgumentNullException.ThrowIfNull(tariffs)` plus a covering test. `api/Shared/TariffResolution.cs` — fixed
- [x] [Review][Patch] No documentation on the new public `TariffResolution.Resolve` — no XML doc comment explains the inclusive `<=` boundary semantics or the `TariffId` tie-break rule; the six deleted duplicate copies' explanatory comments (e.g. "don't recreate it") were removed with nothing replacing them on the new shared method. [api/Shared/TariffResolution.cs] — fixed
- [x] [Review][Patch] Missing test for the inclusive boundary case (`ContractStartDate == date` exactly) — existing tests only cover strictly-before/strictly-after dates; a regression flipping `<=` to `<` wouldn't be caught. [api.Tests/Shared/TariffResolutionTests.cs] — fixed
- [x] [Review][Patch] Dangling blank line left before the closing brace in four files after deleting the trailing private method. [api/Features/Insights/BudgetAlertDetector.cs, InvoiceDeviationDetector.cs, ReplacementDetector.cs, StandbyDetector.cs] — fixed
- [x] [Review][Patch] Dev Agent Record completion note overstates the six-suite test count (says "92 tests"; the exact Task 3.2 filter run actually returns 87 — 92 only holds once the 5 new `TariffResolutionTests` are folded in). All 87 do pass unmodified; this is a documentation-accuracy fix, not a functional one. [11-1-centralize-resolvetariff-into-a-shared-utility.md — Dev Agent Record] — fixed
- [x] [Review][Defer] Six call sites pass semantically different "date" values into the same shared function (`DecompositionEngine`'s timezone-adjusted local midnight vs. `KpiCalculator`'s raw `ReadingDate`/`now` vs. the other four's `DateTimeOffset.UtcNow`) — centralizing the comparison algorithm doesn't unify what each caller considers "the date." Pre-existing across all six original duplicated methods, not introduced by this diff. [api/Features/Dashboard/KpiCalculator.cs, api/Features/Decomposition/DecompositionEngine.cs, api/Features/Insights/BudgetAlertDetector.cs, InvoiceDeviationDetector.cs, ReplacementDetector.cs, StandbyDetector.cs] — deferred, pre-existing

## Dev Notes

### Why this story exists
Flagged by the Epic 10 retrospective (Action Item #1): `ResolveTariff(IReadOnlyList<Tariff> tariffs, DateTimeOffset date)` is byte-for-byte duplicated across six files. The original `TariffResolver` class was correctly deleted as dead code during the Epic 9 retrospective cleanup (zero real callers at the time) — the duplication grew back afterward as each of Epic 10's four detectors needed the identical logic and was explicitly told (per each story's own Dev Notes) to duplicate rather than share, a reasonable per-story call that no longer holds up at six copies.

### The exact current implementation (all six copies are identical)
```csharp
private static Tariff? ResolveTariff(IReadOnlyList<Tariff> tariffs, DateTimeOffset date)
{
    Tariff? best = null;
    foreach (var t in tariffs)
    {
        if (t.ContractStartDate <= date && (best is null || t.ContractStartDate > best.ContractStartDate))
            best = t;
    }
    return best;
}
```
Verified present, character-for-character identical, at:
- `api/Features/Dashboard/KpiCalculator.cs:160-169`
- `api/Features/Decomposition/DecompositionEngine.cs:248-257`
- `api/Features/Insights/StandbyDetector.cs:98-107` (has a comment: `// Duplicated verbatim from KpiCalculator.cs/DecompositionEngine.cs per this codebase's established per-engine duplication convention — TariffResolver was deleted, don't recreate it.` — this comment becomes obsolete and must be deleted along with the method)
- `api/Features/Insights/ReplacementDetector.cs:144-153`
- `api/Features/Insights/BudgetAlertDetector.cs:108-117`
- `api/Features/Insights/InvoiceDeviationDetector.cs:117-126`

### Call sites to update (verified exact current call signatures)
- `KpiCalculator.cs:82` — `ResolveTariff(tariffs, readings[i].ReadingDate)` (per-period historical cost)
- `KpiCalculator.cs:115` — `ResolveTariff(tariffs, now)` (current projected monthly cost)
- `DecompositionEngine.cs:67` — `ResolveTariff(tariffs, ToLocalMidnight(date))`
- `StandbyDetector.cs:73` — `ResolveTariff(tariffs, DateTimeOffset.UtcNow)`
- `ReplacementDetector.cs:37` — `ResolveTariff(tariffs, DateTimeOffset.UtcNow)`
- `BudgetAlertDetector.cs:43` — `ResolveTariff(tariffs, window[i].ReadingDate)`
- `InvoiceDeviationDetector.cs:68` — `ResolveTariff(tariffs, DateTimeOffset.UtcNow)`

All six pass an already-loaded `IReadOnlyList<Tariff>` (no DB call inside the method) — the new shared utility must preserve this signature exactly so no call site needs to change how it loads `tariffs`.

### Tie-break defect being fixed
`t.ContractStartDate > best.ContractStartDate` is a **strict** comparison. When two tariffs share the exact same `ContractStartDate`, neither wins over the other under strict `>`, so the result silently depends on which one `db.Tariffs`' unordered enumeration happens to visit first — non-deterministic across runs/query plans. Fix: add `TariffId` as a deterministic secondary sort key so the tie always resolves the same way regardless of enumeration order.

### Verification: no existing test exercises the tie-break case
Searched all six existing test files (`KpiCalculatorTests.cs`, `DecompositionEngineTests.cs`, `StandbyDetectorTests.cs`, `ReplacementDetectorTests.cs`, `BudgetAlertDetectorTests.cs`, `InvoiceDeviationDetectorTests.cs`) for cases constructing two tariffs with the same `ContractStartDate` — none found. This means AC #2's "except where a test specifically exercised the old non-deterministic tie-break" clause does not apply here: no existing test needs updating, only the six files' `ResolveTariff` calls need to point at the new utility. If dev agent discovers a tie-break-dependent test during Task 3.2 that was missed in this analysis, treat it as an "gap found during story creation" case and update it to assert the new deterministic behavior rather than skip it.

### `Tariff` entity shape (for the new utility's signature and test fixtures)
```csharp
// api/Data/Entities/Tariff.cs
public class Tariff
{
    public Guid TariffId { get; set; }
    public Guid FlatId { get; set; }
    public decimal PricePerKwh { get; set; }
    public decimal MonthlyBaseFee { get; set; }
    public string? ProviderName { get; set; }
    public DateTimeOffset ContractStartDate { get; set; }
    public int? ContractDurationMonths { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Flat Flat { get; set; } = null!;
}
```

### Existing `api/Shared/` conventions to match
Three existing files in `api/Shared/` establish the pattern for this new one — all are `public static class` with `namespace EnergyTracker.Api.Shared;`, no DI, no constructor:
- `DecimalPrecisionValidatorExtensions.cs` — static extension methods
- `ConcurrencyExtensions.cs` — static extension methods
- `JsonSerializationDefaults.cs` — static config helper

`TariffResolution.cs` follows the same shape but as a plain static method (`Resolve(...)`), not an extension method — there's no natural `this` receiver (it takes a list + a date, not "extends" any one type).

### Architecture doc note (informational, no action required)
`architecture.md`'s source-tree diagram (line ~132, ~159-161) still lists `api/Shared/TariffResolver.cs` as `# period-accurate tariff lookup — the central domain invariant` — this reflects the pre-Epic-9-retro state before that file was deleted as dead code, and is now stale. This story's `TariffResolution.cs` fulfills the same architectural role the doc already anticipated, just under a different name and shape (a plain static method vs. the old resolver class) and a different origin (centralizing six duplicates, not resurrecting the deleted class). No doc update is in scope for this story — flagging only so the dev agent doesn't second-guess the plan when noticing the discrepancy.

### Project Structure Notes
- New file: `api/Shared/TariffResolution.cs` — matches existing `api/Shared/` flat-file convention, no subfolder
- New test file: `api.Tests/Shared/TariffResolutionTests.cs` — mirrors `api/Shared/` under `api.Tests/`, consistent with this codebase's `api.Tests/{mirror of api/}` structure (e.g. `api.Tests/Features/Dashboard/` mirrors `api/Features/Dashboard/`)
- No new folders needed under `api.Tests/` other than `Shared/` (does not currently exist — verify and create)
- No `Insights.Data` JSON, no EF Core config, no migration — this is a pure in-memory refactor touching seven files (one new, six edited) plus one new test file

### Testing Rules (from project context)
- xUnit + Shouldly (`.ShouldBe(...)`), not `Assert.Equal` — see `KpiCalculatorTests.cs` convention: `result.X.ShouldBe(y)`
- No DB/EF Core needed for `TariffResolutionTests.cs` — pure function over an in-memory `List<Tariff>`, construct `Tariff` instances directly (see `MakeTariff` helper pattern in `KpiCalculatorTests.cs:18-19`)
- Test placement: `api.Tests/Shared/{Class}Tests.cs` mirrors `api/Shared/{Class}.cs`
- Do not add InMemory EF Core provider tests for this — no DB access in scope

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

None — no failures encountered; implementation followed the story's Dev Notes exactly (verified copies, call sites, and signatures all matched).

### Completion Notes List

- Created `api/Shared/TariffResolution.cs` with `public static Tariff? Resolve(IReadOnlyList<Tariff> tariffs, DateTimeOffset date)`. Ported the six identical implementations verbatim and added the deterministic tie-break: when two tariffs share `ContractStartDate`, the one with the higher `TariffId` (via `Guid.CompareTo`) wins, regardless of enumeration order.
- Migrated all six call sites (`KpiCalculator`, `DecompositionEngine`, `StandbyDetector`, `ReplacementDetector`, `BudgetAlertDetector`, `InvoiceDeviationDetector`): deleted each private `ResolveTariff` method (including the now-obsolete "duplicated verbatim... don't recreate it" comments), added `using EnergyTracker.Api.Shared;`, and repointed each call to `TariffResolution.Resolve(...)`.
- Wrote `api.Tests/Shared/TariffResolutionTests.cs` (TDD red-green: written and confirmed passing against the new utility) covering no-tariffs → null, no-active-tariff-on-date → null, single active tariff, multiple tariffs picking the most recent on/before the date, and the tie-break case asserted in both list orderings.
- Ran the six migrated files' existing test suites (87 tests, filtered to `KpiCalculatorTests|DecompositionEngineTests|StandbyDetectorTests|ReplacementDetectorTests|BudgetAlertDetectorTests|InvoiceDeviationDetectorTests`) unmodified — all pass, confirming no test relied on the old non-deterministic tie-break, matching the story's pre-verification. (Corrected 2026-07-26: originally misreported as 92, which conflated this count with the 5 new `TariffResolutionTests`.)
- Ran the full backend suite (`dotnet test` from repo root): 451/451 passed, no regressions.
- Confirmed via `grep -rn "ResolveTariff"` across `api/` and `api.Tests/` that no reference to the old per-file method remains anywhere.

### File List

- `api/Shared/TariffResolution.cs` (new)
- `api.Tests/Shared/TariffResolutionTests.cs` (new)
- `api/Features/Dashboard/KpiCalculator.cs` (modified)
- `api/Features/Decomposition/DecompositionEngine.cs` (modified)
- `api/Features/Insights/StandbyDetector.cs` (modified)
- `api/Features/Insights/ReplacementDetector.cs` (modified)
- `api/Features/Insights/BudgetAlertDetector.cs` (modified)
- `api/Features/Insights/InvoiceDeviationDetector.cs` (modified)
