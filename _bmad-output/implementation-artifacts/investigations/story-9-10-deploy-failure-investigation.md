# Investigation: Story 9.10 deployment — frontend build failure

> **Folded into `architecture.md`** (2026-07-22 doc consolidation, Epic 9 retro Action Item #2): the durable pipeline fact — `tsc -b` type-checks test files as part of the production build with no pre-merge gate — is now AD-22a in Infrastructure & Deployment, independent of this specific incident. This file remains as the historical record.

## Hand-off Brief

1. **What happened.** The `Build frontend` step of the Azure Static Web Apps CI/CD pipeline (run [29688486010](https://github.com/ralfonsoftware/energy-tracker/actions/runs/29688486010/job/88197024162)) failed at `tsc -b` with 32 TypeScript errors across 15 `*.test.tsx`/`*.test.ts` files, all stemming from commit `395a4b2` (story 9.10, "Optimistic-Concurrency Hardening") widening several DTO/hook contracts with new required fields without updating every test file that constructs those shapes.
2. **Where the case stands.** Root cause is Confirmed. Two independent clusters, same mechanism (incomplete test-fixture migration): a `rowVersion: string` field added to 3 response/2 request types (12 files affected), and a `refetch` field added to `useUserSettings()`'s return value (3 files affected, including 2 in a different feature slice that cross-imports the hook).
3. **What's needed next.** Mechanical fix: add the missing field to each listed mock/fixture object. Recommend `bmad-quick-dev` — no design decision required, just restoring type-consistency across the touched files.

## Case Info

| Field            | Value                                                                                                             |
| ---------------- | ------------------------------------------------------------------------------------------------------------------ |
| Ticket           | N/A (reported via GitHub Actions run link)                                                                        |
| Date opened      | 2026-07-19                                                                                                         |
| Status           | Concluded                                                                                                          |
| System           | GitHub Actions, `azure-static-web-apps.yml`, `Build frontend` step (`npm ci && npm run build` → `tsc -b && vite build`) |
| Evidence sources | GitHub Actions job log (`gh run view 29688486010 --job 88197024162 --log-failed`), git history (commit `395a4b2`), source tree |

## Problem Statement

User report: "the deployment of story 9.10 failed. frontend build failed." with a link to the failing GitHub Actions job. Confirmed accurate — the `Build frontend` step failed with exit code 2 from `tsc -b`.

## Evidence Inventory

| Source                                  | Status    | Notes                                                                                   |
| ---------------------------------------- | --------- | ---------------------------------------------------------------------------------------- |
| GitHub Actions failed-step log           | Available | Full `tsc -b` error output retrieved via `gh run view --log-failed`.                     |
| Commit `395a4b2` diff                    | Available | 67 files changed; full stat and targeted per-file diffs reviewed.                        |
| Working tree (current `main`, post-merge)| Available | Errors reproducible by reading the exact lines cited in the log.                         |
| CI pipeline definition                    | Available | `.github/workflows/azure-static-web-apps.yml` — single pipeline, no separate test stage. |

## Timeline of Events

| Time (UTC)           | Event                                                                 | Source                          | Confidence |
| --------------------- | ---------------------------------------------------------------------- | -------------------------------- | ---------- |
| 2026-07-19 15:13 (local, commit) | Commit `395a4b2` "story 9.10 Optimistic-Concurrency Hardening" pushed to `main` | `git log` | Confirmed |
| 2026-07-19T13:16:07Z  | CI job `deploy` → step `Build frontend` starts (`npm ci`)              | Job log                          | Confirmed  |
| 2026-07-19T13:16:13Z  | `npm ci` completes, `tsc -b && vite build` begins                      | Job log                          | Confirmed  |
| 2026-07-19T13:16:19Z  | `tsc -b` emits 32 `TS2741`/`TS2322`/`TS2345` errors, process exits 2   | Job log                          | Confirmed  |

## Confirmed Findings

### Finding 1: `rowVersion` made required on 3 response types and 2 request types, but only some consuming test files were updated

**Evidence:** `git show 395a4b2` — `client/src/features/flat-structure/api/flatStructureApi.ts` (+2), `client/src/features/readings/api/readingApi.ts` (+3), `client/src/features/tariffs/api/tariffApi.ts` (+2), plus corresponding backend DTO changes in `api/Features/{FlatStructure,Readings,Tariffs}/*Models.cs` all adding a `RowVersion`/`rowVersion` column driven by the new `AddOptimisticConcurrencyRowVersions` EF migration.

**Detail:** The story added optimistic-concurrency tokens end-to-end (DB column → EF entity → DTO → TS type). `FlatStructureResponse`, `ReadingResponse`, `TariffResponse` gained a required `rowVersion: string`; `UpdateFlatStructureRequest` and `PatchTariffRequest`/patch-reading arguments gained a required `rowVersion: string` input. The commit updated fixtures in `FlatStructureEditor.test.tsx`, `ReadingHistorySheet.test.tsx`, and `TariffList.test.tsx` — but only partially (see Finding 3) — and did not touch these files at all, which construct the same types and now fail `tsc -b`:

- `client/src/features/flat-structure/hooks/useFlatStructure.test.ts:12`
- `client/src/features/flat-structure/hooks/useUpdateFlatStructure.test.ts:12,36,47`
- `client/src/features/readings/hooks/usePatchReading.test.ts:12,38,49`
- `client/src/features/readings/hooks/useReadingHistory.test.ts:13`
- `client/src/features/readings/hooks/useSubmitReading.test.ts:12`
- `client/src/features/tariffs/components/TariffForm.test.tsx:21,31`
- `client/src/features/tariffs/hooks/useCreateTariff.test.ts:12`
- `client/src/features/tariffs/hooks/usePatchTariff.test.ts:12,40,51`
- `client/src/features/tariffs/hooks/useTariffs.test.ts:13`

### Finding 2: `refetch` made required on `useUserSettings()`'s return value, but 2 of 3 consuming files (in a different feature slice) were never touched

**Evidence:** `git show 395a4b2 -- client/src/features/settings/hooks/useUserSettings.ts`:
```diff
-  const { data: settings, isLoading, isError } = useQuery({ ... })
+  const { data: settings, isLoading, isError, refetch } = useQuery({ ... })
-  return { settings, isLoading, isError }
+  return { settings, isLoading, isError, refetch }
```

**Detail:** `useUserSettings` is defined in `client/src/features/settings/hooks/` but consumed cross-feature (see Side Findings) by `client/src/features/onboarding/OnboardingPage.tsx:31` and `client/src/features/onboarding/components/OnboardingGate.tsx:5`. The commit only edited `client/src/features/settings/SettingsPage.test.tsx` (and even there, incompletely — Finding 3). It never touched:

- `client/src/features/onboarding/OnboardingPage.test.tsx:33,39,45,51`
- `client/src/features/onboarding/components/OnboardingGate.test.tsx:32,38,45,51,59`

All 9 error sites mock `useUserSettings` (or the equivalent `useQuery`-shaped result) with an object literal missing `refetch`, which no longer structurally matches the hook's new return type.

### Finding 3: Even the files the commit did touch have leftover unmigrated call sites

**Evidence:** `client/src/features/settings/SettingsPage.test.tsx:47` (`describe('SettingsPage TariffSettingsRoute')` → `beforeEach`) and `:81` still call `mockUseUserSettings.mockReturnValue({ settings, isLoading, isError })` without `refetch`, even though other `mockReturnValue` sites earlier in the same file were fixed. Same pattern in `client/src/features/flat-structure/components/FlatStructureEditor.test.tsx:61` — the shared `defaultTemplateResponse` fixture used across multiple `it()` blocks was never given a base `rowVersion`, so both direct uses (line 61) and `Partial<FlatStructureResponse>`-override spreads of it (line 68, where the override's `rowVersion?` widens to `string | undefined`) fail.

**Detail:** This indicates the fixture migration was done by editing specific known-affected call sites rather than searching exhaustively for every construction of the changed types within the touched files, let alone across feature-slice boundaries.

## Deduced Conclusions

### Deduction 1: The build step fails, not the tests — because `tsc -b` type-checks test files too

**Based on:** Finding 1, Finding 2, Finding 3; CI step definition (`npm run build` → `tsc -b && vite build`); `project-context.md` "Known gaps — do not re-implement or work around: No `dotnet test` / `npm test` in CI — tests do not run in CI pipeline."

**Reasoning:** No `tsconfig` exclusion separates `*.test.ts(x)` from the production build's type-check scope, so `tsc -b` compiles (but does not execute) every test file in the project as part of `npm run build`. Test files with stale mock/fixture shapes are therefore capable of blocking a production deploy even though the tests themselves never run in this CI pipeline.

**Conclusion:** This is why a change that "only" broke test fixtures manifested as a full frontend build/deploy failure rather than a silently-red test suite.

### Deduction 2: Both clusters share one mechanism — widen a shared type, migrate call sites incompletely

**Based on:** Findings 1–3.

**Reasoning:** In both clusters, a legitimate, correctly-implemented type change (adding a concurrency token; exposing `refetch` for a settings-refresh use case) was migrated only at the call sites the author happened to touch or think of, not exhaustively across every file referencing the type — including files in another feature slice for the `refetch` case.

**Conclusion:** Root cause is a scope-completeness gap in the story 9.10 commit's test-fixture updates, not a logic defect in the shipped production code paths.

## Hypothesized Paths

None — evidence was sufficient to reach a Confirmed root cause without unconfirmed branches.

## Missing Evidence

None outstanding — the failing log, the introducing commit, and the current source state fully explain the failure.

## Source Code Trace

| Element       | Detail                                                                                                    |
| ------------- | ------------------------------------------------------------------------------------------------------------ |
| Error origin  | `tsc -b` invoked from `client/package.json`'s `build` script, run by `.github/workflows/azure-static-web-apps.yml` step `Build frontend` |
| Trigger       | `npm run build` on every push to `main` (single-pipeline, no staging environment per `project-context.md`) |
| Condition     | Any `*.test.ts(x)` file in the type-check scope constructs an object literal against a type that commit `395a4b2` widened, without including the new required field |
| Related files | See Findings 1–3 for the full list of 15 files / 32 error sites; introducing change: `client/src/features/settings/hooks/useUserSettings.ts`, `client/src/features/{flat-structure,readings,tariffs}/api/*Api.ts` |

## Conclusion

**Confidence:** High

Root cause is Confirmed: commit `395a4b2` (story 9.10) widened `FlatStructureResponse`, `ReadingResponse`, `TariffResponse`, `UpdateFlatStructureRequest`, `PatchTariffRequest`-style request bodies (added required `rowVersion: string`) and `useUserSettings()`'s return shape (added required `refetch`), but the accompanying test-fixture updates were incomplete — both across files never touched (9 files) and within files partially edited (3 files, at specific leftover call sites). Because `tsc -b` type-checks test files as part of the production build (tests are never executed in CI, but they are compiled), these 32 stale object literals block the deploy. No part of the production runtime code is implicated.

## Recommended Next Steps

### Fix direction

Single mechanism, applied at 15 file / 32 call-site granularity — add the newly-required field to each mock/fixture object literal:

- **`rowVersion` cluster** (12 sites across 9 files, plus the 2 partial files from Finding 3): add a `rowVersion: 'AQID'`-style string to every `FlatStructureResponse`/`ReadingResponse`/`TariffResponse` fixture and every `UpdateFlatStructureRequest`/`PatchTariffRequest`/patch-reading-args literal listed in Finding 1 and Finding 3. For `FlatStructureEditor.test.tsx`, fix at the shared `defaultTemplateResponse` constant (line 61) so downstream `Partial<>` overrides at line 68 inherit a concrete `rowVersion` rather than widening it to optional.
- **`refetch` cluster** (9 sites across 3 files from Finding 2 and Finding 3): add `refetch: vi.fn()` (or equivalent) to every `mockUseUserSettings.mockReturnValue({...})` call in `OnboardingPage.test.tsx`, `OnboardingGate.test.tsx`, and the two remaining sites in `SettingsPage.test.tsx`.

No production code changes are needed; this is exclusively a test-fixture consistency fix.

### Diagnostic

None needed — root cause is Confirmed and the fix is mechanical. Suggest running `npm run build` locally in `client/` after the fixture edits to confirm `tsc -b` passes before re-pushing, since CI currently has no separate type-check-only gate to catch this pre-merge.

## Reproduction Plan

1. `cd client && npm ci`
2. `npm run build` (equivalently `tsc -b && vite build`)
3. Observe the same 32 errors reproduced locally at the file:line locations listed in Finding 1–3.

## Side Findings

- `client/src/features/onboarding/OnboardingPage.tsx:3` and `client/src/features/onboarding/components/OnboardingGate.tsx:2` import `useUserSettings` from `@/features/settings/hooks/useUserSettings` — a cross-feature hook import. `project-context.md`'s VSA slice-isolation rule states "Each slice is self-contained — cross-slice hook imports are forbidden." This existing violation is exactly why the `refetch` contract change silently fanned out into the `onboarding` slice's tests without any onboarding-related lines appearing in the introducing commit's diff — worth flagging for a follow-up cleanup independent of this fix.
- No pre-merge CI gate currently runs `tsc -b` (or any build/typecheck) on pull requests before merge to `main` — the failure was only caught at deploy time, on `main` itself, per `project-context.md`'s "No `dotnet test` / `npm test` in CI" gap and the single-pipeline/no-staging-environment setup. A PR-time typecheck step would have caught this before merge.
