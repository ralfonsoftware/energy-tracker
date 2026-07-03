---
title: 'Extract client/src/lib/localDate.ts — shared civil-date utilities'
type: 'refactor'
created: '2026-07-03'
status: 'done'
context: []
baseline_commit: '6394afa13a76d683a3482038876d56095f8b619f'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `TariffForm.tsx`, `TariffLockIndicator.tsx`, and `TariffList.tsx` each hand-roll their own local-civil-date logic (today-as-string, ISO-to-local-date-parts, add-months, future-date comparison). Story 4.2's review fixed a UTC-vs-local bug in `TariffForm.todayIsoDate()` and documented the decision "treat contract dates as timezone-independent civil dates — use local date parts, not UTC." Story 4.4 explicitly left `TariffList.tsx`'s `toUtcDateString`/`isUpcoming` untouched ("Left their UTC-vs-local logic untouched" — see story file), which still compares a UTC-extracted date against a local-extracted "today," violating the documented decision and causing incorrect "upcoming" labeling and list-date formatting for users west of UTC. Duplication also means the same class of bug can silently resurface in one file after being fixed in another (as already happened once).

**Approach:** Extract four functions into `client/src/lib/localDate.ts` — `toLocalDateString`, `parseLocalDate`, `addMonths`, `isFutureLocalDate` — implementing the documented local-civil-date-parts semantics once. Migrate all three tariff files to import and use them, deleting their local duplicate implementations. This also fixes the live `TariffList.tsx` UTC/local mismatch as a direct consequence of consolidation, not a separate change.

## Boundaries & Constraints

**Always:**
- All four functions live in `client/src/lib/localDate.ts`, named exports, no default export.
- Behavior is local-civil-date-parts based throughout — never `toISOString()`, never `getUTC*` accessors, never compare a UTC-derived value against a local-derived one.
- `parseLocalDate` and `toLocalDateString` fully replace `TariffForm.tsx`'s `todayIsoDate`/`toLocalDateInputValue`, `TariffLockIndicator.tsx`'s inline `new Date(contractStartDate)` + getter reconstruction, and `TariffList.tsx`'s `toUtcDateString`/`todayLocalDateString`. Delete the originals — no dead code left behind.
- `addMonths` fully replaces `TariffLockIndicator.tsx`'s manual `lockedUntil.setMonth(...)` arithmetic.
- `isFutureLocalDate` fully replaces `TariffList.tsx`'s `isUpcoming`; all call sites (`TariffRow`, `activeTariff` filter) switch to it.
- No behavior change to any already-correct call site (e.g. `TariffForm.tsx`'s existing `formatDate` stays visually identical — only its internals route through the new utility).
- Unit tests in `client/src/lib/localDate.test.ts` cover each function including a timezone-boundary case (mirroring `TariffLockIndicator.test.tsx`'s existing `vi.stubEnv('TZ', 'America/Sao_Paulo')` pattern) proving local-parts (not UTC) semantics.

**Ask First:** None anticipated — this is a mechanical consolidation of already-decided semantics.

**Never:**
- Do not touch backend date handling (`DateTimeOffset` C# code) — out of scope, unrelated layer.
- Do not change the `TariffLockPolicy`/`TariffResolver` locking or resolution logic — this spec is display/UI date-parsing only.
- Do not introduce a date library dependency (e.g. `date-fns`, `luxon`) — stay dependency-free, matching existing hand-rolled style.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Round-trip a UTC-midnight ISO string | `parseLocalDate('2026-01-01T00:00:00Z')` in TZ `America/Sao_Paulo` (UTC-3) | Returns local Date for `2025-12-31` (matches `toLocalDateString` = `'2025-12-31'`), not `2026-01-01` | N/A |
| Add months across a local-date boundary west of UTC | `addMonths(parseLocalDate('2026-01-01T02:00:00Z'), 1)` in TZ `America/Sao_Paulo` | Result formats as January 2026 (Dec 2025 + 1 month), not February 2026 | N/A |
| Future-date comparison at the UTC/local boundary | `isFutureLocalDate('2099-01-01T00:00:00Z')` | `true` in any timezone (far future, unambiguous) | N/A |
| Today is not future | `isFutureLocalDate(toLocalDateString(new Date()) + 'T00:00:00Z')` | `false` | N/A |

</frozen-after-approval>

## Code Map

- `client/src/lib/localDate.ts` -- NEW: `toLocalDateString`, `parseLocalDate`, `addMonths`, `isFutureLocalDate`
- `client/src/lib/localDate.test.ts` -- NEW: unit tests, including TZ-boundary case
- `client/src/features/tariffs/components/TariffForm.tsx` -- remove `todayIsoDate`/`toLocalDateInputValue`, import from `localDate.ts`, simplify `formatDate`
- `client/src/features/tariffs/components/TariffLockIndicator.tsx` -- remove inline date-parts/setMonth logic, import `parseLocalDate`/`addMonths`
- `client/src/features/tariffs/components/TariffList.tsx` -- remove `toUtcDateString`/`todayLocalDateString`/`isUpcoming`, import `parseLocalDate`/`isFutureLocalDate`, fix `formatDate` to parse via `parseLocalDate` first
- `client/src/features/tariffs/components/TariffList.test.tsx` -- add a TZ-boundary regression test for the fixed upcoming/formatDate bug

## Tasks & Acceptance

**Execution:**
- [x] `client/src/lib/localDate.ts` -- create with `toLocalDateString(date: Date): string`, `parseLocalDate(isoDateOrDateTime: string): Date`, `addMonths(date: Date, months: number): Date`, `isFutureLocalDate(isoDateOrDateTime: string): boolean` -- single source of truth for civil-date semantics
- [x] `client/src/lib/localDate.test.ts` -- create, covering the I/O matrix above plus straightforward same-timezone cases
- [x] `client/src/features/tariffs/components/TariffForm.tsx` -- replace `todayIsoDate()` calls with `toLocalDateString(new Date())`; replace `toLocalDateInputValue(iso)` calls with `toLocalDateString(parseLocalDate(iso))`; simplify `formatDate` to `Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' }).format(parseLocalDate(isoDate))`; delete the two local function definitions
- [x] `client/src/features/tariffs/components/TariffLockIndicator.tsx` -- replace `const start = new Date(contractStartDate)` + getter reconstruction with `const start = parseLocalDate(contractStartDate)`; replace manual `setMonth` block with `addMonths(start, contractDurationMonths)`; use `start` directly instead of re-deriving `formattedStart`
- [x] `client/src/features/tariffs/components/TariffList.tsx` -- delete `toUtcDateString`, `todayLocalDateString`, `isUpcoming`; replace all `isUpcoming(x)` call sites with `isFutureLocalDate(x)`; change module-level `formatDate` to `Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' }).format(parseLocalDate(isoDate))`
- [x] `client/src/features/tariffs/components/TariffList.test.tsx` -- add `TariffList_ContractStartsTodayViewedWestOfUtc_DoesNotShowUpcomingLabel` using `vi.stubEnv('TZ', 'America/Sao_Paulo')` + frozen system time, proving the fixed comparison (renamed from the originally planned test name to match the actual scenario proven)

**Acceptance Criteria:**
- Given `client/src/lib/localDate.ts` exists, when any of the three tariff files are inspected, then none contain `getUTC*`, `toISOString()`, or duplicate local-date-parts logic — all route through the shared module
- Given a tariff with `contractStartDate` just after UTC midnight, when viewed by a user in a UTC-negative-offset timezone, then the "upcoming" label and list date display are consistent with the local civil date (regression-proof of the bug this spec fixes)
- Given the existing `TariffLockIndicator.test.tsx` TZ-boundary tests, when run after migration, then they pass unchanged (behavior-preserving refactor for this file)

## Design Notes

The four functions form a small, closed API: `toLocalDateString` (Date → string) and `parseLocalDate` (string → Date) are inverses; `addMonths` and `isFutureLocalDate` are built on top of them, not independent implementations. `parseLocalDate` internally does what `TariffForm.toLocalDateInputValue` already does correctly today — construct a `Date`, read its *local* getters, rebuild a local-midnight `Date` from those parts — this is the pattern to generalize, not reinvent.

```ts
export function toLocalDateString(date: Date): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

export function parseLocalDate(isoDateOrDateTime: string): Date {
  const [year, month, day] = toLocalDateString(new Date(isoDateOrDateTime)).split('-').map(Number)
  return new Date(year, month - 1, day)
}
```

## Verification

**Commands:**
- `cd client && npx vitest run src/lib/localDate.test.ts src/features/tariffs` -- expected: all pass, including new TZ-boundary tests
- `cd client && npm run lint` -- expected: no new lint errors (no `@ts-ignore`, no unused old helper exports)

## Suggested Review Order

**Shared utility (entry point)**

- Start here — the four functions the whole refactor hangs on; `parseLocalDate` carries a WHY comment on the local-vs-UTC invariant three prior bugs were rooted in.
  [`localDate.ts:1`](../../client/src/lib/localDate.ts#L1)

- `parseLocalDate` extracts the *local* calendar date of an instant — never `getUTC*`, the exact class of bug this module exists to prevent.
  [`localDate.ts:11`](../../client/src/lib/localDate.ts#L11)

- `addMonths` builds on `parseLocalDate`'s local-midnight `Date`; known month-end overflow limitation logged in `deferred-work.md`, not fixed here.
  [`localDate.ts:16`](../../client/src/lib/localDate.ts#L16)

- `isFutureLocalDate` replaces `TariffList`'s old UTC-vs-local `isUpcoming` mismatch — both sides of the comparison now go through the same local extraction.
  [`localDate.ts:22`](../../client/src/lib/localDate.ts#L22)

**Bug fix: TariffList's UTC/local mismatch**

- The two call sites where the actual live bug (wrong "upcoming" labeling for users west of UTC) is fixed by switching to `isFutureLocalDate`.
  [`TariffList.tsx:53`](../../client/src/features/tariffs/components/TariffList.tsx#L53), [`TariffList.tsx:160`](../../client/src/features/tariffs/components/TariffList.tsx#L160)

- `formatDate` now parses via `parseLocalDate` first instead of raw `new Date(isoDate)`, fixing the same bug class for the list's date display.
  [`TariffList.tsx:21`](../../client/src/features/tariffs/components/TariffList.tsx#L21)

**Behavior-preserving migration: TariffLockIndicator**

- `start` now comes from `parseLocalDate` instead of an inline `new Date` + getter reconstruction — same output, single source of truth.
  [`TariffLockIndicator.tsx:13`](../../client/src/features/tariffs/components/TariffLockIndicator.tsx#L13)

- Manual `setMonth` arithmetic replaced by `addMonths` — identical behavior (including its known overflow edge case, unchanged).
  [`TariffLockIndicator.tsx:16`](../../client/src/features/tariffs/components/TariffLockIndicator.tsx#L16)

**Behavior-preserving migration: TariffForm**

- Imports the shared utility in place of the two local functions this file used to hand-roll.
  [`TariffForm.tsx:7`](../../client/src/features/tariffs/components/TariffForm.tsx#L7)

**Tests (peripherals)**

- New shared-utility test suite, including the TZ-boundary cases proving local-parts (not UTC) semantics.
  [`localDate.test.ts:1`](../../client/src/lib/localDate.test.ts#L1)

- New regression test proving the fixed `TariffList` bug end-to-end, with TZ/fake-timer cleanup wrapped in `finally`.
  [`TariffList.test.tsx:164`](../../client/src/features/tariffs/components/TariffList.test.tsx#L164)
