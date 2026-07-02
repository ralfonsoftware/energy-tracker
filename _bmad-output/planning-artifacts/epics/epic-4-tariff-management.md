# Epic 4: Tariff Management

A user can maintain a full tariff history with effective dates, optional contract periods, and forward-dated tariff changes. All cost figures use the tariff active on the date of consumption, not the current tariff.

## Story 4.1: Tariff CRUD Backend — List, Create & Contract Lock Enforcement

As a user,
I want to add new tariff entries, list my tariff history, and have the app prevent me from editing price fields on active contracts,
So that my tariff history is accurate and locked rates cannot be accidentally changed.

**Acceptance Criteria:**

**Given** `GET /api/v1/flats/{flatId}/tariffs`,
**When** called,
**Then** `GetTariffsFunction` returns all Tariff entries in descending effective-date order as `TariffResponse` records (`TariffId`, `EffectiveDate` datetimeoffset, `PricePerKwh` decimal, `MonthlyBaseFee` decimal, `ProviderName` nullable string, `ContractStartDate` nullable datetimeoffset, `ContractDurationMonths` nullable int, `IsLocked` bool derived from: ContractStartDate is not null AND in the past AND ContractDurationMonths is not null); HTTP 200; ≤ 2s response time.

**Given** `POST /api/v1/flats/{flatId}/tariffs` with a valid request body,
**When** `CreateTariffFunction.RunAsync` executes,
**Then** a new `Tariff` record is created with all fields stored locale-neutrally (`EffectiveDate` as datetimeoffset, `PricePerKwh` and `MonthlyBaseFee` as `decimal`); HTTP 201 with `Location` header; ≤ 2s response time.
**And** a future `EffectiveDate` is accepted without affecting any past cost calculations (FR-12).

**Given** `POST /api/v1/flats/{flatId}/tariffs` with an `EffectiveDate` that already has a Tariff entry for this Flat,
**When** `CreateTariffFunction.RunAsync` executes,
**Then** HTTP 409 Problem Details (`type: "https://tools.ietf.org/html/rfc9110#section-15.5.10"`, `title: "Conflict"`) is returned; no record is created.
**And** this relies on the `IX_Tariffs_FlatId_EffectiveDate` unique index (added via migration `MakeTariffEffectiveDateUnique` ahead of this epic) — the function checks for an existing entry before insert so the collision never surfaces as an unhandled DB-constraint 500.

**Given** `TariffValidator` (FluentValidation),
**When** `PricePerKwh ≤ 0` or `≥ 10`, `MonthlyBaseFee < 0` or `≥ 1000`, or `EffectiveDate` is missing,
**Then** HTTP 400 Problem Details is returned; no record is created.
**And** the upper bounds match the project-wide numeric-bound convention established in `OnboardingValidator` and `PatchFlatValidator` during the Epic 3 retrospective (2026-07-02).

**Given** `PATCH /api/v1/flats/{flatId}/tariffs/{tariffId}` attempting to update `PricePerKwh` or `MonthlyBaseFee`,
**When** the Tariff entry has `ContractStartDate` in the past AND `ContractDurationMonths` is not null, AND the request's optional `LockOverride` boolean is not `true` (missing or `false`),
**Then** HTTP 422 Problem Details with `type: "tariff-locked"` is returned; the price fields are not modified.
**And** non-price fields (`ProviderName`, `ContractStartDate`, `ContractDurationMonths`) are updated successfully regardless of lock status.

**Given** the same locked-tariff `PATCH` request but with `LockOverride: true` in the request body,
**When** `PatchTariffFunction` processes it,
**Then** the price fields are updated normally, subject to `TariffValidator`'s existing bounds; `LockOverride` is a request-only flag — it is not persisted as a column and has no effect when the tariff is not locked.

**Given** the `Tariffs` EF Core entity and `TariffConfiguration`,
**When** reviewed,
**Then** all column mappings use Fluent API; `PricePerKwh` and `MonthlyBaseFee` are `decimal`; `EffectiveDate` and `ContractStartDate` are `datetimeoffset`; index `IX_Tariffs_FlatId_EffectiveDate` exists; zero Data Annotation attributes on the entity class.

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

As a user,
I want price fields on active contracts to be visibly locked and uneditable, and I want to update my planned annual spend near the tariff configuration,
So that locked rates cannot be accidentally modified and my budget target is easy to find.

**Acceptance Criteria:**

**Given** a tariff entry with an active contract period,
**When** its edit form opens,
**Then** `PricePerKwh` and `MonthlyBaseFee` render as read-only and visually greyed out, each with an inline lock icon (`accent-tariff-locked` #d97706) and the label "Locked — contract active until {month year}"; non-price fields remain fully editable; the lock state is immediately visible on form open — no dialog or tap-to-reveal.

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
