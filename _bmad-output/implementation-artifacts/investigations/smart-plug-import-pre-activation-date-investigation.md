# Investigation: Smart plug import behavior for data dated before a device's `InUseSince`

## Hand-off Brief

1. **What happened.** No question of "what happened" — this is an exploration of intended behavior, not an incident. Confirmed: import, gap-filling, and reconciliation store/process smart plug rows purely by date/plug, with zero awareness of `Device.InUseSince`; downstream, `DecompositionEngine`'s `IsDeviceActiveOn` gate exists but is applied only to the standalone-estimate attribution path, never to measured (`PlugId`-backed) plug data.
2. **Where the case stands.** Concluded. Root behavior fully traced end-to-end from import through decomposition, and confirmed as a deliberate, documented scope decision (Story 12.1), not an oversight.
3. **What's needed next.** No fix required unless Ralf wants the measured path gated too — that would be new scope, not a bug fix. See Recommended Next Steps.

## Case Info

| Field            | Value                                                                      |
| ---------------- | -------------------------------------------------------------------------- |
| Ticket           | N/A (ad-hoc question from Ralf)                                            |
| Date opened      | 2026-08-02                                                                  |
| Status           | Concluded                                                                   |
| System           | energy-tracker backend (.NET 10 / EF Core), `api/Features/SmartPlugImport`, `api/Features/Decomposition` |
| Evidence sources | Source code (confirmed by direct read), story file `12-1-device-existence-window-estimated-consumption-gating.md`, `deferred-work.md` |

## Problem Statement

Ralf asked: "How does the smart plug import behave if I have import data which happened before the device activation ('in use since') date?" — i.e., does the import pipeline reject, clamp, or otherwise treat rows whose date precedes `Device.InUseSince` differently from any other imported row?

## Evidence Inventory

| Source   | Status    | Notes     |
| -------- | --------- | --------- |
| `api/Features/SmartPlugImport/ProcessImportFunction.cs` | Available | Read in full |
| `api/Features/SmartPlugImport/EveHomeParser.cs`, `MerossParser.cs` | Available | Grepped for `InUseSince`/`Device` — zero hits |
| `api/Features/SmartPlugImport/InterpolationEngine.cs` | Available | Read; operates only on `SmartPlugDailyData` keyed by plug/date |
| `api/Features/SmartPlugImport/ReconciliationEngine.cs` | Available | Grepped — zero `InUseSince`/`Decommission` hits |
| `api/Features/Decomposition/DecompositionEngine.cs` | Available | Read relevant sections (lines 195-350) directly |
| `_bmad-output/implementation-artifacts/12-1-device-existence-window-estimated-consumption-gating.md` | Available | Read in full — the story that introduced `InUseSince`/`IsDeviceActiveOn` |
| `_bmad-output/implementation-artifacts/deferred-work.md` | Available | Story 7.1 review entry on the measured-path "no data" ambiguity |

## Investigation Backlog

| # | Path to Explore | Priority | Status | Notes |
| - | --------------- | -------- | ------ | ----- |
| 1 | Import pipeline (parsers, `ProcessImportFunction`) for `InUseSince` awareness | High | Done | No awareness found |
| 2 | `InterpolationEngine`/`ReconciliationEngine` for `InUseSince` awareness | High | Done | No awareness found |
| 3 | `DecompositionEngine` for `InUseSince` gating and which attribution path it covers | High | Done | Gates estimate path only, confirmed via code + story doc |
| 4 | Confirm whether this is a known/deliberate scope decision vs. an unnoticed gap | Medium | Done | Confirmed deliberate — Story 12.1 "What NOT to touch" + `deferred-work.md` Story 7.1 entry |

## Timeline of Events

Not applicable — exploration case, no incident timeline. Relevant design history:

| Time | Event | Source | Confidence |
| ---- | ----- | ------ | ---------- |
| 2026-07-13 | Story 7.1 code review flags "no data" vs. "verified zero" ambiguity for the measured path as deferred, no AC | `deferred-work.md:397-398` | Confirmed |
| 2026-08-01 | Story 12.1 ships `InUseSince`/`DecommissionedDate` gating, explicitly scoped to the standalone-estimate path only; measured path explicitly excluded | `12-1-...md` Task 2, "What NOT to touch" | Confirmed |

## Confirmed Findings

### Finding 1: Import pipeline stores all rows unconditionally, regardless of any device's `InUseSince`

**Evidence:** `api/Features/SmartPlugImport/ProcessImportFunction.cs:91-115` — `DispatchToParserAsync` calls `EveHomeParser.ParseAndStoreAsync(flatId, plugId, ...)` / `MerossParser.ParseAndStoreAsync(flatId, plugId, ...)`, both keyed only by `flatId`/`plugId`, no `Device`/`InUseSince` reference in either parser file.

**Detail:** Every parsed row is written to `SmartPlugDailyData` (keyed by `FlatId`/`PlugId`/`Date`) with no comparison against any device's activation window. A row dated before `InUseSince` is stored identically to any other row.

### Finding 2: Gap-filling and reconciliation are equally unaware of `InUseSince`

**Evidence:** `api/Features/SmartPlugImport/InterpolationEngine.cs:16-100` — operates purely on the `minDate`/`maxDate` of existing `SmartPlugDailyData` rows for a plug; no `Device`/`InUseSince` reference. `ReconciliationEngine.cs` — grepped, zero `InUseSince`/`Decommission` matches.

**Detail:** If a pre-activation row creates a gap adjacent to other rows, `InterpolationEngine` will happily interpolate across it and persist synthetic `IsInterpolated = true` rows for those pre-activation dates too — same lack of awareness as raw import.

### Finding 3: `DecompositionEngine.IsDeviceActiveOn` exists but gates only the standalone-estimate path, never measured plug data

**Evidence:** `api/Features/Decomposition/DecompositionEngine.cs:236-246`:
```csharp
if (resolvedPp.PlugId is not null)
{
    dayKwh = plugDailySeries.GetValueOrDefault(resolvedPp.PlugId, []).GetValueOrDefault(date);
    dayApproach = AttributionApproach.Measured;
    claimedPlugDays.Add((resolvedPp.PlugId, date));
}
else
{
    dayKwh = IsDeviceActiveOn(device, localDate) ? dailyEstimate : 0m;
    dayApproach = standaloneApproach;
}
```
and the gate definition at `DecompositionEngine.cs:348-350`.

**Detail:** For a `PlugId`-backed (measured) device, `dayKwh` comes straight from `plugDailySeries` with no `IsDeviceActiveOn` check — every day's measured value counts, including days before `InUseSince`. The gate only fires in the `else` branch, which is the synthetic per-day estimate for standalone (EU-label/self-measured) devices. `BuildSmartStripDecomposition` (smart-strip sub-devices) sums `series.Values.Sum()` unconditionally too — same absence confirmed by direct grep.

### Finding 4: This is a documented, deliberate scope decision, not an unnoticed gap

**Evidence:** `_bmad-output/implementation-artifacts/12-1-device-existence-window-estimated-consumption-gating.md`, "What NOT to touch" section:
> "Any measured-device path (`pp.PlugId is not null` branches...) — existence windows apply only to the estimate-based standalone path per AC2's explicit scope... A plugged, measured device's existence window is out of scope for this story (its consumption is already zero for any day with no `SmartPlugDailyData` row — a different, already-existing 'no data' ambiguity flagged separately in `deferred-work.md`'s Story 7.1 entry, explicitly not folded into this story since it concerns the *measured* path, not the *estimate* path this story touches)."

And `deferred-work.md:397-398` (Story 7.1 review, 2026-07-13):
> "Per-device 'no data available' isn't distinguished from 'verified zero consumption' — a plugged device with zero `SmartPlugDailyData` rows in range... reports `Kwh = 0` or a partial sum indistinguishable from genuine zero/full consumption. No AC addresses this."

**Detail:** The team's stated reasoning for scoping `InUseSince` gating to estimates only: for a measured device, the "no data before I owned the plug" case was assumed to self-resolve because there'd typically be no `SmartPlugDailyData` rows for those dates anyway. Ralf's scenario — rows that *do* exist for pre-activation dates, e.g. because a plug was tracking before its `Device` record's `InUseSince` was set, or an import brought in historical data — falls outside that assumption and is not handled.

## Deduced Conclusions

### Deduction 1: Pre-activation imported smart plug data is fully counted everywhere except cost-attribution correctness for the device's own room/consumption breakdown

**Based on:** Findings 1-3.

**Reasoning:** Import → interpolation → reconciliation → decomposition's measured-attribution branch form one continuous, `InUseSince`-blind pipeline. Nothing in that chain checks the device's activation window against the data's date.

**Conclusion:** If you import Meross/EveHome data with rows dated before the linked device's `InUseSince`, those rows are stored, potentially gap-filled, included in the main-meter reconciliation, and attributed in full to that device/room in Decomposition — exactly as if the device had been active that whole time. `InUseSince` has zero effect on measured (plug-backed) devices; it only suppresses estimate-based (EU-label/self-measured) devices' synthetic daily figures for the same scenario.

## Hypothesized Paths

None — the mechanism was fully traceable to Confirmed evidence; no hypothesis needed.

## Missing Evidence

None blocking — the question is answerable end-to-end from Confirmed code paths and documented design intent.

## Source Code Trace

| Element       | Detail                                      |
| ------------- | -------------------------------------------- |
| Entry point   | `api/Features/SmartPlugImport/ProcessImportFunction.cs:91` (`DispatchToParserAsync`) |
| Trigger       | Blob-triggered import job for a `.csv`/`.xlsx` upload against a `flatId`/`plugId` |
| Condition     | Any imported row whose `Date` predates the linked `Device.InUseSince` |
| Related files | `EveHomeParser.cs`, `MerossParser.cs`, `InterpolationEngine.cs`, `ReconciliationEngine.cs`, `DecompositionEngine.cs:236-246,348-350` |

## Conclusion

**Confidence:** High (Confirmed root cause, fully traced through source, corroborated by the story doc that explicitly scoped this out).

Smart plug import has **no `InUseSince` awareness at all**. Data dated before a device's activation date is imported, gap-filled, reconciled, and attributed in Decomposition exactly like any other data — `InUseSince` gating exists in `DecompositionEngine` but only applies to standalone/estimate-based devices, not to measured (plug-backed) ones. This was a deliberate scope decision in Story 12.1, made under the assumption that a measured device simply wouldn't have `SmartPlugDailyData` rows before it existed — an assumption your scenario (data that *does* exist for pre-activation dates) breaks.

## Recommended Next Steps

### Fix direction

If this behavior should change, the mechanism is: extend `IsDeviceActiveOn` gating (or an equivalent per-day check) into the measured branch at `DecompositionEngine.cs:236-241`, so `dayKwh` is zeroed for dates outside `[InUseSince, DecommissionedDate]` even when `plugDailySeries` has a real value — mirroring what already happens for the estimate branch. This is a **behavior change requiring a design decision**, not a bug fix: should pre-activation measured data be excluded from attribution (kwh silently dropped, same as the estimate path), redirected to an "unattributed/orphaned" bucket, or should the import itself reject/warn on rows outside the device's window? Each has different UX implications (e.g. main-meter reconciliation totals would need to stay consistent either way).

### Diagnostic

None needed — behavior is deterministic and fully understood.

## Reproduction Plan

1. Add a `Device` with `InUseSince` set to, say, 2026-08-01.
2. Import a Meross/EveHome file for its linked `PlugId` containing rows dated 2026-07-20 through 2026-08-05.
3. Observe: all rows persist in `SmartPlugDailyData` (verify via `GetImportStatusFunction` or a DB query).
4. Run Decomposition for a period spanning 2026-07-20 through 2026-08-05.
5. Observe: the device's room/decomposition figures include the pre-2026-08-01 kWh in full — no exclusion, no warning.

## Side Findings

- `InterpolationEngine` will also gap-fill and mark `IsInterpolated = true` for dates before `InUseSince` if they fall between two real data points — the pre-activation blindness extends to synthetic data too, not just raw imported rows. (`api/Features/SmartPlugImport/InterpolationEngine.cs:16-100`)
- The same story (12.1) also left Smart Power Strip sub-devices' *estimate* path ungated (AC4, deliberate) — a separate, already-documented follow-up, distinct from this measured-path finding.
