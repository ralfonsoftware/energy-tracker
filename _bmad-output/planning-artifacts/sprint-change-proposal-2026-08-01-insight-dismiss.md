# Sprint Change Proposal — 2026-08-01 (Story 12.4: Insight Dismiss and Reactivate)

## Section 1: Issue Summary

**Trigger:** Ralf requested a new feature: the ability to dismiss an Insight and later reactivate it.

**Problem:** `Insight.cs` (`api/Data/Entities/Insight.cs:11-23`) has no status/dismissed concept — only `InsightId, FlatId, RunId, Type, DeviceId, Data, CreatedAt`. Story 11.13/11.14 (FR-51) already narrowed the default Insights view to "most-recent row per `(Type, Device)` identity" and explicitly named this as a stepping stone: *"...excluded from the default response... older superseded rows remain queryable in the data store for a future historical/dismiss view"* (`prd.md`, FR-51 body text). There is currently no way for a user to act on that — an Insight they've already handled (e.g., replaced a device, adjusted a habit) stays visible indefinitely, and a discovery run will keep re-surfacing it under FR-51's normal 5%-tolerance rule the moment the figure drifts.

**Evidence:** `Insight.cs:11-23` (no status field); `InsightDeduplication.cs:28-46` (5%-tolerance comparison against most-recent row per identity, the mechanism a dismiss feature must integrate with); `GetInsightsFunction.cs:58-94` (existing per-identity grouping, the mechanism a dismiss feature must filter through); `InsightCard.tsx` (pure display component, no action affordances at all today); `prd.md` FR-51 body text (explicitly anticipates "a future historical/dismiss view").

## Section 2: Impact Analysis

**Epic Impact:** No existing epic's plan is disrupted. Epic 12 (Device Lifecycle & Date-Aware Decomposition Attribution, `backlog`, gated behind Epic 11's retrospective) gains a fourth story. Thematically this story is unrelated to FR-52/53's device-lifecycle scope and to FR-54's Decomposition-view scope — it's Insights-feature work (Epic 10/11 territory) — but per Ralf's explicit choice it is added to Epic 12 rather than reopening the in-progress Epic 11, following the same precedent Story 12.3 established. No other epic affected; no resequencing.

**Story Impact:** One new story — **12.4** (full-stack: new entity columns + migration, new PATCH endpoint, an extension to the existing FR-51 de-duplication check, two new frontend mutation hooks, and new UI affordances on `InsightCard`/`InsightsTab`). Effort is materially larger than Story 12.3's frontend-only addition.

**Artifact Conflicts:**
- PRD (`prd.md`): new **FR-55** added in §4.11 Actionable Insights (after FR-51, the FR cluster it actually belongs to — same split as FR-54 living in §4.10 despite its Epic-12 placement). §6.4 Release 4 scope bullet list gains an FR-55 line.
- `requirements-inventory.md`: FR-55 added to the Release 2 FR bucket (adjacent to FR-43, matching its PRD-section placement) and the FR Coverage Map (`FR-55: Epic 12 — Insight dismiss and reactivate`). New **UX-DR22** added. **Also backfilled FR-51** into this file and the FR Coverage Map — a pre-existing gap discovered during this pass: FR-51 has existed in `prd.md` since 2026-07-27 (Story 11.13/11.14) but was never added here, and Epic 11 had zero representation in the FR Coverage Map. Flagged to Ralf and approved as part of this proposal.
- `epic-12-device-lifecycle-and-date-aware-decomposition-attribution.md`: header `FRs covered` line gains FR-55; new **Story 12.4** appended with full Given/When/Then ACs.
- `epic-list.md`: Epic 12 entry updated to list FR-52 through FR-55, both new UX-DRs, and a note on the two thematically-unrelated stories bucketed into this epic. This file had also drifted out of sync after Story 12.3 (never updated for FR-54) — backfilled in the same edit.
- `sprint-status.yaml`: `12-4-insight-dismiss-and-reactivate: backlog` added under the Epic 12 block; header `last_updated` comment updated.
- Architecture (`architecture.md`): new **AD-8c** documenting the de-dup/dismiss design (also backfilling FR-51's previously-undocumented design in the same entry, since the two are mechanically inseparable); `Insights` entity table row gains two nullable/default columns; new Requirements Coverage table row for FR-51/FR-55. The pre-existing gap where FR-52/53/54 are absent from that same coverage table is **not** fixed here — out of scope for an Insights-dismiss change, flagged as a known residual gap for Epic 12's own eventual cleanup.
- UI/UX: new UX-DR22 (dismiss/reactivate affordances); reuses existing `InsightCard` chrome and icon-button conventions — no new visual pattern invented.

**Technical Impact:** Backend: new EF Core migration (two columns on `Insight`, no data loss, default `false`/`null` for existing rows); new `PatchInsightFunction` (route `PATCH v1/flats/{flatId}/insights/{insightId}`, modeled on `PatchFlatFunction.cs`'s tenant-check + body-parse shape); `InsightDeduplication.cs`'s existing most-recent-row lookup extended with a dismissed-short-circuit check, consumed by all four detector call sites (`StandbyDetector`, `ReplacementDetector`, `BudgetAlertDetector`, `InvoiceDeviationDetector`) with no per-detector logic duplication since the check lives in the shared helper; `GetInsightsFunction.cs`'s per-identity grouping gains an active/dismissed filter. Frontend: `InsightCard.tsx` gains its first-ever action affordance; `InsightsTab.tsx` gains an Active/Dismissed toggle; two new mutation hooks (`useDismissInsight`, `useReactivateInsight`) in the existing hooks/API-module pattern. No breaking API contract changes — the new PATCH endpoint and query param are additive.

## Section 3: Recommended Approach

**Selected: Option 1 — Direct Adjustment.** Add Story 12.4 to the existing Epic 12; amend PRD/requirements-inventory/architecture with FR-55 (and backfill FR-51's documentation gap alongside it); no rollback; no MVP scope change.

**Rationale:** Self-contained new capability that reuses two already-trusted mechanisms rather than inventing new architecture: FR-51's identity-collapsing dedup lookup (extended, not replaced) and the existing PATCH-endpoint convention (`PatchFlatFunction.cs`). A single `IsDismissed`/`DismissedAt` flag on the existing "current representative row per identity" serves both the view-suppression and detection-suppression requirements, avoiding a separate suppression table. Effort: **Medium**. Risk: **Low** (additive schema change, no existing contract broken).

**Design decisions confirmed with Ralf during this proposal:**
1. **Dismiss scope:** Dismissing suppresses the *entire finding identity* (`Type` + `Device`), not just the single displayed row — a dismissed identity does not resurface via discovery even if the figure changes, until reactivated.
2. **Reactivate scope:** Reactivating clears the dismissal *and* resumes normal FR-51 detection for that identity, in one action.
3. **Epic placement:** Story 12.4 lands in Epic 12, per Ralf's explicit choice, despite the thematic mismatch with FR-52/53 (device lifecycle) and FR-54 (decomposition) — same precedent as Story 12.3.
4. **Mechanical design:** One boolean flag on the existing per-identity representative row (no new suppression table), confirmed against the mechanics of FR-51's already-implemented de-duplication.
5. **Documentation backfill:** FR-51 gets its first-ever entry in `requirements-inventory.md` and its first-ever AD in `architecture.md`, bundled into this proposal since FR-55 is mechanically inseparable from it. The `epic-list.md` gap for FR-54/UX-DR21 (from Story 12.3) is also backfilled here. The Requirements Coverage table's FR-52/53/54 gap is explicitly left unfixed, as it's Epic-12-proper's own pre-existing gap, not created by this change.

## Section 4: Detailed Change Proposals

All edits below have been applied directly to their respective files as part of this `bmad-correct-course` pass.

### PRD (`prd.md`)
- §4.11 Actionable Insights: new **FR-55** added after FR-51's consequences, before the `---` separator preceding §4.12 Localization.
- §6.4 Release 4 scope bullets: new FR-55 line added after the FR-54 bullet.

### `requirements-inventory.md`
- Release 2 FR bucket: **FR-51** backfilled after FR-43 (with a note explaining the backfill).
- Release 4 FR bucket: **FR-55** added after FR-54.
- UX Design Requirements: **UX-DR22** added after UX-DR21.
- FR Coverage Map: **FR-51** inserted after FR-47 (`Epic 11 — Insight de-duplication and historical retention`); **FR-55** inserted after FR-54 (`Epic 12 — Insight dismiss and reactivate`).

### `epic-12-device-lifecycle-and-date-aware-decomposition-attribution.md`
- Header: `FRs covered` extended to FR-52, FR-53, FR-54, **FR-55**.
- New **Story 12.4: Insight Dismiss and Reactivate** appended in full Given/When/Then form (8 ACs covering: entity columns + migration; PATCH endpoint + tenant check; default-read active/dismissed filtering; dedup-check suppression; reactivate resuming normal detection; frontend action affordances; new mutation hooks; test coverage across both layers).

### `epic-list.md`
- Epic 12 summary paragraph extended to note Stories 12.3/12.4 as thematically-unrelated additions bucketed here per Ralf's choice, with pointers to both proposal docs.
- `FRs covered` extended to FR-52 through FR-55 (backfilling the FR-54 gap left by the 12.3 proposal).
- `UX items` extended to list UX-DR21 and UX-DR22 (backfilling the UX-DR21 gap left by the 12.3 proposal).

### `architecture.md`
- New **AD-8c** (Data Architecture section, after AD-8b): documents FR-51's de-dup mechanism and FR-55's dismiss/reactivate design as one mechanically-unified decision.
- Entity model table: `Insights` row gains `IsDismissed` (bool, default false) and `DismissedAt` (nullable datetimeoffset).
- Requirements Coverage table: new row `Insight De-dup / Dismiss | FR-51, FR-55 | ...` added after the existing `Insights | FR-36–39` row.

### `sprint-status.yaml`
- `12-4-insight-dismiss-and-reactivate: backlog` added under the Epic 12 block.
- Header `last_updated` comment extended to note this third same-day `bmad-correct-course` pass.

## Section 5: Implementation Handoff

**Change scope: Moderate.** New entity columns + migration, new API endpoint, an extension to shared de-duplication logic touching four detector call sites, and new frontend UI affordances (first-ever action row on `InsightCard`). Not a single-file fix, but fully self-contained — no cross-epic dependencies, no existing contract changes.

**Routed to:** Developer agent (`bmad-agent-dev` / `bmad-dev-story` or `bmad-quick-dev`) for direct implementation of Story 12.4 when Epic 12 is picked up (after Epic 11's retrospective). Recommended sequencing: 12.4 has no dependency on 12.1/12.2/12.3 and could be implemented in any order within Epic 12.

**Success criteria:** `PatchInsightFunction` dismisses/reactivates an Insight with correct tenant isolation; `InsightDeduplication`'s suppression check prevents new rows for a dismissed identity regardless of the 5% tolerance; `GetInsightsFunction` correctly filters active vs. dismissed; `InsightCard`/`InsightsTab` render the correct action button per view and the toggle switches views; full test coverage (`InsightDeduplicationTests.cs`, `PatchInsightFunction` tests, `GetInsightsFunction` tests, `InsightsTab`/`InsightCard` frontend tests) as specified in Story 12.4's final AC.

---

*All edits described in Section 4 have been applied directly to `prd.md`, `requirements-inventory.md`, `epic-12-device-lifecycle-and-date-aware-decomposition-attribution.md`, `epic-list.md`, `architecture.md`, and `sprint-status.yaml` as part of this `bmad-correct-course` pass, per Ralf's approval.*
