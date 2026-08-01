# Investigation: Story 12.1 deployment failure (GitHub Actions run 30709243022)

## Hand-off Brief

1. **What happened.** The `deploy` job's "Build frontend" step (`npm run build` → `tsc -b && vite build`) failed with two `TS2739` errors: `client/src/features/flat-structure/components/FlatStructureEditor.test.tsx:118` and `:336` construct `DeviceResponse`-typed mock objects that predate Story 12.1 and were never updated with the two new required fields (`inUseSince`, `decommissionedDate`) that Story 12.1 added to the `DeviceResponse` type — Confirmed.
2. **Where the case stands.** Root cause Confirmed with High confidence and reproduced locally (`npx tsc -b`). Concluded — no further evidence needed.
3. **What's needed next.** Trivial two-line fix (add the two missing fields to both mock literals) — recommend `bmad-quick-dev`. Separately, a systemic verification-command gap was found and is recorded as a Side Finding: this repo's project-referenced `tsconfig.json` makes bare `npx tsc --noEmit` silently check zero files, so it can never catch this class of error — it is the exact command ~20+ story files (including 12.1's own Task 6.6) document as the type-check verification step.

## Case Info

| Field            | Value                                                                                          |
| ---------------- | ------------------------------------------------------------------------------------------------ |
| Ticket           | N/A — GitHub Actions run [30709243022](https://github.com/ralfonsoftware/energy-tracker/actions/runs/30709243022) |
| Date opened      | 2026-08-01                                                                                       |
| Status           | Concluded                                                                                         |
| System           | GitHub Actions (`ubuntu-24.04`), Node 22, npm 11.17.0, .NET 10, workflow `.github/workflows/azure-static-web-apps.yml` |
| Evidence sources | GitHub Actions run logs (`gh run view`), local reproduction (`npx tsc -b`), git history (`d0da556`), workflow YAML, `tsconfig.json`/`tsconfig.app.json` |

## Problem Statement

User-reported: "the deployment of story 12.1 failed." Run 30709243022 on `main`, triggered by push. The `test` job passed; the `deploy` job failed at the "Build frontend" step.

## Evidence Inventory

| Source                                    | Status    | Notes                                                                                   |
| ------------------------------------------ | --------- | ---------------------------------------------------------------------------------------- |
| GitHub Actions run logs                    | Available | `gh run view 30709243022 --log-failed`; exact `tsc` error text with file:line captured |
| `.github/workflows/azure-static-web-apps.yml` | Available | Confirms `test` job runs `npm test -- --run` (Vitest, no type-check); `deploy` job runs `npm run build` (`tsc -b && vite build`, full type-check) |
| Git history (`d0da556`)                    | Available | Story 12.1's committed diff — `git show d0da556 --stat` confirms `FlatStructureEditor.test.tsx` was **not** among the 23 changed files |
| Local reproduction                         | Available | `npx tsc -b` on the current (clean, matching `origin/main`) working tree reproduces both errors exactly |
| `tsconfig.json` / `tsconfig.app.json`      | Available | Root `tsconfig.json` has `"files": []` and only `references` — explains why bare `npx tsc --noEmit` is a silent no-op in this repo |

## Investigation Backlog

| # | Path to Explore | Priority | Status | Notes |
| - | --------------- | -------- | ------ | ----- |
| 1 | Audit all ~20+ story files documenting `npx tsc --noEmit` as the verification command; correct to `npx tsc -b` (or `npm run build`) | Medium | Open | Systemic gap, not blocking this fix; see Side Findings |
| 2 | Consider adding a pre-push/pre-commit hook or a CI-earlier `test` job step running `npx tsc -b`, so a build-breaking type error fails fast in the `test` job instead of only surfacing in `deploy` | Low | Open | Would have caught this before merge to `main`, since `test` currently runs before `deploy` but doesn't type-check at all |

## Timeline of Events

| Time (UTC)          | Event                                                                                   | Source                          | Confidence |
| -------------------- | ---------------------------------------------------------------------------------------- | -------------------------------- | ---------- |
| 2026-08-01 (unknown, prior) | `DeviceResponse` type in `flatStructureApi.ts` gains two new required fields (`inUseSince`, `decommissionedDate`) as part of Story 12.1 | `d0da556` diff                   | Confirmed  |
| 2026-08-01 18:56:28 +02:00 | Commit `d0da556` "feat: story 12.1" pushed to `main` — does not touch `FlatStructureEditor.test.tsx` | `git log`/`git show d0da556`     | Confirmed  |
| 2026-08-01T16:58:49Z | GitHub Actions run 30709243022 starts (`push` trigger)                                  | `gh run view --log-failed`       | Confirmed  |
| ~16:58–17:00Z (test job) | `test` job's "Test frontend" step runs `npm test -- --run` (Vitest) — passes; Vitest transpiles via esbuild and does not perform TypeScript type-checking | workflow YAML + run summary (`test` ✓ in 2m1s) | Confirmed  |
| 2026-08-01T16:59:53Z | `deploy` job's "Build frontend" step runs `npm run build` → `tsc -b && vite build`; `tsc -b` fails with `TS2739` at `FlatStructureEditor.test.tsx:118` and `:336` | log line 302-303 in fetched output | Confirmed  |
| 2026-08-01T16:59:xx Z | Deploy job exits with code 2; deploy job fails; `deploy_preview`/downstream steps skipped | `gh run view` job summary        | Confirmed  |

## Confirmed Findings

### Finding 1: `deploy` job fails in `tsc -b`, not in tests or `vite build` proper

**Evidence:** `gh run view 30709243022 --repo ralfonsoftware/energy-tracker --log-failed` → log lines 302-303:
```
##[error]src/features/flat-structure/components/FlatStructureEditor.test.tsx(118,15): error TS2739: Type '{ deviceId: string; ...; selfMeasuredPeriod: null; }' is missing the following properties from type 'DeviceResponse': inUseSince, decommissionedDate
##[error]src/features/flat-structure/components/FlatStructureEditor.test.tsx(336,19): error TS2739: ... (identical)
```

**Detail:** `.github/workflows/azure-static-web-apps.yml:83-87` — the `deploy` job's "Build frontend" step runs `npm ci && npm run build`, and `client/package.json`'s `build` script is `tsc -b && vite build`. The failure is `tsc -b` (project-reference build mode), which exits before `vite build` ever runs.

### Finding 2: The two failing mock objects predate Story 12.1 and were never touched by it

**Evidence:** `git show d0da556 --stat` — 23 files changed, none named `FlatStructureEditor.test.tsx`. `client/src/features/flat-structure/api/flatStructureApi.ts` (4 lines changed in that commit) is where `DeviceResponse` gained `inUseSince: string | null` and `decommissionedDate: string | null` as **required** (non-optional) fields.

**Detail:** `FlatStructureEditor.test.tsx:118-130` and `:336-348` each construct an object literal assigned into a `DeviceResponse`-shaped position (via `seededResponseWithDevice()`/inline fixture) listing 10 fields (`deviceId`, `name`, `type`, `manufacturer`, `model`, `purchaseDate`, `consumptionApproach`, `euLabelClass`, `euAnnualKwh`, `selfMeasuredKwh`, `selfMeasuredPeriod`) — the exact field set `DeviceResponse` had *before* Story 12.1. Story 12.1's own scope (Story file "Project Structure Notes") explicitly lists only `DeviceEditor.tsx`/`DeviceEditor.test.tsx`/`draftModel.ts`/`flatStructureApi.ts`/locale files as frontend files touched — `FlatStructureEditor.test.tsx` was correctly out of scope for the story's *implementation*, but the type change it made is a breaking change for any pre-existing `DeviceResponse` fixture elsewhere in the codebase, and this one wasn't caught.

### Finding 3: The `test` CI job cannot catch this class of error — Vitest doesn't type-check

**Evidence:** `.github/workflows/azure-static-web-apps.yml:36-40` — `test` job's "Test frontend" step: `npm ci && npm test -- --run` (`client/package.json`'s `test` script is bare `vitest`). Vitest uses esbuild for transpilation, which strips TypeScript types without validating them. The `test` job passed (✓ in 2m1s per `gh run view` summary) despite the type error existing in the same file at the same commit.

**Detail:** This explains the split-brain outcome (`test` ✓, `deploy` ✗) — it is not a flaky/nondeterministic CI failure. The same commit, run at the same time, produces a deterministic pass in `test` and a deterministic fail in `deploy` because the two jobs run fundamentally different validation (transpile-only vs. full type-check).

### Finding 4: Bare `npx tsc --noEmit` — the command story files document as the verification step — is a silent no-op in this repo

**Evidence:** `client/tsconfig.json`:
```json
{
  "files": [],
  "references": [
    { "path": "./tsconfig.app.json" },
    { "path": "./tsconfig.node.json" }
  ]
}
```
Local reproduction: `cd client && npx tsc --noEmit` → **zero output, exit 0** (verified twice against the exact code CI built for this run). `cd client && npx tsc -b` → reproduces both `TS2739` errors exactly, matching the CI log.

**Detail:** With `"files": []` and no `"include"`, `tsc`'s default (non-`-b`) invocation resolves `tsconfig.json` as the project root and finds nothing to compile — it exits 0 having checked 0 files. `tsconfig.app.json` (the file that actually has `"include": ["src"]` and the real `compilerOptions`) is only picked up via `tsc -b`'s project-reference resolution, or via an explicit `--project client/tsconfig.app.json`. `npm run build`'s `tsc -b && vite build` is correct; a bare `npx tsc --noEmit` is not equivalent in this repo and will never surface a type error anywhere in `src/`.

## Deduced Conclusions

### Deduction 1: This was not caught before merge because every verification step that ran actually used the no-op command

**Based on:** Finding 3, Finding 4, and Story 12.1's own Task 6.6 (`_bmad-output/implementation-artifacts/12-1-...md`: "Run `npx tsc --noEmit` and `npm run lint`... clean") and Dev Agent Record completion notes ("`npx tsc --noEmit` clean").

**Reasoning:** The dev agent implementing Story 12.1 ran the documented verification command, got a clean (but meaningless) result, and reported success truthfully based on what it saw. The code review that followed also ran `npx tsc --noEmit` as part of its patch-verification step and got the same clean, meaningless result. Neither the `test` CI job (Vitest, no type-checking) nor any human step in between ran the one command that actually performs a full type-check (`tsc -b` / `npm run build`) until the `deploy` job did, post-merge.

**Conclusion:** The failure is not a one-off omission by Story 12.1's implementation — it's the deterministic outcome of a repo-wide verification gap that will reproduce for any future story that changes a widely-consumed shared type (like `DeviceResponse`) without exhaustively grepping every fixture site by hand.

## Hypothesized Paths

None — root cause is Confirmed and reproduced locally; no open hypotheses remain.

## Missing Evidence

None — all evidence needed to reach a Confirmed conclusion was obtained.

## Source Code Trace

| Element       | Detail                                                                                                   |
| ------------- | ---------------------------------------------------------------------------------------------------------- |
| Error origin  | `client/src/features/flat-structure/components/FlatStructureEditor.test.tsx:118` and `:336` — two `DeviceResponse`-shaped object literals missing `inUseSince`/`decommissionedDate` |
| Trigger       | `tsc -b` (invoked by `npm run build`, invoked by the `deploy` job's "Build frontend" step)                  |
| Condition     | `DeviceResponse` (`client/src/features/flat-structure/api/flatStructureApi.ts`) requires `inUseSince: string \| null` and `decommissionedDate: string \| null` as of commit `d0da556`; TypeScript's excess/missing-property structural check (`TS2739`) fires wherever an object is assigned into a `DeviceResponse`-typed position without them |
| Related files | `client/src/features/flat-structure/api/flatStructureApi.ts` (type definition, changed); `client/src/features/flat-structure/components/DeviceEditor.test.tsx` (sibling fixture — correctly updated by Story 12.1, not affected); `client/tsconfig.json` / `tsconfig.app.json` (verification-command gap, Side Finding) |

## Conclusion

**Confidence:** High — Confirmed root cause, reproduced locally with the exact command CI runs (`npx tsc -b`), against the exact commit CI built (`d0da556`, matches `origin/main`).

Story 12.1 added two new required fields to the shared `DeviceResponse` type but only updated the fixtures in the one test file it touched (`DeviceEditor.test.tsx`). A second, pre-existing test file (`FlatStructureEditor.test.tsx`) constructs `DeviceResponse`-shaped mocks in two places and was never audited for the breaking type change, because no verification step that actually ran (dev agent, code review, or the `test` CI job) performs a real TypeScript type-check in this repo — `npx tsc --noEmit` silently checks zero files here, and `npm test`/Vitest doesn't type-check at all. The `deploy` job's `npm run build` (`tsc -b`) is the only step in the whole pipeline that does, which is why the failure only appeared post-merge, at deploy time.

## Recommended Next Steps

### Fix direction

**Mechanism 1 — the actual break (trivial):** Add `inUseSince: null, decommissionedDate: null,` to both object literals in `FlatStructureEditor.test.tsx` (lines ~129 and ~347, alongside the existing `selfMeasuredPeriod: null,`). Two-line diff, no logic change — these are inert mock fixtures.

**Mechanism 2 — the systemic gap (not blocking, tracked in backlog):** Correct the documented verification command from `npx tsc --noEmit` to `npx tsc -b` (or `npm run build`) everywhere it's cited (story template guidance, `bmad-code-review`'s own patch-verification step, and retroactively in the ~20 existing story files' Dev Notes/Task lists, on a "whenever next touched" basis rather than a mass edit). Optionally, add a type-check step to the `test` CI job so this class of error fails fast pre-merge instead of at deploy.

### Diagnostic

None needed — root cause is Confirmed.

## Reproduction Plan

1. `cd client && npm ci`
2. `npx tsc -b` — reproduces both `TS2739` errors immediately (no need to run the full `npm run build`/`vite build`).
3. To verify the fix: add the two missing fields to both literals in `FlatStructureEditor.test.tsx`, re-run `npx tsc -b` — expect exit 0, no output.

## Side Findings

- **Verification-command gap is repo-wide, not story-specific.** `grep -rl "tsc --noEmit"` across `_bmad-output/implementation-artifacts/*.md` matches ~20 story files (e.g. `10-4-...md`, `11-7-...md`, `8-3-...md`, `9-6-...md`, and Story 12.1's own file) that document `npx tsc --noEmit` as their type-check verification step. Every one of those "clean" reports was checking zero files. This doesn't mean those stories necessarily have live type errors today (most were followed by later stories' `npm run build` succeeding in CI, which would have caught regressions in already-shipped code) — but the verification step itself provided no actual signal at the time it was run, in every one of those stories.
- **The `test` and `deploy` CI jobs validate different things by design (Vitest vs. `tsc -b`), and this split is otherwise a reasonable speed/coverage tradeoff** — flagging only because the gap in signal (no job runs `tsc -b` until deploy) is what let this specific defect slip through both the dev agent's and this session's own code-review verification undetected.
