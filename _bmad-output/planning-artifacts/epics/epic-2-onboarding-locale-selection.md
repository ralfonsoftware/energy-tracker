# Epic 2: Onboarding & Locale Selection

A first-time user can complete the guided onboarding flow to configure their flat name, annual kWh baseline (preset or custom), and initial energy tariff — and select their preferred locale (de-DE / en-US). The onboarding gate ensures no main feature is accessible until setup is complete.

## Story 2.1: i18n Infrastructure & Locale Settings API

As a user,
I want the app to detect my preferred locale from my browser and display all text, numbers, dates, and currency in my locale's format,
So that the app feels native to my region from the very first screen.

**Acceptance Criteria:**

**Given** the `client/` project,
**When** the i18n infrastructure is set up,
**Then** react-i18next is initialized in `lib/i18n.ts` with `i18next-browser-languagedetector`, namespace-split translation files exist for both `de-DE` and `en-US` under `locales/{locale}/` (at minimum: `common.json`, `onboarding.json`, `settings.json`), and all UI strings are rendered via `useTranslation` hooks — zero hardcoded locale-specific strings in any component.

**Given** a new browser session with `Accept-Language: de-DE`,
**When** the app loads with no server-stored locale override,
**Then** all UI text renders in German, numbers use comma-decimal (e.g. `1,27 €`), dates use `dd.mm.yyyy`, and times use `HH:mm`.

**Given** a new browser session with `Accept-Language: en-US`,
**When** the app loads with no server-stored locale override,
**Then** all UI text renders in English, numbers use period-decimal (e.g. `$1.27`), dates use `mm/dd/yyyy`, and times use `h:mm AM/PM`.

**Given** the `GET /api/v1/user/settings` and `PUT /api/v1/user/settings` endpoints,
**When** a locale override is stored via PUT,
**Then** a subsequent GET returns the stored locale; `LocaleResolver` in `api/Shared/LocaleResolver.cs` applies the stored override over the `Accept-Language` header; the `Users.LocaleOverride` column stores the value; all currency, number, and date values in the database remain locale-neutral (ISO 8601 with offset, decimal-point numbers, fixed-decimal currency).

**Given** `GET /api/v1/user/settings`,
**When** called by an authenticated user,
**Then** the response includes `hasFlat: bool` — `true` when at least one `Flat` record exists for the resolved `UserId`, `false` otherwise; this field is derived at query time (no stored flag); it requires no additional DB writes.

**Given** a currency amount stored during a `de-DE` session,
**When** the locale is subsequently changed to `en-US`,
**Then** the stored value renders correctly with `$` symbol and period decimal — no re-storage required.

---

## Story 2.2: Onboarding Gate & Intro Screen

As a first-time user,
I want the app to intercept my first visit and show an intro screen before I can reach any main feature,
So that I understand the app's purpose and can start the setup flow.

**Acceptance Criteria:**

**Given** `useUserSettings` (TanStack Query key: `['settings']`) fetches `GET /api/v1/user/settings` on app load,
**When** `hasFlat === false`,
**Then** `OnboardingGate.tsx` intercepts navigation to any main route (`/`, `/insights`, `/decomposition`, `/settings`) and redirects to `/onboarding`; the main tab bar / sidebar is not visible during onboarding.

**Given** an authenticated user with no existing Flat (new user),
**When** any main app route is accessed (`/`, `/insights`, `/decomposition`, `/settings`),
**Then** `OnboardingGate.tsx` intercepts the navigation and redirects to `/onboarding`; the main tab bar / sidebar is not visible during onboarding.

**Given** the `/onboarding` route,
**When** the Intro screen renders,
**Then** it shows: the app name, the value proposition copy "Know what your energy costs, every day.", a locale dropdown in the top-right (`DE ▾` / `EN ▾`), and a "Get Started" CTA button; no other navigation elements are shown.

**Given** the locale dropdown on the Intro screen,
**When** a locale is selected,
**Then** all text on the current screen immediately re-renders in the selected language and `PUT /api/v1/user/settings` stores the override server-side.

**Given** a locale change is applied during onboarding,
**When** the new locale renders,
**Then** all visible UI strings update in the same render cycle — no full-page reload, no flash of untranslated content, no scroll position reset.

**Given** the user has entered text in any form field when locale is switched,
**When** locale is applied,
**Then** all previously entered field values are preserved exactly; only labels, placeholders, and error messages re-render in the new locale.

**Given** the new locale introduces longer strings (e.g. German labels),
**When** the layout reflows,
**Then** no CTA button is pushed off-screen and no input overlaps its label.

**Given** a user who has already completed onboarding (`hasFlat === true`),
**When** they navigate to `/onboarding`,
**Then** they are redirected to the Dashboard (`/`) — the gate does not re-trigger.

**Given** a step indicator component,
**When** the onboarding flow is active,
**Then** the current step position (Intro / Step 1 / Step 2) is visible; the step indicator is hidden outside the onboarding flow.

---

## Story 2.3: Onboarding Step 1 — Flat Name

As a first-time user,
I want to name my flat in Step 1 of onboarding,
So that my energy data is associated with a meaningful label I recognize.

**Acceptance Criteria:**

**Given** the user taps "Get Started" on the Intro screen,
**When** Step 1 renders,
**Then** a text input labelled for flat name entry is auto-focused; the "Continue" button is inactive until a non-empty name is entered; `input.value.trim()` is used for the empty check — whitespace-only values do not enable "Continue".

**Given** the user has typed only whitespace characters into the name field,
**When** the component evaluates the field value,
**Then** "Continue" remains disabled; no validation error is shown until the user blurs the field.

**Given** a flat name is entered and "Continue" is tapped,
**When** the step advances,
**Then** the entered name is held in client state and Step 2 renders; no backend call is made yet (all data is submitted together at Step 2 completion).

**Given** Step 1 is active and the user navigates back,
**When** returning to the Intro screen,
**Then** no data is lost and the user can re-enter Step 1 with the previously typed value still present.

**Given** the flat name input on a mobile device and the soft keyboard opens,
**When** the keyboard is fully raised,
**Then** the "Continue" button is still fully visible within the visible viewport without requiring a scroll.

**Story Note (2.3):** Implement keyboard-aware CTA using `position: sticky; bottom: 0` on the CTA container inside a scrollable parent, or listen to `visualViewport.resize` and adjust padding.

**Given** the flat name input,
**When** rendered,
**Then** it uses `border-radius: 12px` (input token), standard text keyboard, and `body-sm` label styling.

---

## Story 2.4: Onboarding Step 2 — Energy Contract & Completion

As a first-time user,
I want to configure my annual energy baseline and initial tariff in Step 2 and submit the complete setup,
So that the app can calculate my costs and budget from the moment I enter my first meter reading.

**Acceptance Criteria:**

**Given** Step 2 renders,
**When** the Annual kWh Baseline section is shown,
**Then** four household-size preset buttons appear (1 person ≈ 1,500 kWh; 2 persons ≈ 2,500 kWh; 3 persons ≈ 3,500 kWh; 4 persons ≈ 4,250 kWh) and a custom numeric input.

**Given** the user taps a preset tile,
**When** tapped,
**Then** the tile enters a selected visual state (highlighted border + checkmark) AND the kWh input is prefilled with the preset value AND focus moves to the kWh input field.

**Given** a preset tile is selected and the user modifies the kWh input (any keystroke that changes the value),
**When** the value changes,
**Then** the tile deselects (returns to default visual state) and the input retains the user-typed value.

**Given** the user manually types a value into the kWh field that exactly matches a preset value,
**When** typed,
**Then** the corresponding tile does NOT auto-select — manual entry is not equivalent to tile selection.

**Given** the Tariff section in Step 2,
**When** rendered,
**Then** fixed monthly base fee and price per kWh are required fields; provider name, contract start date, and contract duration (1 / 6 / 12 / 24 months) are optional.

**Given** Annual kWh Baseline and price per kWh are both entered,
**When** either value changes,
**Then** the planned annual spend field auto-calculates as `(annual_kwh × price_per_kwh) + (monthly_base_fee × 12)`, shows the derivation formula below the field, and remains manually editable by the user.

**Given** the user enters a value in the planned spend field,
**When** entered,
**Then** the field displays an "override active" indicator (e.g. small tag "Custom budget") signalling it is decoupled from the auto-calculation.

**Given** a spend override is active AND the user changes the kWh or tariff values,
**When** the other fields change,
**Then** the spend field is NOT recalculated — the override persists; the user must clear the spend field manually to return to auto-calculation.

**Given** the user clears the planned spend field,
**When** the field loses focus,
**Then** the field returns to showing a computed placeholder (e.g. "~€420 / yr based on current tariff") and the "override active" indicator is removed.

**Given** the user is on Step 2 and taps "Back" to Step 1, then "Continue" again to return,
**When** Step 2 re-renders,
**Then** all previously entered kWh, preset tile selection, tariff fields, and planned spend values are restored exactly as left.

**Story Note (2.4):** Preserve all onboarding wizard state in component state or a lightweight store slice — do not rely on browser history state alone.

**Given** all required fields are valid and "Complete Setup" is tapped,
**When** `POST /api/v1/onboarding` is called,
**Then** the backend creates a `Flats` record (`AnnualKwhBaseline` as `decimal`, `SpikeThreshold` defaulting to `2.0`, `PlannedAnnualSpend` as nullable `decimal` — stored from the user's Step 2 input, null if not provided) and a `Tariffs` record (`EffectiveDate` = today as `datetimeoffset`, all monetary values as `decimal`, locale-neutral); HTTP 201 is returned; `['settings']` TanStack Query key is invalidated (causing `hasFlat` to return `true`); the onboarding gate clears; the user is redirected to `/`.
**And** `OnboardingValidator` (FluentValidation) enforces: flat name non-empty, `AnnualKwhBaseline > 0`, `PricePerKwh > 0`, `MonthlyBaseFee >= 0`; failures return HTTP 400 Problem Details; zero Data Annotation attributes on entity classes.

**Given** the `Flats` EF Core entity and `FlatConfiguration`,
**When** reviewed,
**Then** `FlatConfiguration` defines: `FlatId` (guid PK), `UserId` (FK to `Users`, cascade delete), `Name` (nvarchar), `AnnualKwhBaseline` (decimal), `SpikeThreshold` (decimal, default `2.0`), `PlannedAnnualSpend` (nullable decimal); all mappings via Fluent API only; zero Data Annotation attributes on the `Flat` entity class.

**Given** the `Tariffs` EF Core entity and `TariffConfiguration`,
**When** reviewed,
**Then** `TariffConfiguration` defines the complete schema: `TariffId` (guid PK), `FlatId` (FK to `Flats`, cascade delete), `EffectiveDate` (datetimeoffset), `PricePerKwh` (decimal), `MonthlyBaseFee` (decimal), `ProviderName` (nullable nvarchar), `ContractStartDate` (nullable datetimeoffset), `ContractDurationMonths` (nullable int); index `IX_Tariffs_FlatId_EffectiveDate`; all mappings via Fluent API only; zero Data Annotation attributes on the `Tariff` entity class. This migration creates the full Tariffs schema — Story 4.1 adds application logic only, no further schema changes to this table.

**Given** the tariff price/kWh or monthly base fee fields,
**When** rendered with locale `de-DE` (or any locale using comma as decimal separator),
**Then** the field accepts a comma as the decimal separator (e.g. "0,28") and correctly parses the value; a period is treated as a thousands separator.

**Given** locale is `en-US`,
**When** the user types "3,500" into a numeric field,
**Then** it is accepted as 3500 (thousands separator); a period is the decimal separator.

**Given** the user submits a value that cannot be parsed in the active locale (e.g. "3.5.0"),
**When** the field loses focus,
**Then** an inline validation error reads "Please enter a valid number" in the active locale language.

**Story Note (2.4):** Do not rely on browser default locale for number parsing — use the locale resolved by the i18n context from Story 2.1.

**Given** the "Complete Setup" API call is in-flight,
**When** pending,
**Then** the button displays a loading spinner, is disabled, and shows the label "Saving…".

**Given** the API call fails (network error or 5xx),
**When** the error response is received,
**Then** the inline error "Something went wrong. Your data wasn't saved — please try again." appears below the CTA; the button reverts to "Complete Setup" (enabled); all entered values are preserved — the user can retry without re-entering any data.

---

## Story 2.5: Settings — Flat Name, Annual kWh Baseline & Locale

As a returning user,
I want to update my flat name, annual kWh baseline, and locale from Settings,
So that I can refine my setup at any time without restarting onboarding.

**Acceptance Criteria:**

**Given** the Settings root screen,
**When** rendered,
**Then** a Flat card shows the current flat name with a "kWh Baseline" quick link; a "Language & Region" section is present; an Account section shows a Sign Out action.

**Given** the user taps the flat name on the Flat card,
**When** tapped,
**Then** the name text transforms into an inline editable input pre-filled with the current value; a "Save" action appears adjacent (or via keyboard "Done").

**Given** the user confirms the inline name edit,
**When** "Save" / "Done" is tapped,
**Then** the UI immediately shows the new name (optimistic update) AND `PATCH /api/v1/flats/{flatId}` is sent in the background with body `{ "name": string }`.

**Given** the PATCH request fails,
**When** the error response is received,
**Then** the name reverts to the previous value and the inline error "Couldn't save changes — please try again." appears; the input re-opens with the failed new value so the user does not need to retype.

**Given** the "kWh Baseline" quick link,
**When** tapped,
**Then** the user is navigated to a full edit screen reusing the consumption form from Story 2.4 (preset tiles + kWh input + planned spend) with current values pre-populated; a "Save changes" CTA and a back/cancel affordance are present.

**Given** the user saves baseline changes and `PATCH /api/v1/flats/{flatId}` succeeds,
**When** the response is received,
**Then** the user is returned to the Settings screen with updated values shown and the change takes effect immediately on future budget pressure evaluations.

**Given** the PATCH request for baseline changes fails,
**When** the error response is received,
**Then** the user remains on the edit screen with an error banner; no data is lost and all entered values remain in the form.

**Story Note (2.5 — PATCH endpoint contract):** `PATCH /api/v1/flats/{flatId}` accepts a partial body `{ "name"?: string, "annualKwhBaseline"?: number, "plannedAnnualSpend"?: number | null }`. Omitted fields are not updated. Explicit `null` for `plannedAnnualSpend` clears the override. Returns HTTP 200 with the updated Flat resource (not 204 — client needs the persisted values to confirm the optimistic update).

**Given** the "Language & Region" locale dropdown,
**When** changed to a different locale,
**Then** `PUT /api/v1/user/settings` stores the override server-side; all UI text, numbers, dates, times, and currency immediately re-render in the new locale without a page reload; accessing the app from any other browser subsequently restores the stored locale automatically.

**Given** the user taps "Sign Out" in the Account section,
**When** tapped,
**Then** a confirmation dialog appears with title "Sign out?", body "You'll need to sign in again to access your data.", and two actions: "Cancel" (dismisses, no action) and "Sign out" (destructive styling, proceeds).

**Given** the user confirms sign-out,
**When** confirmed,
**Then** the browser is redirected to `/.auth/logout` (SWA Easy Auth built-in endpoint); no backend code is required — the Sign Out action is a link or `window.location.href` assignment; the user lands on the app sign-in screen after the SWA session is cleared.

**Given** the `/.auth/logout` redirect fails,
**When** failure occurs,
**Then** local session state is cleared anyway and the user is still redirected to the sign-in screen — retaining a broken session is worse than a silent failure.

---
