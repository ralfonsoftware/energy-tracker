# Epic 12: Device Lifecycle & Date-Aware Decomposition Attribution

Devices gain an existence window (when they started/stopped consuming power) and a room-assignment history, so Decomposition figures stay accurate as the user's device inventory and physical layout change over time — closing a gap where `Device.PurchaseDate` (FR-29) was captured but never consulted, and where room attribution only ever reflected the *current* Flat Structure snapshot regardless of query period. Sourced from an architecture review and `_bmad-output/brainstorming/brainstorming-session-2026-08-01-14-56.md`, not from the original PRD or a retrospective. Plug/strip-hardware (`PlugId`) relocation tracking is explicitly out of scope — deliberately simplified to manual delete-and-re-add via the existing Flat Structure editor, not a gap.

**FRs covered:** FR-52, FR-53, FR-54, FR-55

---

## Story 12.1: Device Existence Window — Estimated-Consumption Gating

As a user,
I want a device I've just added to not be counted as consuming power before I actually installed it (and a device I've decommissioned to stop being counted afterward),
So that my Decomposition figures for past and future periods stay accurate as my device inventory changes.

**Acceptance Criteria:**

**Given** `DeviceConfiguration.cs`,
**When** reviewed,
**Then** `Device` gains two nullable columns: `InUseSince` (`DateTimeOffset?`) and `DecommissionedDate` (`DateTimeOffset?`); Fluent API only; the migration sets no default — both are `null` for every pre-existing row.

**Given** `DecompositionEngine.cs`'s standalone-device estimate path (`ResolveStandaloneApproach` / `dailyEstimate * dayCount`),
**When** a Device has `ConsumptionApproach != None`,
**Then** its estimated daily kWh is counted only for days within `[InUseSince, DecommissionedDate]` (either bound open-ended if unset) intersected with `[startDate, endDate]`; days outside that window contribute zero kWh/cost for that device.

**Given** a Device with both `InUseSince` and `DecommissionedDate` left unset,
**When** Decomposition is computed,
**Then** behavior is byte-for-byte unchanged from today — full-period inclusion, preserving backward compatibility for all existing data with no migration-driven behavior change.

**Given** Smart Power Strip sub-devices (`BuildSmartStripDecomposition`'s pool math),
**When** a sub-device has `InUseSince`/`DecommissionedDate` set,
**Then** this story does **not** apply the clamp to strip pool math — sub-device shares continue using whole-period estimates; date-sliced strip pooling is explicitly deferred as a follow-up, not silently unsupported.

**Given** the Flat Structure editor's Device form,
**When** adding or editing a Device,
**Then** an optional "In use since" date field is available, pre-filled with today's date as a suggested default when adding a new Device (editable/clearable, matching FR-52's default-behavior requirement).

**Given** `DecompositionEngineTests.cs`,
**When** run,
**Then** tests cover: `InUseSince` mid-period (partial inclusion), `DecommissionedDate` mid-period (partial inclusion), neither set (full inclusion, regression guard), and a strip sub-device with dates set (pool math unaffected, per the exclusion above).

---

## Story 12.2: Device Room-Assignment History — Date-Aware Decomposition Attribution

As a user,
I want the app to remember which room a device was in when I move it,
So that my Decomposition figures correctly split its consumption across the rooms it actually occupied during a query period, without me having to enter any dates manually.

**Acceptance Criteria:**

**Given** a new `DeviceAssignmentPeriod` entity,
**When** reviewed,
**Then** `DeviceAssignmentPeriodConfiguration.cs` defines `Id` (guid PK), `DeviceId` (FK, cascade delete), `PowerPointId` (FK), `FlatId` (FK, cascade delete), `From` (`DateTimeOffset`), `To` (`DateTimeOffset?`, `null` = current/open period); Fluent API only; index on `(DeviceId, From)`.

**Given** the migration introducing this table,
**When** applied,
**Then** it backfills one open-ended period per existing `Device` — `From = Device.InUseSince ?? Flat.CreatedAt`, `PowerPointId` = the device's current value, `To = null` — so no existing device disappears from Decomposition once room resolution switches to period-based lookup.

**Given** `UpdateFlatStructureFunction`'s full-replace save (`PUT /api/v1/flats/{flatId}/structure`),
**When** a Device's incoming `PowerPointId` differs from its currently persisted value,
**Then** the open `DeviceAssignmentPeriod` for that Device is closed (`To = now`) and a new one is inserted (`From = now`, new `PowerPointId`) — fully automatic, no UI or manual date entry; a brand-new Device gets its first period seeded with `From = Device.InUseSince ?? now`.

**Given** `DecompositionEngine.cs`'s room-grouping,
**When** computing a period,
**Then** each Device's daily kWh/cost (both measured and Story 12.1's window-gated estimated) is attributed per day to whichever Room its resolved `PowerPointId` belongs to on that day, resolved via the same "latest period with `From <= date`" idiom already established by `TariffResolution.Resolve`.

**Given** a Device whose room changed mid-period,
**When** the Decomposition response is built,
**Then** the Device appears under each Room it occupied, each with its own `DeviceDecomposition` entry scoped to the days it belonged there — partial totals across Rooms sum to the Device's correct period total.

**Given** Smart Power Strip sub-devices,
**When** a sub-device's own assignment doesn't change mid-period (the normal case),
**Then** this story's change is transparent to strip pool math; a sub-device moving strips mid-period is explicitly out of scope (same rationale as Story 12.1), tracked as a follow-up.

**Given** extended `DecompositionEngineTests.cs` plus new tests for the assignment-period resolution helper,
**When** run,
**Then** tests cover: no room change (unaffected, regression guard); one mid-period move (correct per-room split, totals preserved); pre-existing device with only its backfilled period (unchanged behavior); migration backfill correctness.

---

## Story 12.3: Decomposition Tab — Period Total Consumption Summary

As a user,
I want to see my total kWh and cost for the currently selected period displayed alongside the period selector,
So that I can easily relate the individual Room and Device breakdown figures to the whole period total.

**Acceptance Criteria:**

**Given** `DecompositionTab.tsx` renders successfully (`IsUnavailable = false`),
**When** the period's data loads,
**Then** a Period Total summary tile renders directly alongside/below `PeriodSelector.tsx`, above the Residual card, showing `totalKwh` and `totalCost` for the selected period — reusing the existing glass-surface `KpiTile` visual pattern; no new API call, purely consumes the already-fetched `DecompositionResponse.totalKwh`/`totalCost` fields (both already present in the contract since Story 7.1; `totalCost` is currently unused in the tab).

**Given** the query is loading,
**When** the tab renders,
**Then** the Period Total tile shows `KpiTile`'s skeleton state, sized to avoid layout shift when data arrives.

**Given** `DecompositionResponse.IsUnavailable = true`,
**When** the unavailable state renders,
**Then** the Period Total tile is **not** shown — consistent with FR-34 (no partial/zero figures for unavailable periods) and the existing Residual-card suppression behavior.

**Given** the active Locale,
**When** `totalKwh`/`totalCost` render,
**Then** values are formatted via the same `Intl.NumberFormat` helpers already used elsewhere in the Decomposition feature — no hardcoded formatting.

**Given** `DecompositionTab.test.tsx`,
**When** run,
**Then** tests cover: tile renders correct kWh/cost on success; tile shows skeleton while loading; tile is absent when `IsUnavailable = true`.

---

## Story 12.4: Insight Dismiss and Reactivate

As a user,
I want to dismiss an Insight I've already acted on or don't care about, and bring it back later if I change my mind,
So that my Insights view stays focused on things that still need my attention, without losing the ability to undo a dismissal by mistake.

**Acceptance Criteria:**

**Given** `Insight.cs`,
**When** reviewed,
**Then** it gains two new columns: `IsDismissed` (`bool`, not null, default `false`) and `DismissedAt` (`DateTimeOffset?`); Fluent API only via `InsightConfiguration.cs`; the migration sets `IsDismissed = false` for all pre-existing rows — no behavior change for undismissed data.

**Given** a new `PatchInsightFunction` (`PATCH v1/flats/{flatId}/insights/{insightId}`, modeled on `PatchFlatFunction.cs`'s tenant-check + body-parse shape),
**When** the request body sets `isDismissed: true`,
**Then** the targeted `Insight` row is updated with `IsDismissed = true, DismissedAt = now`; when `isDismissed: false`, `IsDismissed = false, DismissedAt = null`. Tenant check: `flatId` must belong to the resolved `userId`, and `insightId` must belong to `flatId` — 403/404 otherwise.

**Given** `GetInsightsFunction.cs`'s per-identity grouping,
**When** the default request is made (no `status` param, or `status=active`),
**Then** rows with `IsDismissed = true` are excluded from the per-identity selection; when `status=dismissed` is passed, only the current `IsDismissed = true` row per identity is returned, using the same grouping logic.

**Given** `InsightDeduplication.IsNearDuplicateOfMostRecentAsync` and its four call sites (`StandbyDetector`, `ReplacementDetector`, `BudgetAlertDetector`, `InvoiceDeviationDetector`),
**When** the most-recently-stored row for a `(FlatId, Type, DeviceId)` identity has `IsDismissed = true`,
**Then** no new `Insight` row is persisted for that identity regardless of FR-51's 5% tolerance comparison — the identity stays suppressed until reactivated.

**Given** a reactivated Insight (`IsDismissed` flipped back to `false`),
**When** a subsequent discovery run evaluates that identity,
**Then** FR-51's normal 5%-tolerance comparison resumes — a new row persists only if the new figure differs by more than 5% from the reactivated row's stored value.

**Given** `InsightCard.tsx` (currently a pure display component with no action row) and `InsightsTab.tsx`,
**When** implemented,
**Then** `InsightCard` gains a dismiss icon button in the default "Active" view (aria-label per UX-DR11) and a reactivate icon button when rendered in a "Dismissed" view; `InsightsTab` gains an Active/Dismissed toggle that switches the query param passed to `useInsights` and determines which action button renders.

**Given** `insightsApi.ts` and `useInsights.ts`,
**When** implemented,
**Then** two new mutation hooks are added (`useDismissInsight`, `useReactivateInsight`), each calling the new PATCH endpoint and invalidating `['insights', flatId]` in `onSuccess`, per the project's standard mutation-hook pattern.

**Given** backend and frontend test suites,
**When** run,
**Then** tests cover: dismissed identity suppresses persistence regardless of tolerance (`InsightDeduplicationTests.cs`); dismiss/reactivate toggle and tenant-isolation 403 (`PatchInsightFunction` tests); active vs dismissed filtering (`GetInsightsFunction` tests); toggle switches view and correct action button renders per state (`InsightsTab`/`InsightCard` tests).
