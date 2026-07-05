# Sprint Change Proposal — Pre-Epic-6 Hardening (2026-07-05)

## 1. Issue Summary

Following the Epic 5 Retrospective (2026-07-05), Ralf ran a party-mode roundtable with four independent agents (John/PM, Winston/Architect, Amelia/Dev, Murat/Test Architect) against the full `deferred-work.md` backlog (260+ lines accumulated since Epic 1) to identify what should land before Epic 6 (Smart Plug Import & Device Registry) begins. Each agent picked a top 3 independently; convergence plus one explicit addition from Ralf produced four items to formalize into the sprint plan **before any Epic 6 story is created**:

1. **No CI test gate** — `.github/workflows/azure-static-web-apps.yml` has no `dotnet test`/`npm test` step and no `pull_request` trigger. 456+ existing tests (208 backend, 248+ frontend) run nowhere in CI. Unanimous pick across all four agents — highest-leverage, lowest-cost item on the board, and the only item that protects the correctness of every other fix.
2. **`OnboardingValidator.cs` never validates `PlannedAnnualSpend`** — confirmed pre-existing via `git show` at baseline, missed by the decimal-precision sweep (commit `89b4fd5`) that added the same rule to every sibling validator (`CreateFlatValidator`, `PatchFlatValidator`). A value like `500.56789` bypasses validation entirely on the onboarding endpoint today. Picked by John, Amelia, Murat.
3. **No optimistic concurrency control anywhere in the codebase** — flagged since Epic 3, never fixed. Epic 6 introduces the first background-job write path (blob-triggered import processing) that can race a human edit or a retried trigger, which is a materially different risk profile than today's human-vs-human low-probability races. Picked by Winston, Murat — both scoped this to Epic 6's new tables only, not a full retrofit.
4. **No delete affordance for rooms/power points/devices in the Flat Structure editor** — picked by John ("a missing verb," not polish), and separately confirmed by Ralf: "add ... from John - like it and miss it." Users can add structure but not remove a mistake; this gets more entangled once Epic 6 wires SmartPlug data onto these entities.

## 2. Impact Analysis

- **PRD Impact:** None. All four items are engineering-hardening/bugfix work, not new product requirements; none map to a new FR.
- **Architecture Impact:** Item 3 introduces the codebase's first `RowVersion`/concurrency-token pattern (scoped to `ImportJob`, `SmartPlugDailyData`, `SmartPlugIntervalData`) — `architecture.md`'s EF Core conventions section gains a documented pattern rather than a conflict. No other architecture changes needed.
- **UX Impact:** Item 4 adds a delete affordance to `client/src/features/flat-structure/components/`; no wireframe exists for this in the UX spec (Story 5.4 explicitly scoped delete out per its AC), so this is new UI within an existing screen, not a spec conflict.
- **Epic Impact:**
  - **Epic 5** is fully shipped (all 5 stories `done`). Item 4 is new capability on top of already-shipped Story 5.4, not a retroactive AC change to a completed story — handled as a new story rather than reopening history.
  - **Epic 6** is `backlog` with zero story files created yet, so amending its epic file / story sequencing costs zero rework (same zero-cost condition as the 2026-07-03 Story 5.1 AC precedent).
  - No other planned epics (7, 8) are affected.
- **Technical Impact:** Items 1, 2, 4 are cross-cutting and don't depend on Epic 6's new data model — they're bundled into one new prep story. Item 3 depends directly on Epic 6's new tables, so it's added as an AC amendment to Story 6.1 itself rather than a separate story.
- **Data Impact:** Item 3 adds a `RowVersion` column to three tables that don't exist yet (created by Story 6.1's own migration) — no migration-ordering risk.

## 3. Recommended Approach

**Hybrid Direct Adjustment** — same category as the 2026-07-03 precedent, split two ways because the four items have different natural homes:

- **Items 1, 2, 4** (CI gate, validator fix, delete affordance) are unrelated to Epic 6's specific schema and are bundled into a **new Story 6.0**, sequenced before Story 6.1 in `epic-6-smart-plug-import-device-registry.md`.
- **Item 3** (scoped optimistic concurrency) is added as a **new AC directly on Story 6.1**, since it only makes sense in terms of the tables Story 6.1 itself creates.

**Naming note:** the new story is numbered **6.0**, not renumbered into the 6.1–6.6 sequence with everything shifted up. A renumbering approach was considered and rejected: `Story 6.1`–`6.6` are already referenced by number in finalized historical documents (`epic-5-retro-2026-07-05.md` Action Items #4/#5, `deferred-work.md`, `implementation-readiness-report-2026-06-28.md`) that describe *this specific* import-pipeline story as "6.1." Renumbering would silently invalidate those references. `6.0` is a one-time, clearly-flagged deviation from the project's strict `X.1, X.2, ...` convention, chosen to avoid that staleness risk — not a new precedent for future epics.

- **Effort:** Low — one new story (mostly independent, mechanical fixes), one AC append to a not-yet-created story.
- **Risk:** Low — no code touched by this proposal itself; Epic 6 has no story files yet, so nothing is reworked.
- **Sequencing:** Story 6.0 is created and completed first; Story 6.1 (amended) follows; Stories 6.2–6.6 unaffected and unchanged.

## 4. Detailed Change Proposals

### `epics/epic-6-smart-plug-import-device-registry.md` — new Story 6.0 (inserted before Story 6.1)

```
## Story 6.0: Pre-Epic-6 Hardening — CI Test Gate, Onboarding Validator Fix & Flat Structure Delete Affordance

As a user and as the team maintaining this app,
I want the existing test suite to actually gate merges, the onboarding
form to validate the same fields its siblings already validate, and a
way to remove a mistakenly-added room/power point/device,
So that Epic 6 builds on a codebase where regressions are caught before
merge, a known validation bypass is closed, and flat structure
management isn't a one-way ratchet.

**Acceptance Criteria:**

**Given** `.github/workflows/azure-static-web-apps.yml`,
**When** the workflow is updated,
**Then** it adds a `dotnet test` step (running `api.Tests`) and an
`npm test` step (running the Vitest suite in `client/`), both required
to pass before the build/publish jobs run; the trigger block adds
`pull_request: branches: [main]` alongside the existing trigger, so
every PR runs the same gate before merge — not just pushes to `main`.

**Given** `api/Features/Onboarding/OnboardingValidator.cs`,
**When** `PlannedAnnualSpend` is validated,
**Then** it receives the same rule already present on
`CreateFlatValidator`/`PatchFlatValidator` for the identical
`Flat.PlannedAnnualSpend` column: `GreaterThan(0)`, `LessThan(50000)`,
`PrecisionScale(18, 4, true)`; a new or extended
`OnboardingValidatorTests.cs` case asserts a value with more than 4
decimal places, and a value outside the (0, 50000) range, are both
rejected with 400 Problem Details.

**Given** the Flat Structure editor
(`client/src/features/flat-structure/components/`),
**When** a user views an existing Room, Power Point, or Device,
**Then** a delete affordance is present for each; tapping it removes
the item from the client-side draft model (removing a Room also
removes its child Power Points/Devices from the draft); a single
confirmation step (inline or modal) is required before removal to
guard against accidental loss; on Save, the removal is carried by the
existing `PUT /api/v1/flats/{flatId}/structure` full-replace contract
(delete-and-reinsert transaction) — no new backend endpoint is needed.

**Given** the three fixes above,
**When** the story reaches `done`,
**Then** `dotnet test` and `npm test` both pass locally and (per AC1,
now) in CI; `OnboardingValidatorTests.cs` covers the new rule;
`FlatStructureEditor.test.tsx` (or equivalent) covers delete-with-
confirm for a Room, a Power Point, and a Device.

---

```

### `epics/epic-6-smart-plug-import-device-registry.md` — amendment to Story 6.1 (append new AC)

```
OLD (end of Story 6.1's AC block, before the `---` separator):
**Given** import error categorization (FR-28),
**When** `ImportJob.ErrorCategory` is set,
**Then** the frontend maps it to exactly one user-facing message:
`DataUnreadable` → "Data cannot be read."; `ProcessingFailed` →
"Processing failed — try again."; `ServiceUnavailable` → "Service
temporarily unavailable — try again later."

NEW (appended AC before the `---` separator):
**Given** import error categorization (FR-28),
**When** `ImportJob.ErrorCategory` is set,
**Then** the frontend maps it to exactly one user-facing message:
`DataUnreadable` → "Data cannot be read."; `ProcessingFailed` →
"Processing failed — try again."; `ServiceUnavailable` → "Service
temporarily unavailable — try again later."

**Given** `ImportJob`, `SmartPlugDailyData`, and `SmartPlugIntervalData`
can be written concurrently (e.g., a retried blob trigger re-processing
the same job while a status poll or a structure edit is in flight),
**When** `ImportJobConfiguration` is defined,
**Then** `ImportJob` includes a `RowVersion` (SQL Server `rowversion`)
column configured via EF Core `.IsRowVersion()`; a
`DbUpdateConcurrencyException` on save is caught and surfaces as
`ImportJob.Status = Failed`, `ErrorCategory = ProcessingFailed` —
never an unhandled 500 or a silent overwrite; this is the codebase's
first concurrency-token pattern, deliberately scoped to these three
new tables only — `Flat`, `Tariff`, and `MeterReading` remain
last-write-wins, tracked separately in `deferred-work.md`.
```

**Rationale:** Mirrors the 2026-07-03 precedent of amending a not-yet-created story's AC directly (Story 5.1's hard test-coverage requirement) — zero rework cost since Epic 6 has no story files yet. Keeps the concurrency fix scoped to exactly the tables it protects, per Winston's and Murat's explicit framing in the roundtable, rather than reopening the architecture-wide concurrency gap as a separate initiative.

### `_bmad-output/implementation-artifacts/sprint-status.yaml`

```
OLD:
  # Epic 6: Smart Plug Import & Device Registry
  epic-6: backlog
  6-1-import-pipeline-infrastructure-upload-job-tracking-and-blob-trigger: backlog

NEW:
  # Epic 6: Smart Plug Import & Device Registry
  epic-6: backlog
  6-0-pre-epic-6-hardening-ci-test-gate-onboarding-validator-fix-and-flat-structure-delete-affordance: backlog
  6-1-import-pipeline-infrastructure-upload-job-tracking-and-blob-trigger: backlog
```

(Stories 6-2 through 6-6 and `epic-6-retrospective` are unchanged.)

### `_bmad-output/implementation-artifacts/deferred-work.md`

No new section needed — item 3 (concurrency) is now tracked as a Story 6.1 AC rather than a deferred item; items 1, 2, 4 are now tracked as Story 6.0 ACs. The existing entries these items originated from (CI gate: "1-1 review 2026-06-27"; validator gap: "decimal-precision-validation-policy review 2026-07-05"; delete affordance: "story-5.4 review 2026-07-05") are left as historical record and not deleted — consistent with how already-resolved items elsewhere in this file are marked `~~RESOLVED~~` rather than removed. This proposal does not edit `deferred-work.md` directly; the next code-review pass that closes these items should strike them through with a `RESOLVED` note per the file's existing convention.

## 5. Implementation Handoff

**Scope classification: Moderate** — backlog reorganization (new story inserted, `sprint-status.yaml` updated) plus a not-yet-implemented AC amendment; technical risk and effort are both Low since no existing code or completed story is touched.

- **Routed to:** Developer agent (Amelia), for both the new Story 6.0 implementation and, later, Story 6.1's amended AC when that story is implemented. No PM/Architect replan needed — Winston's and Murat's scoping decisions are already folded into the AC text above.
- **Success criteria:**
  - Story 6.0 reaches `done`: CI runs `dotnet test`/`npm test` on every PR; `OnboardingValidatorTests.cs` covers the new rule; delete affordance ships with test coverage for Room/PowerPoint/Device.
  - Story 6.1, when implemented, includes the `RowVersion` column and concurrency-exception handling as a hard AC, not optional polish.

## Status

**Approved by Ralf (2026-07-05), batch mode**, following the party-mode roundtable (John, Winston, Amelia, Murat). Applied directly as part of this Correct Course run:

- `epics/epic-6-smart-plug-import-device-registry.md` — Story 6.0 inserted before Story 6.1; Story 6.1's AC amended with the scoped `RowVersion` requirement.
- `sprint-status.yaml` — `6-0-pre-epic-6-hardening-ci-test-gate-onboarding-validator-fix-and-flat-structure-delete-affordance: backlog` added ahead of `6-1-...`; `last_updated` header updated.
