# Sprint Change Proposal — 2026-08-01 (Story 12.3: Decomposition Tab — Period Total Consumption Summary)

## Section 1: Issue Summary

**Trigger:** Ralf requested a small new feature for the Decomposition tab: show the total monthly (period) consumption alongside the period selector, to make it easier to relate individual Room/Device breakdown figures to the overall total for the selected period.

**Problem:** `DecompositionTab.tsx` renders `PeriodSelector.tsx` as a standalone control with no total-consumption figure near it. The user has to add up Room card totals mentally (or trust the Residual card's implicit percentage) to understand how individual figures relate to the period as a whole.

**Evidence:** `DecompositionResponse` (`client/src/features/decomposition/api/decompositionApi.ts:35-43`) already carries `totalKwh` and `totalCost` — `totalKwh` is consumed internally by `ResidualCard.tsx` for its percentage calculation, but `totalCost` is entirely unused in `DecompositionTab.tsx`, and neither is surfaced as its own visible figure. This is a pure UI surfacing gap, not a data or backend gap.

## Section 2: Impact Analysis

**Epic Impact:** No existing epic's plan is disrupted. Epic 12 (Device Lifecycle & Date-Aware Decomposition Attribution, added earlier today via a separate `bmad-correct-course` pass, still fully `backlog`) gains a third story. Thematically this story is unrelated to FR-52/FR-53's device-lifecycle scope (it's Epic-7-shaped Decomposition-tab UI work), but per Ralf's explicit choice it is added to Epic 12 rather than reopening the already-`done` Epic 7. No other epic affected; no resequencing. The story has no dependency on 12.1/12.2 and could ship independently of the Epic-11-retrospective gate, but per Ralf's choice it stays behind that gate for simplicity — Epic 12 remains one sequencing block.

**Story Impact:** One new story — **12.3** (frontend-only, low risk, no backend/API/migration changes; consumes an existing API field).

**Artifact Conflicts:**
- PRD (`prd.md`): new **FR-54** added under §4.10 Consumption Decomposition (after FR-34, not under Device Registry — it belongs to the Decomposition-view FR cluster despite living in the Epic-12 doc). §6.4 Release 4 scope bullet list gains an FR-54 line noting its distinct origin (direct request, not architecture review/brainstorming like FR-52/53).
- `requirements-inventory.md`: FR-54 added to the Release 4 FR bucket and the FR Coverage Map (`FR-54: Epic 12 — Period total consumption summary`); new **UX-DR21** added (Period Total summary tile — glass KpiTile pattern, skeleton-aware, suppressed in unavailable state).
- `epic-12-device-lifecycle-and-date-aware-decomposition-attribution.md`: header `FRs covered` line gains FR-54; new **Story 12.3** appended with full Given/When/Then ACs.
- `sprint-status.yaml`: `12-3-decomposition-tab-period-total-consumption-summary: backlog` added under the Epic 12 block; header `last_updated` comment updated.
- Architecture: **no changes.** No new component, pattern, migration, or API contract — `totalKwh`/`totalCost` already exist on `DecompositionResponse` since Story 7.1.
- UI/UX: new UX-DR21 (see above); reuses the existing `KpiTile.tsx` glass-surface pattern (`client/src/features/dashboard/components/KpiTile.tsx`) rather than inventing a new visual component.

**Technical Impact:** Frontend-only. `DecompositionTab.tsx` gains a Period Total tile (rendered via `KpiTile` or an equivalent styled wrapper) positioned near `PeriodSelector.tsx`, sourced from data the component already fetches. New test coverage in `DecompositionTab.test.tsx` for the three states (success, loading skeleton, unavailable-suppressed). No new backend endpoint, no EF Core changes, no migration.

## Section 3: Recommended Approach

**Selected: Option 1 — Direct Adjustment.** Add Story 12.3 to the existing Epic 12; amend PRD/requirements-inventory with FR-54 and UX-DR21; no rollback; no MVP scope change.

**Rationale:** Minimal, self-contained, frontend-only addition that reuses an existing API field and an existing UI pattern (`KpiTile`). Effort: **Low**. Risk: **Low**.

**Design decisions confirmed with Ralf during this proposal:**
1. **Epic placement:** Story 12.3 lands in Epic 12 (not a reopened Epic 7), per Ralf's explicit choice, despite the thematic mismatch with FR-52/53.
2. **Sequencing:** Story 12.3 stays behind Epic 12's existing Epic-11-retrospective gate rather than being carved out as independently shippable, for simplicity.
3. **Scope:** Shows both `totalKwh` and `totalCost` (kWh headline + € subline, matching the `KpiTile` pattern), suppressed in the "decomposition unavailable" state consistent with FR-34.

## Section 4: Detailed Change Proposals

### PRD (`prd.md`)

**§4.10 Consumption Decomposition** — new FR added after FR-34:

```
OLD: (FR-34 block, then --- separator, then ### 4.11 Actionable Insights)

NEW: (FR-34 block, then)

#### FR-54: Period total consumption summary
The Decomposition view shows the selected period's total kWh and total cost alongside the period selector, so the user can relate individual Room and Device breakdown figures to the period total without doing mental math. Not shown in the "decomposition unavailable" state, consistent with FR-34.

**Consequences (testable):**
- Selecting a period with Smart Plug Data shows a total-kWh and total-cost figure next to the period selector.
- Selecting a period with no Smart Plug Data (the "decomposition unavailable" state) does not show the total figure.

(then --- separator, then ### 4.11 Actionable Insights, unchanged)
```

**§6.4 Release 4 scope bullets:**

```
OLD:
- Device existence window gating estimated-device inclusion in Decomposition (FR-52)
- Device room-assignment history for date-aware Decomposition attribution (FR-53)

NEW:
- Device existence window gating estimated-device inclusion in Decomposition (FR-52)
- Device room-assignment history for date-aware Decomposition attribution (FR-53)
- Period total consumption summary alongside the Decomposition period selector (FR-54) — added 2026-08-01 via `bmad-correct-course`, sourced directly from Ralf, not from architecture review or brainstorming like FR-52/53
```

**Rationale:** FR-54 documents user-facing behavior; placed in the FR cluster that actually owns the Decomposition view (§4.10) rather than Device Registry (§4.9), for discoverability, while the Release 4 scope list (§6.4) still tracks it under Epic 12 per the chosen epic placement.

### `requirements-inventory.md`

```
OLD:
FR-53: The app tracks a Device's Power Point assignment history automatically (no manual date entry). Decomposition attributes a Device's daily consumption to whichever Room it belonged to on that day, splitting across Rooms for a period spanning a reassignment.

NEW:
FR-53: The app tracks a Device's Power Point assignment history automatically (no manual date entry). Decomposition attributes a Device's daily consumption to whichever Room it belonged to on that day, splitting across Rooms for a period spanning a reassignment.
FR-54: The Decomposition tab shows the period's total kWh and total cost alongside the period selector, so the user can relate individual Room/Device figures to the period total. Not shown in the "decomposition unavailable" state.
```

```
OLD:
UX-DR20: Onboarding flow — ...

## FR Coverage Map

NEW:
UX-DR20: Onboarding flow — ...

UX-DR21: Period Total summary tile — glass-surface KpiTile pattern (label + kWh headline + € subline) rendered alongside the Decomposition period selector; skeleton-aware while loading; suppressed in the "decomposition unavailable" state.

## FR Coverage Map
```

```
OLD:
FR-53: Epic 12 — Device room-assignment history

NEW:
FR-53: Epic 12 — Device room-assignment history
FR-54: Epic 12 — Period total consumption summary
```

### `epic-12-device-lifecycle-and-date-aware-decomposition-attribution.md`

```
OLD: **FRs covered:** FR-52, FR-53
NEW: **FRs covered:** FR-52, FR-53, FR-54
```

New **Story 12.3** appended at the end of the file (full text applied verbatim — see the story file for the complete Given/When/Then ACs):

> ### Story 12.3: Decomposition Tab — Period Total Consumption Summary
>
> As a user, I want to see my total kWh and cost for the currently selected period displayed alongside the period selector, so that I can easily relate the individual Room and Device breakdown figures to the whole period total.
>
> Five Given/When/Then ACs cover: (1) the tile renders on success using `KpiTile`, consuming already-fetched `totalKwh`/`totalCost`; (2) skeleton state while loading; (3) suppression when `IsUnavailable = true`; (4) Locale-aware formatting; (5) test coverage for all three states.

### `sprint-status.yaml`

```
OLD:
  epic-12: backlog
  12-1-device-existence-window-estimated-consumption-gating: backlog
  12-2-device-room-assignment-history-date-aware-decomposition-attribution: backlog

NEW:
  epic-12: backlog
  12-1-device-existence-window-estimated-consumption-gating: backlog
  12-2-device-room-assignment-history-date-aware-decomposition-attribution: backlog
  12-3-decomposition-tab-period-total-consumption-summary: backlog
```

## Section 5: Implementation Handoff

**Change scope: Minor.** Single frontend-only story, no architecture/backend/migration work, no cross-epic dependencies.

**Routed to:** Developer agent (`bmad-agent-dev` / `bmad-dev-story` or `bmad-quick-dev`) for direct implementation of Story 12.3 when Epic 12 is picked up (after Epic 11's retrospective).

**Success criteria:** `DecompositionTab.tsx` shows a Period Total tile (kWh + cost) beside the period selector on successful loads; tile shows a skeleton while loading; tile does not render in the "decomposition unavailable" state; `DecompositionTab.test.tsx` covers all three states; no backend/API/migration changes introduced.

---

*All edits described in Section 4 have been applied directly to `prd.md`, `requirements-inventory.md`, `epic-12-device-lifecycle-and-date-aware-decomposition-attribution.md`, and `sprint-status.yaml` as part of this `bmad-correct-course` pass, per Ralf's approval.*
