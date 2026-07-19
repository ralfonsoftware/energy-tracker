---
title: 'Story 9.10 Frontend Build Fixture Fix'
type: 'bugfix'
created: '2026-07-19'
status: 'done'
context: []
route: 'one-shot'
---

## Intent

**Problem:** Story 9.10's commit widened `FlatStructureResponse`/`ReadingResponse`/`TariffResponse`/`UpdateFlatStructureRequest`/`PatchTariffRequest` with a required `rowVersion` field and `useUserSettings()`'s return value with a required `refetch`, but only migrated some of the test files constructing those shapes — leaving 32 `tsc -b` errors across 15 files that broke the frontend production build (`npm run build`) and blocked deployment.

**Approach:** Add the missing `rowVersion`/`refetch` fields to every stale test fixture, mock, and mutate-call argument identified by investigation case file `investigations/story-9-10-deploy-failure-investigation.md`, then fix the handful of runtime `toHaveBeenCalledWith` assertions that the widened fixtures caused to newly diverge from actual mutate payloads. Test-only change; no production code touched.

## Suggested Review Order

**`rowVersion` fixture widening (flat-structure, readings, tariffs)**

- Base fixture gains `rowVersion` — every downstream `Partial<>` override in this file now inherits a concrete value instead of widening to optional.
  [`FlatStructureEditor.test.tsx:65`](../../client/src/features/flat-structure/components/FlatStructureEditor.test.tsx#L65)

- `ReadingResponse` array fixture used across multiple test files gains `rowVersion` per item.
  [`ReadingHistorySheet.test.tsx:27`](../../client/src/features/readings/components/ReadingHistorySheet.test.tsx#L27)

- Same widening pattern repeated across `useFlatStructure`, `useUpdateFlatStructure`, `usePatchReading`, `useReadingHistory`, `useSubmitReading`, `TariffForm`, `TariffList`, `useCreateTariff`, `usePatchTariff`, `useTariffs` fixtures and mutate-call arguments (mechanical repeats of the same fix, not separately annotated).

**`refetch` fixture widening (cross-feature `useUserSettings` mocks)**

- `useUserSettings()` return-type mock gains `refetch` — this hook is defined in the `settings` slice but mocked here because `onboarding` cross-imports it directly.
  [`OnboardingPage.test.tsx:33`](../../client/src/features/onboarding/OnboardingPage.test.tsx#L33)

- Same mock shape repeated 4 more times in this file and 5 times in the sibling gate component's tests.
  [`OnboardingGate.test.tsx:32`](../../client/src/features/onboarding/components/OnboardingGate.test.tsx#L32)

- The hook's own feature slice had 2 leftover un-migrated call sites despite the introducing commit touching this file.
  [`SettingsPage.test.tsx:47`](../../client/src/features/settings/SettingsPage.test.tsx#L47)

**Runtime assertion fixes (revealed only by running the suite, not by `tsc`)**

- Six `toHaveBeenCalledWith` assertions expected the pre-widening mutate payload; each now includes `rowVersion: 'AQID'` traced back to the `seededResponse()` fixture the component actually reads from.
  [`FlatStructureEditor.test.tsx:380`](../../client/src/features/flat-structure/components/FlatStructureEditor.test.tsx#L380)

- Same fix for the one affected assertion in the readings slice, traced to `sampleReadings[0].rowVersion`.
  [`ReadingHistorySheet.test.tsx:144`](../../client/src/features/readings/components/ReadingHistorySheet.test.tsx#L144)

**Deferred (not fixed here)**

- Three `mockUseUserSettings` sites elsewhere (`AddFlatForm.test.tsx`, `AccountSettings.test.tsx`, `FlatSwitcher.test.tsx`) still omit `refetch` but are cast through `as unknown as ReturnType<...>`, so they don't block `tsc -b`. Logged in `deferred-work.md`.
