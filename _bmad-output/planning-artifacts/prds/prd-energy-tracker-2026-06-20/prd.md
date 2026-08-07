---
title: "PRD: energy-tracker"
status: final
created: 2026-06-20
updated: 2026-08-07
---

# PRD: energy-tracker

## 0. Document Purpose

This PRD is the single source of requirements truth for the energy-tracker application — written for the developer-owner (PM, architect, and implementer in one person) and any downstream workflow agents consuming it for UX design, architecture, or epic/story generation. It is structured around Glossary-anchored vocabulary (§3), features grouped with globally-numbered FRs nested under each (§4), and tagged assumptions indexed in §9. Source documents — `brief.md`, `SPEC.md`, `smart-plug-formats.md`, and `locale-formats.md` — are referenced for traceability; this PRD does not duplicate their narrative but builds capability requirements from them. `status: final` marks this PRD as the actively-used source of truth, not a closed document — it has been amended in place across four Releases so far, and every amendment is logged in `.decision-log.md`. The SPEC is the canonical contract for Release 1 and Release 2 scope; where this PRD and the SPEC differ *within that scope*, the SPEC governs. Release 3 (§4.13) and Release 4 (§4.9's device-lifecycle FRs, §4.10's date-aware attribution, §4.11's dismiss/reactivate) were added after `SPEC.md` was last updated — for those, this PRD is authoritative until `SPEC.md` is reconciled.

---

## 1. Vision

energy-tracker is a personal energy monitoring web application that replaces a spreadsheet with a purpose-built, mobile-first experience. Working from home makes household energy a material monthly cost — yet the only available tool is a friction-heavy spreadsheet that is blind to which rooms or devices are responsible. The developer-owner reads the flat's main electricity meter in the basement, opens the app on their phone, and has daily and weekly cost figures updated in under sixty seconds. That single loop — read, enter, see cost — is the product's irreducible core.

On top of that core, the app progressively builds a picture of *where* the energy goes: smart plug file exports from Eve Home and Meross devices are parsed and reconciled against the main meter, a flat structure model maps each plug to a room and device, and EU energy label ratings fill in the gaps for uninstrumented appliances. The result is a consumption decomposition that shrinks the unexplained Residual as more plugs are added — making it possible, over time, to name standby offenders, quantify replacement payback, and know whether the month is tracking toward a surprising invoice before the invoice arrives. Partial plug coverage is the normal starting state, not a failure condition.

Three design principles hold throughout: **cost-first** (every metric surfaces in euros, not just kWh; Tariff is a first-class input) — with one deliberate exception: the KPI dashboard's budget-delta comparison anchors to kWh against the Annual kWh Baseline, since euro figures shift with every Tariff change and would make the delta incomparable across periods (see FR-14) — **residual-aware** (unattributed consumption is explicit and always shown — the app never pretends to full coverage), and **hub-free** (no always-on smart-hub hardware, no smart-hub-vendor cloud subscription, no direct smart-plug-vendor API integration — file uploads and manual entries are the only ingestion paths). The product is self-hosted on Azure, built for one person's flat today, but architected to accommodate additional flats without redesign. Per-user tenant isolation (NFR-2) means the data model would also support additional users, but multi-user support is not a committed roadmap item (see §5 Non-Goals) — nor is the native iOS companion app — its anticipated existence remains a directional constraint on the API surface (§9) rather than a scheduled feature.

---

## 2. Target User

### 2.1 Jobs To Be Done

- **Functional:** Enter a meter reading from my phone in under a minute, without friction.
- **Functional:** Know my daily and weekly cost in euros without calculating anything.
- **Functional:** Upload smart plug exports and see which rooms and devices are responsible for my bill.
- **Functional:** Find out which devices I should unplug or replace, and by how much it matters in euros.
- **Functional:** Enter a future tariff change and trust that no historical cost figures change.
- **Emotional:** Stop being surprised by my annual energy invoice.
- **Contextual:** Do this from my phone while standing in the basement next to the meter.

### 2.2 Non-Users

- Households wanting real-time or near-real-time monitoring (this app uses manual reads and file uploads)
- Users of smart plug brands other than Eve Home or Meross
- Anyone expecting a hosted service — this is self-deployed on the owner's Azure subscription

### 2.3 Key User Journeys

**UJ-1. The developer-owner reads the meter and checks cost.**
After reading the basement meter, opens the app on their phone, taps the reading entry screen, types the kWh value, submits. Sees the KPI dashboard update with new daily average, weekly average, and projected monthly cost in euros. Total time under sixty seconds.

**UJ-2. The developer-owner uploads a week's worth of smart plug data.**
Downloads exports from the Eve Home app (Excel) and Meross app (CSV), uploads both to energy-tracker. The app parses and reconciles them against the main meter; any date gaps are filled by interpolation with a notification. Views the decomposition screen for the past week: sees which room and device was responsible for most consumption, with the residual shown explicitly.

**UJ-3. The developer-owner reviews insights before the monthly invoice.**
Opens the insights page at month end. Sees the rolling monthly projection against their planned annual budget. Finds a named device flagged as a standby offender with a quantified monthly cost. Decides whether to investigate or defer.

---

## 3. Glossary

- **Flat** — A single European-style dwelling unit (apartment or house) tracked in the app. One user may manage multiple Flats. All data (Readings, Tariffs, Smart Plug Data, Flat Structure) is scoped to a Flat.
- **Main Meter** — The electricity meter for a Flat, read manually by the user.
- **Meter Reading** — A single manually-entered kWh value from the Main Meter, timestamped to a specific date and time.
- **Tariff** — A pricing configuration for a Flat: fixed monthly base fee + price per kWh, with a required contract start date. Multiple Tariff entries form a history; each covers its period until the next entry's contract start date.
- **Contract Period** — An optional duration (1, 6, 12, or 24 months) attached to a Tariff entry, stored as a reminder to review the tariff around contract renewal — it has no effect on price locking (FR-11).
- **Smart Plug** — A WiFi-connected outlet (single-outlet; for multi-outlet strips, see Smart Power Strip) that tracks per-plug energy consumption and exports data as a file (Eve Home: Excel; Meross: CSV).
- **Smart Power Strip** — A WiFi-connected multi-outlet power strip that tracks total per-strip energy consumption and exports data as a file (Eve Home: Excel; Meross: CSV). Unlike a Smart Plug, a Smart Power Strip exports the combined consumption of all connected outlets; per-device attribution within a strip requires device-level estimates.
- **Strip Outlet** — A single outlet on a Smart Power Strip, to which one Device is assigned. Strip Outlet consumption cannot be directly measured; it is estimated proportionally from the device's EU label or self-measured value relative to the strip's measured total.
- **Smart Plug Data** — The normalized daily kWh timeline produced by importing a Smart Plug export file.
- **Flat Structure** — The four-level physical hierarchy defined for a Flat: Flat → Rooms → Power Points → Devices.
- **Room** — A named physical space within a Flat (e.g., living room, bedroom).
- **Power Point** — A physical socket or outlet within a Room to which a Smart Plug or Device is assigned.
- **Device** — An appliance or piece of equipment assigned to a Power Point. May have a consumption profile (EU energy label or self-measured).
- **Decomposition** — A breakdown of total Flat consumption into attributed amounts (per Smart Plug / Device) plus a Residual, for a given time period.
- **Residual** — Consumption recorded by the Main Meter that is not attributable to any Smart Plug or Device. Always shown; never suppressed.
- **Interpolated Value** — A daily kWh value synthesized by linear interpolation to fill a mid-period gap in a Smart Plug export. Internally marked; shown with a hint wherever it appears.
- **Insight** — An automatically generated finding surfaced on the Insights page: a standby offender, a replacement candidate, a budget pressure alert, or an invoice deviation hint.
- **Onboarding** — The one-time first-use setup flow in which the user enters their Flat name, annual kWh baseline, and initial Tariff before accessing any main feature.
- **Annual kWh Baseline** — A user-supplied estimate of annual Flat consumption, used to detect budget pressure and invoice deviation. Can be entered as a specific value or chosen from household-size presets.
- **Planned Annual Spend** — A user-supplied annual budget target in the active Locale's currency, auto-derived at Onboarding from the Annual kWh Baseline and initial Tariff (editable thereafter). Drives the Budget Pressure Alert Insight (FR-37).
- **Budget kWh Anchor** — A derived kWh figure, frozen at the moment Planned Annual Spend is set or edited, used to detect budget pressure independent of later Tariff changes (FR-37).
- **Locale** — A combined language-and-region setting (e.g., `de-DE`, `en-US`) that governs all text, number formatting, date formatting, and currency symbol.

---

## 4. Features

### 4.1 Authentication

**Description:** All routes in the app require authentication. The user authenticates via an OpenID Connect-compatible identity provider — Azure Entra ID at launch. Unauthenticated requests redirect to the OIDC login flow. Sessions persist across browser restarts. The identity provider is swappable through configuration alone; no code changes are required to switch providers.

**Functional Requirements:**

#### FR-1: Authentication gate
Any user can access the app only after completing authentication via the configured OIDC identity provider. Unauthenticated requests to any route — including deep links — redirect to the OIDC login flow and return to the requested route after successful authentication.

**Consequences (testable):**
- A direct URL to any app route, accessed without a valid session, redirects to the OIDC login page.
- After completing login, the user lands on the originally requested route (not the app root).

#### FR-2: Session persistence
An authenticated session persists across browser restarts without requiring re-authentication until the session expires or the user signs out.

**Consequences (testable):**
- Closing and reopening the browser retains the authenticated session.
- An expired or invalid session redirects to login.

#### FR-3: Configurable identity provider
The identity provider is specified via configuration (environment variables or config file). Changing the provider requires no code changes, only configuration changes.

**Consequences (testable):**
- Swapping the configured OIDC provider and redeploying routes all auth flows through the new provider without code modification.

---

### 4.2 Onboarding

**Description:** On first use, before the user can reach any main feature, they complete a one-time onboarding setup: entering their first Flat's name, establishing an Annual kWh Baseline, and configuring their initial Tariff. The Annual kWh Baseline can be entered as a specific kWh value or selected from household-size presets. All onboarding fields remain editable via Settings at any time. Realizes UJ-1.

**Functional Requirements:**

#### FR-4: First-use onboarding gate
A new user (no existing Flat) cannot access any main app feature until Onboarding is complete. Onboarding collects: Flat name, Annual kWh Baseline (specific value or preset), initial Tariff (fixed monthly base fee and price per kWh required; provider name, contract start date, and contract duration optional), and Planned Annual Spend (auto-derived from Annual kWh Baseline × price per kWh + monthly base fee × 12; editable by the user).

**Consequences (testable):**
- Navigating to any main route before Onboarding completion redirects to the Onboarding flow.
- Onboarding completes only when both the Annual kWh Baseline and the required Tariff fields are provided.
- The Planned Annual Spend field is pre-populated from the entered Tariff and kWh Baseline, with the derivation calculation displayed; the user may override the derived value before completing Onboarding.
- If the initial Tariff's contract start date is left blank, it defaults to the Onboarding completion date rather than being left unset (see FR-6) — Onboarding never produces a Tariff entry without one, satisfying FR-10's required-field invariant.

#### FR-5: Annual kWh Baseline entry
The user can enter the Annual kWh Baseline either as a specific numeric value or by selecting one of four household-size presets: 1 person ≈ 1,500 kWh; 2 persons ≈ 2,500 kWh; 3 persons ≈ 3,500 kWh; 4 persons ≈ 4,250 kWh (rough estimates, not sourced from external data — see [A-11]). The selected or entered value is stored as the Flat's Annual kWh Baseline.

**Consequences (testable):**
- Each preset selection populates the Annual kWh Baseline field with the corresponding value.
- A manually entered specific value overrides any selected preset.

#### FR-6: Onboarding Tariff entry
The initial Tariff is captured during Onboarding with the fixed monthly base fee and price per kWh as required fields; provider name, contract start date, and contract duration (1, 6, 12, or 24 months) are optional. The entered Tariff is immediately reflected in all cost calculations after Onboarding completes.

**Consequences (testable):**
- A cost figure calculated immediately after Onboarding uses the Tariff entered during Onboarding.
- If the contract start date is omitted, it defaults to the Onboarding completion date rather than being left unset — every Tariff entry, including this one, always has a contract start date per FR-10's requirement.

#### FR-7: Onboarding settings editability
All Onboarding fields — Flat name, Annual kWh Baseline, Tariff, and Planned Annual Spend — are accessible and editable from Settings after initial setup. The Planned Annual Spend is editable from Settings near the Tariff configuration, not in a separate budget section.

**Consequences (testable):**
- Updating the Annual kWh Baseline from Settings changes the baseline used in Insight calculations immediately.
- Updating the Planned Annual Spend from Settings re-derives its Budget kWh Anchor (see FR-37) using the Tariff active at the moment of the edit, and takes effect on future budget pressure alert evaluations.

---

### 4.3 Meter Reading Entry

**Description:** The user submits a manual Meter Reading from the Main Meter via a mobile browser. The entry surface is optimized for one-handed mobile use; the round-trip from opening the screen to a persisted reading must complete within the performance budget. Realizes UJ-1.

**Functional Requirements:**

#### FR-8: Meter Reading submission
The user can submit a Meter Reading (numeric kWh value) for the active Flat. The Reading is stored with the date and time of submission. If the entered value is lower than the Flat's last recorded Reading, the entry form displays a warning ("Lower than your last reading (X kWh) — is this correct?") but still permits the user to proceed, accommodating meter replacement or correction scenarios.

**Consequences (testable):**
- A submitted Reading is retrievable via the dashboard with its recorded date and time.
- Server-side processing time (from request received to response dispatched, client network excluded) for a Reading submission is within the performance budget defined in §NFR-1.
- Entering a kWh value lower than the Flat's last recorded Reading displays the warning but does not block submission.
- A submitted kWh value with more than 4 decimal places is rejected with a validation error before it reaches storage.

#### FR-9: Retroactive Reading entry
The user can enter a Meter Reading for a past date. Retroactive Readings are costed at the Tariff that was active on their entered date, not the current Tariff.

**Consequences (testable):**
- A Reading entered for a past date where a different Tariff was active uses that historical Tariff for cost calculations.

#### FR-48: Meter Reading correction and history
A previously submitted Meter Reading can be edited after submission. Editing a Reading preserves and displays the original value as a correction note; no separate approval workflow or immutability requirement applies, given single-user scope. The Flat's chronological Reading history — date/time, kWh value, and any correction note — is viewable from the Dashboard and Insights trend charts.

**Consequences (testable):**
- Editing a Reading's kWh value stores the original value as a visible correction note rather than overwriting it silently.
- The Flat's Reading history, including corrected entries, is viewable in chronological order.
- For Flats with long reading histories, the Reading History view loads readings incrementally (most recent first) rather than fetching the entire history at once.

---

### 4.4 Tariff Management

**Description:** The user maintains a Tariff history for each Flat. Each Tariff entry has a required contract start date, a fixed monthly base fee, and a price per kWh; provider name and contract duration are optional. The contract start date is the sole anchor for both cost-period resolution and price locking: once it has passed, that entry's prices are locked pending an explicit override. Future Tariff changes — including provider switches — can be pre-entered with a future contract start date. Historical cost figures are always calculated at the Tariff active during the period in question. Dynamic/variable-rate tariffs with no fixed price per kWh are explicitly out of scope.

**Functional Requirements:**

#### FR-10: Tariff configuration
The user can create a Tariff entry for the active Flat specifying: contract start date (required) — the date this price takes effect, whether in the past, present, or future — fixed monthly base fee in the active Locale's currency (required), price per kWh (required), provider name (optional), and contract duration in months — 1, 6, 12, or 24 (optional, informational reminder only — see FR-11).

**Consequences (testable):**
- A Tariff entry is stored with all provided fields and its contract start date.

#### FR-11: Period-locked Tariff prices
Every Tariff entry's contract start date determines whether its price fields are locked. If the contract start date is on or before today, price fields (price per kWh, monthly base fee) require explicit override confirmation before they can be modified. If the contract start date is in the future, price fields remain freely editable — no consumption has been costed against them yet. Contract duration, if provided, does not affect this lock; it is stored only as a reminder to review the tariff around contract renewal.

**Consequences (testable):**
- An attempt to edit the price fields of a Tariff entry whose contract start date has passed, without an explicit override, is rejected.

#### FR-12: Future Tariff pre-entry
The user can create a Tariff entry with a future contract start date. This does not alter any cost calculations for periods before that date, and its price fields remain freely editable per FR-11 until the date arrives.

**Consequences (testable):**
- A Tariff entry with a future contract start date has no effect on cost figures for any past period.

#### FR-13: Period-accurate historical costing
All cost calculations — KPI Dashboard figures, Decomposition costs, budget projections — use the Tariff active on the date of the relevant consumption, not the current Tariff.

**Consequences (testable):**
- Meter Readings entered for past periods are costed at the Tariff active at the time of those readings, even after a Tariff change.

---

### 4.5 KPI Dashboard

**Description:** After entering a Meter Reading, the user immediately sees a summary dashboard for the active Flat. The dashboard displays: daily average kWh, weekly average kWh, daily cost, weekly cost, and projected monthly cost in the active Locale's currency. All figures reflect the current Tariff in each period. Realizes UJ-1.

**Functional Requirements:**

#### FR-14: KPI Dashboard display
The KPI Dashboard for the active Flat displays: daily average kWh, weekly average kWh, daily cost in the active Locale's currency, weekly cost in the active Locale's currency, and projected monthly cost in the active Locale's currency. The dashboard additionally displays a budget delta against a daily budget derived from the Annual kWh Baseline (Annual kWh Baseline ÷ 365). The delta is expressed in kWh (e.g., "↓ 0.8 kWh under budget"), not euros — kWh is the stable anchor across Tariff changes, whereas euro figures shift with every Tariff update (see §1 Vision).

**Consequences (testable):**
- Each of the five KPI figures is visible on the Dashboard.
- All currency figures match independently calculated values for the same period.
- The budget-delta figure is denominated in kWh, not currency, and remains numerically stable across a Tariff change within the same period.

#### FR-15: Immediate Dashboard update
Dashboard figures update immediately after a new Meter Reading is saved, without requiring a page refresh.

**Consequences (testable):**
- After submitting a Meter Reading, the Dashboard figures reflect the new data before the user navigates away.

---

### 4.6 Trends and Spike Detection

**Description:** The app maintains a consumption trend visualization and alerts the user when daily usage deviates unusually from the recent baseline. Spike detection compares each day's consumption against the 7-day rolling average; a configurable threshold (default 2×) determines what constitutes a spike. Realizes UJ-3.

**Functional Requirements:**

#### FR-16: Consumption trend visualization
The app displays a trend chart of historical daily consumption for the active Flat, derived from Meter Readings. The Insights tab's trend chart defaults to a 30-day period — a broader window than the Dashboard's shorter sparkline, providing the context needed for standby-offender and budget-pressure insight patterns without duplicating the Dashboard view.

**Consequences (testable):**
- The trend chart correctly reflects historical Meter Readings for a given date range.
- The Insights tab's trend chart opens showing a 30-day period by default.

#### FR-17: Spike detection
The app detects daily consumption spikes that exceed a configurable threshold above the 7-day rolling average. The default threshold is 2× the rolling average. A spike is visually encoded in the trend chart as a distinctly styled bar (amber). Full spike context is accessible from the Insights tab. No separate banner or notification is generated — this is a deliberate choice to avoid adding an interruption channel to the product's core 60-second reading loop (protecting SM-C2); a user following the Insights-review habit the product is designed around (UJ-3) encounters the spike there. The threshold is user-configurable per Flat.

**Consequences (testable):**
- A day whose consumption exceeds 2× the 7-day rolling average (at default threshold) is rendered as a distinctly styled (amber) bar in the trend chart.
- Changing the threshold to a custom value causes spike detection to use that value instead.
- The trend visualization reflects spike days distinctly from normal days.

#### FR-56: Meter-reset visual indicator
When a Meter Reading is lower than the immediately preceding Reading — indicating the Main Meter was replaced or reset, not a data-entry error (already handled separately by FR-8's warning at submission time) — the trend chart renders that day with a distinct visual treatment (a hatched/reset-icon bar) instead of as ordinary or spike-styled consumption. This applies to the same trend chart described in FR-16, including its use within the Insights tab.

**Consequences (testable):**
- A day whose consumption derives from a reading pair where the later reading is lower than the earlier one is rendered with the distinct meter-reset visual treatment, not as a normal or spike bar.
- The meter-reset treatment takes precedence over the spike-detection styling (FR-17) for the same day.

---

### 4.7 Multi-Flat Management

**Description:** The user can manage multiple Flats. A persistent header component allows the user to switch between Flats; the last active Flat is remembered across sessions. Each Flat has an independently manageable physical Flat Structure (Rooms → Power Points → Devices). Flat Structure setup is only required for smart plug import and Decomposition; the meter reading entry and KPI Dashboard are accessible without a defined structure. Deleting a Flat permanently removes all its data. Realizes UJ-2. *(Release 2)*

**Functional Requirements:**

#### FR-18: Multiple Flat support
The user can create and manage more than one Flat within their account. Each Flat has its own Meter Readings, Tariff history, Smart Plug Data, and Flat Structure.

**Consequences (testable):**
- A second Flat can be created; its Meter Readings, Tariff entries, and Smart Plug Data are independent of the first Flat's.

#### FR-19: Flat switcher
A header component visible across all app surfaces allows the user to switch between their Flats. The active Flat's name is displayed in the header.

**Consequences (testable):**
- The Flat switcher lists all of the user's Flats.
- Selecting a Flat from the switcher loads that Flat's data on all screens.

#### FR-20: Last active Flat persistence
The last active Flat is remembered across browser sessions. On returning to the app, the previously active Flat is loaded automatically.

**Consequences (testable):**
- After closing and reopening the browser, the app opens to the Flat that was active when the session ended.

#### FR-21: Flat Structure definition
For each Flat, the user can define a Flat Structure: Rooms within the Flat, Power Points within each Room, and Devices assigned to each Power Point. Each Smart Plug can be assigned to exactly one Power Point. The Power Point assignment is the authoritative source of the `plug_id` used in the unified Smart Plug Data timeline; `plug_id` is never derived from file metadata (device name or filename).

A Smart Power Strip can also be assigned to a Power Point. A Smart Power Strip exposes multiple Strip Outlets, each of which can have one Device assigned. The strip's `plug_id` is assigned at the Power Point level (same as for a Smart Plug); strip outlets inherit the strip's `plug_id` as a prefix.

**Consequences (testable):**
- A Room can be created within a Flat; a Power Point can be created within that Room; a Device can be assigned to that Power Point.
- A Smart Plug assigned to one Power Point cannot simultaneously be assigned to another.
- Smart Plug Data imported for a plug is linked to its assigned Power Point by `plug_id`; renaming the device in the export file does not change the assignment.
- A Smart Power Strip can be assigned to a Power Point; its Strip Outlets each carry a `plug_id` prefixed by the strip's `plug_id`.

#### FR-22: Default room template
When the user initiates Flat Structure setup for the first time on a Flat, a default Room template is pre-populated for customization: living room, bedroom, kitchen, bathroom, hallway.

**Consequences (testable):**
- Opening Flat Structure setup for a Flat with no existing structure presents the five default Rooms.

#### FR-23: Flat deletion with cascade
Deleting a Flat permanently removes all data associated with it: all Meter Readings, Tariff entries, Smart Plug Data, Flat Structure, and Device registrations. No orphaned records remain. Deleting a Flat requires the user to type the Flat's exact name to enable the Delete action, matching the friction of the data's irreversibility.

**Consequences (testable):**
- After deleting a Flat, no Meter Readings, Tariff entries, Smart Plug records, or Device registrations for that Flat exist in the data store.
- The Delete action for a Flat is disabled until the user has typed the Flat's exact name into a confirmation field.

---

### 4.8 Smart Plug Import

**Description:** The user uploads smart plug export files — Eve Home Excel files or Meross CSV files — covering any period, including past periods. The app parses each file into a unified daily kWh timeline, detects mid-period date gaps, fills them by linear interpolation, and reconciles the timeline against Main Meter totals. Interpolated values are flagged internally and shown with a hint wherever they appear. Import failures produce one of three categorized user-facing messages. Realizes UJ-2. *(Release 2)*

**Functional Requirements:**

#### FR-24: Eve Home Excel import
The app parses Eve Home `.xlsx` export files (single sheet `Gesamtverbrauch`, reverse-chronological ~10-minute interval rows in Wh) into a daily kWh timeline per plug. Device name is extracted from cell A1; Room name from cell A2 (informational only). Multiple exports for the same plug with overlapping periods are deduplicated by timestamp before aggregation. Rows with empty values are skipped.

**Consequences (testable):**
- A valid Eve Home file loads without error and produces a daily kWh timeline.
- Two overlapping Eve Home exports for the same plug produce deduplicated daily totals.
- Eve Home `Datum` timestamps are treated as local time and are **not** converted to UTC; conversion would corrupt daily aggregation boundaries by shifting interval assignments across midnight.

#### FR-25: Meross CSV import
The app parses Meross `.csv` export files (UTF-8 with optional BOM, tab-separated with per-value comma prefix) into a daily kWh timeline per plug. Device name is extracted from the filename using the pattern `Power Monitor Day Data - {device_name} - {YYYYMMDD}.csv`. BOM, trailing whitespace, and empty rows are stripped before parsing.

**Consequences (testable):**
- A valid Meross CSV file loads without error and produces a daily kWh timeline.
- A Meross file with a UTF-8 BOM parses correctly.
- A row with `Power Consumption-(kWh)` value of `0.000` is stored as a valid zero-consumption day and is **not** treated as a missing date subject to gap detection or interpolation.

#### FR-26: Gap detection and linear interpolation
If a plug's timeline contains missing dates within its covered period (first date to last date in the export), the gaps are detected, the user is notified with the affected date ranges, and the gaps are filled by linear interpolation between the surrounding anchor values (capped at the per-day average of the 7 days before the gap). Interpolated values are internally marked as attributed consumption; a hint is shown to the user whenever a viewed period contains interpolated data.

**Consequences (testable):**
- An export with a mid-period gap (missing date within the covered range) completes with a notification listing the affected date range(s).
- Missing days are recorded as interpolated values; the gap-containing period displays an interpolated-data hint.
- Periods with no imported Smart Plug data at all show as "decomposition unavailable" — gap detection applies only within an export's active range, not to periods before or after the export.
- A date with a recorded value of `0.000` kWh is not a gap and is not interpolated — gap detection considers only absent dates, not zero-valued ones.

#### FR-27: Smart Plug reconciliation against Main Meter
For periods where both Main Meter Readings and Smart Plug Data exist, the app reconciles total attributed consumption (including interpolated values) against the Main Meter total. Attributed kWh across all plugs does not exceed the Main Meter total for any period. Unattributed consumption becomes the Residual.

**Consequences (testable):**
- Total attributed kWh + Residual = Main Meter total for any period with Smart Plug Data (within ±0.1 kWh for clean periods with no interpolated values; within ±1.0 kWh for periods containing any interpolated values, reflecting interpolation uncertainty).
- Because Smart Plug measurement error can cause attributed kWh to exceed the Main Meter total, the Residual can be negative for a given period; the app does not clamp this — a negative Residual is displayed exactly as computed, signaling a data-quality issue rather than a true negative consumption.

#### FR-28: Import error categorization
Import failures are logged internally and surfaced to the user as one of three categorized messages: (1) data cannot be read, (2) processing failed — user should retry, (3) service temporarily unavailable — user should retry later. Raw error detail is not exposed to the user.

**Consequences (testable):**
- An unreadable or corrupt file surfaces a "data cannot be read" categorized message.
- An internal processing failure surfaces a "processing failed — retry" message.
- A service outage surfaces a "service temporarily unavailable — retry later" message.

#### FR-49: Smart Plug import progress indicator
Once a Smart Plug import is initiated, a persistent progress indicator remains visible on the import surface until background processing completes. The user is not blocked from using the rest of the app while an import is processing.

**Consequences (testable):**
- A persistent progress indicator is visible on the import surface from initiation until the import job completes.
- The user can navigate to other app surfaces while an import is processing.

---

### 4.9 Device Registry

**Description:** The user can register Devices with metadata and configure their energy consumption via one of two approaches: EU energy label (class + annual kWh figure) or self-measured average (daily or weekly kWh). Both approaches contribute an estimated baseline to the Decomposition. A standalone Device (no Smart Plug, no Smart Power Strip assignment) with no consumption approach configured shows no kWh or cost figure in Decomposition — not a zero value, since no measurement or estimate exists for it — with a prompt to configure its consumption profile. Realizes UJ-2. *(Release 2)*

**Functional Requirements:**

#### FR-29: Device metadata registration
The user can register a Device with: type, manufacturer, and model (required); purchase date (optional). A registered Device is assigned to a Power Point in the Flat Structure.

**Consequences (testable):**
- A Device with the required fields can be saved and appears in the Flat Structure at its assigned Power Point.

#### FR-30: EU energy label consumption
The user can configure a Device's consumption via EU energy label. The label's stated annual kWh figure is required — it drives the daily estimate used in Decomposition. The energy class rating is optional, recorded for potential future use such as replacement-candidate detection. The app derives a daily estimate from the annual figure. The Device appears in Decomposition with the derived daily estimate, marked as estimated.

**Consequences (testable):**
- A Device configured via EU label displays its annual kWh and derived daily estimate in the Decomposition view, marked as estimated.
- A Device cannot be saved with an EU label approach unless the annual kWh figure is provided; the energy class rating may be left blank.

#### FR-31: Self-measured consumption
The user can configure a Device's consumption as a self-measured average: a daily or weekly kWh value entered by the user. The period defaults to Daily; the user can switch it to Weekly via a toggle before entering the kWh value. The Device appears in Decomposition with the configured value, marked as estimated.

**Consequences (testable):**
- A Device configured with a self-measured daily or weekly value displays that value in the Decomposition view, marked as estimated.
- The self-measured consumption form opens with Daily selected by default.

#### FR-50: Unmeasured standalone device — no figure shown
A standalone Device (no Smart Plug, no Smart Power Strip assignment) with no consumption approach configured displays no kWh or cost figure in Decomposition — distinct from a Smart Power Strip's unconfigured sub-device, which receives a real nominal share under FR-32. A prompt to configure the Device's consumption profile is shown in its place.

**Consequences (testable):**
- A standalone Device with `ConsumptionApproach = None` shows no kWh/cost figure and no zero value in the Decomposition view — only a configuration prompt.

#### FR-52: Device existence window
The user can specify when a Device started consuming power (`InUseSince`) and, optionally, when it was decommissioned (`DecommissionedDate`). When set, these dates gate the Device's inclusion in Decomposition for periods outside that window; when unset, the Device is treated as always included (backward-compatible default). `InUseSince`/`DecommissionedDate` apply independently of `PurchaseDate` (FR-29), which remains purely informational (e.g., for warranty tracking).

**Consequences (testable):**
- A Device with `InUseSince` set to a date within the query period contributes estimated kWh only for days on/after that date, not the full period.
- A Device with `DecommissionedDate` set stops contributing estimated kWh for days after that date.
- A Device with neither date set behaves exactly as before — full-period inclusion, preserving backward compatibility for all existing data.

#### FR-53: Device room-assignment history
The app tracks a Device's Power Point assignment history over time. When a Device's Power Point assignment changes via the Flat Structure editor, the app records the date of the change automatically — no manual date entry required. For any Decomposition query period spanning a Device's reassignment, the Device's consumption for each day is attributed to the Room its Power Point belonged to on that day, not to its current Room.

**Consequences (testable):**
- A Device reassigned to a different Power Point on date X, queried over a period spanning both before and after X, shows consumption split between the two Rooms by date.
- Reassigning a Device requires no manual date entry — the change is recorded automatically at save time.
- Existing Devices (predating this feature) resolve to their current Power Point for their entire history via a one-time migration backfill — no device silently disappears from Decomposition.
- Interpolation (FR-26) and date-aware attribution are independent pipeline stages keyed by calendar date: a Smart Plug gap is filled at import time into the plug's daily timeline, before attribution runs; attribution then reads whichever value — measured or interpolated — exists for a given date and assigns it to whichever Room the Device's Power Point belonged to on that same date. An interpolated day that spans a reassignment boundary is attributed entirely to whichever side of the boundary that specific calendar date falls on, exactly like a measured day — no special-casing is needed or performed.

---

### 4.10 Consumption Decomposition

**Description:** For any period where Smart Plug Data has been imported, the user can view consumption broken down by Room and Device. Partial plug coverage is the normal starting state — the Residual (Main Meter total minus all attributed consumption) shrinks as more Smart Plugs are added and is not a failure condition. The Residual is always shown, never suppressed. Periods without Smart Plug Data are shown as explicitly unavailable — no zeros, no partial figures — with a prompt to import data. Periods containing Interpolated Values display a hint. Realizes UJ-2, UJ-3. *(Release 2)*

**Functional Requirements:**

#### FR-32: Decomposition view
For any period with Smart Plug Data, the Decomposition view shows attributed consumption per Room and per Device, derived from Smart Plug Data plus Device estimated baselines (EU label and self-measured). The view offers a period selector — This week, This month, Last month, This year, Custom — defaulting to This month. Last month is a first-class option, supporting review early in the current month before it has accumulated meaningful data.

**Consequences (testable):**
- The Decomposition view for a period with Smart Plug Data shows per-Room and per-Device consumption figures.
- Attributed totals + Residual = Main Meter total for the period (within ±0.1 kWh for clean periods; within ±1.0 kWh for periods containing interpolated values — see FR-27).
- For a Smart Power Strip, the strip's measured total is displayed as the authoritative kWh figure. Each configured sub-device's share is weighted by its own estimated kWh relative to the pool of configured estimates plus one nominal weight per unconfigured sub-device (the average estimated kWh across configured sub-devices); each unconfigured sub-device receives an identical nominal share, displayed at reduced visual prominence with a prompt to configure its device profile. When a strip has zero configured sub-devices, every sub-device receives an equal split of the strip's measured total. All shares sum to exactly the strip's measured total (see architecture.md AD-8a for the exact formula).
- The Decomposition view opens showing This month by default; Last month is selectable and shows that period's figures.

#### FR-33: Residual always shown
The Residual is shown in the Decomposition view for every period with Smart Plug Data, including when the Residual is zero. The Residual is never suppressed.

**Consequences (testable):**
- The Residual line is present in the Decomposition view even when its value is zero.

#### FR-34: Decomposition unavailable state
Periods with no imported Smart Plug Data are shown in the Decomposition view as "decomposition unavailable" with a prompt to import data. Partial or zero figures are never shown for unavailable periods.

**Consequences (testable):**
- Selecting a period with no Smart Plug Data in the Decomposition view displays the "decomposition unavailable" state and an import prompt.
- Selecting a period with interpolated data displays the interpolated-data hint.

#### FR-54: Period total consumption summary
The Decomposition view shows the selected period's total kWh and total cost alongside the period selector, so the user can relate individual Room and Device breakdown figures to the period total without doing mental math. Not shown in the "decomposition unavailable" state, consistent with FR-34.

**Consequences (testable):**
- Selecting a period with Smart Plug Data shows a total-kWh and total-cost figure next to the period selector.
- Selecting a period with no Smart Plug Data (the "decomposition unavailable" state) does not show the total figure.

---

### 4.11 Actionable Insights

**Description:** The Insights page automatically surfaces four categories of findings: high-standby offenders (named Devices with draw > 2 W during hours outside a configured usage window), high-consumption replacement candidates (with quantified payback in euros), budget pressure alerts (when rolling projections exceed the user's Planned Annual Spend), and invoice deviation hints (when rolling annual kWh consumption deviates significantly from the Annual kWh Baseline set in Onboarding). Insight discovery runs on a daily schedule at 02:00 UTC and can also be triggered manually. The user sees a progress indicator while discovery is running; prior insights remain visible during a new run. Realizes UJ-3. *(Release 2)*

**Functional Requirements:**

#### FR-35: High-standby offender detection
Based on Smart Plug interval data, the app identifies Devices drawing more than 2 W outside their configured usage window and flags them as Insights with the Device name and a quantified monthly cost figure in the active Locale's currency. Standby detection operates exclusively on Eve Home plug data via the raw ~10-minute interval records retained at import. Meross plugs are excluded from standby detection because Meross exports provide daily aggregates only — sub-daily resolution required for this feature is unavailable for that format.

**Consequences (testable):**
- A Device connected to an Eve Home plug, with recorded interval draw consistently above 2 W during out-of-use hours, is flagged as a standby offender Insight once sufficient data exists.
- The Insight includes the Device name and a monthly cost figure (kWh and €).
- Devices connected only to Meross plugs do not appear in standby offender Insights; no error is shown — this is an explicit format limitation, not a failure condition.

#### FR-36: Replacement candidate detection
Among all Devices with an estimated or measured annual kWh figure, the app ranks by estimated annual cost and considers the top 20% (rounded up, minimum 1) as candidates. A candidate becomes a replacement Insight only if its registered EU energy label class is C or worse; the suggested replacement class is one step better than its current class, with estimated savings computed as 15% of the candidate's annual cost. A measured Device (via Smart Plug) needs at least 7 distinct days of data in the trailing 30 days to have an annual figure computed at all; without that, it falls back to its configured EU-label or self-measured estimate per FR-30/FR-31.

**Consequences (testable):**
- A Device outside the top 20% by estimated annual cost, or with no EU label class registered, or with a class better than C, does not surface as a replacement candidate.
- A replacement candidate Insight names a specific Device with a suggested EU label class one step better than its current one and an estimated savings figure equal to 15% of its estimated annual cost.
- A measured Device with fewer than 7 distinct days of Smart Plug data in the trailing 30 days is evaluated using its EU-label or self-measured estimate instead, not excluded outright.

#### FR-37: Budget pressure alert
At the moment Planned Annual Spend is set or edited (Onboarding or Settings), the app derives a Budget kWh Anchor — (Planned Annual Spend − annualized monthly base fee) ÷ price per kWh — using the Tariff active at that moment, and stores it. This anchor is frozen: it does not change when the Tariff later changes, only when the user next edits Planned Annual Spend. This mirrors FR-14's kWh-anchoring rationale (§1) — the alert tracks actual consumption pressure, not Tariff-price movement. The app computes a rolling projected annual kWh consumption for the active Flat and generates a budget pressure alert Insight when the projection exceeds the stored Budget kWh Anchor.

**Consequences (testable):**
- Setting Planned Annual Spend to €1,200 with an active Tariff of €0.35/kWh and a €10/month base fee derives a Budget kWh Anchor of (1,200 − 120) ÷ 0.35 ≈ 3,085.7 kWh.
- A subsequent Tariff price change alone, with no change in consumption, does not alter the stored Budget kWh Anchor and therefore does not trigger a new alert.
- A budget pressure alert appears on the Insights page when the rolling projected annual kWh exceeds the stored Budget kWh Anchor.
- If the annualized base fee alone is greater than or equal to Planned Annual Spend, the Budget kWh Anchor is 0, and the alert fires as soon as any consumption is measured.
- **Not yet implemented as of this PRD revision (2026-08-07):** `BudgetAlertDetector.cs` currently compares projected annual *cost* (€) against Planned Annual Spend (€) directly — the euro-vs-euro comparison this FR replaces, which could false-positive on a Tariff price increase alone. Implementing this redefinition requires a new story to add the budget-kWh-anchor field and its derivation/recomputation logic.

#### FR-38: Scheduled and manual insight discovery
Insight discovery runs automatically on a daily schedule at 02:00 UTC. The user can also trigger discovery manually from the Insights page. Results are shown as soon as they are discovered.

**Consequences (testable):**
- A manually triggered discovery run updates the Insights page with new findings.
- Insights from a prior run remain visible while a new run is in progress.

#### FR-39: Discovery progress indicator
A visible progress indicator is displayed for the full duration of an insight discovery run, whether triggered manually or by the daily schedule.

**Consequences (testable):**
- A progress indicator is visible from the start to the end of a discovery run.

#### FR-43: Invoice deviation hint
The app computes a rolling annual kWh figure for the active Flat and generates an invoice deviation Insight when consumption is trending ±10% or more above or below the Annual kWh Baseline set during Onboarding. The Insight surfaces the projected annual kWh, the configured baseline, and the implied euro difference at the current Tariff.

**Consequences (testable):**
- An invoice deviation Insight appears when rolling annual kWh consumption deviates by ±10% or more from the Annual kWh Baseline.
- The Insight displays the projected annual kWh, the baseline, and the difference in euros at the current Tariff.
- Updating the Annual kWh Baseline in Settings takes effect immediately on future insight evaluations.

#### FR-51: Insight de-duplication and historical retention
When a discovery run's detector produces a finding whose primary quantified figure (the Insight's main €/kWh figure) is within 5% of the same finding identity's (`Type`, and `Device` where applicable — Standby/Replacement are per-device, Budget/InvoiceDeviation are per-flat) most recently stored value, the new finding is not persisted; the existing, older `Insight` row remains the current representative for that finding. A finding whose primary figure differs by more than 5% from the most recent stored value for that identity is persisted as a new, distinct `Insight` row. The superseded row is never deleted, but the Insights tab's default view shows only the single most-recently-stored `Insight` row per `(Type, Device)` identity — older superseded rows remain queryable in the data store for a future historical/dismiss view, but are excluded from the default response. No `Insight` row is ever deleted by this behavior.

**Consequences (testable):**
- A detector finding matching the most recently stored Insight of the same Type/Device within 5% does not create a new `Insight` row.
- A detector finding differing from the most recently stored Insight of the same Type/Device by more than 5% creates a new `Insight` row; the prior row is not deleted or modified, but is no longer returned by the default Insights read.
- The Insights page never shows more than one card per `(Type, Device)` identity at a time, regardless of how many historical `Insight` rows exist for it in the data store.

#### FR-55: Insight dismiss and reactivate
A user can dismiss an Insight they've acted on or don't want to see, and later reactivate it. Dismissing an Insight suppresses its entire finding identity (`Type`, and `Device` where applicable) from both the default Insights view and future discovery runs — a dismissed identity does not resurface with a new finding, even if the underlying figure changes, until the user reactivates it. Reactivating restores the Insight to the default view and allows future discovery runs to detect and persist new findings for that identity again, per FR-51's existing de-duplication rule.

**Consequences (testable):**
- Dismissing an Insight removes it from the default Insights view immediately.
- While an identity is dismissed, a scheduled or manual discovery run that would otherwise persist a new finding for that identity (per FR-51) does not do so.
- Reactivating a dismissed Insight returns it to the default Insights view.
- After reactivation, a subsequent discovery run persists a new finding for that identity per FR-51's normal 5%-tolerance rule.

---

### 4.12 Localization

**Description:** The user selects a Locale (language + region) from Settings. All UI text is rendered in the selected language; all numbers, dates, times, and currency values are formatted to the Locale's conventions (see `locale-formats.md`). All data is stored and transmitted locale-neutrally. Supported Locales at launch: `de-DE` (German) and `en-US` (English — United States).

**Functional Requirements:**

#### FR-40: Locale selection
The user can select their preferred Locale from Settings. Supported Locales at launch: `de-DE` and `en-US`. The selection takes effect immediately across all app surfaces. The initial Locale is derived from the browser's `Accept-Language` header; once selected, the override is stored server-side in the user profile and applies across all browsers and sessions without requiring reconfiguration.

**Consequences (testable):**
- Switching from `de-DE` to `en-US` (or vice versa) updates all UI text, number separators, date format, time format, and currency symbol throughout the app without a page reload.
- After closing and reopening the browser — or accessing the app from a different browser — the previously selected Locale is restored automatically from the server-stored user profile.
- A new session in any browser defaults to the `Accept-Language`-derived Locale only when no server-stored override exists.

#### FR-41: Locale-aware rendering
All numbers, dates, times, and currency values are formatted according to the active Locale's conventions. `locale-formats.md` is the authoritative format specification; this FR defines the requirement, that file defines the exact output. Currency follows the Locale (€ for `de-DE`; $ for `en-US`). No hardcoded locale-specific formatting anywhere in the codebase.

**Consequences (testable):**
- In `de-DE`: currency renders as value-then-symbol with space and comma decimal (e.g., `1,27 €`); time renders as 24-hour `HH:mm` (e.g., `14:12`); date renders as `dd.mm.yyyy` (e.g., `12.04.2026`).
- In `en-US`: currency renders as symbol-then-value with period decimal (e.g., `$1.27`); time renders as 12-hour `h:mm AM/PM` (e.g., `2:12 PM`); date renders as `mm/dd/yyyy` (e.g., `04/12/2026`).
- A currency amount stored as a fixed-decimal value renders with the correct symbol, separators, and precision for the active Locale.
- Switching Locale updates all existing displayed values to the new format.

#### FR-42: Locale-neutral storage
All data is stored and transmitted locale-neutrally: ISO 8601 datetimes with timezone offset, decimal-point numbers, currency as fixed-decimal values. Locale formatting is applied only at render time.

**Consequences (testable):**
- Currency amounts stored during a `de-DE` session render correctly when the Locale is subsequently changed to `en-US`.
- A datetime stored in one session is retrieved correctly regardless of the Locale active at retrieval time.

---

### 4.13 UI & Behavior Consistency

**Description:** A cross-cutting set of interaction and layout requirements added post-Epic-7 retro (Release 3), closing consistency gaps that surfaced across multiple existing surfaces (Flat Structure editing, save/cancel affordances, dropdown/overlay rendering, and responsive device layout) rather than describing a new feature.

**Functional Requirements:**

#### FR-44: Flat Structure autosave for structural edits
Structural edits to Flat Structure — adding or deleting a Room — save automatically on each change; no separate manual "Save" action is required for structural changes.

**Consequences (testable):**
- Adding or deleting a Room persists immediately, without requiring a separate Save action.

#### FR-45: Save/cancel action placement and viewport visibility
Every save/cancel action in a form or sheet is positioned adjacent to the fields it commits, and remains within the visible viewport without requiring the user to scroll to find it, across all supported browsers (including Safari).

**Consequences (testable):**
- A save/cancel action for any form or sheet is visible within the viewport without scrolling, on every supported browser.

#### FR-46: Dropdown/overlay visibility
Every dropdown/overlay/popover in the app renders fully visible and unclipped within the viewport, regardless of its trigger's position on the page.

**Consequences (testable):**
- A dropdown/overlay/popover opened from any trigger position renders fully visible, with no portion clipped by an ancestor element.

#### FR-47: Responsive device card grid
Within a Room Card, device cards lay out in a single full-width column below 768px viewport width, a 2-column grid from 768px up to 1023px, and a 3-column grid at 1024px and above.

**Consequences (testable):**
- At a viewport width below 768px, device cards render in a single full-width column.
- At a viewport width from 768px to 1023px, device cards render in a 2-column grid.
- At a viewport width of 1024px or more, device cards render in a 3-column grid.

---

## 5. Non-Goals (Explicit)

- Native iOS app (any release in this spec; architecture must not prevent it in future)
- Multi-user management UI — each user manages only their own Flats; no admin or cross-user views
- Real-time or near-real-time energy monitoring — this app is manual-read and file-upload driven
- Export or reporting features (CSV/PDF output)
- Direct smart plug API integration (Eve, Meross, Home Assistant, or any other) — file exports only
- Tariff comparison wizard
- Multi-tenant hosted version for other households (architecture must support it, UI must not target it)
- Support for smart plug brands other than Eve Home and Meross in the current scope (Release 1-4)

---

## 6. MVP Scope

### 6.1 Release 1 — Core Tracking (In Scope)

*Release 1 is a self-contained spreadsheet replacement: it delivers value on its own without any flat structure, smart plug data, or device history. Release 2 layers attribution on top and requires a populated Flat Structure and prior reading history to be meaningful.*

- Responsive mobile-first web app (Azure Static Web App frontend, .NET Azure Functions backend)
- Authentication via OIDC / Azure Entra ID (FR-1, FR-2, FR-3)
- First-use Onboarding flow (FR-4, FR-5, FR-6, FR-7)
- Manual Meter Reading entry — mobile-optimized (FR-8, FR-9)
- Tariff configuration with effective dates and period locking (FR-10, FR-11, FR-12, FR-13)
- KPI Dashboard: daily/weekly kWh and cost, projected monthly cost (FR-14, FR-15)
- Spike detection and trend visualization (FR-16, FR-17)
- Localization: `de-DE` and `en-US` (FR-40, FR-41, FR-42)

### 6.2 Release 2 — Consumption Decomposition (In Scope)

- Multi-Flat management and Flat switcher (FR-18, FR-19, FR-20)
- Flat Structure definition with default Room template (FR-21, FR-22, FR-23)
- Smart Plug import: Eve Home Excel and Meross CSV (FR-24, FR-25)
- Gap detection and linear interpolation with user notification (FR-26)
- Smart Plug reconciliation against Main Meter (FR-27)
- Import error categorization (FR-28)
- Device Registry: EU label and self-measured consumption (FR-29, FR-30, FR-31)
- Consumption Decomposition view with Residual and unavailability state (FR-32, FR-33, FR-34)
- Actionable Insights: standby offenders, replacement candidates, budget alerts, invoice deviation hints (FR-35, FR-36, FR-37, FR-43, FR-51)
- Scheduled and manual insight discovery with progress indicator (FR-38, FR-39)

### 6.3 Release 3 — UI & Behavior Consistency (In Scope)

*Added post-Epic-7 retro (Epic 8), extended with a requirements-level fix surfaced by Epic 9 hardening work. Cross-cutting interaction/layout fixes and corrections across existing Release 1/2 surfaces — no new feature area.*

- Flat Structure autosave for structural edits (FR-44)
- Save/cancel action placement and viewport visibility (FR-45)
- Dropdown/overlay visibility (FR-46)
- Responsive device card grid on tablet/desktop (FR-47)
- Meter-reset visual indicator on the trend chart (FR-56)

### 6.4 Release 4 — Device Lifecycle Attribution (In Scope)

*Added 2026-08-01 via architecture review + brainstorming session, not the original PRD or a retrospective — see `epics/epic-12-device-lifecycle-and-date-aware-decomposition-attribution.md`.*

- Device existence window gating estimated-device inclusion in Decomposition (FR-52)
- Device room-assignment history for date-aware Decomposition attribution (FR-53)
- Period total consumption summary alongside the Decomposition period selector (FR-54) — added 2026-08-01 via `bmad-correct-course`, sourced directly from Ralf, not from architecture review or brainstorming like FR-52/53
- Insight dismiss and reactivate (FR-55) — added 2026-08-01 via `bmad-correct-course`, sourced directly from Ralf; placed in Epic 12 despite living in the Actionable Insights FR cluster (§4.11), same split as FR-54/§4.10

### 6.5 Out of Scope (All Releases)

See §5 Non-Goals for the full explicit exclusions list.

---

## 7. Success Metrics

**Primary**

- **SM-1: Reading entry speed** — A Meter Reading submitted from a mobile browser is persisted and the KPI Dashboard reflects the new data in under 60 seconds of wall-clock time (user experience, network included). Target: 100% of entries in controlled conditions. Validates FR-8, FR-14, FR-15.
- **SM-2: Cost accuracy** — Dashboard cost figures match independently calculated values (correct Tariff × kWh) for the same period. Target: zero discrepancy on spot-check across three periods. Validates FR-13, FR-14.
- **SM-3: Decomposition correctness** — Attributed kWh + Residual = Main Meter total for any period with Smart Plug Data (within ±0.1 kWh for clean periods; within ±1.0 kWh for periods containing interpolated values). Validates FR-27, FR-32.
- **SM-4: Insight actionability** — After one month of use with Smart Plug exports uploaded, the Insights page names at least one specific Device with a quantified figure (kWh or €). Validates FR-35, FR-36.

**Secondary**

- **SM-5: Invoice predictability** — Rolling monthly projection is within 10% of the eventual annual invoice when calculated against the full year's readings. Validates FR-13 (period-accurate costing underlies any annual cost projection).
- **SM-6: Import gap handling** — An export with mid-period gaps completes without error and surfaces a gap notification. Validates FR-26.

**Counter-metrics (do not optimize)**

- **SM-C1: Insight volume** — Do not optimize for surfacing more Insights. An insight that doesn't name a specific Device or quantify a figure is noise, not signal. Counterbalances SM-4.
- **SM-C2: Feature depth over usability** — The 60-second reading entry (SM-1) must not be degraded by added UI complexity. Counterbalances any feature additions to the reading flow.

*Note: FR-44–47 and FR-56 (Release 3), and FR-54/FR-55 (Release 4), are process/quality fixes with no dedicated Success Metric, consistent with how hardening epics are excluded from §6's Release table. FR-52's existence-window gating and FR-53's date-aware attribution correctness are not yet covered by SM-3, which validates only the Main-Meter-reconciliation invariant (FR-27/FR-32) — extending SM-3 for temporal correctness is deferred to a future PRD revision once Release 4 has enough field data to define a meaningful target, rather than adding an unvalidated metric now.*

---

## 8. Cross-Cutting NFRs

### NFR-1: Performance

Three performance tiers govern server-side response behavior:

**Tier 1 — Synchronous, ≤ 2 seconds.** All standard server actions (Meter Reading submission, Tariff entry, Dashboard load, Flat management CRUD, Locale change, etc.) must respond within 2 seconds server-side. This supports the user-facing 60-second total wall-clock target for reading entry (SM-1).

**Tier 2 — Synchronous with UI hint, ≤ 30 seconds.** Actions that may exceed 2 seconds show a visible UI indicator that processing is still active. These actions must complete within 30 seconds (the Azure Functions default timeout). No action in the current feature set is expected to fall in this tier, but it is available for operations identified during architecture.

**Tier 3 — Fully background, notification on completion.** Operations that exceed or cannot be bounded within 30 seconds run entirely in the background. The user is not made to wait; instead, a notification is delivered when the operation completes. Smart Plug import processing (blob-triggered Function) and scheduled insight discovery fall in this tier. FR-39 covers the progress indicator for insight discovery specifically.

KPI Dashboard load for a Flat with up to 2 years of Readings: Tier 1 (≤ 2 seconds).

### NFR-2: Security and Data Isolation

- Each authenticated user's data is fully tenant-isolated by user ID. No cross-user data access is possible at any layer.
- All requests go through authentication (FR-1); no unauthenticated endpoints exist except the OIDC callback.
- Currency values are stored as fixed-decimal (C# `decimal`); no floating-point representation of monetary values anywhere in the data or API layers.

### NFR-3: Internationalization (i18n)

- All UI text goes through the localization framework. No hardcoded locale-specific strings, number formats, date formats, or currency symbols anywhere in the codebase.
- All data is stored and transmitted locale-neutrally: ISO 8601 datetimes with timezone offset (`+HH:MM`), decimal-point numbers, currency as fixed-decimal.
- All datetime values carry explicit timezone information. Scheduled jobs execute in UTC.
- The user's locale selection is stored server-side in the user profile. The `Accept-Language` header provides the default when no server-stored override exists. Locale formatting is applied only at render time via `Intl.NumberFormat` and `Intl.DateTimeFormat`.

### NFR-4: Reliability and Async Processing

- Smart Plug file uploads are stored in Azure Blob Storage and processed asynchronously by a blob-triggered Azure Function.
- Azure Storage Account queues handle lightweight internal messaging.
- Persistent data store: Azure SQL Basic DTU (~€5/month, 2 GB). Selected over Cosmos DB because the normalized relational data model (User → Flat → Rooms → Power Points → Devices) and the period-accurate tariff costing query (multi-table join with date-range correlated subquery) are SQL's native territory; Cosmos DB would require application-side joins with no benefit at this data volume.

---

## 9. Platform

- **Frontend:** Azure Static Web App; responsive, mobile-first.
- **Backend:** .NET Azure Functions.
- **File storage:** Azure Blob Storage (smart plug uploads).
- **Messaging:** Azure Storage Account queues.
- **Persistent store:** Azure SQL Basic DTU (~€5/month) — see NFR-4.
- **Auth:** OIDC / OAuth 2.0; Azure Entra ID as initial provider; provider-agnostic by configuration.
- **Primary form factor:** Mobile browser. Desktop browser supported as responsive layout.
- **No native app** in the current scope (Release 1-4; see §5 Non-Goals). A future native Swift iOS companion app (home screen widgets, quick entry shortcut) is a directional constraint on the product's API surface, not a scheduled feature — endpoint design must not assume web-only clients.
- **Hosting:** Owner's Azure subscription. Single-tenant per authenticated user (keyed by user ID). No cross-tenant access. No multi-user management UI.

---

## 10. Open Questions

*All open questions from the original PRD (Release 1/2) are resolved, with full decision history in `.decision-log.md`. Release 3 (§6.3) and Release 4 (§6.4) decisions were made after PRD finalization, via retro/architecture/brainstorming/correct-course sessions rather than a dedicated Open Questions cycle — they are narrated inline in §6.3/§6.4's provenance notes and in each FR's description, not tracked as separate decision-log entries.*

**Resolved in UX session (2026-06-20):**

- **Q-3 — Tariff lock enforcement UX:** Price fields on a locked Tariff entry render inline as read-only (greyed, with a lock icon and "Locked — contract active until [month year]"). No dialog or tap-to-reveal. Non-price fields remain editable. *(UX D-22)*
- **Q-4 — Budget settings placement:** The Planned Annual Spend (for FR-37 budget pressure alerts) is collected during Onboarding in Step 2 alongside the Annual kWh Baseline (auto-derived, editable). In Settings it lives near the Tariff configuration, not in a separate budget section. *(UX D-23)*
- **Q-6 — Flat deletion confirmation UX:** User must type the exact Flat name to enable the Delete action. Friction matches irreversibility. *(UX D-24)*

**Resolved in architecture session (2026-06-21):**

- **Q-7 — Reconciliation tolerance for interpolated periods:** ±1.0 kWh (10× the clean-data tolerance), reflecting interpolation uncertainty. Governs FR-27, FR-32, SM-3. *(Architecture AD-2)*
- **Q-8 — Invoice deviation significance threshold:** ±10% of the Annual kWh Baseline. Governs FR-43. *(Architecture)*
- **FR-35 (standby data resolution):** Eve Home raw ~10-minute interval rows are retained at import (`SmartPlugIntervalData` table). Standby detection operates on this interval data for Eve Home plugs. Meross plugs are excluded from standby detection — Meross exports provide daily aggregates only. *(Architecture AD-2)*

---

## 11. Assumptions Index

*Resolved assumptions (A-1: performance tiers; A-7: browser-local Locale superseded by AD-18 server-stored override) are recorded in `.decision-log.md`. All remaining assumptions confirmed:*

- **[A-2]** A single authenticated user manages one or more Flats; no Flat sharing between users in the current scope (Release 1-4). Source: §5, Constraints.
- **[A-3]** "Flat" maps to a single European-style dwelling unit; a user may own or rent multiple. Source: SPEC.
- **[A-4]** The four-level hierarchy (Flat → Rooms → Power Points → Devices) covers the realistic physical layout. Source: SPEC.
- **[A-5]** Scheduled insight discovery (02:00 daily) executes in UTC. Source: NFR-3.
- **[A-6]** Interpolation cap (7-day pre-gap average) applies uniformly to both Eve Home and Meross imports. Source: smart-plug-formats.md.
- **[A-8]** *Resolved.* Eve Home retains raw ~10-minute interval rows at import (`SmartPlugIntervalData`); standby detection operates on this data. Meross is excluded from standby detection — daily aggregates only. Source: architecture AD-2.
- **[A-9]** *Resolved.* Invoice deviation significance threshold: ±10% of Annual kWh Baseline. Source: architecture, FR-43.
- **[A-10]** *Resolved.* Reconciliation tolerance for periods containing interpolated values: ±1.0 kWh. Source: architecture, FR-27, FR-32, SM-3.
- **[A-11]** Household-size kWh presets (FR-5: 1,500 / 2,500 / 3,500 / 4,250 kWh for 1–4 persons) are a rough PM judgment call made during initial Discovery, not sourced from an external dataset or citation. Source: PM judgment, 2026-06-20.
