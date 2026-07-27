# Sprint Change Proposal — 2026-07-27 (FR-51 Amendment)

## Section 1: Issue Summary

**Trigger:** Following up on `sprint-change-proposal-2026-07-27.md` (which introduced FR-51 and Story 11.13) and today's production DB cleanup (2 pre-deploy duplicate `Insight` rows removed for flat `d3155f5b…`, documented in `insights-duplicated-across-runs-investigation.md`'s `## Follow-up: 2026-07-27` section).

**Problem:** FR-51, as ratified earlier today, deliberately chose unlimited historical retention: every distinct (>5% different) `Insight` finding stays in the data store *and stays visible on the Insights tab forever* — explicitly "to support a future 'dismiss a finding' feature without further schema work now" (per the original proposal's Section 3). That dismiss feature does not exist yet. Ralf, on reflection after seeing this tradeoff play out in production, confirmed the concern is real: the Insights tab will accumulate every legitimately-distinct (non-near-duplicate) finding indefinitely over a flat's lifetime, with no way to manage the growing list until a future feature ships — an interim gap, not a permanent-policy objection.

**Evidence:** `prd.md:471-477` (FR-51, pre-amendment wording, "both remain visible"); `epic-11-post-epic-10-hardening-and-technical-debt-resolution.md:272` (Story 11.13's AC explicitly leaving `GetInsightsFunction.cs` unchanged "since the read path was already correct"); original investigation `insights-duplicated-across-runs-investigation.md` Findings 1-3 / Deduction 1 (unbounded growth, deterministic, will recur for every flat).

## Section 2: Impact Analysis

**Epic Impact:** Epic 10 (`done`) not reopened — amended by reference, same pattern as the original FR-51 introduction. Epic 11 (`in-progress`) gains Story 11.14. No other Epic 11 story affected; Story 11.13 remains `done` and correctly implemented for what it covers (write-time guard) — this amendment adds a read-time complement, it does not revert or redo 11.13.

**Story Impact:** New Story 11.14 added. Story 11.13's file gains an inline amendment note on its final AC (which is now historically-accurate-but-superseded, not currently-correct) rather than being rewritten, to preserve an honest record of what was actually implemented on that story.

**Artifact Conflicts:**
- PRD: FR-51 wording amended in `prd.md:471-477` — retention guarantee (no row ever deleted) unchanged; visibility guarantee narrowed to "most-recent-per-identity by default."
- Epic 11 doc: new Story 11.14 appended; Story 11.13's final AC annotated with a superseded-note; epic intro paragraph gains a sentence on 11.14's origin.
- `epic-list.md`: Epic 11 summary sentence and `FRs covered` line updated (FR-51 now covers Stories 11.13 *and* 11.14).
- `sprint-status.yaml`: new `11-14-...: backlog` entry added after 11.13; `last_updated` header updated.
- Architecture: no conflict. No schema change; no new migration; `Insights.Data` opaque-JSON rule unaffected (11.14 reuses 11.13's existing JSON-parsing helper, adds no new parsing).
- UI/UX: no conflict — `InsightCard.tsx` / `InsightsTab.tsx` render unchanged; only which rows the API returns changes (fewer rows, same shape).
- Other artifacts: no impact.

**Technical Impact:** `GetInsightsFunction.cs:49-53`'s query changes from unscoped-by-flat to grouped-by-`(Type, DeviceId)`-most-recent. No deletion logic, no API contract/shape change (`InsightDto` unchanged), no schema migration. One existing test (`GetInsightsFunctionTests.cs:75-91`) requires updating, not just extending, since it currently locks in the old all-time-unscoped contract.

## Section 3: Recommended Approach

**Selected: Option 1 — Direct Adjustment.** Amend FR-51's wording, add Story 11.14 to Epic 11, update sprint tracking. No rollback of Story 11.13 (its write-time guard remains correct and necessary — it's what keeps 11.14's per-identity "most recent" row from itself being a near-duplicate of a slightly-older one). No MVP/scope review needed.

**Rationale:** Self-contained (one Function's query + one test file), low risk (no schema change, no deletion, additive to what 11.13 already built), and directly closes the gap Ralf identified without discarding any of today's earlier work. Effort: Low-Medium. Risk: Low.

**Design decisions confirmed with Ralf during this proposal:**
1. **Scope, not rollback:** this is an amendment to FR-51's *visibility* clause only — the *retention* clause (no row ever deleted) is unchanged, preserving the original future-dismiss-feature intent.
2. **Grouping key:** `(Type, DeviceId)`, matching Story 11.13's existing `InsightDeduplication` identity definition exactly — no new concept, no `RunId` filtering (which would incorrectly hide a still-current finding whose type simply didn't re-fire in the latest run).
3. **Tie-break:** `CreatedAt` descending, then `InsightId` descending — matching `InsightDeduplication.cs:31`'s existing tie-break for consistency between the write guard and the read scope.

## Section 4: Detailed Change Proposals

### PRD — FR-51 amended
**File:** `prd.md:471-477` — applied. Visibility clause narrowed to most-recent-per-identity; retention clause (no deletion) unchanged; consequences list updated to match.

### Epic 11 — new Story 11.14 + annotation on 11.13
**File:** `epic-11-post-epic-10-hardening-and-technical-debt-resolution.md` — applied. Story 11.14 appended in full Given/When/Then form; Story 11.13's final AC annotated as superseded-but-historically-accurate; epic intro gains a sentence on 11.14.

### epic-list.md — summary update
**File:** `epic-list.md:55-56` — applied. Summary sentence and `FRs covered` line updated.

### sprint-status.yaml — new backlog entry
**File:** `sprint-status.yaml` — applied. `11-14-scope-default-insights-read-to-most-recent-per-identity: backlog` added; `last_updated` header updated.

## Section 5: Implementation Handoff

**Scope classification: Minor** — the epic/PRD/sprint bookkeeping is already done as part of this proposal; the remaining work (the actual code change) is a single, contained Developer-agent task.

**Handoff:** Story 11.14 is Ready for Development. Recommended next step: `bmad-create-story` to materialize the full story-context file, then `bmad-dev-story` for implementation.

**Deliverables produced by this proposal:**
- This Sprint Change Proposal document
- Updated PRD (FR-51 amended)
- Updated Epic 11 (Story 11.14 added, Story 11.13 annotated)
- Updated `epic-list.md`
- Updated `sprint-status.yaml`
