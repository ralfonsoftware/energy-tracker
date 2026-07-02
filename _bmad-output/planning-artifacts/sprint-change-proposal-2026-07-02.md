# Sprint Change Proposal — Epic 4 AC Gaps (2026-07-02)

## 1. Issue Summary

During a pre-Epic-4 readiness check (via `bmad-help`), Epic 4's acceptance criteria for Story 4.1 were compared against decisions made during the Epic 3 retrospective (2026-07-02). Two gaps were found between what the epic specifies and what the codebase already assumes:

1. A unique index `IX_Tariffs_FlatId_EffectiveDate` (migration `MakeTariffEffectiveDateUnique`) was added specifically because Epic 4's tariff CRUD removes the uniqueness guarantee that onboarding alone previously provided structurally. Story 4.1's `POST` acceptance criteria never specified behavior when a submitted `EffectiveDate` collides with an existing entry — as written, this would surface as an unhandled DB-constraint exception (500) rather than a clean error response.
2. The Epic 3 retro established `PricePerKwh < 10` and `MonthlyBaseFee < 1000` as a project-wide numeric upper-bound convention, already enforced in `OnboardingValidator.cs` and `PatchFlatValidator.cs`. Story 4.1's `TariffValidator` acceptance criteria only specified lower-bound checks (`≤ 0`, `< 0`), omitting the same upper bounds for the identical fields.

Both gaps were confirmed by reading the current codebase (`api/Data/Migrations/20260702121947_MakeTariffEffectiveDateUnique.cs`, `api/Features/Onboarding/OnboardingValidator.cs`, `api/Features/Flats/PatchFlatValidator.cs`) and the Epic 3 retro document (`_bmad-output/implementation-artifacts/epic-3-retro-2026-07-02.md`), which explicitly flagged both items as resolved-for-Epic-4 groundwork.

## 2. Impact Analysis

- **Epic Impact:** Epic 4 (Tariff Management) only. No impact to Epics 1–3 (already implemented) or Epics 5–8 (not yet started).
- **Story Impact:** Story 4.1 (Tariff CRUD Backend) acceptance criteria updated. Stories 4.2 and 4.3 unaffected — their AC already assume the 4.1 backend contract without depending on the specific gaps found.
- **Artifact Conflicts:** None with PRD, Architecture, or UX docs — this is an epic-level AC correction, not a scope or requirements change. No FR is added or removed.
- **Technical Impact:** None retroactively — the underlying migration and validator conventions already exist in the codebase; this proposal only brings the epic's written AC in line with them before Story 4.1 implementation begins.

## 3. Recommended Approach

**Direct Adjustment** — add two acceptance-criteria items to Story 4.1 in `epic-4-tariff-management.md`. No rollback, no MVP scope change, no PRD/architecture rework needed.

- **Effort:** Trivial (documentation-only change to an epic file not yet implemented).
- **Risk:** None — corrects the spec before code is written against it, avoiding a rework cycle during Story 4.1's code review.
- **Timeline impact:** None; Story 4.1 has not started.

## 4. Detailed Change Proposals

### Epic 4 — Story 4.1 (`_bmad-output/planning-artifacts/epics/epic-4-tariff-management.md`)

**Change A — new AC for duplicate `EffectiveDate`:**

OLD (no such AC existed; inserted immediately after the existing `POST` AC block):
```
(none)
```

NEW:
```
**Given** `POST /api/v1/flats/{flatId}/tariffs` with an `EffectiveDate` that already has a Tariff entry for this Flat,
**When** `CreateTariffFunction.RunAsync` executes,
**Then** HTTP 409 Problem Details (`type: "https://tools.ietf.org/html/rfc9110#section-15.5.10"`, `title: "Conflict"`) is returned; no record is created.
**And** this relies on the `IX_Tariffs_FlatId_EffectiveDate` unique index (added via migration `MakeTariffEffectiveDateUnique` ahead of this epic) — the function checks for an existing entry before insert so the collision never surfaces as an unhandled DB-constraint 500.
```

**Rationale:** The unique index was added in the Epic 3 retro specifically because Epic 4's CRUD removes onboarding's structural uniqueness guarantee. The 409 Conflict pattern (status, `type`, `title` shape) matches the existing convention in `CompleteOnboardingFunction.cs` for "resource already exists" (`Onboarding already completed.`), keeping the new endpoint consistent with the rest of the API.

**Change B — extend `TariffValidator` AC with upper bounds:**

OLD:
```
**Given** `TariffValidator` (FluentValidation),
**When** `PricePerKwh ≤ 0`, `MonthlyBaseFee < 0`, or `EffectiveDate` is missing,
**Then** HTTP 400 Problem Details is returned; no record is created.
```

NEW:
```
**Given** `TariffValidator` (FluentValidation),
**When** `PricePerKwh ≤ 0` or `≥ 10`, `MonthlyBaseFee < 0` or `≥ 1000`, or `EffectiveDate` is missing,
**Then** HTTP 400 Problem Details is returned; no record is created.
**And** the upper bounds match the project-wide numeric-bound convention established in `OnboardingValidator` and `PatchFlatValidator` during the Epic 3 retrospective (2026-07-02).
```

**Rationale:** `PricePerKwh < 10` and `MonthlyBaseFee < 1000` are already enforced for the same two fields in `OnboardingValidator.cs`. Story 4.1 should apply identical bounds rather than only guarding against negative/zero values, avoiding an inconsistent validation surface between onboarding and tariff CRUD.

## 5. Implementation Handoff

**Scope classification: Minor** — both changes are epic-file text edits, already applied directly to `epic-4-tariff-management.md`. No PO/DEV backlog reorganization or PM/Architect replan required.

- **Routed to:** Developer agent (`bmad-agent-dev` / `bmad-dev-story`) — implement `CreateTariffFunction` and `TariffValidator` in Story 4.1 against the now-updated AC.
- **Success criteria:** Story 4.1's implementation includes a pre-insert duplicate-`EffectiveDate` check returning 409 Problem Details, and `TariffValidator` enforces `PricePerKwh` in `(0, 10)` and `MonthlyBaseFee` in `[0, 1000)`.

## Status

Approved by Ralf (2026-07-02). Both changes applied directly to `epic-4-tariff-management.md`.
