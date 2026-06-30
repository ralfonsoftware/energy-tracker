# Epic 7: Consumption Decomposition

A user can view their energy consumption broken down by room and device for any selectable period with smart plug data. The Residual (unattributed kWh) is always shown and never suppressed, including when zero. Periods without smart plug data show an explicit unavailable state with an import CTA.

## Story 7.1: Decomposition Backend — Engine, API & Cost Attribution

As a user,
I want the app to compute my consumption breakdown by room and device for any date range I choose, with attributed costs using the tariff that was active during each day,
So that I get accurate per-room and per-device kWh and cost figures that are consistent with my billing history.

**Acceptance Criteria:**

**Given** `DecompositionModels.cs` in `api/Features/Decomposition/`,
**When** reviewed,
**Then** `DecompositionResponse` (C# record) contains: `Period` (startDate, endDate); `TotalKwh` (decimal); `TotalCost` (decimal); `IsUnavailable` (bool); `HasInterpolatedData` (bool); `Residual` (`ResidualItem`: kWh, cost, always present); `Rooms` (list of `RoomDecomposition`: roomId, roomName, kWh, cost, devices); each `DeviceDecomposition` has: deviceId, name, kWh, cost, `approach` (Measured/EuLabel/SelfMeasured), `isSmartStrip` (bool), `subDevices?` (list with deviceId, name, kWh, cost, isConfigured, isUnconfigured). No Data Annotation attributes on any class.

**Given** `DecompositionEngine.cs` processes a period with SmartPlugDailyData present,
**When** `ComputeAsync(flatId, startDate, endDate, ct)` is called,
**Then** for each Device with a linked `plugId`: kWh = sum of `SmartPlugDailyData.KwhValue` for that plugId in the period, `approach = Measured`; for each Device with `ConsumptionApproach = EuLabel`: daily estimate = `EuAnnualKwh ÷ 365`, projected across period days, `approach = EuLabel`; for each Device with `ConsumptionApproach = SelfMeasured`: daily kWh from `SelfMeasuredKwh`/`SelfMeasuredPeriod`, `approach = SelfMeasured`; devices with no approach and no plug contribute 0; devices are grouped by room; `TotalKwh` = sum of all MainMeter daily readings for period.

**Given** a Smart Power Strip device,
**When** its sub-devices are attributed,
**Then** the Strip's measured total kWh (from SmartPlugDailyData for the strip's plugId) is split among sub-devices: configured sub-devices receive proportional shares based on their own smart plug measurements where available, otherwise equal-share of the unattributed strip remainder; unconfigured sub-devices receive an equal-share portion of the remaining unattributed kWh; `isConfigured` / `isUnconfigured` set accordingly; the strip's total exactly equals the sum of all sub-device shares within ±0.01 kWh rounding tolerance.

**Given** `TariffResolver` is called for cost attribution,
**When** computing costs,
**Then** each day's kWh is multiplied by the import tariff active on that calendar date (FR-13 period-accurate costing); summed into device/room/total cost fields; all cost fields are `decimal` — no float or double.

**Given** `Residual` computation,
**When** included in the response,
**Then** `Residual.Kwh = TotalKwh − sum(all device kWh)`; `Residual.Cost` uses the same period-accurate TariffResolver; the invariant `Residual.Kwh + attributed kWh = TotalKwh` holds within ±0.1 kWh; `Residual.Kwh` may be zero but is always included in the response (FR-33).

**Given** a period with no SmartPlugDailyData for any plug in the flat,
**When** `ComputeAsync` is called,
**Then** `DecompositionResponse.IsUnavailable = true`; `Rooms` is empty; `Residual` is still present with `Kwh = 0`; HTTP 200 is returned (not 404).

**Given** any `SmartPlugDailyData` row in the period has `IsInterpolated = true`,
**When** building the response,
**Then** `HasInterpolatedData = true` on the response object.

**Given** `GET /api/v1/flats/{flatId}/decomposition?startDate={date}&endDate={date}`,
**When** `GetDecompositionFunction.RunAsync` executes,
**Then** `startDate` and `endDate` are required query params; missing or invalid dates return HTTP 400 Problem Details; `endDate < startDate` returns HTTP 400; tenant check enforces flatId belongs to authenticated userId (HTTP 403 on mismatch); valid request returns HTTP 200 with `DecompositionResponse`.

**Given** `DecompositionEngineTests.cs` in `api.Tests/Features/Decomposition/`,
**When** run,
**Then** tests cover: measured device attribution; EU label daily estimate; SelfMeasured daily estimate; Smart Power Strip proportional split; unconfigured sub-devices get equal share; Residual = TotalKwh − attributed within tolerance; period with no SmartPlugData sets `IsUnavailable = true`; `HasInterpolatedData = true` when any row is interpolated; `Residual.Kwh` is 0 but present when all kWh is attributed.

---

## Story 7.2: Decomposition Tab — Period Selector, Residual Card & Unavailable State

As a user,
I want to select a date range from a dropdown and immediately see my Residual (unattributed kWh) as the first card in a glass surface — even when it is zero — so I always know how much of my consumption is unaccounted for,
And when no smart plug data exists for the period, I want a clear unavailable state with a direct link to import data.

**Acceptance Criteria:**

**Given** the Decomposition tab renders,
**When** it mounts,
**Then** `DecompositionTab.tsx` renders a period selector dropdown with options: "This week", "This month" (default), "Last month", "This year", "Custom range"; Custom range reveals a start/end date picker pair; the selected period's `startDate` and `endDate` drive the TanStack Query key `['decomposition', flatId, { startDate, endDate }]`; a new selection triggers an immediate refetch with loading skeleton.

**Given** `useDecomposition(flatId, startDate, endDate)` via TanStack Query,
**When** data loads successfully and `IsUnavailable = false`,
**Then** the Residual card renders first before all Room cards regardless of the Residual value; it is never suppressed even when `Residual.Kwh === 0`.

**Given** the Residual card renders,
**When** `Residual.Kwh > 0` or `Residual.Kwh === 0`,
**Then** in both cases: card uses the glass surface system with a `residual-tint` amber overlay; the card header shows "Residual" (i18n key); kWh value and attributed cost are shown; no collapse or hide control is present; card is visually first in the list.

**Given** `DecompositionResponse.HasInterpolatedData = true`,
**When** the tab renders,
**Then** a non-blocking info banner appears above the cards: "Some values have been interpolated from incomplete import data." (i18n key); it does not block interaction with any card.

**Given** `DecompositionResponse.IsUnavailable = true`,
**When** the tab renders,
**Then** instead of cards, an unavailable state renders with: an informational icon, heading "No smart plug data for this period" (i18n key), body copy "Upload a smart plug export to see your breakdown", and a primary CTA button "Import Data" that navigates to the Import surface; the Residual card is not shown in this state (FR-34).

**Given** the query is in a loading state,
**When** the tab renders,
**Then** skeleton placeholders match the height of the Residual card and two Room cards; no layout shift when real data arrives.

**Given** the query returns an error,
**When** the tab renders,
**Then** an error state with "Couldn't load decomposition" (i18n key) and a Retry button is shown.

---

## Story 7.3: Room Cards, Device Card Variants & Smart Power Strip Card

As a user,
I want to see my consumption grouped by room with device cards that visually distinguish between directly measured devices and estimated ones, and Smart Power Strip sub-devices that clearly show which are configured and which are not,
So that I understand both what I know precisely and where estimates are being used.

**Acceptance Criteria:**

**Given** `RoomCard.tsx` renders for a room in the decomposition response,
**When** displayed,
**Then** room name, total room kWh, and total room cost are shown in the card header; child DeviceCards are rendered in a vertical list within the room; rooms are ordered by descending kWh.

**Given** a device with `approach = Measured` (smart plug device),
**When** its `DeviceCard` renders,
**Then** it uses the **rich/measured variant**: large card layout; device name prominent; kWh and cost values; a "Measured" badge (`accent-success` colour token); no estimate disclaimer; ordered before estimated devices within the room.

**Given** a device with `approach = EuLabel` or `SelfMeasured`,
**When** its `DeviceCard` renders,
**Then** it uses the **compact/estimated variant**: smaller card; device name; kWh and cost values; an "Estimated" badge (`accent-warning` colour token); a one-line disclaimer beneath: "Based on EU label" or "Based on self-measured average" depending on approach (i18n keys).

**Given** a device with `isSmartStrip = true`,
**When** `SmartStripCard.tsx` renders,
**Then** the card header shows the strip name, total measured kWh, and cost; sub-device rows are listed beneath the header in a compact list.

**Given** a sub-device row where `isConfigured = true`,
**When** rendered,
**Then** the row shows at full opacity (1.0); device name; attributed kWh share and cost; no configure hint.

**Given** a sub-device row where `isUnconfigured = true`,
**When** rendered,
**Then** the row renders at `opacity: 0.45`; device name shows as "Unassigned outlet {n}" or the stored name; an inline chip or hint reading "Configure" navigates to the Flat Structure editor at the Smart Power Strip's PowerPoint; the row's kWh shows its equal-share portion.

**Given** device cards within a room,
**When** ordered,
**Then** Measured devices appear before Estimated devices; within each group, descending kWh order; the Smart Power Strip card is treated as Measured if it has a plugId.

**Given** a room with zero devices having any approach or plug configured,
**When** it appears in the response with a non-zero kWh,
**Then** it renders one compact card reading "Direct consumption" with the room's kWh (unattributed room total); no badge is shown.

---
