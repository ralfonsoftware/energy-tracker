# Sprint Change Proposal — 2026-07-27

## Section 1: Issue Summary

**Problem:** The Insights tab shows every finding twice (screenshot evidence: "Hochgerechneter Jahresverbauch" and "Geschirrspüler" cards each duplicated with identical values). Reported by Ralf on 2026-07-27 after a manual insights trigger the prior day produced correct output, followed by a doubled display the next day.

**Root cause** (fully traced in `_bmad-output/implementation-artifacts/investigations/insights-duplicated-across-runs-investigation.md`, Confidence: High): `ScheduledInsightsFunction` creates a new `InsightRun` for every flat every night at 02:00 UTC unconditionally, independent of any manual trigger. Each of the four detectors (`StandbyDetector`, `ReplacementDetector`, `BudgetAlertDetector`, `InvoiceDeviationDetector`) unconditionally writes a new `Insight` row whenever its own threshold condition is met, with no check against previously stored findings. `GetInsightsFunction` returns all `Insight` rows for the flat (by original design, per Story 10.1's AC, to satisfy FR-38's "prior insights remain visible during a new run" requirement) — so once a second run produces near-identical output, both sets display. This is architectural and deterministic, not a race condition, and is unrelated to Story 11.2's redelivery-race fix (already shipped, `commit 4ac3900`) — that hypothesis was independently checked and refuted during the investigation.

**Evidence:** `api/Features/Insights/GetInsightsFunction.cs:49-53` (unscoped query), `api/Features/Insights/ProcessInsightsFunction.cs:78-83` (cleanup scoped only to the same `RunId`), `api/Features/Insights/ScheduledInsightsFunction.cs:14-36` (unconditional nightly run creation), production screenshot (2x duplication of 2 distinct card types).

## Section 2: Impact Analysis

**Epic Impact:** Epic 10 (Actionable Insights) is `done` and not reopened — the gap is in Story 10.1's original API contract, which is amended by reference (not rewritten) via a new FR and a new story. Epic 11 (Post-Epic-10 Hardening), currently `in-progress`, is the natural home and gains one new story (11.13). No other Epic 11 story (11.3-11.12) is affected.

**Story Impact:** New Story 11.13 added. No existing story's scope changes. Notably, `GetInsightsFunction.cs` and its existing test suite (`GetInsightsFunctionTests.cs`) require **no changes** — the read path was already correct for whatever rows exist; the fix is entirely a write-time guard in the four detectors.

**Artifact Conflicts:**
- PRD: new FR-51 added to §4.11 Actionable Insights (write-time de-duplication + retention). §6.2 Release 2 FR list updated to include it.
- Epic 11 doc: new Story 11.13 appended, with a note explaining its off-cycle origin (production investigation, not the original Epic 10 retro batch); epic summary and `FRs covered` line updated.
- `epic-list.md`: Epic 11's summary line updated to mention the new story and FR-51.
- `sprint-status.yaml`: new `11-13-...: backlog` entry added, with a comment flagging recommended priority ahead of 11.3-11.12 since it's a live production bug rather than a latent gap.
- Architecture: no conflict. AD-8 ("hard deletes throughout") permits — doesn't mandate — deletion; the fix requires *not* deleting, which is compatible. `Insights.Data`'s "opaque JSON, deserialize in application layer" rule (project-context.md) is honored: the new shared dedup utility parses JSON in C#, no LINQ predicates against the JSON column.
- UI/UX: no conflict, no new UX-DR needed — `InsightCard.tsx` renders unchanged; only which/how-many rows the API produces changes.
- Other artifacts (CI/IaC/deployment): no impact.

**Technical Impact:** New shared utility `api/Shared/InsightDeduplication.cs`; one additional call site in each of the four detector files, gated on a ±5% relative-tolerance comparison against the most recently stored `Insight` for the same `(FlatId, Type, DeviceId)` identity. No schema migration, no deletion logic, no API contract change.

## Section 3: Recommended Approach

**Selected: Option 1 — Direct Adjustment.** Add FR-51 to the PRD, add Story 11.13 to Epic 11, update sprint tracking. No rollback needed (Stories 10.1/10.2/10.4/11.2 all remain correct for what they do — this closes a gap none of them were scoped to cover). No MVP/scope review needed (no Release boundary or core-goal impact).

**Rationale:** The fix is self-contained (four detector files + one new shared utility), carries low risk (no deletion, no schema change, no API contract change, `GetInsightsFunction` untouched), and Epic 11 already exists as exactly the right home for post-Epic-10 hardening work. Effort: Medium. Risk: Low.

**Design decisions made during this proposal (confirmed with Ralf):**
1. **Dedup direction:** write-time skip (older finding kept, newer near-duplicate not persisted) — not read-time collapsing. Simpler, and means the read path (`GetInsightsFunction`) needs no changes at all.
2. **Tolerance:** ±5% relative difference on each Insight type's primary quantified figure (`estimatedMonthlyCost` / `estimatedSavingsEur` / `overspendEur` / `impliedDeltaEur`), grouped by `(Type, DeviceId)`.
3. **Retention:** no `Insight` row is ever deleted — full history is preserved by construction (a materially different finding, beyond the 5% tolerance, is simply a new row alongside the old one), explicitly to support a future "dismiss a finding" feature without further schema work now.

## Section 4: Detailed Change Proposals

### PRD — new FR-51
**File:** `_bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md`
**Location:** §4.11 Actionable Insights, after FR-43; also added to the §6.2 Release 2 FR list.
**Change:** New FR (full text applied to the file) specifying write-time de-duplication and retention, with three testable consequences. See file for exact wording.
**Justification:** Formalizes retention + dedup behavior in FR/AC-testable form; closes the gap FR-38 left open (continuity *during* a run, no rule *across completed* runs).

### Epic 11 — new Story 11.13
**File:** `_bmad-output/planning-artifacts/epics/epic-11-post-epic-10-hardening-and-technical-debt-resolution.md`
**Location:** Appended after Story 11.12.
**Change:** Full story with Given/When/Then ACs specifying the `InsightDeduplication` shared utility's exact signature and tolerance formula, the four detector call sites (cited by file:line), and test coverage requirements. Epic summary/intro and `FRs covered` line updated to reference it.
**Justification:** Gives a dev agent an unambiguous, Ready-for-Development spec — exact file:line locations, exact JSON property names per Insight type, exact tolerance formula, explicit statement that `GetInsightsFunction` needs no change.

### epic-list.md — summary update
**File:** `_bmad-output/planning-artifacts/epics/epic-list.md`
**Change:** Epic 11 entry's summary sentence and `FRs covered` line updated to mention Story 11.13 / FR-51.

### sprint-status.yaml — new backlog entry
**File:** `_bmad-output/implementation-artifacts/sprint-status.yaml`
**Change:** `11-13-insight-deduplication-skip-writing-near-identical-findings: backlog` added after 11-12, with a priority-flagging comment. `last_updated` header updated.

## Section 5: Implementation Handoff

**Scope classification: Moderate** — required backlog reorganization (new FR, new story, sprint tracking update) before implementation could begin; now that these are in place, the actual code change is a contained, single-developer-agent task.

**Handoff:** Story 11.13 is Ready for Development. Recommended next step: `bmad-create-story` (to materialize the full story-context file from this epic entry) or directly `bmad-dev-story` if the epic AC detail above is already sufficient context. Recommend prioritizing ahead of Stories 11.3-11.12 given this is a live, user-visible production bug.

**Deliverables produced by this proposal:**
- This Sprint Change Proposal document
- Updated PRD (FR-51)
- Updated Epic 11 (Story 11.13)
- Updated epic-list.md
- Updated sprint-status.yaml
