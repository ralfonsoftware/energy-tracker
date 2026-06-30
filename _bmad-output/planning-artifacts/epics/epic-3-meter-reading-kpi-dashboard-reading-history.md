# Epic 3: Meter Reading, KPI Dashboard & Reading History

The irreducible core of the product: a user can enter a meter reading in under 60 seconds and immediately see daily, weekly, and projected monthly cost in euros on the Euro Burn Dashboard. Spike detection and trend chart are included. Reading history with correction is accessible from the chart.

## Story 3.1: Meter Reading Submission — Backend

As a user,
I want to submit a meter reading from my phone and have it persisted immediately,
So that I have a timestamped record of my consumption that the app can use for cost calculations.

**Acceptance Criteria:**

**Given** a `POST /api/v1/flats/{flatId}/readings` request with a valid `kwhValue` (decimal > 0) and `readingDate` (datetimeoffset),
**When** `SubmitReadingFunction.RunAsync` executes,
**Then** `TenantResolver` verifies `flatId` belongs to the resolved `UserId` (HTTP 403 otherwise); a `MeterReading` record is created with `ReadingId` (guid), `FlatId`, `KwhValue` (decimal), `ReadingDate` (datetimeoffset), `IsCorrected = false`, `OriginalKwhValue = null`; HTTP 201 is returned with a `ReadingResponse` record and `Location` header.
**And** server-side processing time is ≤ 2 seconds (NFR-1 Tier 1).

**Given** a reading with a past `readingDate` (retroactive entry per FR-9),
**When** submitted,
**Then** it is stored with the provided past date; cost calculations use the Tariff active on that past date via `TariffResolver.ResolveAsync(flatId, readingDate, ct)`, not the current Tariff.

**Given** `ReadingValidator` (FluentValidation),
**When** `kwhValue ≤ 0` or `readingDate` is missing,
**Then** HTTP 400 Problem Details is returned; no record is created.

**Given** the `MeterReadings` EF Core entity and `MeterReadingConfiguration`,
**When** reviewed,
**Then** all column mappings use Fluent API; `KwhValue` and `OriginalKwhValue` are `decimal`; `ReadingDate` is `datetimeoffset`; zero Data Annotation attributes appear on the entity class.

---

## Story 3.2: KPI Dashboard — Backend Computation

As a user,
I want the server to compute my daily average, weekly average, and projected monthly cost from my meter readings,
So that the dashboard always reflects period-accurate figures in euros.

**Acceptance Criteria:**

**Given** a `GET /api/v1/flats/{flatId}/dashboard` request,
**When** `GetDashboardFunction.RunAsync` executes,
**Then** `KpiCalculator` returns a `DashboardSummary` record with: `DailyAvgKwh`, `WeeklyAvgKwh`, `DailyAvgCost`, `WeeklyAvgCost`, `ProjectedMonthlyCost` (all decimal), `LastReadingDate` (datetimeoffset nullable), `TodayKwh` (decimal), `DailyBudgetKwh` (decimal — `AnnualKwhBaseline ÷ 365`), `SpikeDays` (array of date strings); all cost figures computed via `TariffResolver` using the Tariff active on each period's date.
**And** response time ≤ 2 seconds for a Flat with up to 2 years of Readings.

**Given** a Flat with no Meter Readings yet,
**When** `GET /api/v1/flats/{flatId}/dashboard` is called,
**Then** HTTP 200 is returned with all KPI values as `0` and `LastReadingDate` as `null` — no error.

**Given** a Flat with readings spanning two different Tariff periods,
**When** the dashboard is computed,
**Then** each reading's cost uses the Tariff active on its `ReadingDate`, not the current Tariff.

**Given** all JSON responses from this endpoint,
**When** inspected,
**Then** all field names are camelCase; datetimes include explicit timezone offset; decimal values are JSON numbers.

---

## Story 3.3: KPI Dashboard Frontend — Euro Burn Design & Grid

As a user,
I want to see my KPI figures on the Euro Burn Dashboard with the gradient background encoding my consumption against my daily budget,
So that my energy cost is visible at a glance and the ambient background tells me how I am doing before I read a single number.

**Acceptance Criteria:**

**Given** an authenticated user on the Dashboard with readings present,
**When** the page loads,
**Then** `useDashboard` (TanStack Query key: `['dashboard', flatId]`) fetches the dashboard; four KPI tiles render in a 2×2 grid on phone / 4-across on tablet, each as a glass card (`backdrop-filter: blur(20px) saturate(180%)`, `background: rgba(255,255,255,0.08)`, `border: 1px solid rgba(255,255,255,0.14)`, `border-radius: 18px`, `padding: 16px 18px`).

**Given** the four KPI tiles,
**When** rendered,
**Then** Tile 1 (Daily avg): headline `{DailyAvgKwh} kWh` at display-kpi (22px/700/−0.02em), subline `€{DailyAvgCost}` at body-sm; budget delta line reads `↓ X kWh under budget` in `accent-under-budget` / `↑ X kWh over budget` in `accent-over-budget` / `— at daily budget` at label-caps size; tertiary caption reads "based on {AnnualKwhBaseline} kWh/yr". Tiles 2–4 follow the same paired kWh+€ structure for weekly and projected monthly figures.

**Given** `TodayKwh` and `DailyBudgetKwh` from the dashboard response,
**When** the Euro Burn Gradient Background renders,
**Then** gradient stop positions shift based on the consumption-to-budget ratio: ≤ −50% clips to the cool edge; ≥ +50% clips to the warm edge; angle is 160deg on phone, 140deg on tablet; no midpoint marker is added.

**Given** the Dashboard with no readings (cold open),
**When** rendered,
**Then** all KPI tiles show `—` (dashes); gradient renders at the neutral midpoint; "Last read:" shows "never"; the Enter Reading CTA is prominently visible.

**Given** `isLoading === true` on first fetch,
**When** the dashboard renders,
**Then** skeleton placeholders appear in the KPI tile positions — no content flash.

---

## Story 3.4: Enter Reading CTA, Bottom Sheet & Immediate Dashboard Update

As a user,
I want to tap Enter Reading, type the meter value, tap Save, and immediately see the KPI tiles update,
So that the core meter-reading loop is fast, frictionless, and confirmatory.

**Acceptance Criteria:**

**Given** the Dashboard on phone,
**When** rendered,
**Then** the Enter Reading CTA is a full-width pill (`border-radius: 9999px`, `padding: 16px 24px`, `background: rgba(255,255,255,0.10)`, `backdrop-filter: blur(20px) saturate(180%)`, `border: 1.5px solid rgba(255,255,255,0.40)`) with label "Enter Reading" in `body` type role.

**Given** the Dashboard on tablet (≥768px),
**When** rendered,
**Then** the CTA is a 44×44px compact button (`border-radius: 14px`) in the content header top-right showing the Lucide `Zap` icon (20×20px) only — no text label.

**Given** the Enter Reading CTA is tapped,
**When** the bottom sheet opens,
**Then** it slides up from the bottom with a drag handle; the kWh input is auto-focused with `inputmode="numeric"`; the date/time field is pre-filled with the current timestamp; Save is inactive until `kwhValue > 0`; the hint "Date and time will be saved with your reading." appears below the date field.

**Given** a valid kWh value is entered and Save is tapped,
**When** `useSubmitReading` mutation calls `POST /api/v1/flats/{flatId}/readings` and succeeds,
**Then** (1) the sheet closes, (2) TanStack Query key `['dashboard', flatId]` is invalidated triggering an immediate refetch, (3) all four KPI tiles animate with a count animation to their new values; "Last read:" updates to the submitted reading's date/time. With `prefers-reduced-motion: reduce` active, values update immediately with no animation.

**Given** the entered kWh value is lower than the last recorded reading,
**When** the value is typed,
**Then** the inline warning "Lower than your last reading ({lastKwhValue} kWh) — is this correct?" appears below the input; Save remains active.

**Given** the POST returns an error,
**When** the mutation fails,
**Then** the sheet stays open; the typed value is preserved; "Couldn't save — try again." appears near the Save button.

**Given** the bottom sheet while open,
**When** focus is managed,
**Then** focus is trapped within the sheet; closing returns focus to the CTA; all interactive elements meet 44×44pt minimum tap targets; validation messages use `aria-live="polite"`.

---

## Story 3.5: Trend Chart & Spike Detection

As a user,
I want to see a bar chart of my daily consumption history with spike days highlighted in amber,
So that I can immediately spot unusual consumption and know to check the Insights tab for context.

**Acceptance Criteria:**

**Given** `KpiCalculator` computing the dashboard,
**When** spike detection runs,
**Then** a day is flagged as a spike when its consumption exceeds `Flat.SpikeThreshold × 7-day rolling average` (default `2.0`); flagged dates are returned in `SpikeDays`; `SpikeThreshold` is stored as `decimal` on the `Flats` entity and is user-configurable per Flat.

**Given** `TrendChart.tsx`,
**When** rendered with dashboard data,
**Then** a recharts bar chart displays daily consumption bars; spike-day bars render with `fill: #f59e0b` (accent-spike amber); non-spike bars use the standard subdued color; the chart card has standard glass card treatment (`border-radius: 18px`, `backdrop-filter`).

**Given** a spike bar is tapped,
**When** the interaction occurs,
**Then** no banner or notification is generated on the Dashboard — full spike context is accessible only from the Insights tab.

**Given** the trend chart card header,
**When** rendered,
**Then** a 20×20px clock/list icon in `text-secondary` is positioned top-right with a minimum 44×44pt tap target and explicit `aria-label`; tapping it opens the Reading History bottom sheet.

---

## Story 3.6: Reading History — View & Correction

As a user,
I want to view all my past meter readings and correct any that were entered incorrectly,
So that my consumption history is accurate and the original value is preserved for reference.

**Acceptance Criteria:**

**Given** `GET /api/v1/flats/{flatId}/readings`,
**When** called,
**Then** `GetReadingHistoryFunction` returns all Readings in reverse-chronological order as `ReadingResponse` records (`ReadingId`, `KwhValue` decimal, `ReadingDate` datetimeoffset, `IsCorrected` bool, `OriginalKwhValue` nullable decimal); HTTP 200; ≤ 2s response time.

**Given** tapping the clock/list icon in the trend chart card header,
**When** the Reading History bottom sheet opens,
**Then** `useReadingHistory` (key: `['readings', flatId]`) fetches the list; entries render in reverse-chronological order with date/time and kWh value; entries with `IsCorrected = true` show a "corrected" note inline.

**Given** a reading entry is tapped,
**When** the edit form opens,
**Then** kWh value and date/time fields are pre-populated with current values; the same numeric input pattern applies (`inputmode="numeric"`, 44pt targets, focus trap).

**Given** the edit form is saved with a new kWh value,
**When** `PATCH /api/v1/flats/{flatId}/readings/{readingId}` is called,
**Then** `KwhValue` is updated; `IsCorrected = true`; `OriginalKwhValue` is set to the previous value (not overwritten on subsequent corrections); HTTP 200; TanStack Query keys `['readings', flatId]` and `['dashboard', flatId]` are invalidated.

**Given** the Reading History sheet fails to load,
**When** the fetch errors,
**Then** the sheet shows "Couldn't load reading history." with a Retry link; the sheet stays open.

---
