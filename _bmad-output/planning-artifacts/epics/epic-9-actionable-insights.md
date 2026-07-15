# Epic 9: Actionable Insights

The Insights tab automatically surfaces four categories of findings — standby offenders, replacement candidates, budget pressure alerts, and invoice deviation hints — via a daily scheduled job and a manual trigger. Prior insights remain visible while a new run is in progress.

## Story 9.1: Insights Infrastructure — Data Model, Run Tracking, Schedule & API

As a user,
I want the app to automatically discover insights every night and let me trigger a refresh manually, with prior insights staying visible while a new run completes,
So that I always see the most recent findings and never land on an empty screen while a run is in progress.

**Acceptance Criteria:**

**Given** EF Core migrations for `InsightRuns` and `Insights`,
**When** reviewed,
**Then** `InsightRunConfiguration` defines `RunId` (guid PK), `FlatId` (FK, cascade delete), `Status` (enum: Pending/Processing/Complete/Failed), `StartedAt` (datetimeoffset), `CompletedAt` (nullable datetimeoffset). `InsightConfiguration` defines `InsightId` (guid PK), `FlatId` (FK, cascade delete), `RunId` (FK, set-null on run delete), `Type` (enum: Standby/Replacement/Budget/InvoiceDeviation), `DeviceId` (nullable guid FK), `Data` (nvarchar(max) JSON column), `CreatedAt` (datetimeoffset); index on `(FlatId, Type, CreatedAt desc)`. Zero Data Annotation attributes on any entity class.

**Given** `ScheduledInsightsFunction.cs` with `[TimerTrigger("0 0 2 * * *")]`,
**When** it fires at 02:00 UTC,
**Then** it queries all `FlatId` values for active users; for each flat it enqueues a discovery message containing `{ flatId, runId }` onto the insights Azure Storage queue using Managed Identity; no HTTP response — fire-and-forget.

**Given** `POST /api/v1/flats/{flatId}/insights/trigger`,
**When** `TriggerInsightsFunction.RunAsync` executes,
**Then** a new `InsightRun` with `Status = Pending` is created and saved; a discovery message `{ flatId, runId }` is enqueued; HTTP 202 is returned with `{ runId }`; tenant check enforces flatId belongs to authenticated userId (HTTP 403 on mismatch); if a run with `Status = Pending` or `Processing` already exists for this flatId, the existing `runId` is returned with HTTP 202 (no duplicate runs).

**Given** `ProcessInsightsFunction.cs` with `[QueueTrigger("insights-discovery")]`,
**When** a discovery message is dequeued,
**Then** `InsightRun.Status` is set to `Processing`; all four detectors are called in sequence: `StandbyDetector`, `ReplacementDetector`, `BudgetAlertDetector`, `InvoiceDeviationDetector`; each detector's findings are written as `Insight` rows; on successful completion, `InsightRun.Status = Complete` and `CompletedAt` is set; on unhandled exception, `InsightRun.Status = Failed`; detector errors are logged to Application Insights but do not suppress other detectors — each runs independently.

**Given** `GET /api/v1/flats/{flatId}/insights`,
**When** `GetInsightsFunction.RunAsync` executes,
**Then** HTTP 200 returns `{ runStatus: { status, startedAt, completedAt? }, insights: [...] }` where `insights` is all `Insight` rows for the flat sorted by `CreatedAt desc`; the most recent run's status is included regardless of whether it is still running; tenant check applied; TanStack Query cache key: `['insights', flatId]`.

**Given** `InsightModels.cs`,
**When** reviewed,
**Then** `InsightsResponse` (C# record) has `RunStatus` (`RunStatusDto`: status, startedAt, completedAt) and `Insights` (list of `InsightDto`). `InsightDto` has: `insightId` (guid), `type` (string enum), `deviceId` (nullable guid), `data` (raw JSON passthrough — serialized as-is to client), `createdAt` (datetimeoffset). No Data Annotation attributes.

---

## Story 9.2: Standby Offender & Replacement Candidate Detectors

As a user,
I want to be told when a specific device is drawing power outside its normal hours of use and when a high-consumption device could be replaced at a known payback, with exact device names and quantified euro figures,
So that I can take targeted action rather than guessing where to investigate.

**Acceptance Criteria:**

**Given** `StandbyDetector.cs` invoked during a discovery run for a flat,
**When** the flat has Devices linked to Eve Home plugs with interval data in `SmartPlugIntervalData`,
**Then** for each such device: the detector queries the last 30 days of `SmartPlugIntervalData` rows for the device's `plugId`; rows outside the flat's configured usage window (default 22:00–08:00 local time) are filtered as "out-of-use hours"; a device is flagged as a standby offender when its mean out-of-use watt draw exceeds 2 W across at least 7 days of interval data; for each offender, one `Insight` row is written with `Type = Standby`, the `DeviceId` set, and `Data` JSON: `{ "deviceName": string, "meanStandbyWatts": decimal, "estimatedMonthlyKwh": decimal, "estimatedMonthlyCost": decimal }`; cost uses the current active tariff via `TariffResolver`.

**Given** `StandbyDetector.cs` processing a flat that has Devices linked only to Meross plugs,
**When** detecting standby offenders,
**Then** Meross-linked devices are excluded entirely — no Insight is created and no error is surfaced; this is an explicit format limitation, not a failure condition (FR-35).

**Given** `StandbyDetector.cs` processing a flat with fewer than 7 days of interval data,
**When** invoked,
**Then** no standby `Insight` rows are written; the detector exits cleanly; `InsightRun` proceeds to the next detector.

**Given** `ReplacementDetector.cs` invoked during a discovery run,
**When** the flat has Devices with measurable annual consumption (via SmartPlugDailyData or EU label or SelfMeasured approach),
**Then** for each device with computable annual cost: devices in the top 20% of consumption whose EU label class is C or below are flagged as replacement candidates; one `Insight` row is written per candidate with `Type = Replacement` and `Data` JSON: `{ "deviceName": string, "estimatedAnnualKwh": decimal, "estimatedAnnualCost": decimal, "suggestedClass": string, "estimatedSavingsEur": decimal }`; savings are estimated from the delta between current consumption and a next-class-up EU label target; all decimal fields are `decimal` — no float.

**Given** `StandbyDetectorTests.cs` and `ReplacementDetectorTests.cs` in `api.Tests/Features/Insights/`,
**When** run,
**Then** standby tests cover: Eve Home device above 2W threshold over 7+ days → Insight written; device below threshold → no Insight; Meross device → excluded with no error; <7 days data → no Insight. Replacement tests cover: high-consumption C-rated device → Insight with savings; A-class device → no Insight; no devices with approach → no Insights.

---

## Story 9.3: Budget Pressure & Invoice Deviation Detectors

As a user,
I want to be warned when my projected annual spend is tracking over budget and when my rolling annual consumption is diverging significantly from my baseline, with exact euro and kWh figures,
So that I can act before the invoice arrives rather than after.

**Acceptance Criteria:**

**Given** `BudgetAlertDetector.cs` invoked during a discovery run,
**When** the flat has `PlannedAnnualSpend` configured and at least 30 days of `MeterReadings`,
**Then** the detector computes `dailyAverageCost` over the 30-day rolling window using period-accurate TariffResolver; annualised projection = `dailyAverageCost × 365`; when `projectedAnnualCost > PlannedAnnualSpend`: one `Insight` row is written with `Type = Budget` and `Data` JSON: `{ "projectedAnnualCost": decimal, "plannedAnnualSpend": decimal, "overspendEur": decimal }`; all fields are `decimal` — no float.

**Given** `BudgetAlertDetector.cs` when `projectedAnnualCost ≤ PlannedAnnualSpend`,
**When** the run completes,
**Then** no Budget `Insight` row is written; no error generated.

**Given** `BudgetAlertDetector.cs` when `PlannedAnnualSpend` is null,
**When** invoked,
**Then** the detector skips execution for the flat and exits cleanly.

**Given** `InvoiceDeviationDetector.cs` invoked during a discovery run,
**When** the flat has `AnnualKwhBaseline` configured and at least 60 days of `MeterReadings`,
**Then** the detector computes `dailyAverageKwh` over 60 days; projected annual kWh = `dailyAverageKwh × 365`; deviation = `|projected − baseline| / baseline`; when `deviation ≥ 0.10` (≥10%): one `Insight` row is written with `Type = InvoiceDeviation` and `Data` JSON: `{ "projectedAnnualKwh": decimal, "baselineKwh": decimal, "deviationPct": decimal, "impliedDeltaEur": decimal, "direction": "above" | "below" }`; `impliedDeltaEur = (projectedAnnualKwh − baselineKwh) × currentTariffKwhRate`; all decimal fields are `decimal` — no float.

**Given** `InvoiceDeviationDetector.cs` when deviation is below the ±10% threshold,
**When** the run completes,
**Then** no InvoiceDeviation `Insight` row is written; no error generated.

**Given** `InvoiceDeviationDetector.cs` when `AnnualKwhBaseline` is null or fewer than 60 days of readings exist,
**When** invoked,
**Then** the detector skips execution for the flat and exits cleanly.

**Given** `BudgetAlertDetectorTests.cs` and `InvoiceDeviationDetectorTests.cs` in `api.Tests/Features/Insights/`,
**When** run,
**Then** budget tests cover: projected > planned → Insight with correct amounts; projected ≤ planned → no Insight; null PlannedAnnualSpend → skip. Invoice deviation tests cover: +15% deviation → Insight with direction=above; −12% → Insight with direction=below; +8% → no Insight; null baseline → skip; <60 days data → skip.

---

## Story 9.4: Insights Tab — Trend Chart, Insight Cards & Discovery Progress

As a user,
I want to open the Insights tab and see my 30-day trend chart alongside auto-discovered insight cards, with a visible progress indicator during a discovery run and prior cards staying visible underneath,
And when I tap "Refresh insights" it immediately shows the run in progress rather than waiting.

**Acceptance Criteria:**

**Given** `InsightsTab.tsx` mounts,
**When** the Insights tab is activated,
**Then** the 30-day `TrendChart` component (from Epic 3) renders at the top at full width; spike bars are amber-coloured (already implemented in Epic 3); below the chart, `useInsights` queries `['insights', flatId]` via `GET /api/v1/flats/{flatId}/insights`; a "Refresh insights" button is present.

**Given** `useInsights(flatId)` returns `runStatus.status = Processing` or `Pending`,
**When** the Insights section renders,
**Then** `InsightDiscoveryProgress.tsx` renders above the insight cards with an animated progress indicator and label "Discovering insights…" (i18n key); any prior insight cards from the previous run remain visible and interactive below the progress component; `useInsights` polls every 5 seconds until `status = Complete` or `Failed`, then issues a final refetch (FR-39).

**Given** `runStatus.status = Complete` and insights are available,
**When** the Insights section renders,
**Then** `InsightDiscoveryProgress.tsx` is hidden; `InsightCard.tsx` renders one card per insight in a scrollable vertical list (2-column grid on tablet); cards are ordered by `createdAt desc`.

**Given** an `InsightCard` with `type = Standby`,
**When** rendered,
**Then** the card shows: standby icon; `data.deviceName`; "Drawing {meanStandbyWatts} W outside usage hours" (i18n key); "Estimated monthly cost: {estimatedMonthlyCost}" formatted via `Intl.NumberFormat` with the active locale's currency.

**Given** an `InsightCard` with `type = Replacement`,
**When** rendered,
**Then** the card shows: replacement icon; device name; "Annual cost: {estimatedAnnualCost}" and "Potential savings: {estimatedSavingsEur}/year" (i18n keys); suggested EU class label shown.

**Given** an `InsightCard` with `type = Budget`,
**When** rendered,
**Then** the card shows: `accent-error` left border; budget warning icon; "Projected annual spend: {projectedAnnualCost}" and "Planned: {plannedAnnualSpend}" and "Over by: {overspendEur}" (i18n keys); all amounts formatted via `Intl.NumberFormat`.

**Given** an `InsightCard` with `type = InvoiceDeviation`,
**When** rendered,
**Then** the card shows: invoice icon; "Projected annual usage: {projectedAnnualKwh} kWh" and "Your baseline: {baselineKwh} kWh" and "Implied difference: {impliedDeltaEur}" with direction indicator (above/below); all figures locale-formatted.

**Given** `useInsights` returns an empty `insights` array and `runStatus.status = Complete`,
**When** the Insights section renders,
**Then** empty state: "No findings this run. Everything looks within normal range." (i18n key); trend chart remains visible above.

**Given** no completed run exists and fewer than 30 days of readings (insufficient data),
**When** the Insights section renders,
**Then** empty state: "Not enough data for insights. Add readings and import smart plug data to generate insight cards." (i18n key); trend chart still renders if readings exist.

**Given** `useTriggerInsights` fires when "Refresh insights" is tapped,
**When** `POST /api/v1/flats/{flatId}/insights/trigger` returns 202,
**Then** TanStack Query key `['insights', flatId]` is immediately refetched; UI transitions to the "Processing" state; the "Refresh insights" button is disabled while `runStatus.status` is Pending or Processing.
