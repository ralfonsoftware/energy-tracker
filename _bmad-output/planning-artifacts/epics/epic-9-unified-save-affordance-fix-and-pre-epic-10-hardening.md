# Epic 9: Unified Save-Affordance Fix & Pre-Epic-10 Hardening

**Part 1 (Stories 9.1–9.3) — Save/cancel actions across the app follow one consistent visual pattern and placement rule, and the power-point/device-edit save action is actually reachable without scrolling to the end of the page, on every supported browser.** This closes the residual gap Epic 8 left open — sourced from Epic 8's own retrospective (Challenges #1–#2, Action Items #1–#2) and a subsequent investigation (`_bmad-output/implementation-artifacts/investigations/power-points-scroll-visibility-investigation.md`) that found Story 8.2's `StickyActionBar` does not actually satisfy FR-45 as written: it is structurally incapable of remaining visible without scrolling on any Power Point list long enough to exceed the viewport.

**Part 2 (Stories 9.4–9.13) — A prioritized batch of deferred technical-debt and consistency items, cleared before Epic 10 (Actionable Insights) begins.** Sourced from Epic 8 retro Action Item #3 (full audit of `deferred-work.md`) and Action Item #4 (Ralf prioritizes which items get pulled in). Follows the same "Pre-Epic-N Hardening" pattern established by Story 6.0, extended to epic scope here given the size of the batch. Several stories in this part require a product/policy decision from Ralf before implementation can proceed — each such story states the decision explicitly as its first AC, gated before any code change, matching this project's established design-gate pattern (Stories 8.4, 9.1).

## Story 9.1: Unified Save-Affordance Design Decision

As a user,
I want the Save action to look and behave the same way everywhere in Flat Structure settings — the room list, a room's Power Point list, and the Device edit screen,
So that I recognize and trust the save affordance regardless of which screen I'm on.

**Note (2026-07-18, design approved — Sally proposed, Ralf approved; full spec at `.decision-log.md#D-45`):** one visual language, two placement rules. Full-screen edit contexts (`RoomEditor.tsx`, `DeviceEditor.tsx`) reuse `StickyActionBar`'s existing glass-pill styling and offset math verbatim — only the positioning mechanism changes from `position: sticky` to `position: fixed`, the same proven mechanism `BottomTabBar.tsx` already uses (fixed, bottom-anchored, safe-area-hardened since Story 5.5). The room list keeps its per-row inline placement (collapsing many independent per-room saves into one global bar would be an interaction regression) but is reskinned to the same border/background/accent/spinner treatment.

**Acceptance Criteria:**

**Given** the approved D-45 design,
**When** implemented for `RoomEditor.tsx` and `DeviceEditor.tsx` (Story 9.2),
**Then** the action bar uses `position: fixed; bottom: calc(84px + safe-area-inset-bottom)` on phone / `bottom: 0` on tablet+ (identical offsets to today's `StickyActionBar`, clearing `BottomTabBar`'s 72px + 12px gap), with the same glass-pill visual styling already shipped — no other visual changes.

**Given** the confirmed root cause in `power-points-scroll-visibility-investigation.md` that `position: sticky` on a trailing sibling is mathematically incapable of remaining visible without scrolling once content exceeds the viewport by more than its buffer (live-reproduced: invisible for ~93% of scroll range on a 5-Power-Point room),
**When** implemented,
**Then** `position: fixed` structurally guarantees visibility regardless of content length — verified by Story 9.2's own manual Chrome + Safari check against the investigation's repro case.

**Given** FR-45 ("every save/cancel action... remains within the visible viewport without requiring the user to scroll to find it, across all supported browsers, including Safari"),
**When** implemented,
**Then** the fixed-position mechanism satisfies FR-45 as written, including the Safari clause.

## Story 9.2: Fix RoomEditor & DeviceEditor Save Bar (Structural)

As a user,
I want the Save action on a room's Power Point list and the Cancel/Save row on the Device edit screen to actually remain visible without scrolling, no matter how long the list is or which browser I'm using,
So that I never have to hunt for whether my change was committed.

**Acceptance Criteria:**

**Given** `RoomEditor.tsx`'s and `DeviceEditor.tsx`'s current `StickyActionBar` usage (`StickyActionBar.tsx:9-14`, rendered as a sibling after the scrollable content with only a `pb-32` buffer — confirmed structurally broken by live reproduction against the deployed app: the Save bar is invisible for the first ~93% of scroll range on a 5-Power-Point room, only appearing once scrolled essentially to the end),
**When** the fix is implemented per Story 9.1's approved design,
**Then** the save action remains visible in the viewport at every scroll position, for any number of Power Points/devices in the list — verified by reproducing the investigation's specific repro case (a room with enough Power Points to exceed viewport height) and confirming the Save button is visible without any scrolling.

**Given** both `RoomEditor.tsx` (Power Point save) and `DeviceEditor.tsx` (Cancel/Save) currently share the same `StickyActionBar` component and therefore share the same defect,
**When** the fix is implemented,
**Then** both components adopt the same replacement mechanism from Story 9.1 — not two independently-patched solutions — so future changes to the save-bar pattern only need to be made once.

**Given** this class of layout/scroll-positioning bug is invisible to `jsdom`-based automated tests (confirmed both by this epic's own investigation and by Epic 8 retro Challenge #5),
**When** the fix is complete,
**Then** it is manually verified in both Chrome and real Safari (not only Chromium-based browsers) against a content length equivalent to the investigation's repro case — this closes the open, Safari-unconfirmed Hypothesis 1 from `power-points-scroll-visibility-investigation.md` (the Device-edit Safari blank-space variant) by direct observation.

**Given** the existing `pb-32` spacer comments in `RoomEditor.tsx:57` and `DeviceEditor.tsx:86` that document the old buffer-based intent ("pb-32 clears StickyActionBar's rendered height... so the sticky bar doesn't cover the last field/power point at full scroll"),
**When** the new mechanism is implemented,
**Then** those comments are removed or rewritten to describe the actual mechanism in use — not left describing an approach that's no longer there.

## Story 9.3: Align Room-List Save Affordance

As a user,
I want the room-list view's Save actions — the page-level "Speichern" button and each room's inline Save icon — to look and feel consistent with the Power Point and Device edit save actions,
So that saving works the same way everywhere in Flat Structure settings.

**Acceptance Criteria:**

**Given** `FlatStructureEditor.tsx`'s page-level Save button (`:268-276`) and per-room inline Save icon buttons (`:356-373`), which are functionally correct today — not scroll-broken, confirmed by the investigation (Finding 4: this view's save buttons are structurally separate from `StickyActionBar` and were never meant to be sticky) — but visually distinct from the Power Point/Device-edit save pattern (Epic 8 retro Challenge #1),
**When** restyled per Story 9.1's approved pattern,
**Then** their visual treatment (styling, placement) matches the unified pattern without changing their existing working behavior — autosave-on-room-add/delete (Story 8.1), per-room save-on-dirty, and page-level batched save all continue to function exactly as before.

**Given** this story only changes presentation, not behavior,
**When** implemented,
**Then** no changes are made to `handleSave`, `handleSaveRoom`, `isRoomDirty`, or any other logic in `FlatStructureEditor.tsx` — this is a restyle, not a behavior change; existing tests for save/dirty-state logic continue to pass unmodified.

## Story 9.4: Smart Power Strip Remainder Formula — Spec Amendment & Fix

As a user,
I want unconfigured devices on a Smart Power Strip to actually receive a meaningful share of the strip's unattributed consumption,
So that the Decomposition view doesn't silently hide consumption AC3 itself claims is being shared.

**Acceptance Criteria:**

**Given** `DecompositionEngine.cs:170-189`'s AC3/D-44 proportional-split formula (`device_share = (device_estimated_kWh / sumConfiguredEstimates) * stripMeasuredTotal`), under which configured shares always sum to exactly `stripMeasuredTotal` by construction — making `remainder = stripMeasuredTotal - sum_of_configured_shares` ≈0 in every mixed strip, contradicting AC3's own prose that unconfigured devices "share the remainder equally" — confirmed as a spec-formula property, not an implementation bug,
**When** implemented in `DecompositionEngine.cs`,
**Then** it follows the approved blended-nominal-weight D-44 amendment (`.decision-log.md#D-44`, 2026-07-18): each unconfigured device gets a nominal weight equal to the average of the strip's configured devices' `device_estimated_kWh` (0 if none are configured); all devices (configured, at their real estimate, and unconfigured, at the nominal weight) are pooled into one proportional split of `stripMeasuredTotal`; when the strip has zero configured devices, every device receives an equal split of `stripMeasuredTotal` (the pre-amendment behavior for that case, preserved unchanged).

**Given** the approved formula,
**When** implemented,
**Then** existing configured-device-only strip behavior (fully configured, no unconfigured siblings) is unchanged, a fully-unconfigured strip still splits equally, and a mixed strip now gives every unconfigured device an identical non-zero share while all shares still sum to exactly `stripMeasuredTotal`; regression tests cover all three cases (fully configured, fully unconfigured, mixed).

## Story 9.5: FlatStructureEditor Deep-Link Addressability

As a user,
I want the "Go to settings" chip on an unconfigured Smart Power Strip sub-device to take me directly to that device's settings,
So that I don't have to manually hunt through the room list to find what I was just looking at.

**Acceptance Criteria:**

**Given** `FlatStructureEditor.tsx`'s internal `view` state is keyed by client-generated `crypto.randomUUID()` draft keys that only exist after `useFlatStructure` data loads, with no existing mechanism to target a specific PowerPoint from outside the editor (confirmed a real structural blocker by architecture review, not a shortcut — `deferred-work.md:339`),
**When** this story is implemented,
**Then** the editor accepts a stable, route-addressable identifier via a query parameter (e.g. `?powerPointId={id}`, using the server-side PowerPoint id) and, once `useFlatStructure` data resolves, automatically opens the room/PowerPoint view matching that id.

**Given** `SmartStripCard.tsx`'s "Go to settings" chip on unconfigured sub-devices (Story 7.3 AC7),
**When** clicked,
**Then** it navigates to `/settings/structure?powerPointId={id}` instead of the bare room list, landing the user directly on the correct PowerPoint rather than requiring manual navigation.

**Given** a `powerPointId` that no longer exists (e.g. a stale link after deletion),
**When** the editor loads,
**Then** it falls back gracefully to the room list view — no error, no crash, no dead-end blank state.

## Story 9.6: Consistent Treatment for Unmeasured Devices — Standalone vs. Sub-Device

As a user,
I want a standalone device with no consumption data configured to be visually flagged the same way an unconfigured Smart Power Strip sub-device already is,
So that I don't lose track of a device just because it isn't on a strip.

**Note (2026-07-18, design approved — Sally proposed, Ralf approved; full spec at `.decision-log.md#D-46`):** standalone `approach === 'None'` devices render as a ghost card in `RoomCard.tsx`'s existing device grid (sized like `DeviceCard`'s compact/estimated variant), at `opacity: 0.45` — the same value `SmartStripCard`'s unconfigured sub-device rows already use. No kWh/cost figure is shown (unlike a strip's unconfigured sub-device, a standalone device has no measurement or estimate at all — showing a number would be dishonest). Copy is voice-matched to `DeviceEditor.tsx`'s existing "Configure consumption profile to include this device in Decomposition" note, as a new key in the `decomposition` i18n namespace (not a cross-namespace reference, per this project's per-feature-namespace convention).

**Acceptance Criteria:**

**Given** standalone devices with `approach === 'None'` and no plug are silently excluded from the Room Card device list entirely (Story 7.3 AC2), while the identical "no measurement configured" state on a Smart Power Strip sub-device shows a dimmed row plus a "Go to settings" hint (AC7) — a real UX inconsistency flagged during Story 7.3's review (`deferred-work.md:338`),
**When** implemented in `RoomCard.tsx` and `partitionAndSortDevices`,
**Then** `isNoneApproachNonStrip` devices are no longer filtered out of the grid entirely — they render as the approved ghost card (name dimmed at `opacity: 0.45`, hint text in `text-tertiary`, "Configure consumption profile" pill button), sized and positioned as a normal sibling in the existing responsive device grid (Story 8.4).

**Given** the ghost card's "Configure consumption profile" button,
**When** clicked,
**Then** it deep-links via Story 9.5's `?powerPointId=` mechanism directly to that device's edit screen — not just the room list.

**Given** a room where every device is a standalone `None`-approach device (the pre-existing "Direct consumption" fallback path),
**When** rendered,
**Then** the existing `isDirectConsumptionOnly` fallback behavior is preserved unchanged — this story only affects rooms with at least one measured/estimated device alongside unmeasured standalone ones.

## Story 9.7: Shared Decimal-Precision Validator Extension Method

As the team maintaining this app,
I want the decimal-precision validation rule extracted into one shared helper instead of ten hardcoded call sites,
So that a future change to the precision policy can't drift across files.

**Note (2026-07-18, decision resolved during Epic 9 planning):** the item that originally scoped this story (`deferred-work.md`, pre-dating Story 6.0) claimed decimal values passing validation could still exceed their DB column's scale, causing response/persisted-value divergence. Verified false as of this planning session — Story 6.0's decimal-precision-validation-policy pass (2026-07-05/06) already applied `.PrecisionScale(18, N, true)` to every field, matching each field's DB column scale exactly (`PricePerKwh` 18,6 ↔ `decimal(18,6)`; `MonthlyBaseFee`/`KwhValue`/`AnnualKwhBaseline`/`PlannedAnnualSpend` 18,4 ↔ `decimal(18,4)`, confirmed against `TariffConfiguration.cs`, `MeterReadingConfiguration.cs`, `FlatConfiguration.cs`). Values with excess precision are already rejected with 400 before reaching the DB — no correctness bug remains. This story is rescoped as a pure DRY refactor.

**Acceptance Criteria:**

**Given** ten validator files each hardcode `.PrecisionScale(18, N, true)` inline with no shared constant or extension method (`OnboardingValidator.cs`, `CreateFlatValidator.cs`, `PatchFlatValidator.cs`, `TariffValidator.cs`, `PatchTariffValidator.cs`, `ReadingValidator.cs`, `PatchReadingValidator.cs`, `UpdateFlatStructureValidator.cs`),
**When** implemented,
**Then** a shared extension method (e.g. `RuleFor(...).DecimalPrecision(scale)`, defaulting to precision 18 since every current field uses it) replaces every inline `.PrecisionScale(18, N, true)` call, with identical validation behavior (verified by existing tests continuing to pass unmodified — this is a refactor, not a behavior change).

## Story 9.8: Meter Reset Visual Indicator

As a user,
I want a day where my meter reading dropped below the prior reading to be clearly labeled as a meter reset in my trend chart, instead of silently showing zero consumption with no explanation,
So that replacing a meter doesn't make my history look wrong or broken.

**Note (2026-07-18, decision resolved during Epic 9 planning):** `IsCorrected`/`OriginalKwhValue` (Story 3.6) track user-initiated PATCH corrections, not physical meter resets — a meter reset is a normal new reading that happens to be lower than the prior one, so that field is never populated for this case and can't be used to "bridge the gap" as originally framed. Decision: keep `KpiCalculator.cs`'s existing zero-clamp behavior for consumption/cost math (safe default, no schema or submission-flow change) and add a visual "meter reset" indicator to the trend chart wherever a clamp was applied — detectable directly from existing data (`currentReading < previousReading`), no new field needed. This also aligns with the existing "lower than your last reading" submission-time warning (`.decision-log.md`, Enter Reading sheet spec) — the badge is the KPI-side counterpart to that same signal.

**Acceptance Criteria:**

**Given** `KpiCalculator.cs`'s `BuildDailySeries` clamps a negative inter-reading delta to 0 kWh/0 cost for that interval — currently with no visual signal that this happened,
**When** implemented,
**Then** each daily consumption entry gains a boolean flag (e.g. `wasMeterReset`, computed in-memory from `currentReading < previousReading`, not persisted) alongside the existing `IsInterpolated`-style metadata already returned by the API.

**Given** the flag is present in `TrendChart`'s data,
**When** rendered,
**Then** a day flagged `wasMeterReset` shows a distinct visual treatment (e.g. a small reset icon or hatched bar, distinguishable from both a normal bar and an amber spike bar) with an accessible text equivalent (matching the existing WCAG 1.4.1 concern already flagged for spike bars, `deferred-work.md`), and a regression test covers both the backend flag computation and the frontend rendering.

## Story 9.9: Reject Explicit Null for AnnualKwhBaseline on PATCH

As a developer integrating with this API,
I want an explicit attempt to clear `annualKwhBaseline` via PATCH to return a clear error instead of silently doing nothing,
So that I'm not left guessing why my update had no effect.

**Note (2026-07-18, decision resolved during Epic 9 planning):** `AnnualKwhBaseline` is `.IsRequired()`/non-nullable at the DB level (`FlatConfiguration.cs`), so adopting `PlannedAnnualSpend`'s "explicit null clears the field" pattern isn't available without making the column nullable end-to-end and null-handling every consumer (dashboard budget-delta math, KPI baseline comparisons) — out of scope absent a real product need for an unset baseline. Decision: keep the field required, but reject an explicit `null` with a 400 instead of silently ignoring it; an omitted field still means "leave unchanged," matching every other PATCH field's convention.

**Acceptance Criteria:**

**Given** `PatchFlatFunction.cs`'s `AnnualKwhBaseline` field currently has no `*Provided` flag, so `{"annualKwhBaseline": null}` silently no-ops rather than erroring, pinned as current behavior by `PatchFlatFunctionTests.RunAsync_AnnualKwhBaselineExplicitNull_SilentlyLeavesExistingValueUnchanged`,
**When** implemented,
**Then** the field is distinguished as omitted (absent from the JSON body — leaves the existing value unchanged, as today) vs. explicitly present and `null` (returns 400 Problem Details: "annualKwhBaseline cannot be cleared — it is a required field").

**Given** the fix,
**When** implemented,
**Then** `PatchFlatFunctionTests.cs`'s existing pinning test is renamed/updated to assert the new 400 response instead of the silent no-op, and a new test confirms omitting the field entirely still leaves the existing value unchanged.

## Story 9.10: Optimistic-Concurrency Hardening — Flat, Structure, Reading & Tariff

As the team maintaining this app,
I want concurrent edits to Flat structure, meter readings, and tariffs to fail loudly instead of silently overwriting each other,
So that a race between two sessions/tabs never loses data without anyone noticing.

**Acceptance Criteria:**

**Given** no optimistic-concurrency control exists for `Flat`, Room/PowerPoint/Device structure edits, `MeterReading` corrections, or `Tariff` PATCH updates — a recurring gap across `DeleteFlatFunction.cs`, `UpdateFlatStructureFunction.cs`, `PatchReadingFunction.cs`, `PatchTariffFunction.cs`, `flatStructureApi.ts` — explicitly **not** resolved by Story 6.1's `RowVersion` addition, which was deliberately scoped to `ImportJob`/`SmartPlugDailyData`/`SmartPlugIntervalData` only (`sprint-change-proposal-2026-07-05-pre-epic6-hardening.md`),
**When** this story is implemented,
**Then** a `RowVersion` concurrency-token column is added to `Flat`, `Room`, `PowerPoint`, `Device`, `MeterReading`, and `Tariff` entities, following the same pattern already established for the Epic 6 tables.

**Given** the new `RowVersion` columns,
**When** a PATCH/PUT/DELETE request's `RowVersion` doesn't match the current DB value,
**Then** the request fails with a 409 Conflict Problem Details response instead of silently last-write-wins, across `DeleteFlatFunction`, `UpdateFlatStructureFunction`, `PatchReadingFunction`, `PatchTariffFunction`, and any other Function writing to these entities.

**Given** this is a cross-cutting hardening pass,
**When** implemented,
**Then** one consolidated migration adds all new `RowVersion` columns together (not six separate migrations), and regression tests cover at least one concurrent-conflict scenario per entity type.

## Story 9.11: Regression Test — KwhValue Precision Rejection

**Note (2026-07-18, verified during Epic 9 planning — same stale premise as 9.7):** `deferred-work.md:149`'s claim (response reflects an unrounded in-memory value while the DB stores a rounded one) is no longer true. `SubmitReadingFunction.cs:64` calls `ReadingValidator.ValidateAsync` before any save, and `ReadingValidator.cs:11` already has `.PrecisionScale(18, 4, true)` on `KwhValue`, matching `MeterReadingConfiguration.cs`'s `decimal(18,4)` column exactly. A reading with more than 4 decimal places is rejected with 400 before `SaveChangesAsync` is ever called — no divergence between response and persisted value can occur. This story is rescoped from a fix to a regression test, since the behavior is correct but unverified by any existing test.

As the team maintaining this app,
I want a regression test proving a >4-decimal-place reading is rejected rather than silently accepted and rounded,
So that this correctness property can't silently regress if `ReadingValidator.cs` is ever touched.

**Acceptance Criteria:**

**Given** `ReadingValidator.cs`'s existing `.PrecisionScale(18, 4, true)` rule on `KwhValue`,
**When** a `SubmitReadingFunction` request is made with a `KwhValue` carrying more than 4 non-trailing-zero decimal places (e.g. `12.34567`),
**Then** the response is 400 Problem Details with the existing "kwhValue must have at most 4 decimal places" message, and no `MeterReading` row is written — verified by a new test in `api.Tests/Features/Readings/SubmitReadingTests.cs`.

## Story 9.12: Audit ContractStartDate Migration for Silent Data Loss

As the team maintaining this app,
I want confirmation that no flat's tariff history was silently altered by the ContractStartDate consolidation migration,
So that a known-unaudited risk doesn't sit open indefinitely.

**Acceptance Criteria:**

**Given** migration `20260703114416_ConsolidateTariffContractStartDate.cs` backfilled `ContractStartDate` from `EffectiveDate` only `WHERE ContractStartDate IS NULL`, silently discarding the old `EffectiveDate` value for any tariff that already had a non-null `ContractStartDate` before the migration — ruled out as the cause of one specific prior investigation but never audited across all flats (`deferred-work.md:266`),
**When** this story is implemented,
**Then** a one-time SQL audit compares pre-migration `EffectiveDate` history (from backup or point-in-time restore, if available) against current `ContractStartDate` values for every `Tariff` row, confirming whether any flat's data was silently altered.

**Given** the audit result,
**When** complete,
**Then** findings are documented — either "no data loss confirmed" or a remediation plan for any affected flat — and this `deferred-work.md` entry is closed.

## Story 9.13: Catch-All 404 Route

As a user,
I want to see a clear "page not found" message if I navigate to a URL that doesn't exist,
So that I'm not left staring at a blank screen wondering if the app is broken.

**Acceptance Criteria:**

**Given** no catch-all `path: '*'` route exists in the React Router configuration, so unknown URLs render a blank `AppShell` with no feedback — flagged three separate times since Epic 1 (`deferred-work.md:53,73`) and never picked up,
**When** this story is implemented,
**Then** a catch-all route renders a simple "Page not found" view with a link back to the Dashboard, styled consistent with the app's existing empty-state visual patterns.

**Given** this route,
**When** a user navigates to any unmatched path, whether authenticated or unauthenticated,
**Then** the 404 view renders correctly in both auth states without crashing or infinite-redirecting.
