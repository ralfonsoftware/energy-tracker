---
baseline_commit: e304b53cd5eed0e9e7c96791a29fe23e5e93f459
---

# Story 11.11: `localDate.ts` Correctness Hardening

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the team maintaining this app,
I want the shared date-handling utility to be correct at its boundaries,
so that fixing a date bug once actually fixes it everywhere, instead of leaving known gaps in the single shared implementation every caller now depends on.

## Acceptance Criteria

1. **Given** `addMonths`'s current unguarded `setMonth()` call (`client/src/lib/localDate.ts:16-20`), **when** implemented, **then** it clamps to the last valid day of the target month instead of overflowing (e.g. Jan 31 + 1 month → Feb 28, not Mar 3), with a regression test covering at least one month-end overflow case per month-length variant (28/29/30/31-day target months).

2. **Given** no `NaN`/Invalid-Date guarding exists anywhere in the file, **when** implemented, **then** `parseLocalDate` and `toLocalDateString` detect an invalid resulting date and throw a clear, descriptive error rather than silently producing `"NaN-NaN-NaN"` or deferring to an uncaught `RangeError` at a distant call site — since the backend always returns valid `DateTimeOffset` values today, this is a fail-fast/clarity improvement, not a new runtime safety net for a reachable production path.

3. **Given** `TariffForm.tsx`'s create-flow write-path asymmetry (the last of three instances of this exact bug class in this codebase — the other two were already fixed for the "upcoming" comparison in Story 4.2 and `TariffList`'s display in the `localDate.ts` extraction), **when** implemented, **then** the create-flow submit path constructs the ISO string using the same local-calendar-date convention `parseLocalDate` expects to read back (not a hardcoded UTC-midnight suffix), with a regression test confirming a tariff created "today" round-trips to display as the same calendar date the user picked, run with a mocked timezone offset on at least one side of UTC.

## Tasks / Subtasks

- [x] Task 1: Fix `addMonths` month-end overflow (AC: 1)
  - [x] In `client/src/lib/localDate.ts`, replace the unguarded `result.setMonth(result.getMonth() + months)` with day-clamping logic: compute the target month's last valid day (e.g. `new Date(year, targetMonthIndex + 1, 0).getDate()` — day `0` of the following month yields the last day of the target month) and clamp the source day to it before constructing the result date.
  - [x] Add regression tests to `client/src/lib/localDate.test.ts`'s existing `describe('addMonths', ...)` block: Jan 31 + 1 month → Feb 28 (non-leap year, e.g. 2026), Jan 31 + 1 month → Feb 29 (leap year, e.g. 2028), Mar 31 + 1 month → Apr 30, and one same-length-or-no-clamp-needed case (e.g. Jan 30 + 1 month → Mar 1... no — pick a target 31-day month, e.g. Feb 28 + 1 month → Mar 28, to confirm no unintended clamping when none is needed).
  - [x] Do not change the existing `addMonths_OneMonthAcrossLocalDateBoundaryWestOfUtc_...` or `addMonths_TwelveMonths_...` tests — both must continue passing unmodified.

- [x] Task 2: Add NaN/Invalid-Date guarding (AC: 2)
  - [x] Add a small internal helper (e.g. `assertValidDate(date: Date): void`) that checks `Number.isNaN(date.getTime())` and `throw`s a plain `new Error('...')` with a message identifying which function/input failed — this codebase's established convention for client-side guard errors is a plain `throw new Error('descriptive message')` (see `client/src/features/*/hooks/use*.ts`'s `if (!flatId) throw new Error('flatId is required')` pattern), not a custom error class.
  - [x] Call the guard at the end of `parseLocalDate` (after constructing the result `Date`) and at the start of `toLocalDateString` (validating its `Date` argument) — this covers `isFutureLocalDate` transitively since it calls `parseLocalDate` internally.
  - [x] Add regression tests: `parseLocalDate` given an unparseable string (e.g. `'not-a-date'`) throws; `toLocalDateString` given `new Date('not-a-date')` throws. Do not add guarding to `addMonths` — it only ever receives an already-validated `Date` from its callers in this codebase, and validating an already-guarded value would be redundant per this story's fail-fast (not defensive-everywhere) scope.

- [x] Task 3: Fix `TariffForm.tsx` create-flow write-path asymmetry (AC: 3)
  - [x] In `client/src/features/tariffs/components/TariffForm.tsx`, `onSubmitCreate` (line 126), replace `` contractStartDate: `${data.contractStartDate}T00:00:00Z` `` (which hardcodes UTC midnight regardless of the user's timezone) with a conversion that constructs the ISO string from **local** midnight for the picked calendar date, so `parseLocalDate`'s local-calendar-date extraction reads back the same day the user selected in any timezone. `data.contractStartDate` is already a `YYYY-MM-DD` string (from the `<input type="date">`, matching `toLocalDateString`'s output format used elsewhere in this same file at line 77).
  - [x] Consider whether this conversion belongs as a new named export in `localDate.ts` (the file this story is hardening, and the natural home per its "single shared implementation" mandate) rather than inline in `TariffForm.tsx` — your call, but if added, follow the file's existing plain-function-export style (no class, no default export).
  - [x] Add a regression test in `TariffForm.test.tsx` (or the nearest existing test file covering the create-flow submit): mock the timezone to one side of UTC (e.g. `vi.stubEnv('TZ', 'America/Sao_Paulo')`, matching `localDate.test.ts`'s established pattern), submit the create form with today's date selected, and assert the `contractStartDate` value passed to `createMutate` — when round-tripped through `parseLocalDate`/`toLocalDateString` — equals the calendar date that was picked, not one day earlier.
  - [x] Do not touch `onSubmitEdit` or any other write path — the epic's audit confirmed this asymmetry exists only in the create-flow submit; the edit flow doesn't write `contractStartDate` at all (it's read-only in edit mode, per `TariffForm.tsx:193-210`).

- [x] Task 4: Close the stale `deferred-work.md` entry (all ACs)
  - [x] Locate `## Deferred from: code review of localDate.ts extraction (2026-07-03)` in `_bmad-output/implementation-artifacts/deferred-work.md` (around line 300) and strike through all three bullets this story resolves (the `addMonths` overflow bullet, the NaN/Invalid-Date guarding bullet, and the round-trip asymmetry bullet), appending `**Closed by Story 11.11 (2026-07-31).**` to each, matching the established `~~strikethrough~~` + closing-note convention (see the same file's `## Deferred from: code review of 3-5-trend-chart-and-spike-detection round 2` section for the exact format). Leave the fourth bullet in that section (`formatDate` duplication between `TariffForm.tsx`/`TariffList.tsx`) untouched — it is explicitly out of scope for this story.

- [x] Task 5: Full regression pass
  - [x] Run `npm run test` (or the project's Vitest invocation) in `client/` and confirm all `localDate.test.ts` and `TariffForm.test.tsx`/`TariffLockIndicator.test.tsx`/`TariffList.test.tsx` (if they exist) tests pass — `TariffLockIndicator.tsx` and `TariffList.tsx` both call `parseLocalDate`/`addMonths` and must not regress from the new guarding or clamping behavior.
  - [x] Run `npm run lint` in `client/` — no `// @ts-ignore` or `as any`.

### Review Findings

- [x] [Review][Patch] Unvalidated `contractStartDate` can crash `onSubmitCreate` uncaught [client/src/features/tariffs/schemas/tariffSchema.ts:7] — `contractStartDate: z.string().min(1, 'Required')` only checks non-emptiness, never date format. The old write path (`` `${data.contractStartDate}T00:00:00Z` ``) never threw regardless of input; the new `toLocalMidnightIsoString(data.contractStartDate)` (`TariffForm.tsx:126`) calls `assertValidDate` and throws for any non-empty-but-unparseable string (e.g. a browser falling back to a plain text input for `type="date"`). There is no try/catch around the `createMutate` call, so this would crash the submit handler uncaught. **Fixed:** added `.regex(/^\d{4}-\d{2}-\d{2}$/, 'Invalid date')` to the zod schema, closing the gap at the validation boundary without adding a try/catch.
- [x] [Review][Defer] `parseLocalDate`/`formatDate` can still throw uncaught on malformed backend-sourced dates [client/src/features/tariffs/components/TariffForm.tsx:70,194] — deferred, pre-existing. Explicitly scoped out by this story's own Dev Notes ("the backend always returns valid `DateTimeOffset` values today... not a new runtime safety net for a reachable production path"); no error boundary exists anywhere in `client/src` today, and this story deliberately didn't add one.
- [x] [Review][Defer] AC3's regression test covers only one timezone direction (west of UTC) [client/src/features/tariffs/components/TariffForm.test.tsx:206-225] — deferred, pre-existing test-coverage gap. Satisfies AC3 as literally written ("at least one side of UTC"); an east-of-UTC case would harden confidence but isn't required by the spec.
- [x] [Review][Defer] `addMonths` has no regression test for negative `months` or a Dec→Jan year rollover under the new clamping logic [client/src/lib/localDate.ts:35-41] — deferred, pre-existing test-coverage gap. Logic reads correct on inspection (JS `Date` normalizes month/year rollover in both directions) but is untested for negative deltas.

## Dev Notes

### Why this story exists now (not a from-scratch audit)

All three bugs were already identified and explicitly deferred, verbatim, in `deferred-work.md`'s `## Deferred from: code review of localDate.ts extraction (2026-07-03)` section — the epic's Note text for this story is a close paraphrase of that entry. This story is the fix pass for a known, already-diagnosed gap, not new discovery work. Task 4 closes that entry using this codebase's established convention (see Story 11.10's identical pattern for closing a stale `deferred-work.md` entry).

### Current file contents (read in full during story creation)

`client/src/lib/localDate.ts` (20 lines, 4 exported functions — no other functions in the file):

```
toLocalDateString(date: Date): string       // line 1 — formats YYYY-MM-DD from LOCAL getters
parseLocalDate(isoDateTime: string): Date    // line 11 — extracts LOCAL calendar-date parts from an ISO instant
addMonths(date: Date, months: number): Date  // line 16 — THE BUG: unguarded setMonth() overflow
isFutureLocalDate(isoDateTime: string): boolean  // line 22 — calls parseLocalDate + toLocalDateString internally
```

`parseLocalDate`'s doc comment (line 8-10) already states its contract precisely: "Extracts the LOCAL calendar date of the instant an ISO datetime names — never the UTC calendar date... using UTC parts here has caused the same off-by-one-day bug three times in this codebase." AC3's fix is the third and final occurrence of that exact bug class this comment refers to.

### AC3 — exact mechanics of the bug (so the fix isn't guessed at)

`TariffForm.tsx:126`, inside `onSubmitCreate`:
```
contractStartDate: `${data.contractStartDate}T00:00:00Z`,
```
`data.contractStartDate` is a `YYYY-MM-DD` string from the native `<input type="date">` (registered via `register('contractStartDate')` at line 204), representing the calendar date the user picked in **their local timezone** — no timezone information is inherent to it. Appending `T00:00:00Z` forces this to be interpreted as UTC midnight, an arbitrary and incorrect instant for any user not in UTC.

Later, when this same value is read back (e.g. `TariffForm.tsx:70` for edit-mode defaults, `TariffLockIndicator.tsx:13`, `TariffList.tsx:22`), `parseLocalDate` extracts the **local** calendar-date parts (`getFullYear()`/`getMonth()`/`getDate()`) from that instant. For a user west of UTC (e.g. `America/Sao_Paulo`, UTC-3), `2026-07-31T00:00:00Z` is `2026-07-30 21:00` local time — `parseLocalDate` extracts `2026-07-30`, one calendar day earlier than what was picked.

The fix must produce an ISO string that, when re-interpreted by `parseLocalDate`, yields back the exact calendar date the user selected — i.e. the ISO string must represent **local midnight** for that date (not UTC midnight). Constructing a `Date` from a date-time string with no `Z`/offset suffix (e.g. `` new Date(`${data.contractStartDate}T00:00:00`) ``) is interpreted by the JS engine as local time, and calling `.toISOString()` on that `Date` correctly serializes it to the equivalent UTC instant — this is the same technique already implicit in how `parseLocalDate` reads values back, just applied at write time instead of read time.

### AC1 — clamping algorithm

Standard "clamp to last day of target month" pattern: the target month's last valid day is `new Date(year, targetMonthIndex + 1, 0).getDate()` (JS `Date` treats day `0` of month `N+1` as the last day of month `N`). Clamp the source day to `Math.min(sourceDay, lastValidDayOfTargetMonth)` before constructing the result. `targetMonthIndex` can exceed `11` or go negative — `new Date(year, targetMonthIndex, day)` handles year rollover correctly on its own (this part of the existing behavior, exercised by the passing `addMonths_TwelveMonths_AdvancesYearByOne` test, must not regress).

### AC2 — scope boundary (don't over-guard)

Per the epic's own framing: "the backend always returns valid `DateTimeOffset` values today, this is a fail-fast/clarity improvement, not a new runtime safety net for a reachable production path." Do not add try/catch error boundaries anywhere in `client/src` as part of this story, and do not guard `addMonths` itself — it only receives `Date` objects already produced by `parseLocalDate` or `new Date()` in every current call site (`TariffLockIndicator.tsx:13,16`), so guarding its input would be redundant with the guards added in `parseLocalDate`/`toLocalDateString`.

### Call sites — full inventory (verified via grep, read in full)

All 4 consumers of `localDate.ts`, none of which are touched by this story except `TariffForm.tsx` (AC3):
- `client/src/features/tariffs/components/TariffList.tsx` — `parseLocalDate`, `isFutureLocalDate` (display + upcoming-tariff filtering)
- `client/src/features/tariffs/components/TariffForm.tsx` — `toLocalDateString`, `parseLocalDate` (AC3's target)
- `client/src/features/tariffs/components/TariffLockIndicator.tsx` — `parseLocalDate`, `addMonths` (lock-until date display — benefits from AC1's fix automatically once `localDate.ts` is fixed, no separate change needed here)
- `client/src/features/decomposition/lib/periods.ts` — `toLocalDateString` only (period boundary formatting; unaffected by any of these three bugs — no `addMonths` or `parseLocalDate` usage)

### Testing standards summary

- [Source: _bmad-output/project-context.md] Vitest `globals: true` (`describe`/`it`/`expect` are global, do not import from `vitest`); `.test.ts`/`.test.tsx` co-located next to the file under test; query by role/label/text over CSS class or `data-testid`.
- `localDate.test.ts` already establishes the `vi.stubEnv('TZ', '<IANA zone>')` / `vi.unstubAllEnvs()` (in `afterEach`) pattern for timezone-dependent tests — reuse it exactly for both AC1's and AC3's new tests. Do not introduce a different timezone-mocking approach (e.g. mocking `Intl.DateTimeFormat` or `Date` globally).
- `TariffForm.tsx` test file location, if it exists: `client/src/features/tariffs/components/TariffForm.test.tsx` — check before assuming it needs to be created from scratch.

### Project Structure Notes

- Files touched: `client/src/lib/localDate.ts` (production code, AC1+AC2), `client/src/lib/localDate.test.ts` (AC1+AC2 regression tests), `client/src/features/tariffs/components/TariffForm.tsx` (production code, AC3), `client/src/features/tariffs/components/TariffForm.test.tsx` (AC3 regression test — create if it doesn't exist, following this feature folder's existing test conventions), `_bmad-output/implementation-artifacts/deferred-work.md` (Task 4).
- No backend changes, no new dependencies, no migrations, no i18n additions (no new user-visible strings — the new thrown errors are developer-facing, not rendered).
- `client/src/lib/` has no subdirectory structure requirement (unlike feature folders) — `localDate.ts` and its test stay flat, consistent with existing sibling files (`apiClient.ts`, `localeNumber.ts`, `useSubmitGuard.ts`).

### Previous story intelligence (Story 11.10)

Story 11.10 was a backend-only verification story (Onboarding/PatchFlat HTTP test coverage) with zero shared surface area with this story — no transferable code patterns apply. `deferred-work.md` was checked for a `blocks: Story 11.11` tag per this project's standing process — none found (only tag in the file targets Stories 10.2/10.3, unrelated).

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-11-post-epic-10-hardening-and-technical-debt-resolution.md#Story 11.11] — original epic AC text (used verbatim above).
- [Source: _bmad-output/implementation-artifacts/deferred-work.md — "## Deferred from: code review of localDate.ts extraction (2026-07-03)"] — the original diagnosis this story's epic text paraphrases; the entry Task 4 closes (3 of its 4 bullets).
- [Source: client/src/lib/localDate.ts] — full current implementation, read in full during story creation.
- [Source: client/src/lib/localDate.test.ts] — full current test suite (established `vi.stubEnv('TZ', ...)` pattern), read in full during story creation.
- [Source: client/src/features/tariffs/components/TariffForm.tsx] — full current implementation, read in full during story creation; line 126 is AC3's exact target.
- [Source: client/src/features/tariffs/components/TariffLockIndicator.tsx] and [Source: client/src/features/tariffs/components/TariffList.tsx] — the other 2 call sites, read in full to confirm no changes needed beyond `localDate.ts` itself.
- [Source: client/src/features/insights/hooks/useTriggerInsights.ts], [Source: client/src/features/tariffs/hooks/usePatchTariff.ts] — basis for the established `throw new Error('message')` guard-error convention applied in AC2.
- [Source: _bmad-output/project-context.md] — frontend testing conventions applied above.
- [Source: _bmad-output/implementation-artifacts/11-10-http-level-test-coverage-onboarding-and-patchflat.md] — previous story in this epic; confirmed no shared surface area.

## Change Log

- 2026-07-31: Story created. All three ACs map directly to a still-open `deferred-work.md` entry from the 2026-07-03 `localDate.ts` extraction review — this is a fix pass for an already-diagnosed gap, not new discovery. Full call-site inventory (4 consumers) confirmed via grep; only `TariffForm.tsx` needs a production-code change beyond `localDate.ts` itself.
- 2026-07-31: Story implemented — all 3 ACs fixed via TDD (red/green per task), `deferred-work.md`'s localDate.ts extraction entry closed (3 of 4 bullets), full regression pass green (476 tests, tsc -b clean, lint clean). Status → review.
- 2026-07-31: Code review complete (0 decision-needed, 1 patch, 3 defer, 12 dismissed). No AC violations found. Patch applied: tightened `tariffSchema.ts`'s `contractStartDate` validation with a `YYYY-MM-DD` regex to close a crash path where `toLocalMidnightIsoString` could throw uncaught on a malformed (non-empty) value. 3 low-severity items deferred to `deferred-work.md`. Status → done.

## Dev Agent Record

### Agent Model Used

claude-sonnet-5

### Debug Log References

None — no issues requiring a debug log; all tests passed red→green on first implementation attempt per task.

### Completion Notes List

- AC1: `addMonths` now clamps the source day to the target month's last valid day (`new Date(year, targetMonthIndex + 1, 0).getDate()`) before constructing the result, instead of letting an unguarded `setMonth()` overflow into the following month. Added 4 regression tests (Jan 31→Feb 28 non-leap, Jan 31→Feb 29 leap, Mar 31→Apr 30, Feb 28→Mar 28 no-clamp-needed control case); the two pre-existing `addMonths` tests were left unmodified and still pass.
- AC2: Added an internal `assertValidDate(date, context)` helper (`Number.isNaN(date.getTime())` check, plain `throw new Error(...)` per this codebase's established guard-error convention) called at the end of `parseLocalDate` and the start of `toLocalDateString`. `isFutureLocalDate` is covered transitively. `addMonths` was deliberately left unguarded per the story's explicit scope boundary (it only ever receives already-validated `Date`s from current callers). Added 2 regression tests for the unparseable-string and invalid-`Date` cases.
- AC3: Added a new named export `toLocalMidnightIsoString(yyyyMmDd: string): string` to `localDate.ts` — the write-side counterpart to `parseLocalDate`'s local-calendar-date read, constructing the ISO string from local midnight (via a no-offset `Date` constructor + `.toISOString()`) rather than a hardcoded UTC-midnight suffix. `TariffForm.tsx`'s `onSubmitCreate` now calls this instead of `` `${data.contractStartDate}T00:00:00Z` ``. Added a regression test in `TariffForm.test.tsx` that mocks `TZ=America/Sao_Paulo` (west of UTC), submits the create form, and confirms the `contractStartDate` payload round-trips through `parseLocalDate`/`toLocalDateString` to the exact calendar date picked — this test reproduced the one-day-off bug against the old code (confirmed red) before the fix (confirmed green). `onSubmitEdit` was not touched.
- Task 4: Closed 3 of the 4 bullets in `deferred-work.md`'s `## Deferred from: code review of localDate.ts extraction (2026-07-03)` section (`addMonths` overflow, NaN/Invalid-Date guarding, round-trip asymmetry) with `~~strikethrough~~` + `**Closed by Story 11.11 (2026-07-31).**`, matching the file's established convention. The 4th bullet (`formatDate` duplication) was left untouched — explicitly out of scope.
- Full regression: `npm run test` → 476/476 tests pass across 69 files (client). `npx tsc -b` → clean, no type errors. `npm run lint` (oxlint) → clean except pre-existing unrelated `router.tsx` fast-refresh warnings (not touched by this story). No `@ts-ignore` or `as any` introduced.

### File List

- `client/src/lib/localDate.ts` (modified — AC1, AC2, AC3 support function)
- `client/src/lib/localDate.test.ts` (modified — AC1, AC2 regression tests)
- `client/src/features/tariffs/components/TariffForm.tsx` (modified — AC3)
- `client/src/features/tariffs/components/TariffForm.test.tsx` (modified — AC3 regression test)
- `_bmad-output/implementation-artifacts/deferred-work.md` (modified — Task 4)
