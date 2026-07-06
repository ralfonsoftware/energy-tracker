---
baseline_commit: ce66ba1d57ff21f3831a7fbc5476765873b20185
---

# Story 6.0: Pre-Epic-6 Hardening — CI Test Gate, Onboarding Validator Fix & Flat Structure Delete Affordance

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user and as the team maintaining this app,
I want the existing test suite to actually gate merges, the onboarding form to validate the same fields its siblings already validate, and a way to remove a mistakenly-added room/power point/device,
so that Epic 6 builds on a codebase where regressions are caught before merge, a known validation bypass is closed, and flat structure management isn't a one-way ratchet.

## Acceptance Criteria

1. **Given** `.github/workflows/azure-static-web-apps.yml`, **when** the workflow is updated, **then** it adds a `dotnet test` step (running `api.Tests`) and an `npm test` step (running the Vitest suite in `client/`), both required to pass before the build/publish jobs run; the trigger block adds `pull_request: branches: [main]` alongside the existing trigger, so every PR runs the same gate before merge — not just pushes to `main`.

2. **Given** `api/Features/Onboarding/OnboardingValidator.cs`, **when** `PlannedAnnualSpend` is validated, **then** it receives the same rule already present on `CreateFlatValidator`/`PatchFlatValidator` for the identical `Flat.PlannedAnnualSpend` column: `GreaterThan(0)`, `LessThan(50000)`, `PrecisionScale(18, 4, true)`; a new or extended `OnboardingValidatorTests.cs` case asserts a value with more than 4 decimal places, and a value outside the (0, 50000) range, are both rejected with 400 Problem Details.

3. **Given** the Flat Structure editor (`client/src/features/flat-structure/components/`), **when** a user views an existing Room, Power Point, or Device, **then** a delete affordance is present for each; tapping it removes the item from the client-side draft model (removing a Room also removes its child Power Points/Devices from the draft); a single confirmation step (inline or modal) is required before removal to guard against accidental loss; on Save, the removal is carried by the existing `PUT /api/v1/flats/{flatId}/structure` full-replace contract (delete-and-reinsert transaction) — no new backend endpoint is needed.

4. **Given** the three fixes above, **when** the story reaches `done`, **then** `dotnet test` and `npm test` both pass locally and (per AC1, now) in CI; `OnboardingValidatorTests.cs` covers the new rule; `FlatStructureEditor.test.tsx` (or equivalent) covers delete-with-confirm for a Room, a Power Point, and a Device.

## Tasks / Subtasks

- [x] Task 1: Add `pull_request` trigger to CI workflow (AC: 1)
  - [x] **The `dotnet test`/`npm test` steps already exist** in `.github/workflows/azure-static-web-apps.yml` (added in commit `72b9ac5`, "fix: add CI test steps for frontend and backend") — do not re-add them. `Test frontend` (`npm ci && npm test -- --run`) runs before `Build frontend`, and `Test backend` (`dotnet test api.Tests/`) runs before `Publish Functions app`, all within the single `build_and_deploy` job, so a test failure already blocks the build/deploy steps that follow it in the same job — AC1's "required to pass before the build/publish jobs run" is already satisfied by step ordering.
  - [x] The actual gap: the `on:` block only has `push: branches: [main]` + `workflow_dispatch` — there is no `pull_request` trigger, so this gate only runs *after* a merge to `main`, never as a PR check that could block a bad merge in the first place. Add:
    ```yaml
    on:
      push:
        branches:
          - main
        paths:
          - 'api/**'
          - 'client/**'
      pull_request:
        branches:
          - main
      workflow_dispatch:
    ```
  - [x] Do not add a `paths:` filter under `pull_request` — the intent (per AC1 and the sprint-change-proposal) is that *every* PR into `main` runs the gate, not just ones touching `api/**`/`client/**`. Leave the existing `paths:` filter under `push` untouched (that scoping is pre-existing and out of scope here).
  - [x] Deploy steps (`Deploy frontend to Azure Static Web Apps`, `Deploy Azure Functions app`) will now also attempt to run on `pull_request` events inside the same job, since there's only one job in this workflow. This is a pre-existing structural issue (single job, no job-level `if:` gating deploy-only steps to `push`) that is out of scope to fully redesign for this story — but a PR run attempting `azure/login@v2` and deploy steps with a repo whose OIDC federated credential is scoped to the `main` branch (see the workflow's own comment) will fail those steps on a PR run. Flag this in Completion Notes if observed; do not attempt a full job-splitting redesign unless it blocks verifying AC1 (a minimal, defensible fix if it does block verification: gate the two `Deploy` steps with `if: github.event_name == 'push'`, keeping scope otherwise unchanged).

- [x] Task 2: Add missing `PlannedAnnualSpend` validation rule to `OnboardingValidator.cs` (AC: 2)
  - [x] `api/Features/Onboarding/OnboardingValidator.cs` currently has **zero** rules for `PlannedAnnualSpend` — confirmed by reading the file; every other field (`AnnualKwhBaseline`, `PricePerKwh`, `MonthlyBaseFee`) has a rule, but this one is silently skipped, letting `CompleteOnboardingFunction` accept any decimal value (including negative, >50000, or >4 decimal places) unvalidated.
  - [x] Add, mirroring `CreateFlatValidator.cs`'s existing rule for the identical `Flat.PlannedAnnualSpend` column exactly (same bounds, same precision, same nullability handling — `CompleteOnboardingRequest.PlannedAnnualSpend` is `decimal?`):
    ```csharp
    RuleFor(r => r.PlannedAnnualSpend).GreaterThan(0m).LessThan(50000m)
        .WithMessage("plannedAnnualSpend must be greater than 0 and less than 50000.")
        .PrecisionScale(18, 4, true)
        .WithMessage("plannedAnnualSpend must have at most 4 decimal places.")
        .When(r => r.PlannedAnnualSpend is not null);
    ```
  - [x] Do not change any other rule in this file — AC2 scopes this to `PlannedAnnualSpend` only.

- [x] Task 3: Backend tests for the new validator rule (AC: 2, 4)
  - [x] Extend `api.Tests/Features/Onboarding/OnboardingValidatorTests.cs`. The existing `MakeRequest` helper hardcodes `PlannedAnnualSpend: null` — add a `plannedAnnualSpend` optional parameter (default `null`) to `MakeRequest` so new tests can override it without touching the other 11 existing tests.
  - [x] Add test cases mirroring the existing decimal-precision/bounds test style already in this file (e.g. `Validate_AnnualKwhBaselineExceedsFourDecimalPlaces_Fails`):
    - `PlannedAnnualSpend` with more than 4 decimal places (e.g. `500.56789m`) → `IsValid` false.
    - `PlannedAnnualSpend` at or above `50000` → `IsValid` false.
    - `PlannedAnnualSpend` at or below `0` → `IsValid` false.
    - `PlannedAnnualSpend: null` (the default) → still valid (confirms the `.When()` guard doesn't regress the common case where onboarding doesn't set this optional field).
  - [x] AC2 also requires confirming the 400 Problem Details behavior end-to-end, not just the validator in isolation — check `api.Tests/Features/Onboarding/CompleteOnboardingFunctionTests.cs` for whether it already has a validation-failure-returns-400 test; if one exists for another field, no new Function-level test is strictly required (the validator is the single source of truth wired into the same function via DI) — only add one if the existing Function tests don't already exercise *any* validation failure path.

- [x] Task 4: Delete affordance — data model changes are none; UI changes in `client/src/features/flat-structure/components/` (AC: 3)
  - [x] No backend or draft-model shape changes needed. `draftModel.ts`'s `toUpdateRequest()` already just maps whatever is left in `draftRooms`/`room.powerPoints`/`powerPoint.devices` arrays — removing an item from a draft array before Save is sufficient; `UpdateFlatStructureFunction.cs` (`api/Features/FlatStructure/UpdateFlatStructureFunction.cs`) already deletes all existing `Rooms` for the flat and re-inserts exactly what's in the request body on every Save (`db.Rooms.RemoveRange(existingRooms)` then `db.Rooms.AddRange(newRooms)`), which already cascades to child `PowerPoints`/`Devices` via EF's configured cascade delete. Confirmed: no new endpoint, no new request/response field.
  - [x] **Room delete** — in `FlatStructureEditor.tsx`'s list view (the `<ul>` of room `<li>` rows, around line 230): add a delete button per room row. Add local state `const [confirmDeleteRoomKey, setConfirmDeleteRoomKey] = useState<string | null>(null)`. Add handler:
    ```ts
    const handleDeleteRoom = (roomKey: string) => {
      setSaveSuccess(false)
      setDraftRooms(prev => prev.filter(room => room.key !== roomKey))
      setConfirmDeleteRoomKey(null)
    }
    ```
    In each room `<li>`, when `confirmDeleteRoomKey === room.key`, render an inline confirm row (e.g. replacing/alongside the existing name input and power-points-summary button) with a Cancel button (`onClick={() => setConfirmDeleteRoomKey(null)}`) and a Delete/Confirm button (`onClick={() => handleDeleteRoom(room.key)}`); otherwise render a trash-icon button (`onClick={() => setConfirmDeleteRoomKey(room.key)}`) alongside the existing power-points-summary button. Use `lucide-react`'s `Trash2` icon (already a project dependency, already used elsewhere for icons e.g. `TariffLockIndicator.tsx`) with an `aria-label` — do not rely on icon-only buttons without an accessible name, since tests query by role/label per project convention.
  - [x] **Power Point delete** — `PowerPointEditor.tsx` currently has no way to remove itself from its parent room; its `onChange` prop only replaces its own fields, it cannot signal "remove me." Add a new required prop `onDelete: () => void` to `PowerPointEditor`'s `Props` type. In `RoomEditor.tsx`, where `PowerPointEditor` is rendered (in the `room.powerPoints.map(...)` block), pass `onDelete={() => onChange({ ...room, powerPoints: room.powerPoints.filter(pp => pp.key !== powerPoint.key) })}`. Inside `PowerPointEditor.tsx`, add the same local `confirming`/trash-icon/inline-confirm pattern as the Room row (own local `useState<boolean>` is sufficient here since each `PowerPointEditor` instance is already scoped to one power point).
  - [x] **Device delete** — in `PowerPointEditor.tsx`'s existing device list (`powerPoint.devices.map(...)`, the `<li>` rows that currently show `device.name` + an "Edit device" link), add a delete button per device row using the same confirm pattern, keyed by `device.key` (use a `confirmDeleteDeviceKey: string | null` local state, analogous to the Room-level pattern, since there can be multiple devices in one list). On confirm: `onChange({ ...powerPoint, devices: powerPoint.devices.filter(d => d.key !== device.key) })`.
  - [x] Do **not** add a delete affordance inside `DeviceEditor.tsx` itself (the full-screen device add/edit form) — AC3 only requires the affordance where the user "views" the item, which for all three entity types is their row/card in the list views (`FlatStructureEditor.tsx`'s room list, `PowerPointEditor.tsx`'s own card and its device list). `DeviceEditor.tsx` is unaffected by this story.
  - [x] Add new i18n keys to **both** `client/src/locales/en-US/flat-structure.json` and `client/src/locales/de-DE/flat-structure.json` (per `project-context.md`'s i18n rule — every user-visible string goes through `useTranslation`, no hardcoded strings). Suggested keys (exact strings are yours to word naturally in each locale, keep them short):
    - Under `room`: `"delete": "Delete room"`, `"deletePrompt": "Delete this room and everything in it?"`
    - Under `powerPoint`: `"delete": "Delete power point"`, `"deletePrompt": "Delete this power point and its devices?"`
    - Under `device` (the list-row delete, not `device.cancel`/`device.save` which are the full-screen form's existing buttons): `"delete": "Delete device"`, `"deletePrompt": "Delete this device?"`
    - A small shared pair reused by all three confirm rows, e.g. under a new top-level `"confirm"` key: `"cancel": "Cancel"`, `"delete": "Delete"`.
  - [x] This story does **not** touch the `flat-structure` namespace registration in `client/src/lib/i18n.ts` — that namespace already exists (used since Story 5.4); only the JSON files change.

- [x] Task 5: Frontend tests for delete-with-confirm (AC: 3, 4)
  - [x] Extend `client/src/features/flat-structure/components/FlatStructureEditor.test.tsx` using the file's existing `seededResponse()`/`renderEditor()` helpers and mocked-`t` pattern (`t: (k, opts) => ...`, so translated text renders as the raw key string — assert against key strings like `'room.delete'`/`'confirm.delete'`, matching how every other test in this file already asserts on key strings, not natural-language copy).
  - [x] **Room delete test**: seed two rooms (`seededResponse()` already gives "Office"/"Garage"), click the Room delete affordance for one room, assert the inline confirm UI appears (e.g. query the confirm button by its key-string role name) *and* the room's name input is still present (not yet deleted) — then click confirm, and assert `screen.queryByDisplayValue('Office')` (or whichever room) is now absent, while the other room remains.
  - [x] **Power Point delete test**: navigate into a room view (`await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[0])`, same pattern as the file's existing `ClickRoomRow` test), click that power point's delete affordance, confirm, and assert the power point's name input (e.g. `screen.queryByDisplayValue('Desk Outlet')`) is gone.
  - [x] **Device delete test**: this requires a room/power-point with a pre-existing device in the seed data — the current `seededResponse()` helper has empty `devices: []` arrays on both power points; either extend `seededResponse()` with an optional device (using an override, mirroring how the file already does per-test overrides via `seededResponse({ rooms: [...] })`) or construct a bespoke `FlatStructureResponse` inline for this one test. Navigate into the room, assert the device row is visible, click delete, confirm, assert the device row/name is gone.
  - [x] **Cancel-path test** (at least one, to cover "single confirmation step" actually guarding against accidental taps): click a delete affordance to arm it, click Cancel instead of Confirm, assert the item is still present.
  - [x] Also add/extend a `PowerPointEditor.test.tsx` if none exists today (there is currently no dedicated test file for this component — its behavior is currently only exercised indirectly through `FlatStructureEditor.test.tsx`'s room-view assertions); if the existing indirect coverage above is judged sufficient to hit every line of the new delete logic, a new dedicated file is not mandatory, but note the decision in Completion Notes.

- [x] Task 6: Full verification pass before marking ready for review (AC: 4)
  - [x] Backend: `dotnet test api.Tests/` (or `dotnet test` from repo root) — all green, including the new `OnboardingValidatorTests.cs` cases.
  - [x] Frontend: `npm test -- --run` and `npx tsc -b` and `npm run lint`, all from `client/` — all green, including the new/extended `FlatStructureEditor.test.tsx` (and `PowerPointEditor.test.tsx` if added) cases.
  - [x] Manually verify the CI YAML change with `yamllint` or by eyeballing indentation carefully — a malformed `on:` block silently disables the entire workflow with no local test signal; if `act` or similar local GitHub Actions runner is available, prefer that over eyeballing alone.
  - [x] Manually exercise the Flat Structure editor delete flow in the running app (Room, Power Point, Device — each: arm → cancel → still there; arm → confirm → gone; Save → reload → confirms the removal persisted via the full-replace PUT).

### Review Findings

- [x] [Review][Decision] `pull_request` runs will now execute `Run EF Core migrations` and both `Deploy` steps against production, since the single-job workflow has no gating beyond the new trigger — **Resolved: split into two jobs.** `.github/workflows/azure-static-web-apps.yml` now has a `test` job (checkout, frontend/backend test steps, no Azure Login, runs on every trigger including `pull_request`) and a separate `deploy` job (`needs: test`, `if: github.event_name == 'push' || github.event_name == 'workflow_dispatch'`) carrying Login/Build/Publish/Migrations/Deploy. PR runs now only ever execute tests; production migrations/deploy remain reachable solely from `push`/manual dispatch.
- [x] [Review][Decision] No guard prevents deleting the last remaining room, allowing Save to persist an empty flat structure — **Resolved: block Save when zero rooms remain.** Added `hasNoRooms = draftRooms.length === 0` in `FlatStructureEditor.tsx`, included in the `handleSave` early-return guard and the Save button's `disabled` condition, with a new `editor.noRoomsError` banner (added to both locale files) shown alongside the existing `blankNameError`/`plugIdConflict` messages. New test: `FlatStructureEditor_DeleteLastRemainingRoom_DisablesSaveAndShowsError`.
- [x] [Review][Patch] Boundary test names promise more coverage than they deliver [api.Tests/Features/Onboarding/OnboardingValidatorTests.cs:78-91] — **Fixed**: added `Validate_PlannedAnnualSpendAboveUpperBound_Fails` (`50001m`) and `Validate_PlannedAnnualSpendBelowLowerBound_Fails` (`-5m`) alongside the existing exact-boundary tests.
- [x] [Review][Patch] No positive test proving a value at exactly 4 decimal places (the documented limit) passes validation [api.Tests/Features/Onboarding/OnboardingValidatorTests.cs] — **Fixed**: added `Validate_PlannedAnnualSpendWithFourDecimalPlaces_Succeeds` (`500.5678m`).
- [x] [Review][Patch] `PowerPointEditor`'s Smart Plug ID field and full device list (with live edit/delete controls) remain interactive underneath the "delete this power point" confirmation prompt [client/src/features/flat-structure/components/PowerPointEditor.tsx:32-52] — **Fixed**: Plug ID field, device list, and Add Device button now only render when `!confirmingDelete`.
- [x] [Review][Patch] Room name input remains editable while its delete confirmation prompt is showing [client/src/features/flat-structure/components/FlatStructureEditor.tsx:246-254] — **Fixed**: added `disabled={confirmDeleteRoomKey === room.key}` to the room name input.
- [x] [Review][Patch] `handleDeleteRoom` resets `saveSuccess` but not `saveError` [client/src/features/flat-structure/components/FlatStructureEditor.tsx:74-78] — **Fixed**: `handleDeleteRoom` now also calls `setSaveError(false)`.
- [x] [Review][Patch] Near-identical device fixture duplicated verbatim across two new tests [client/src/features/flat-structure/components/FlatStructureEditor.test.tsx:182-230, 232-275] — **Fixed**: extracted shared `seededResponseWithDevice()` helper, used by both device-delete tests.
- [x] [Review][Defer] FluentValidation `.WithMessage()` only binds to the immediately preceding validator call, so a `PlannedAnnualSpend <= 0` failure surfaces the framework's default message instead of the intended custom one [api/Features/Onboarding/OnboardingValidator.cs:24-28] — deferred, pre-existing: confirmed byte-for-byte identical to the already-shipped pattern in `CreateFlatValidator.cs`/`PatchFlatValidator.cs` (from commit `89b4fd5`), and this story explicitly directed copying the rule verbatim.
- [x] [Review][Defer] No focus management or `aria-live` region when a delete button flips a row into its Cancel/Delete confirmation controls [client/src/features/flat-structure/components/FlatStructureEditor.tsx, PowerPointEditor.tsx] — deferred, pre-existing pattern: not required by AC3's "single confirmation step" wording, and no existing confirm pattern in this codebase (e.g. `FlatDeleteConfirm.tsx`) does this either.
- [x] [Review][Defer] No Escape-key handler, click-outside dismissal, or reset of an armed confirm state on navigating away [client/src/features/flat-structure/components/FlatStructureEditor.tsx, PowerPointEditor.tsx] — deferred, pre-existing pattern: enhancement beyond AC3's literal scope, consistent with how the rest of this editor already behaves.

## Dev Notes

### This story bundles three unrelated hardening fixes — treat each Task block as independent

Per the 2026-07-05 sprint-change-proposal, these three items converged from an independent four-agent review of the deferred-work backlog and share only a "do this before Epic 6 starts" rationale, not a technical dependency. Do not look for connections between the CI fix, the validator fix, and the UI fix — there are none. A 4th item from the same roundtable (scoped optimistic concurrency via `RowVersion`) was deliberately **not** included here — it's an AC amendment on the not-yet-created Story 6.1 instead, since it only makes sense in terms of tables Story 6.1 itself creates. Do not attempt to implement concurrency control in this story.

### AC1's CI fix is smaller than it reads — most of it is already done

Reading `.github/workflows/azure-static-web-apps.yml` as it exists today (commit `72b9ac5` already added both test steps) shows the `dotnet test`/`npm test` steps and their "runs before build/publish" ordering are **already in place**. The only missing piece is the `pull_request` trigger. Do not spend time re-verifying or re-adding the test steps themselves — verify only the trigger block and step-ordering claim, then move on. See Task 1 for the specific, minimal diff and the pre-existing single-job structural caveat about deploy steps potentially firing on PR events.

### AC2's validator gap is a copy-paste omission, not a design decision

`CreateFlatValidator.cs` and `PatchFlatValidator.cs` (`api/Features/Flats/`) both validate `PlannedAnnualSpend` with identical bounds/precision — this was added across the codebase in commit `89b4fd5` ("feat: add decimal-precision validation across all decimal request fields"), but `OnboardingValidator.cs` was missed by that sweep despite validating the same underlying `Flat.PlannedAnnualSpend` column via a different endpoint (`POST` onboarding-completion vs `POST`/`PATCH` flat). Copy the rule verbatim (see Task 2) — do not invent a different bound or message.

### AC3's delete affordance: this is new capability on shipped Story 5.4 code, not a bug fix

Story 5.4 (`FlatStructureEditor.tsx`, `RoomEditor.tsx`, `PowerPointEditor.tsx`, `DeviceEditor.tsx`, `draftModel.ts`) explicitly scoped delete *out* of its AC — there was never a regression here. The three components form a drill-down flow: `FlatStructureEditor` (room list) → `RoomEditor` (one room's power points) → `DeviceEditor` (one device's full-screen form). Devices are also listed inline inside `PowerPointEditor` (rendered from within `RoomEditor`) without opening the full `DeviceEditor` screen. All three delete affordances (Room/PowerPoint/Device) belong in the **list/row** views a user already sees them in — `FlatStructureEditor.tsx`'s room `<li>` rows, and `PowerPointEditor.tsx`'s own card plus its device `<li>` rows — never inside `DeviceEditor.tsx`, which the user only reaches via "add" or "edit," not "view in a list."

`PowerPointEditor.tsx` currently has no way to signal "delete me" to its parent (`onChange` only replaces its own field values) — this story adds a new `onDelete: () => void` prop, wired in `RoomEditor.tsx` where `PowerPointEditor` is instantiated. This is the one place a new prop/wiring change is required beyond adding buttons + local confirm state.

There is no existing shared "confirm delete" component to reuse that fits this UI's weight class: `client/src/features/settings/components/FlatDeleteConfirm.tsx` exists, but it's a heavyweight, type-the-flat-name-to-confirm pattern appropriate for an irreversible, server-side account-level delete with no Save/Cancel step afterward. Deleting a Room/PowerPoint/Device here only mutates the **client-side draft** — the user can still back out by not tapping the outer "Save" button, or by navigating away and re-entering (which reloads from the server via `useFlatStructure`). A lighter single-tap-then-confirm inline pattern (trash icon → inline Cancel/Delete row, no typed confirmation) is the appropriately-scoped "single confirmation step" the AC calls for. Use `lucide-react`'s `Trash2` icon (already a dependency, already used for icons elsewhere, e.g. `TariffLockIndicator.tsx`'s `Lock` icon) with an explicit `aria-label` on the button — icon-only buttons need an accessible name for both a11y and this project's query-by-role/label test convention.

### Don't forget both locale files

Per `project-context.md`'s i18n rule, every new user-visible string (delete button labels, confirm prompts, Cancel/Delete button text) must be added to **both** `client/src/locales/en-US/flat-structure.json` and `client/src/locales/de-DE/flat-structure.json` — the `flat-structure` namespace itself already exists and is already registered in `client/src/lib/i18n.ts` (from Story 5.4), so no namespace-registration change is needed, only new keys in both existing JSON files.

### Testing conventions already established in this file to follow, not reinvent

`FlatStructureEditor.test.tsx` mocks `react-i18next`'s `useTranslation` to return the raw key as `t()`'s output (with a couple of special-cased `opts` handlers for `count`/`roomCount` interpolation) — so all existing assertions in this file check for key strings like `'editor.save'`, not natural-language text. New delete-related assertions must follow the same pattern (assert against `'room.delete'`, `'confirm.delete'`, etc., not the actual English/German copy). The file also establishes `seededResponse()`/`renderEditor()` helpers and per-test override composition (`seededResponse({ rooms: [...] })`) — extend these rather than writing new render/setup boilerplate.

### Project Structure Notes

- Modified files: `.github/workflows/azure-static-web-apps.yml`; `api/Features/Onboarding/OnboardingValidator.cs`; `api.Tests/Features/Onboarding/OnboardingValidatorTests.cs`; `client/src/features/flat-structure/components/FlatStructureEditor.tsx`; `client/src/features/flat-structure/components/RoomEditor.tsx`; `client/src/features/flat-structure/components/PowerPointEditor.tsx`; `client/src/features/flat-structure/components/FlatStructureEditor.test.tsx`; `client/src/locales/en-US/flat-structure.json`; `client/src/locales/de-DE/flat-structure.json`.
- Possible new file: `client/src/features/flat-structure/components/PowerPointEditor.test.tsx` (only if indirect coverage via `FlatStructureEditor.test.tsx` is judged insufficient — see Task 5).
- No changes to: `client/src/features/flat-structure/components/DeviceEditor.tsx`, `client/src/features/flat-structure/components/draftModel.ts` (its existing map/filter functions already handle whatever subset of items remains after a draft-level delete — no shape change needed), `api/Features/FlatStructure/*` (backend already supports full-replace via existing `PUT`), `client/src/lib/i18n.ts` (namespace already registered).
- No new npm/NuGet dependencies — `lucide-react` (for the trash icon) is already a project dependency used elsewhere.

### Testing standards summary

- Backend: xUnit, tests in `api.Tests/Features/Onboarding/`, mirroring existing file/test style exactly (see `OnboardingValidatorTests.cs`'s existing `MakeRequest` helper + one-assertion-per-fact style).
- Frontend: Vitest + `@testing-library/react`, `globals: true`, co-located `.test.tsx`, `jsdom` environment, query by role/label/text (never CSS class/`data-testid`), `react-i18next` mocked to return raw keys — all per `project-context.md` and this file's own existing conventions.
- No new test infrastructure, wrappers, or mocking patterns needed — this story only extends existing test files using patterns already present in them.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-6-smart-plug-import-device-registry.md#Story 6.0] — authoritative AC text (verbatim, reproduced above).
- [Source: _bmad-output/planning-artifacts/sprint-change-proposal-2026-07-05-pre-epic6-hardening.md] — origin/rationale for all three items; explains why item 3 (concurrency) is *not* in this story.
- [Source: .github/workflows/azure-static-web-apps.yml] — current CI workflow; test steps already present (added in commit `72b9ac5`), `pull_request` trigger absent; single `build_and_deploy` job with `push`/`workflow_dispatch` triggers only.
- [Source: api/Features/Onboarding/OnboardingValidator.cs, OnboardingModels.cs] — current validator (no `PlannedAnnualSpend` rule); `CompleteOnboardingRequest.PlannedAnnualSpend` is `decimal?`.
- [Source: api/Features/Flats/CreateFlatValidator.cs, PatchFlatValidator.cs] — sibling validators with the correct, already-implemented `PlannedAnnualSpend` rule to mirror verbatim.
- [Source: api.Tests/Features/Onboarding/OnboardingValidatorTests.cs] — existing test file/style to extend.
- [Source: client/src/features/flat-structure/components/FlatStructureEditor.tsx, RoomEditor.tsx, PowerPointEditor.tsx, DeviceEditor.tsx, draftModel.ts] — current Story 5.4 implementation; drill-down view structure (list → room → device); `draftModel.ts`'s existing map/filter/build functions require no shape changes for delete.
- [Source: api/Features/FlatStructure/UpdateFlatStructureFunction.cs] — confirms full delete-and-reinsert transaction semantics already exist server-side (`db.Rooms.RemoveRange(existingRooms)` / `db.Rooms.AddRange(newRooms)`) — no backend change needed for AC3.
- [Source: client/src/features/settings/components/FlatDeleteConfirm.tsx] — existing but differently-scoped (type-to-confirm, account-level) delete-confirmation pattern in this codebase; explicitly not reused here as too heavyweight for a client-side draft removal — see Dev Notes rationale.
- [Source: client/src/locales/en-US/flat-structure.json, de-DE/flat-structure.json] — existing namespace files to extend with new keys.
- [Source: _bmad-output/project-context.md#i18n, #shadcn/ui, #Testing Rules — Frontend] — i18n dual-locale rule; never hand-edit `components/ui/`; Vitest/query-by-role conventions.

## Change Log

- Added `pull_request` trigger to CI workflow; added missing `PlannedAnnualSpend` validation rule to `OnboardingValidator.cs` with backend tests; added delete-with-confirm affordance for Room/PowerPoint/Device in the Flat Structure editor with frontend tests; full verification pass (`dotnet test` 252/252, `npm test` 260/260, `tsc -b` clean, `lint` clean, manual click-through confirmed by user); status set to `review` (Date: 2026-07-05)

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- Local manual verification required starting the full stack (Azurite, `func start`, Vite, SWA CLI). `func start` failed at boot with `System.InvalidOperationException: A connection string was not found` — root cause was `UseAzureMonitorExporter()` in `Program.cs` expecting the standard `APPLICATIONINSIGHTS_CONNECTION_STRING` env var, while `local.settings.json` stores it under a custom key (`appInsightsConnectionString`). Worked around locally by exporting the standard env var from the existing value before starting `func`; no repo file changes made for this (pre-existing local-dev friction, out of scope for this story).
- `func start` also emits its own warning ("may not correctly load function extensions... use 'dotnet run' instead") — did not switch to `dotnet run` since `func start` worked once Application Insights and Azurite were both available; noting the warning for awareness only.

### Completion Notes List

- **Task 1 (CI trigger):** Added `pull_request: branches: [main]` to the workflow's `on:` block, no `paths:` filter, `push` block left untouched. Confirmed via Ruby's YAML parser that the `on:` block parses with all three trigger keys (`push`/`pull_request`/`workflow_dispatch`) intact — a malformed block would have shown up here. Per the story's own flag: the `Deploy frontend`/`Deploy Azure Functions app` steps (and the `Login to Azure` / `Run EF Core migrations` steps before them) will now also attempt to run on `pull_request` events since this is a single-job workflow with no job-level `if:` gating. This could not be verified against a live GitHub Actions PR run in this session (no such run was triggered), so it's unconfirmed whether `azure/login@v2` actually fails on a PR event given the branch-scoped OIDC federated credential. Per the story's explicit scope guidance, no `if: github.event_name == 'push'` gating was added since it wasn't required to verify AC1 locally (YAML validity + trigger presence). Flagging for a human follow-up if a real PR run shows the job going red on steps after the test gate.
- **Task 2/3 (Onboarding validator):** Rule mirrors `CreateFlatValidator.cs` verbatim. All 4 new test cases plus the existing 10 pass (14/14). Confirmed `CompleteOnboardingFunctionTests.cs` already has a validation-failure→400 test (`RunAsync_EmptyFlatName_Returns400ValidationErrorAndCreatesNothing`), so no new Function-level test was added per the story's guidance.
- **Task 4/5 (Delete affordance):** Room delete keeps the name input visible during the inline confirm step (Cancel/Delete buttons swap in next to it) so the row doesn't lose context mid-confirm; PowerPoint and Device rows swap their whole row/card into the confirm prompt since there's no equivalent "keep visible" requirement for those. Did not add a dedicated `PowerPointEditor.test.tsx` — the 6 new/extended tests in `FlatStructureEditor.test.tsx` exercise every branch of the new delete logic in `PowerPointEditor.tsx` (self-delete confirm + cancel, device-row confirm + cancel) via the existing room-drill-down flow, so a separate file was judged unnecessary.
- **Task 6 (Verification):** `dotnet test` — 252/252 pass. `npm test -- --run` — 260/260 pass (18 in `FlatStructureEditor.test.tsx`, up from 12). `npx tsc -b` clean. `npm run lint` clean (only pre-existing, unrelated `router.tsx` fast-refresh warnings). Manual click-through of Room/PowerPoint/Device delete (arm → cancel → still there; arm → confirm → gone; Save → reload → removal persisted) performed by the user against the full local stack (Azurite + `func start` + Vite + SWA CLI emulator at `localhost:4280`, backed by the real Azure SQL DB) — confirmed working.

### File List

- `.github/workflows/azure-static-web-apps.yml`
- `api/Features/Onboarding/OnboardingValidator.cs`
- `api.Tests/Features/Onboarding/OnboardingValidatorTests.cs`
- `client/src/features/flat-structure/components/FlatStructureEditor.tsx`
- `client/src/features/flat-structure/components/RoomEditor.tsx`
- `client/src/features/flat-structure/components/PowerPointEditor.tsx`
- `client/src/features/flat-structure/components/FlatStructureEditor.test.tsx`
- `client/src/locales/en-US/flat-structure.json`
- `client/src/locales/de-DE/flat-structure.json`
