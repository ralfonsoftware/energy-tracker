# Epic 4: Tariff Management

A user can maintain a full tariff history with effective dates, optional contract periods, and forward-dated tariff changes. All cost figures use the tariff active on the date of consumption, not the current tariff.

## Story 4.1: Tariff CRUD Backend — List, Create & Contract Lock Enforcement

> **Amended 2026-07-03** (`sprint-change-proposal-2026-07-03.md`): the original AC below used `EffectiveDate` as the required temporal field, separate from optional `ContractStartDate`/`ContractDurationMonths`-gated locking. Live testing during the Epic 4 retrospective showed this split doesn't match how users enter real contracts. `ContractStartDate` is now the sole required temporal anchor for both cost-period resolution and price locking; `EffectiveDate` is retired; `ContractDurationMonths` becomes informational only. The AC below reflects the corrected model — implementation lives in **Story 4.4**, not a rewrite of this already-`done` story.

As a user,
I want to add new tariff entries, list my tariff history, and have the app prevent me from editing price fields on active contracts,
So that my tariff history is accurate and locked rates cannot be accidentally changed.

**Acceptance Criteria:**

**Given** `GET /api/v1/flats/{flatId}/tariffs`,
**When** called,
**Then** `GetTariffsFunction` returns all Tariff entries in descending contract-start-date order as `TariffResponse` records (`TariffId`, `ContractStartDate` datetimeoffset (required), `PricePerKwh` decimal, `MonthlyBaseFee` decimal, `ProviderName` nullable string, `ContractDurationMonths` nullable int (informational only), `IsLocked` bool derived from: `ContractStartDate` is on or before today); HTTP 200; ≤ 2s response time.

**Given** `POST /api/v1/flats/{flatId}/tariffs` with a valid request body,
**When** `CreateTariffFunction.RunAsync` executes,
**Then** a new `Tariff` record is created with all fields stored locale-neutrally (`ContractStartDate` as datetimeoffset, `PricePerKwh` and `MonthlyBaseFee` as `decimal`); HTTP 201 with `Location` header; ≤ 2s response time.
**And** a future `ContractStartDate` is accepted without affecting any past cost calculations, and its price fields remain freely editable until that date arrives (FR-12).

**Given** `POST /api/v1/flats/{flatId}/tariffs` with a `ContractStartDate` that already has a Tariff entry for this Flat,
**When** `CreateTariffFunction.RunAsync` executes,
**Then** HTTP 409 Problem Details (`type: "https://tools.ietf.org/html/rfc9110#section-15.5.10"`, `title: "Conflict"`) is returned; no record is created.
**And** this relies on the `IX_Tariffs_FlatId_ContractStartDate` unique index (moved from `EffectiveDate` by Story 4.4's migration) — the function checks for an existing entry before insert so the collision never surfaces as an unhandled DB-constraint 500.

**Given** `TariffValidator` (FluentValidation),
**When** `PricePerKwh ≤ 0` or `≥ 10`, `MonthlyBaseFee < 0` or `≥ 1000`, or `ContractStartDate` is missing,
**Then** HTTP 400 Problem Details is returned; no record is created.
**And** the upper bounds match the project-wide numeric-bound convention established in `OnboardingValidator` and `PatchFlatValidator` during the Epic 3 retrospective (2026-07-02).

**Given** `PATCH /api/v1/flats/{flatId}/tariffs/{tariffId}` attempting to update `PricePerKwh` or `MonthlyBaseFee`,
**When** the Tariff entry's `ContractStartDate` is on or before today, AND the request's optional `LockOverride` boolean is not `true` (missing or `false`),
**Then** HTTP 422 Problem Details with `type: "tariff-locked"` is returned; the price fields are not modified.
**And** non-price fields (`ProviderName`, `ContractDurationMonths`) are updated successfully regardless of lock status, and — since `ContractStartDate` is now immutable via PATCH (it is the natural key) — may be combined with a price-field change in the **same** request; the mutual-exclusion rule from this story's original code review no longer applies.

**Given** the same locked-tariff `PATCH` request but with `LockOverride: true` in the request body,
**When** `PatchTariffFunction` processes it,
**Then** the price fields are updated normally, subject to `TariffValidator`'s existing bounds; `LockOverride` is a request-only flag — it is not persisted as a column and has no effect when the tariff is not locked.

**Given** the `Tariffs` EF Core entity and `TariffConfiguration`,
**When** reviewed,
**Then** all column mappings use Fluent API; `PricePerKwh` and `MonthlyBaseFee` are `decimal`; `ContractStartDate` is `datetimeoffset` and required; index `IX_Tariffs_FlatId_ContractStartDate` exists (unique); zero Data Annotation attributes on the entity class.

---

## Story 4.2: Tariff Management UI — List & Add Form in Settings

As a user,
I want to see all my tariff entries in Settings and add new ones including future-dated changes,
So that I can track my full contract history and pre-enter upcoming rate changes.

**Acceptance Criteria:**

**Given** the user navigates to Settings → Flat card → Tariff quick link,
**When** the Tariff settings screen renders,
**Then** `useTariffs` (TanStack Query key: `['tariffs', flatId]`) fetches the tariff list; all entries render in descending effective-date order showing effective date, price per kWh, monthly base fee, and provider name (if present); an "Add Tariff" button is visible.

**Given** a tariff entry in the list,
**When** rendered,
**Then** the effective date is formatted via `Intl.DateTimeFormat` for the active locale; all currency values are formatted via `Intl.NumberFormat` with the correct symbol for the active locale.

**Given** the "Add Tariff" button is tapped,
**When** `TariffForm.tsx` renders,
**Then** it shows: effective date (required, pre-filled today), price per kWh (required, `inputmode="numeric"`), monthly base fee (required, numeric), provider name (optional, text), contract start date (optional), contract duration dropdown (optional: 1 / 6 / 12 / 24 months); Save is inactive until all required fields are valid.

**Given** the form is submitted with valid data,
**When** `useCreateTariff` mutation calls `POST /api/v1/flats/{flatId}/tariffs`,
**Then** on success: the form closes; TanStack Query keys `['tariffs', flatId]` and `['dashboard', flatId]` are invalidated; the new entry appears in the list.

**Given** a future-dated tariff entry in the list,
**When** displayed,
**Then** it is clearly labelled as upcoming (e.g., "From {date}") and appears at the top of the list.

**Given** `parseLocaleNumber`'s known DE-locale multi-comma truncation defect (flagged in the Epic 3 retrospective, 2026-07-02, as relevant once Epic 4 adds locale-sensitive numeric fields),
**When** the `PricePerKwh` and `MonthlyBaseFee` inputs are implemented in `TariffForm.tsx`,
**Then** the underlying `parseLocaleNumber` defect is fixed (not deferred again) before these two fields ship, with a regression test covering the multi-comma DE-locale input case.

---

## Story 4.3: Tariff Lock Indicator & Planned Annual Spend Settings

> **Amended 2026-07-03** (`sprint-change-proposal-2026-07-03.md`): AC1's lock condition no longer requires `ContractDurationMonths` — see the amendment note on Story 4.1. Implementation lives in Story 4.4.

As a user,
I want price fields on active contracts to be visibly locked and uneditable, and I want to update my planned annual spend near the tariff configuration,
So that locked rates cannot be accidentally modified and my budget target is easy to find.

**Acceptance Criteria:**

**Given** a tariff entry whose `ContractStartDate` is on or before today,
**When** its edit form opens,
**Then** `PricePerKwh` and `MonthlyBaseFee` render as read-only and visually greyed out, each with an inline lock icon (`accent-tariff-locked` #d97706) and the label "Locked — contract active since {month year}" when `ContractDurationMonths` is absent, or "Locked — contract active until {month year}" (contract start + duration) when it is provided; non-price fields remain fully editable; the lock state is immediately visible on form open — no dialog or tap-to-reveal.

**Given** the locked price fields,
**When** the user taps the lock icon or an adjacent "Edit anyway" affordance,
**Then** a confirmation dialog explains that overriding will change the contract's locked rate and asks the user to confirm; on confirm, the price fields become editable and the subsequent `PATCH` includes `LockOverride: true` (see Story 4.1); on cancel, the fields remain locked and no request is sent.

**Given** a PATCH request with modified price fields on a locked tariff submitted directly to the API without `LockOverride: true`,
**When** the backend receives it,
**Then** HTTP 422 Problem Details with `type: "tariff-locked"` is returned — lock enforced server-side regardless of UI state; only an explicit `LockOverride: true` bypasses it.

**Given** the Tariff settings screen,
**When** rendered below the tariff list,
**Then** a "Planned Annual Spend" field shows the current value (`Flats.PlannedAnnualSpend` decimal); an edit control allows the user to update it; saving calls `PATCH /api/v1/flats/{flatId}` (existing `PatchFlatFunction`, which already accepts `plannedAnnualSpend`) and takes effect immediately on future budget pressure alert evaluations (FR-7, FR-37).

**Given** `PatchFlatValidator`,
**When** `PlannedAnnualSpend` is provided,
**Then** it must be `> 0` and `< 50000` (€/year); values outside this range return HTTP 400 Problem Details and the value is not saved — closing the gap where this field previously had no bound, per the numeric-bound convention established in the Epic 3 retrospective (2026-07-02).

**Given** the Annual kWh Baseline or tariff price per kWh values,
**When** shown alongside the Planned Annual Spend field,
**Then** helper text displays the auto-derived value: `({AnnualKwhBaseline} kWh × {PricePerKwh} €/kWh) + ({MonthlyBaseFee} × 12)` so the user can compare it against their manually set target.

**Given** all monetary values in the tariff forms,
**When** displayed,
**Then** they are formatted via `Intl.NumberFormat` for the active locale; stored values in the database remain locale-neutral fixed-decimal.

---

## Story 4.4: Tariff Contract-Date Consolidation

> **Added 2026-07-03** (`sprint-change-proposal-2026-07-03.md`), from the Epic 4 retrospective's Action Item #1. Live testing found that the `EffectiveDate`/`ContractStartDate` split doesn't match how users enter real contracts — a backdated real tariff showed "0 days covered" because `EffectiveDate` (not `ContractStartDate`) drove cost-period resolution and defaulted to "today" with no way to correct it after creation. This story consolidates the two fields into one, fixing the class of bug directly (`deferred-work.md` item W4, flagged before Epic 4 started and never resolved) and simplifying `PatchTariffFunction` as a side effect.

As a user,
I want my tariff's contract start date to be the one field that determines both its cost period and its price lock, without a separate hidden "effective date" I can silently get wrong,
so that entering a pre-existing real contract — including one I'm backdating — produces correct cost coverage from day one, with no unrecoverable mistake possible.

**Acceptance Criteria:**

**Given** the `Tariffs` table and its existing rows,
**When** the migration for this story runs,
**Then** `ContractStartDate` becomes non-nullable; for any existing row where `ContractStartDate` was null, it is backfilled from that row's `EffectiveDate` before the column is dropped; the unique index moves from `IX_Tariffs_FlatId_EffectiveDate` to `IX_Tariffs_FlatId_ContractStartDate`; `EffectiveDate` is removed from the entity and schema entirely.

**Given** `TariffResolver.ResolveAsync(flatId, date, ct)` and `KpiCalculator.ResolveTariff` (the second, independently-implemented resolver flagged for logic duplication in the Epic 3 retrospective),
**When** either resolves the tariff active on a given date,
**Then** both select the Tariff with the latest `ContractStartDate` at or before that date; their public signatures are unchanged, so no caller in Epics 6/7 requires modification.

**Given** `TariffLockPolicy.IsLocked`,
**When** evaluated,
**Then** it returns `ContractStartDate <= DateTimeOffset.UtcNow` — `ContractDurationMonths` is no longer a factor.

**Given** `CreateTariffFunction` and `TariffValidator`,
**When** a request is submitted,
**Then** `ContractStartDate` is required (`NotNull`); the duplicate-date 409 check and unique-index backstop operate on `ContractStartDate`; price/fee bounds are unchanged.

**Given** `PatchTariffFunction` and `PatchTariffRequest`,
**When** implemented,
**Then** `ContractStartDate` is removed from the patchable fields entirely (immutable, same treatment `EffectiveDate` previously had); the mutual-exclusion 400 rule added in Story 4.1's review is removed, since the field it protected can no longer be mutated; a single request may combine price fields with `ProviderName`/`ContractDurationMonths` freely, gated only by the existing lock/`LockOverride` rule on the price fields.

**Given** `CompleteOnboardingFunction` (Onboarding, unaffected in user-facing behavior per FR-6),
**When** the user leaves contract start date blank during Onboarding,
**Then** it continues to default the (renamed) field to `DateTimeOffset.UtcNow`, identical to its current default-to-insertion-time behavior — only the target field name changes.

**Given** `TariffForm.tsx` in edit mode,
**When** rendered,
**Then** `contractStartDate` renders as a read-only label (reusing the pattern already built for the retired `effectiveDate` field); the two-sequential-PATCH-call branching introduced in Story 4.3 (the direct cause of that story's round-2 submit-guard-gap finding) is removed — edit-mode submission is a single `patchTariff` call.

**Given** `TariffForm.tsx` in create mode and `TariffList.tsx`,
**When** rendered,
**Then** the field labelled "Gültig ab"/"Effective date" is relabelled to reflect `ContractStartDate` as the single required date field (pre-filled today, editable); `TariffList`'s "upcoming" label and ordering logic key off `ContractStartDate` instead of the retired `EffectiveDate`.

**Given** the one real affected tariff record (Ralf's, `ContractStartDate` = 2024-10-01, previously non-authoritative),
**When** the migration and backfill complete,
**Then** the KPI Dashboard's tariff-coverage figures for that Flat show correct coverage for the historical reading period, verified manually post-deploy.

**Given** `deferred-work.md`,
**When** this story completes,
**Then** item **W4** and the Story 4.1 cross-field-validation-gap note are marked resolved.

**Given** the existing test suites (`GetTariffsFunctionTests`, `CreateTariffFunctionTests`, `PatchTariffFunctionTests`, `TariffResolverTests`, any `KpiCalculator` tests exercising tariff resolution, `TariffForm.test.tsx`, `TariffList.test.tsx`, `TariffLockIndicator.test.tsx`),
**When** updated for this story,
**Then** all references to `EffectiveDate` are replaced with `ContractStartDate`; tests for the removed PATCH mutual-exclusion rule are removed; a new test covers the migration backfill rule (`ContractStartDate = ContractStartDate ?? EffectiveDate` for pre-existing rows); full backend and frontend suites remain green.

---
