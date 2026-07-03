# Sprint Change Proposal — Epic 5 Story 5.1: Hard-Require DeleteFlatFunction Test Coverage (2026-07-03)

## 1. Issue Summary

Epic 4 Retrospective (2026-07-03) Action Item #4: Story 5.1's current AC describes `DeleteFlatFunction`'s cascade-delete *behavior* in detail but does not explicitly require *test coverage* of that behavior as a condition of the story reaching `done`.

This exact pattern has already caused a slip once: the Epic 3 retrospective asked, in soft language ("consider backfilling"), for HTTP-level test coverage of `CompleteOnboardingFunction`/`PatchFlatFunction`. That request went unaddressed through the entirety of Epic 4 and had to be re-committed with harder language in the Epic 4 retrospective (Action Item #3). `DeleteFlatFunction` is a destructive, irreversible operation (permanent cascade delete across `MeterReadings`, `Tariffs`, `SmartPlugDailyData`, `SmartPlugIntervalData`, `ImportJobs`, `Rooms`/`PowerPoints`/`Devices`, `InsightRuns`, `Insights`) — the cost of an untested regression here is materially higher than for the onboarding/flat-patch functions that already slipped once. The fix is applied now, before Story 5.1 is created, to close the gap at zero cost (no rework).

## 2. Impact Analysis

- **PRD Impact:** None. This is a testing-discipline requirement, not a product requirement change.
- **Architecture Impact:** None. `architecture.md`'s existing test-placement convention (`api.Tests/Features/{Feature}/{Class}Tests.cs`) already covers the pattern; no new section needed.
- **UX Impact:** None.
- **Epic Impact:** Epic 5 only, Story 5.1 only. Epic 5 is still `backlog` in `sprint-status.yaml` with no story file created yet, so this is a pure epic-file AC amendment with zero rework cost. No other Epic 5 stories (5.2–5.4) or later epics (6, 7, 8) are affected.
- **Technical Impact:** None today. The requirement takes effect when Story 5.1 is created (`bmad-create-story`) and implemented (`bmad-dev-story`) — `DeleteFlatFunctionTests.cs` becomes a hard gate for the story to reach `done`.
- **Data Impact:** None.

## 3. Recommended Approach

**Direct Adjustment** — amend Story 5.1's AC in `epic-5-multi-flat-management-flat-structure.md` to add an explicit, testable requirement line. No rollback, no MVP/scope change.

- **Effort:** Low — one AC line added to an epic file not yet consumed by any story.
- **Risk:** Low — contained to Epic 5 Story 5.1; no code or migration touched.
- **Sequencing:** Applied immediately, before Epic 5 story creation begins.

## 4. Detailed Change Proposal

### Epic 5 (`_bmad-output/planning-artifacts/epics/epic-5-multi-flat-management-flat-structure.md`)

Story 5.1, `DELETE /api/v1/flats/{flatId}` AC block:

```
OLD (last line of block):
**And** cascade delete is enforced at the database level via
`OnDelete(DeleteBehavior.Cascade)` in Fluent API on all FK relationships
from `Flats` — not application-side loops.

NEW (appended line):
**And** `DeleteFlatFunctionTests` (HTTP-level, `api.Tests/Features/Flats/`)
is a hard requirement for Story 5.1 to reach `done` — not optional
polish — and must assert: (1) cascade completeness — zero rows remain in
`MeterReadings`, `Tariffs`, `SmartPlugDailyData`, `SmartPlugIntervalData`,
`ImportJobs`, `Rooms`, `PowerPoints`, `Devices`, `InsightRuns`, `Insights`
for the deleted `flatId`; (2) wrong-owner rejection — a `DELETE` for a
`flatId` not owned by the resolved `UserId` returns HTTP 403 and performs
no deletion; (3) no-orphaned-records / sibling isolation — deleting one
Flat leaves all data belonging to any other Flat (same or different
owner) untouched.
```

**Rationale:** Codifies the retro's exact language ("cascade completeness, wrong-owner 403, no-orphaned-records") as a testable, non-optional acceptance criterion, mirroring the Epic 3→4 precedent of re-committing a soft test ask as a hard requirement once it has already slipped once elsewhere in the project.

### `sprint-status.yaml`

No change — `5-1-multi-flat-backend-create-list-and-cascade-delete: backlog` already exists; this proposal only amends the AC text the eventual story file will be created from.

## 5. Implementation Handoff

**Scope classification: Minor** — pure epic-file text edit, no code, no migration, no story file yet in existence.

- **Routed to:** Applied directly as part of this Correct Course run. No further PO/PM/Architect involvement needed.
- **Success criteria:** When Story 5.1 is created (`bmad-create-story`) and later implemented (`bmad-dev-story`), `DeleteFlatFunctionTests.cs` exists in `api.Tests/Features/Flats/`, covers all three named scenarios, and passes before the story is marked `done`.

## Status

Approved by Ralf (2026-07-03), batch mode. Epic 5 file edit applied directly as described above.
