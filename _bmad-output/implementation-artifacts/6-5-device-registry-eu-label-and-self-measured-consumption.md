---
baseline_commit: 613301da2e805af8e927d2426715ec8bcadb66a1
---

# Story 6.5: Device Registry — EU Label & Self-Measured Consumption

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to configure a consumption profile for each device using either its EU energy label or a self-measured average,
so that devices without a direct smart plug contribute an estimated baseline to the Decomposition view.

## Acceptance Criteria

1. **Given** a Device with `ConsumptionApproach = None` in the Flat Structure editor, **when** the user taps "Configure consumption profile", **then** a Choice Step presents two mutually exclusive selector cards: "EU energy label" and "Self-measured average"; selecting one reveals only that approach's fields; the other approach's fields are hidden, not disabled (UX-DR17).

2. **Given** "EU energy label" is selected, **when** the EU label fields render, **then** an energy class rating field (text) and an annual kWh field (`inputmode="numeric"`) are shown; saving updates `Device.ConsumptionApproach = EuLabel`, `EuLabelClass`, and `EuAnnualKwh` (decimal); the derived daily estimate (`EuAnnualKwh ÷ 365`) is displayed below the field for confirmation (FR-30).

3. **Given** "Self-measured average" is selected, **when** the self-measured fields render, **then** a Daily/Weekly toggle is shown with "Daily" pre-selected; the kWh input label updates instantly on toggle switch ("kWh per day" / "kWh per week"); saving updates `Device.ConsumptionApproach = SelfMeasured`, `SelfMeasuredKwh` (decimal), and `SelfMeasuredPeriod` (Daily/Weekly) (FR-31).

4. **Given** saving a consumption profile, **when** `PUT /api/v1/flats/{flatId}/structure` is called, **then** HTTP 200 is returned; TanStack Query key `['flat-structure', flatId]` is invalidated; all decimal values are stored as `decimal` in the database — no float or double.

5. **Given** a gap found during story creation — `UpdateFlatStructureValidator.cs` (added ahead of this story, in Story 5.3) requires `EuLabelClass` to be non-empty whenever `ConsumptionApproach = EuLabel`, but the finalized UX decision (`.decision-log.md` D-41) and PRD FR-30 both specify energy class is **optional** and only the annual kWh figure is required — **when** the EU label form is submitted with an annual kWh value but no energy class, **then** the request succeeds (HTTP 200), not 400; `EuAnnualKwh` remains the only required field for the `EuLabel` approach.

## Tasks / Subtasks

- [x] Task 1: Fix `UpdateFlatStructureValidator.cs` — energy class is optional, not required (AC: 5)
  - [x] In `api/Features/FlatStructure/UpdateFlatStructureValidator.cs`, remove the line `d.RuleFor(dv => dv.EuLabelClass).NotEmpty().When(dv => dv.ConsumptionApproach == ConsumptionApproach.EuLabel);` — leave the existing `d.RuleFor(dv => dv.EuLabelClass).MaximumLength(200);` rule (unconditional, already correct) as the only constraint on `EuLabelClass`.
  - [x] Leave `d.RuleFor(dv => dv.EuAnnualKwh).NotNull().When(dv => dv.ConsumptionApproach == ConsumptionApproach.EuLabel);` unchanged — this is the one required field for `EuLabel` per D-41/FR-30.
  - [x] No other rule changes — `SelfMeasured`'s `NotNull` requirements on `SelfMeasuredKwh`/`SelfMeasuredPeriod` are already correct per FR-31 (both fields are always required together) and are not part of this gap.

- [x] Task 2: Backend test updates for the validator fix (AC: 5)
  - [x] In `api.Tests/Features/FlatStructure/UpdateFlatStructureFunctionTests.cs`, add `RunAsync_EuLabelApproachWithKwhButNoClass_Returns200` — a device with `consumptionApproach: "EuLabel"`, `euAnnualKwh: 150`, no `euLabelClass` field at all — asserts `OkObjectResult`, and that the returned `DeviceResponse.EuLabelClass` is `null`.
  - [x] Add `RunAsync_EuLabelApproachWithClassButNoKwh_Returns400` — a device with `consumptionApproach: "EuLabel"`, `euLabelClass: "A+++"`, no `euAnnualKwh` — asserts `BadRequestObjectResult` (locks in that `EuAnnualKwh` remains the required field).
  - [x] The existing `RunAsync_EuLabelApproachMissingEuLabelClassAndKwh_Returns400` test (missing **both** fields) needs no change — it still returns 400 because `EuAnnualKwh` alone is still required; do not weaken or remove it.

- [x] Task 3: `DeviceEditor.tsx` — Choice Step, EU label fields, self-measured fields (AC: 1, 2, 3)
  - [x] Add local state (alongside the existing `name`/`type`/`manufacturer`/`model` state):
    ```ts
    const [approach, setApproach] = useState<ConsumptionApproach>(device?.consumptionApproach ?? 'None')
    const [configuring, setConfiguring] = useState(approach !== 'None')
    const [euLabelClass, setEuLabelClass] = useState(device?.euLabelClass ?? '')
    const [euAnnualKwhRaw, setEuAnnualKwhRaw] = useState(
      device?.euAnnualKwh !== undefined ? formatNumberForInput(device.euAnnualKwh, i18n.language) : ''
    )
    const [selfMeasuredKwhRaw, setSelfMeasuredKwhRaw] = useState(
      device?.selfMeasuredKwh !== undefined ? formatNumberForInput(device.selfMeasuredKwh, i18n.language) : ''
    )
    const [selfMeasuredPeriod, setSelfMeasuredPeriod] = useState<'Daily' | 'Weekly'>(
      device?.selfMeasuredPeriod ?? 'Daily'
    )
    ```
    Import `i18n` from `@/lib/i18n` and `parseLocaleNumber`, `formatNumberForInput` from `@/lib/localeNumber` — this is the established raw-`useState` numeric-input pattern already used in `client/src/features/readings/components/ReadingHistorySheet.tsx`'s `ReadingEditView` (that component is also plain `useState`, not react-hook-form — `DeviceEditor`/`RoomEditor`/`PowerPointEditor` in this feature slice predate the app's react-hook-form convention and are internally consistent with each other; introducing react-hook-form into only this one field group would be inconsistent with its three sibling editors in the same feature, so this story follows the sibling-component convention instead of the general "Forms" rule in `project-context.md`).
    Import `ConsumptionApproach`, `SelfMeasuredPeriod` as types from `@/features/flat-structure/api/flatStructureApi` (re-exported via `draftModel.ts`'s existing imports — `DraftDevice`'s `consumptionApproach`/`selfMeasuredPeriod` fields already use these types).
  - [x] Replace the static `<p>{t('device.consumptionNote')}</p>` block with:
    - If `approach === 'None' && !configuring`: render the `device.consumptionNote` text **and** a button `t('device.configureProfile')` that sets `configuring = true` on click.
    - Otherwise (already configuring, or `approach !== 'None'` from an existing device): render the Choice Step — two selector-card `<button>`s (`aria-pressed={approach === 'EuLabel'}` / `aria-pressed={approach === 'SelfMeasured'}`), each with a title + one-line description (`t('device.consumptionApproach.euLabelTitle')`/`euLabelSub`, `selfMeasuredTitle`/`selfMeasuredSub`), calling `setApproach('EuLabel')` / `setApproach('SelfMeasured')`. Style as glass cards consistent with this feature's existing `pp-card`/choice styling (`rgba(255,255,255,0.07)` background, `1px solid rgba(255,255,255,0.12)` border, `16px` radius) — matched to the visual language already used by `PowerPointEditor.tsx`'s device-row cards, since no UX mockup exists for this specific sub-screen (the only mockup frame for a device "choice step" — `mockups/flat-structure-editor.html` Frame 3 — covers a different, higher-level choice: "smart plug connected" vs "estimated usage" at device-creation time, not the EU-label-vs-self-measured choice this story implements; do not copy that frame's copy text).
  - [x] When `approach === 'EuLabel'`: render two fields below the cards:
    - Energy class: `<input type="text" value={euLabelClass} onChange={e => setEuLabelClass(e.target.value)} />`, label `t('device.euLabel.classLabel')`, placeholder `t('device.euLabel.classPlaceholder')` — **not** marked required in the UI (per Task 1's fix).
    - Annual kWh: `<input type="text" inputMode="numeric" value={euAnnualKwhRaw} onChange={e => setEuAnnualKwhRaw(e.target.value)} />`, label `t('device.euLabel.annualKwhLabel')`.
    - Below the annual-kWh field, when `euAnnualKwhRaw.trim() !== ''` and `!isNaN(parseLocaleNumber(euAnnualKwhRaw, i18n.language))`: render `t('device.euLabel.dailyEstimate', { value: formatKwh(parseLocaleNumber(euAnnualKwhRaw, i18n.language) / 365) })` where `formatKwh` is a local helper `(value: number) => \`${new Intl.NumberFormat(i18n.language, { maximumFractionDigits: 2 }).format(value)} kWh\`` (same pattern as `ReadingHistorySheet.tsx`'s `formatKwh`/`formatNumber` helpers — render-time `Intl.NumberFormat`, never computed/formatted server-side).
  - [x] When `approach === 'SelfMeasured'`: render:
    - A two-button Daily/Weekly toggle (`aria-pressed={selfMeasuredPeriod === 'Daily'}` / `'Weekly'`), calling `setSelfMeasuredPeriod('Daily' | 'Weekly')`.
    - A kWh input (`type="text" inputMode="numeric"`) whose **label** text is `selfMeasuredPeriod === 'Daily' ? t('device.selfMeasured.kwhLabelDaily') : t('device.selfMeasured.kwhLabelWeekly')` — this must re-render immediately on toggle click (AC3's "updates instantly" — no debounce, no derived-effect, just read `selfMeasuredPeriod` directly in the label expression each render).
  - [x] Update `isSaveEnabled`:
    ```ts
    const parsedEuAnnualKwh = parseLocaleNumber(euAnnualKwhRaw, i18n.language)
    const parsedSelfMeasuredKwh = parseLocaleNumber(selfMeasuredKwhRaw, i18n.language)
    const euValid = approach !== 'EuLabel' || (!isNaN(parsedEuAnnualKwh) && parsedEuAnnualKwh >= 0)
    const selfMeasuredValid = approach !== 'SelfMeasured' || (!isNaN(parsedSelfMeasuredKwh) && parsedSelfMeasuredKwh >= 0)
    const isSaveEnabled = name.trim() !== '' && euValid && selfMeasuredValid
    ```
    `>= 0` (not `> 0`) matches the backend validator's `GreaterThanOrEqualTo(0)` exactly — do not introduce a stricter frontend-only rule.
  - [x] Update `handleSave` to send the approach-appropriate fields only, clearing the other approach's fields rather than carrying stale values forward when a user switches approach mid-edit:
    ```ts
    onSave({
      key: device?.key ?? crypto.randomUUID(),
      name: name.trim(),
      type, manufacturer, model,
      consumptionApproach: approach,
      purchaseDate: device?.purchaseDate,
      euLabelClass: approach === 'EuLabel' ? (euLabelClass.trim() || undefined) : undefined,
      euAnnualKwh: approach === 'EuLabel' ? parsedEuAnnualKwh : undefined,
      selfMeasuredKwh: approach === 'SelfMeasured' ? parsedSelfMeasuredKwh : undefined,
      selfMeasuredPeriod: approach === 'SelfMeasured' ? selfMeasuredPeriod : undefined,
    })
    ```
    This is a behavior change from the current code's comment ("this story's UI doesn't expose these fields... must pass through untouched") — remove that comment, it is now stale.

- [x] Task 4: i18n — add new translation keys to both locale files (AC: 1, 2, 3)
  - [x] `client/src/locales/en-US/flat-structure.json`, inside `"device"`, add (replacing nothing, additive):
    ```json
    "configureProfile": "Configure consumption profile",
    "consumptionApproach": {
      "sectionLabel": "How is this device measured?",
      "euLabelTitle": "EU energy label",
      "euLabelSub": "Energy class and annual kWh from the label",
      "selfMeasuredTitle": "Self-measured average",
      "selfMeasuredSub": "A daily or weekly average you measured yourself"
    },
    "euLabel": {
      "classLabel": "Energy class (optional)",
      "classPlaceholder": "e.g. A+++",
      "annualKwhLabel": "Annual kWh (from label)",
      "annualKwhPlaceholder": "e.g. 150",
      "dailyEstimate": "≈ {{value}} per day"
    },
    "selfMeasured": {
      "periodDaily": "Daily",
      "periodWeekly": "Weekly",
      "kwhLabelDaily": "kWh per day",
      "kwhLabelWeekly": "kWh per week"
    }
    ```
  - [x] `client/src/locales/de-DE/flat-structure.json`, same keys, German values (instrument register per D-28 — factual, no exclamation marks):
    ```json
    "configureProfile": "Verbrauchsprofil konfigurieren",
    "consumptionApproach": {
      "sectionLabel": "Wie wird dieses Gerät gemessen?",
      "euLabelTitle": "EU-Energielabel",
      "euLabelSub": "Energieklasse und Jahres-kWh vom Label",
      "selfMeasuredTitle": "Selbst gemessener Durchschnitt",
      "selfMeasuredSub": "Ein selbst gemessener Tages- oder Wochendurchschnitt"
    },
    "euLabel": {
      "classLabel": "Energieklasse (optional)",
      "classPlaceholder": "z. B. A+++",
      "annualKwhLabel": "Jahres-kWh (vom Label)",
      "annualKwhPlaceholder": "z. B. 150",
      "dailyEstimate": "≈ {{value}} pro Tag"
    },
    "selfMeasured": {
      "periodDaily": "Täglich",
      "periodWeekly": "Wöchentlich",
      "kwhLabelDaily": "kWh pro Tag",
      "kwhLabelWeekly": "kWh pro Woche"
    }
    ```
  - [x] No changes to `client/src/lib/i18n.ts`'s `ns: [...]` array — `flat-structure` namespace is already registered (Story 5.4).

- [x] Task 5: `DeviceEditor.test.tsx` — update/replace tests for the new UI (AC: 1, 2, 3, 4)
  - [x] Delete `DeviceEditor_NoConsumptionApproachChoiceUIRendered` — it asserts the choice UI never renders, which this story makes false by design. Replace with `DeviceEditor_ConfigureProfileTapped_ShowsChoiceStepCards` — render with `device={undefined}`, click the `device.configureProfile` button, assert both `device.consumptionApproach.euLabelTitle` and `device.consumptionApproach.selfMeasuredTitle` are now in the document.
  - [x] Update `DeviceEditor_AlwaysRendersConsumptionNote` — rename to `DeviceEditor_UnconfiguredDevice_RendersConsumptionNoteAndConfigureButton` and additionally assert the `device.configureProfile` button is present; this note/button pair is only for the `None`-and-not-configuring state, not "always" — do not assert its presence when `device.consumptionApproach !== 'None'`.
  - [x] Add `DeviceEditor_EuLabelSelected_ShowsOnlyEuLabelFieldsHidesSelfMeasured` — click `configureProfile`, click the EU-label card, assert `device.euLabel.annualKwhLabel` field is present and `device.selfMeasured.kwhLabelDaily`/`kwhLabelWeekly` text is absent.
  - [x] Add `DeviceEditor_SelfMeasuredSelected_ShowsOnlySelfMeasuredFieldsHidesEuLabel` — the mirror case.
  - [x] Add `DeviceEditor_EuAnnualKwhEntered_ShowsDerivedDailyEstimate` — select EU label, type `365` into the annual-kWh field, assert text containing `1` and `kWh` (e.g. via a regex matcher on the rendered estimate) is present — confirms `365 ÷ 365 = 1`.
  - [x] Add `DeviceEditor_SelfMeasuredToggleSwitchedToWeekly_UpdatesKwhInputLabelInstantly` — select self-measured, assert `device.selfMeasured.kwhLabelDaily` label is present (Daily pre-selected per AC3), click the Weekly toggle button, assert `device.selfMeasured.kwhLabelDaily` is gone and `device.selfMeasured.kwhLabelWeekly` is now present.
  - [x] Add `DeviceEditor_EuLabelApproachMissingAnnualKwh_SaveDisabled` — select EU label, do not fill in the annual-kWh field, assert the Save button is disabled (name alone is not sufficient once an approach is chosen).
  - [x] Add `DeviceEditor_EuLabelApproachWithKwhOnly_SaveEnabledAndCallsOnSaveWithUndefinedClass` — select EU label, type only into the annual-kWh field (leave energy class blank), click Save, assert `onSave` was called with `euLabelClass: undefined` and `euAnnualKwh: 150` (proves the Task 1 optionality fix is honored client-side too).
  - [x] Add `DeviceEditor_SelfMeasuredApproachWithValidKwh_CallsOnSaveWithApproachFields` — select self-measured, keep Daily, type a kWh value, click Save, assert `onSave` called with `consumptionApproach: 'SelfMeasured'`, the parsed `selfMeasuredKwh`, and `selfMeasuredPeriod: 'Daily'`.
  - [x] Rewrite `DeviceEditor_ExistingDeviceWithConsumptionProfile_PreservesItUnchangedOnSave` — the current fixture (`consumptionApproach: 'EuLabel', euLabelClass: 'A+++'`, no `euAnnualKwh`) would now leave Save **disabled** (annual kWh is required for `EuLabel`, per Task 1). Update the fixture to include `euAnnualKwh: 150`, and assert that clicking Save without touching any field calls `onSave` with `consumptionApproach: 'EuLabel'`, `euLabelClass: 'A+++'`, `euAnnualKwh: 150` unchanged — this also implicitly proves the fields are visible immediately for an already-configured device (no need to tap `configureProfile` again, since `configuring` initializes to `true` when `approach !== 'None'`).
  - [x] `DeviceEditor_NewDevice_DefaultsConsumptionApproachToNone` — no change needed; still valid (a brand-new device saved without ever tapping `configureProfile` still saves `consumptionApproach: 'None'`).
  - [x] Existing name/cancel/prefill tests (`DeviceEditor_EmptyName_SaveDisabled`, `DeviceEditor_NameEntered_SaveEnabled`, `DeviceEditor_ExistingDevice_PrefillsFields`, `DeviceEditor_SaveClicked_CallsOnSaveWithTrimmedName`, `DeviceEditor_CancelClicked_CallsOnCancel`) — no change needed, all exercise `approach === 'None'` paths untouched by this story.

### Review Findings

- [x] [Review][Patch] No way to revert consumption approach back to `None` once selected — fixed by adding a "Change" action next to the section label (visible once an approach is chosen) that calls `setApproach('None')`, re-showing both selector cards unselected. New `device.consumptionApproach.changeApproach` i18n key added to both locales. [client/src/features/flat-structure/components/DeviceEditor.tsx]
- [x] [Review][Patch] Client-side numeric validation accepted non-numeric-safe strings — fixed via a new local `isValidKwhRaw` helper that rejects alphabetic characters (blocks `"150abc"`/`"Infinity"`) and uses `Number.isFinite` instead of `!isNaN`. [client/src/features/flat-structure/components/DeviceEditor.tsx]
- [x] [Review][Patch] Client-side Save gate didn't enforce the backend's 4-decimal-place precision limit — the same `isValidKwhRaw` helper now rejects raw input with more than 4 digits after the locale-appropriate decimal separator, matching the backend's `PrecisionScale(18,4,true)`. [client/src/features/flat-structure/components/DeviceEditor.tsx]
- [x] [Review][Patch] `sprint-status.yaml`'s two `last_updated` annotations disagreed — reconciled both to "story 6.5 ready for review", matching the actual `review` status. [_bmad-output/implementation-artifacts/sprint-status.yaml]
- [x] [Review][Patch] Weak test assertion for the derived daily estimate — replaced the loose `/1.*kWh/` regex with an exact-text assertion (`device.euLabel.dailyEstimate:1 kWh`). [client/src/features/flat-structure/components/DeviceEditor.test.tsx]
- [x] [Review][Patch] Mutually-exclusive selector cards and the Daily/Weekly toggle used `aria-pressed` instead of radio-group semantics — converted both to `role="radiogroup"` wrappers with `role="radio"`/`aria-checked` buttons; updated the 8 affected test queries from `getByRole('button', ...)` to `getByRole('radio', ...)`. [client/src/features/flat-structure/components/DeviceEditor.tsx, DeviceEditor.test.tsx]

- [x] Task 6: Full verification pass before marking ready for review (AC: all)
  - [x] `dotnet test api.Tests/` — all green, including the two new validator tests from Task 2, zero regressions.
  - [x] `npm test` (from `client/`) — all green, including the rewritten `DeviceEditor.test.tsx`, zero regressions in `FlatStructureEditor.test.tsx`/`useFlatStructure.test.ts`/`useUpdateFlatStructure.test.ts` (none of those files reference the changed fields, but confirm).
  - [x] `npm run lint` (from `client/`) — no new `oxlint` violations.
  - [x] Confirm no EF Core migration is generated: `dotnet ef migrations has-pending-model-changes` from `api/` → "No changes have been made to the model since the last migration." (already verified true as of this story's baseline commit — the `Device` columns for `ConsumptionApproach`/`EuLabelClass`/`EuAnnualKwh`/`SelfMeasuredKwh`/`SelfMeasuredPeriod` were added in Story 5.3's `AddRoomsPowerPointsAndDevicesTables` migration and only needed to be *populated* from the UI, exactly as Story 6.4 found for `ImportJob.GapNotifications`).
  - [x] No `./infra/deploy.sh` run, no push to live Azure — this story has no infra changes.

## Dev Notes

### This is almost entirely a frontend story — the backend was already built in Story 5.3

`api/Data/Entities/Device.cs`, `DeviceConfiguration.cs`, `FlatStructureModels.cs` (`DeviceResponse`/`DeviceInput`), `UpdateFlatStructureFunction.cs`, and `UpdateFlatStructureValidator.cs` **already** fully model and validate `ConsumptionApproach`/`EuLabelClass`/`EuAnnualKwh`/`SelfMeasuredKwh`/`SelfMeasuredPeriod` — confirmed via `git log --oneline -- api/Data/Entities/Device.cs` showing this landed in commit `10ecdd5` ("story 5.3 - flat structure backend"), forward-built exactly like Story 6.1's `ImportJob.GapNotifications` column was forward-built for Story 6.4. `PUT /api/v1/flats/{flatId}/structure`'s full-replace contract already round-trips every field this story needs (AC4 requires no backend change — it already invalidates `['flat-structure', flatId]` via `useUpdateFlatStructure.ts` and already stores every value as `decimal` per `DeviceConfiguration.cs`'s `HasColumnType("decimal(18,4)")`).

The **one** real backend gap (Task 1/2, AC5) is that the Story 5.3 validator guessed `EuLabelClass` should be required for the `EuLabel` approach, before the UX decision (`.decision-log.md` D-41, dated the same day as the epics but written with more nuance) explicitly resolved it as optional. This is exactly the kind of gap Stories 6.2–6.4 were each built to find by reading real merged code against the real spec, not just the epic's paraphrase — the epic's own AC2 text ("an energy class rating field... and an annual kWh field... are shown") is ambiguous about which is required, so the validator's speculative choice went unnoticed until this story's author cross-referenced D-41.

### `DeviceEditor.tsx` — current state as of baseline commit

The component today (`client/src/features/flat-structure/components/DeviceEditor.tsx`) only edits `name`/`type`/`manufacturer`/`model` and has an explicit code comment stating "this story's UI doesn't expose these fields, so they must pass through untouched" for the consumption-profile fields on `handleSave` — that comment is describing **this exact story's** starting point and must be removed once this story wires the fields up for real. `draftModel.ts`'s `DraftDevice` type already carries all five consumption fields end-to-end (`toDraftRooms` reads them from the API response, `toUpdateRequest` writes them back) — no changes needed to `draftModel.ts` itself, only to what `DeviceEditor.tsx` populates on the `DraftDevice` object it produces.

### Numeric input pattern — follow `ReadingHistorySheet.tsx`, not react-hook-form

This feature's four editor components (`RoomEditor`, `PowerPointEditor`, `DeviceEditor`, and the top-level `FlatStructureEditor`) are all plain `useState`-driven, predating this codebase's react-hook-form/zod convention documented in `project-context.md`'s "Forms" rules. The nearest working precedent for a **plain-useState** decimal kWh input (as opposed to the react-hook-form ones in `EnterReadingSheet.tsx`/`TariffForm.tsx`) is `client/src/features/readings/components/ReadingHistorySheet.tsx`'s `ReadingEditView`: raw string state (`kwhRaw`) via `useState(formatNumberForInput(...))`, parsed on every render via `parseLocaleNumber(kwhRaw, i18n.language)` from `client/src/lib/localeNumber.ts`, with `onChange` storing the untouched raw string (no per-keystroke filtering). Use this exact pattern for `euAnnualKwhRaw` and `selfMeasuredKwhRaw`. Do not introduce `react-hook-form`/`zod` into `DeviceEditor.tsx` alone — that would make it inconsistent with its three sibling components in the same feature slice, which is a worse outcome than the general Forms-rule deviation (all four are already deviating together, consistently).

### No mockup exists for this exact sub-screen — use the feature's own established card style

`mockups/flat-structure-editor.html` Frame 3 ("Add Device — choose approach") shows a **different** choice step ("Smart plug connected" vs "Estimated usage" at device-creation time) — that is FR-29's device-creation flow from Epic 5, already fully built, and is a level above this story's choice. This story's choice (EU label vs self-measured, gated behind `ConsumptionApproach = None` → "Configure consumption profile") has no dedicated mockup frame; `.decision-log.md` D-39/D-41/D-42 and `EXPERIENCE.md`'s Component Patterns table (`Choice step` / `Toggle` rows) describe the *behavior* but not pixel-level styling. Build the new cards/toggle using the visual language already established by this feature's own `PowerPointEditor.tsx` (glass card: `rgba(255,255,255,0.07)` bg, `1px solid rgba(255,255,255,0.12)` border, `16px` border-radius, `backdropFilter: blur(20px) saturate(180%)`) rather than inventing a new visual system.

### Derived daily estimate — client-side only, `Intl.NumberFormat` at render time

Per `project-context.md`'s i18n rule ("Formatting: `Intl.NumberFormat` / `Intl.DateTimeFormat` at render time only — never in the API layer"), `EuAnnualKwh ÷ 365` is computed and formatted entirely in `DeviceEditor.tsx` from the locally-parsed `euAnnualKwhRaw` value — the backend does not compute or return a derived daily estimate anywhere, and none should be added to `DeviceResponse`.

### Testing conventions (confirmed from this feature's actual test files)

Vitest + `@testing-library/react` + `userEvent`, `vi.mock('react-i18next', ...)` returning the raw key as `t()` (so assertions match on translation *keys*, e.g. `screen.getByText('device.euLabel.annualKwhLabel')`, not translated prose) — exactly as `DeviceEditor.test.tsx`, `PowerPointEditor` (untested directly but same pattern via `FlatStructureEditor.test.tsx`), and `ReadingHistorySheet`'s own tests already do. No `QueryClientProvider`/`MemoryRouter` wrapper needed for `DeviceEditor` in isolation — it takes no routing or query hooks as props (`device`/`onSave`/`onCancel` only).

### Project Structure Notes

- Modified files only — no new files, no new API endpoints, no new hooks:
  - `api/Features/FlatStructure/UpdateFlatStructureValidator.cs` (Task 1)
  - `api.Tests/Features/FlatStructure/UpdateFlatStructureFunctionTests.cs` (Task 2)
  - `client/src/features/flat-structure/components/DeviceEditor.tsx` (Task 3)
  - `client/src/locales/en-US/flat-structure.json`, `client/src/locales/de-DE/flat-structure.json` (Task 4)
  - `client/src/features/flat-structure/components/DeviceEditor.test.tsx` (Task 5)
- No changes to: `draftModel.ts` (already carries all fields), `flatStructureApi.ts` (types already complete), `useUpdateFlatStructure.ts`/`useFlatStructure.ts` (invalidation/fetch already correct), `FlatStructureEditor.tsx`/`RoomEditor.tsx`/`PowerPointEditor.tsx` (no prop-shape changes — `DeviceEditor` still receives `device`/`onSave`/`onCancel` only), `UpdateFlatStructureFunction.cs`/`GetFlatStructureFunction.cs`/`FlatStructureModels.cs` (response/request shapes already correct), any `api/Data/Entities|Configurations|Migrations` files (schema already complete, confirmed via `dotnet ef migrations has-pending-model-changes`), `infra/`.
- `client/src/lib/i18n.ts`'s namespace registry: no change — `flat-structure` already registered.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-6-smart-plug-import-device-registry.md#Story 6.5] — authoritative AC text (verbatim, reproduced above as ACs 1–4; AC5 added during story creation per the `EuLabelClass` optionality gap found by reading Story 5.3's actual merged validator against `.decision-log.md` D-41).
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/.decision-log.md#D-39, D-41, D-42] — D-39: choice step behavior (select one, other hidden); D-41: EU label fields, annual kWh required, energy class optional (the source of this story's AC5 gap); D-42: self-measured toggle, Daily pre-selected, single kWh field.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/EXPERIENCE.md#Component Patterns] — "Choice step" and "Toggle" rows describing the Device energy approach selection and Daily/Weekly toggle behavior (UX-DR17).
- [Source: _bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md#4.9 Device Registry, FR-30, FR-31] — functional requirements and testable consequences for EU label and self-measured consumption.
- [Source: _bmad-output/planning-artifacts/architecture.md:217, :871] — `Devices` table column list (`ConsumptionApproach`, `EuLabelClass`, `EuAnnualKwh`, `SelfMeasuredKwh`, `SelfMeasuredPeriod`); Device Registry traceability row confirming `DeviceEditor.tsx` (this story's file) as the intended two-path choice-step component.
- [Source: api/Data/Entities/Device.cs, DeviceConfiguration.cs, api/Features/FlatStructure/FlatStructureModels.cs, UpdateFlatStructureFunction.cs, UpdateFlatStructureValidator.cs] — exact current backend code as merged through Story 5.3/6.4, confirming the schema/API/validation are already forward-built for this story except for the AC5 gap.
- [Source: client/src/features/flat-structure/components/DeviceEditor.tsx, draftModel.ts, DeviceEditor.test.tsx] — exact current frontend code as merged through Story 5.4, confirming the "pass through untouched" comment and the `DeviceEditor_NoConsumptionApproachChoiceUIRendered`/`DeviceEditor_AlwaysRendersConsumptionNote` tests that this story's Task 5 must update.
- [Source: client/src/features/readings/components/ReadingHistorySheet.tsx, client/src/lib/localeNumber.ts] — the plain-`useState` numeric-decimal-input pattern (`parseLocaleNumber`/`formatNumberForInput`) this story's Task 3 follows for `euAnnualKwhRaw`/`selfMeasuredKwhRaw`, and the `Intl.NumberFormat`-based `formatKwh` render-time helper pattern.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/mockups/flat-structure-editor.html:Frame 3] — the "Add Device — choose approach" mockup, confirmed to cover a *different*, already-built choice (smart plug vs estimated) and explicitly not to be copied for this story's EU-label-vs-self-measured cards.
- [Source: _bmad-output/implementation-artifacts/6-4-gap-detection-interpolation-and-main-meter-reconciliation.md] — Story 6.4's Dev Notes precedent for how this codebase handles a story-creation-time gap found by reading real merged code against real specs (AC6 there; AC5 here).

## Change Log

- 2026-07-06: Story created via create-story workflow.
- 2026-07-06: Implementation complete — validator fix, Choice Step/EU label/self-measured UI, i18n keys, and test suite rewrite. All ACs satisfied.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

### Completion Notes List

- Task 1/2: Removed the speculative `NotEmpty()` rule on `EuLabelClass` in `UpdateFlatStructureValidator.cs`, leaving `EuAnnualKwh`'s `NotNull()` as the only required field for the `EuLabel` approach (AC5). Added `RunAsync_EuLabelApproachWithKwhButNoClass_Returns200` and `RunAsync_EuLabelApproachWithClassButNoKwh_Returns400` to lock in the new contract; the existing missing-both-fields test needed no change.
- Task 3: Wired `DeviceEditor.tsx` with the Choice Step (gated behind a "Configure consumption profile" button when `approach === 'None'`), EU label fields (energy class optional, annual kWh required, derived daily estimate rendered client-side via `Intl.NumberFormat`), and self-measured fields (Daily/Weekly toggle, label text switches instantly). Followed the existing plain-`useState`/`parseLocaleNumber` pattern from `ReadingHistorySheet.tsx` rather than introducing react-hook-form, consistent with this feature slice's other editors. Added `aria-label` to the two selector-card buttons so tests (and screen readers) get a distinct accessible name separate from the card's description text.
- Task 4: Added `configureProfile`, `consumptionApproach.*`, `euLabel.*`, and `selfMeasured.*` keys to both `en-US` and `de-DE` `flat-structure.json` locale files.
- Task 5: Rewrote `DeviceEditor.test.tsx` per the story's task list — replaced the "choice UI never renders" test with one confirming it renders on demand, and added coverage for EU label/self-measured field visibility, the derived daily estimate, the toggle's instant label switch, save-gating on required fields, and the optional-energy-class save payload.
- Task 6: `dotnet test api.Tests/` (324 passed), `npm test` from `client/` (268 passed across 42 files), `npm run lint` (no new violations — pre-existing `router.tsx` fast-refresh warnings only), `dotnet ef migrations has-pending-model-changes` confirmed no pending migration. No infra changes.

### File List

- `api/Features/FlatStructure/UpdateFlatStructureValidator.cs` (modified)
- `api.Tests/Features/FlatStructure/UpdateFlatStructureFunctionTests.cs` (modified)
- `client/src/features/flat-structure/components/DeviceEditor.tsx` (modified)
- `client/src/features/flat-structure/components/DeviceEditor.test.tsx` (modified)
- `client/src/locales/en-US/flat-structure.json` (modified)
- `client/src/locales/de-DE/flat-structure.json` (modified)
