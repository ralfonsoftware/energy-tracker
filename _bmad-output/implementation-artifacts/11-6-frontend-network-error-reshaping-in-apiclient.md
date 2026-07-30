---
baseline_commit: 99b64b9566e9539ab059023dab528c6067c0631d
---

# Story 11.6: Frontend Network-Error Reshaping in `apiClient`

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want a dropped network connection to show a sensible error message,
so that I'm not left looking at a broken or blank error state when I'm simply offline.

## Acceptance Criteria

1. **Given** `apiClient.ts`'s `request()` function calls `await fetch(...)` with no `try`/`catch` around it — a genuine network failure (offline, DNS failure, connection drop) throws a raw `TypeError: Failed to fetch` (or the fetch spec's equivalent) that is never reshaped into the `Error & { detail?: string }` shape every calling hook's error-handling code already expects per this project's established convention, **when** implemented, **then** `request()` wraps the `fetch()` call in a `try`/`catch`; on a caught network-level exception (not an HTTP error response — those are already handled), it throws a new `Error` with a `detail` field set to a generic, i18n-friendly network-error message key, matching the shape every other error path in this function already produces.
2. **Given** the fix, **when** tested, **then** a new test in `apiClient.test.ts` (or the nearest existing test file covering `apiClient`) mocks `fetch` to reject with a `TypeError` and asserts the thrown error has the expected `detail` field, and at least one consuming hook's existing error-handling test is confirmed to still pass unmodified (proving the reshaping is transparent to callers already handling `error.detail`).

## Tasks / Subtasks

- [x] Task 1: Wrap the `fetch()` call in `client/src/lib/apiClient.ts`'s `request()` in `try`/`catch` and reshape network-level exceptions (AC: #1)
  - [x] 1.1 Wrap **only** the `await fetch(...)` call (`apiClient.ts:5-10`) in `try`/`catch` — do not wrap `res.json()`, `res.text()`, or any other line in `request()`; the existing `if (!res.ok)` branch (`apiClient.ts:11-16`) already handles HTTP error responses correctly and must not change
  - [x] 1.2 In the `catch` block, build the message via `i18n.t('errors.networkError', { ns: 'common' })` — reuse the existing `common:errors.networkError` key (already present in both `client/src/locales/en-US/common.json` and `client/src/locales/de-DE/common.json`, already used by `DashboardPage.tsx` for generic query-error banners) rather than adding a new locale key
  - [x] 1.3 Construct and throw a new `Error` whose `.message` is that string and whose `.detail` property is also set to that same string (mirror the existing HTTP-error branch's shape exactly: `const err = new Error(message); Object.assign(err, { detail: message }); throw err`) — this preserves the `Error & { detail?: string }` contract every calling hook already relies on
  - [x] 1.4 Import the i18next instance via `import i18n from '@/lib/i18n'` (the default export of the i18next instance itself, not the `useTranslation` React hook — `apiClient.ts` is a plain module, not a component/hook, so it cannot call `useTranslation`) and call `i18n.t(...)` directly on it; this import is safe outside React context and is already used this way — for `i18n.language`, not `i18n.t()` — by 20+ non-component files in this codebase (e.g. `TrendChart.tsx`, `InsightCard.tsx`); there is no existing direct precedent for calling `i18n.t()` from a plain module, but it is a standard, context-free i18next instance method and works identically to calling it via `useTranslation()`'s `t`
- [x] Task 2: Add test coverage confirming the reshaping and its transparency to existing callers (AC: #2)
  - [x] 2.1 Add a new test in `client/src/lib/apiClient.test.ts` that mocks `fetch` to reject with `new TypeError('Failed to fetch')` (matching this test file's existing `vi.stubGlobal('fetch', vi.fn())` / `mockRejectedValue` pattern) and asserts the thrown error's `.detail` (and `.message`) equal the exact `common:errors.networkError` string value
  - [x] 2.2 Confirm the existing HTTP-error-response test path (an `!res.ok` response, e.g. adapt the existing `mockResolvedValue(new Response(..., { status: 4xx }))` shape already used elsewhere in this file) still produces the same `.detail` behavior as before — the `try`/`catch` addition must not alter this branch
  - [x] 2.3 Run the full frontend suite (`npm test` — Vitest, from `client/`) and confirm all existing tests pass unmodified — no hook test needs code changes, since every hook/component test in this codebase mocks its feature's API module (e.g. `vi.mock('@/features/readings/api/readingApi')` in `useSubmitReading.test.ts`) rather than stubbing `fetch` directly, so none of them exercise `apiClient`'s real `fetch()` call path; `apiClient.test.ts` is the only test file in the codebase that stubs `fetch` globally, confirmed via direct search

## Dev Notes

### The exact code shape to apply

Current `client/src/lib/apiClient.ts` (unchanged parts omitted):
```ts
async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const isFormData = init?.body instanceof FormData
  const res = await fetch(`${BASE}${path}`, {
    ...init,
    headers: isFormData
      ? init?.headers
      : { 'Content-Type': 'application/json', ...init?.headers },
  })
  if (!res.ok) {
    const problem = await res.json().catch(() => ({ detail: 'Unknown error' }))
    const err = new Error(problem.detail ?? 'API error')
    Object.assign(err, problem)
    throw err
  }
  ...
}
```

Target shape — wrap only the `fetch()` call:
```ts
import i18n from '@/lib/i18n'

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const isFormData = init?.body instanceof FormData
  let res: Response
  try {
    res = await fetch(`${BASE}${path}`, {
      ...init,
      headers: isFormData
        ? init?.headers
        : { 'Content-Type': 'application/json', ...init?.headers },
    })
  } catch {
    const message = i18n.t('errors.networkError', { ns: 'common' })
    const err = new Error(message)
    Object.assign(err, { detail: message })
    throw err
  }
  if (!res.ok) {
    // unchanged
  }
  ...
}
```

Note the `let res: Response` + assignment-inside-try pattern (rather than declaring `res` via `const` inside the `try`) is required so `res` remains in scope for the rest of the function body below the `try`/`catch`.

### Why reuse `common:errors.networkError` instead of adding a new key

- `client/src/locales/en-US/common.json:15-18` and `client/src/locales/de-DE/common.json:15-18` already define an `errors.networkError` key ("Something went wrong. Please try again." / "Etwas ist schiefgelaufen. Bitte erneut versuchen.") used today by `DashboardPage.tsx:39` for generic query-failure banners.
- This is exactly the message class this story needs — reusing it avoids adding a new locale key that would need translating into both `de-DE` and `en-US`, and keeps the app's generic-failure messaging consistent between a query-level network failure and this fetch-level one.
- `i18n` default namespace is already `common` (`client/src/lib/i18n.ts:35`), so `{ ns: 'common' }` is technically redundant but included for explicitness/readability at a call site far from any `useTranslation('common')` scoping.

### No consuming code reads `.detail`'s content today — only its presence/truthiness matters

Direct `grep -rn "\.detail" client/src/features client/src/lib` (excluding tests) returns exactly one hit: `apiClient.ts:13` itself. No hook or component anywhere in `client/src/features/` inspects `error.detail`'s string content. The established UI convention (confirmed directly in `DashboardPage.tsx:34-40`, `EnterReadingSheet.tsx:112-116`, `FlatBaselineEdit.tsx`, `TariffList.tsx`) is: components check `isError`/`mutation.isError` and render one static, pre-translated banner string (`t('errors.networkError')`, `t('sheet.saveError')`, etc.) — never the live `error.message`/`error.detail` value. This means the fix is low-risk to existing UI: adding a `.detail` value to a previously-undecorated network exception cannot break any current renderer, since nothing renders that value directly.

### `project-context.md`'s documented `Error & { detail?: string }` contract

Per this project's own AI-agent rules: *"`mutation.error` (typed as `Error & { detail?: string }`) is the source for server-side error messages — display as a separate error banner near the Save button, distinct from `form.formState.errors`."* Today this contract silently fails for a genuine network drop — `mutation.error` would be a bare `TypeError` with no `.detail` at all. This story closes that gap so the contract holds for **every** failure mode, not just HTTP-error responses.

### Initialization-order note

`client/src/App.tsx` imports `@/lib/i18n` (for its side-effecting `i18n.init()` call) before any route/component renders, and no `apiClient` call can fire before the app has mounted — so `i18n.t()` is always safe to call from `apiClient.ts` at runtime. No lazy-init guard or fallback string is needed.

### Testing Requirements

- Follow `apiClient.test.ts`'s existing pattern exactly: `vi.stubGlobal('fetch', vi.fn())` in `beforeEach`, `vi.unstubAllGlobals()` in `afterEach` (already present, do not duplicate).
- New network-failure test: `mockFetch.mockRejectedValue(new TypeError('Failed to fetch'))`, then `await expect(apiClient.get('/some/path')).rejects.toMatchObject({ detail: <expected string> })` (or equivalent `try`/`catch` + assertion form matching this file's existing style).
- Test naming convention in this file: `apiClient_<Scenario>_<ExpectedOutcome>` (e.g. existing `apiClient_PostForm_SendsFormDataWithoutJsonContentTypeHeader`) — follow the same `PascalCase`-segments style for the new test name, e.g. `apiClient_FetchThrowsNetworkError_ReshapesToErrorWithDetail`.
- Do not import `i18next` test utilities or mock `i18n` — the real `common:errors.networkError` string value can be asserted directly (import it from the locale JSON or hardcode the known string), consistent with this being a genuine i18n resource rather than a mocked translation function (per project rule: `vi.mock('react-i18next', ...)` is for component tests using `useTranslation`, not for a plain-module test calling the i18next instance's `t()` directly — no such mock is needed or appropriate here).
- Vitest globals (`describe`/`it`/`expect`) — no import needed, already configured project-wide (`vitest.config` / project context rule).

### Project Structure Notes

- Single file modified: `client/src/lib/apiClient.ts`. Single test file modified: `client/src/lib/apiClient.test.ts`. No new files, no locale file changes (reusing an existing key), no schema/API contract changes.
- This is the first purely-frontend story in Epic 11 — Stories 11.1–11.5 were all backend (`api/Features/`). No frontend-specific "previous story" pattern exists within this epic; the closest frontend precedent is Epic 10's Story 10.4 (Insights Tab), which is unrelated to this story's scope (`apiClient` is shared infrastructure, not feature-specific).
- `apiClient.ts` is imported by every feature's `api/` module (`dashboardApi.ts`, `readingApi.ts`, `tariffApi.ts`, etc.) — this change affects the shared HTTP layer used by the entire app, but the change is purely additive to the failure path (a new `catch` branch), so no existing success-path or HTTP-error-path behavior changes.

### Previous Story Intelligence (Story 11.5 — RFC 9457 `type` Field Consistency Sweep)

- Story 11.5 (immediately preceding this one in Epic 11) was also a "pure error-shape addition" story with an explicit AC that no existing `title`/`status`/`detail` assertion should need to change — same discipline applies here: this story must not alter the existing HTTP-error-response (`!res.ok`) branch's behavior or its existing test coverage in `apiClient.test.ts`.
- Story 11.5's dev-agent record emphasized re-confirming adjacent/existing tests still pass unmodified after a targeted fix, and asserting on exact string values (not just presence) — apply both disciplines here: after adding the `catch` branch, re-run the full frontend suite, and assert the exact `errors.networkError` string value in the new test, not just that `.detail` is truthy.
- Unlike 11.5 (backend, RFC-URI-table-driven, 18 files), this story is backend-adjacent-but-frontend: a single shared client file, single new failure branch, reusing an existing i18n key rather than inventing new response-shape conventions.

### Git Intelligence (recent commits)

- `99b64b9` (Story 11.5), `df5a834` (Story 11.4), `457ff51` (CI Node 20→22 bump, infra-only, not a story), `c8805f8` (Story 11.3), `21daef3` (Story 11.14) — all recent Epic 11 commits follow the same shape: one narrow, single-concern fix + matching test-file extension + full relevant test suite run before completion. This story follows the same shape on the frontend side: `npm test` (Vitest) from `client/`, not `dotnet test`.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-11-post-epic-10-hardening-and-technical-debt-resolution.md#Story 11.6] — epic-level AC and rationale
- [Source: client/src/lib/apiClient.ts] — current implementation; the single `try`/`catch` addition point
- [Source: client/src/lib/apiClient.test.ts] — existing test file and its `vi.stubGlobal('fetch', ...)` pattern to extend
- [Source: client/src/lib/i18n.ts] — i18next instance initialization, default export, `defaultNS: 'common'`
- [Source: client/src/locales/en-US/common.json:15-18, client/src/locales/de-DE/common.json:15-18] — existing `errors.networkError` key and both locale string values, reused (not duplicated) by this story
- [Source: client/src/features/dashboard/DashboardPage.tsx:34-40] — existing consumer of `common:errors.networkError` for a generic query-failure banner, the established UI convention this story's new error message aligns with
- [Source: client/src/features/readings/components/EnterReadingSheet.tsx:112-116, client/src/features/readings/hooks/useSubmitReading.ts] — example of a mutation-error banner consuming only `isError`/truthiness, not `.detail` content, confirming the fix is transparent to existing renderers
- [Source: _bmad-output/implementation-artifacts/deferred-work.md:129] — the original deferred item this story closes: *"`fetch()` network errors (e.g., `TypeError: Failed to fetch`) are not reshaped into Problem Details format in `apiClient.ts` — inconsistent error shape for callers. Address when the API client is hardened."* (deferred from code review of Story 1.1, pass 2, 2026-06-27)
- [Source: _bmad-output/project-context.md#Mutations] — the documented `mutation.error` (typed as `Error & { detail?: string }`) contract this story makes hold for network failures, not just HTTP-error responses
- [Source: _bmad-output/implementation-artifacts/11-5-rfc-9457-type-field-consistency-sweep.md] — previous story in this epic; source of the "pure error-shape addition, no existing assertion should change" discipline and the "assert exact values, not just presence" testing discipline

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- `npx vitest run src/lib/apiClient.test.ts` — red phase confirmed 1 failing test (`apiClient_FetchThrowsNetworkError_ReshapesToErrorWithDetail`), 3 pre-existing tests passing
- `npx vitest run src/lib/apiClient.test.ts` — green phase, all 4 tests passing after implementation
- `npm test -- --run` — full frontend suite, 68 files / 442 tests passed, no regressions
- `npm run lint` — clean (only pre-existing, unrelated `router.tsx` fast-refresh warnings)
- `npx tsc --noEmit` — clean

### Completion Notes List

- Wrapped only the `await fetch(...)` call in `request()` in `try`/`catch`; on catch, builds a message via `i18n.t('errors.networkError', { ns: 'common' })` and throws a new `Error` with matching `.detail`, mirroring the existing HTTP-error branch's shape exactly.
- No new locale keys added — reused the existing `common:errors.networkError` key already present in both `en-US` and `de-DE` locale files.
- Added `apiClient_FetchThrowsNetworkError_ReshapesToErrorWithDetail` (network-level rejection) and `apiClient_HttpErrorResponse_StillReshapesToErrorWithDetail` (existing `!res.ok` path, confirming it is untouched) to `apiClient.test.ts`, both asserting the exact expected string values.
- Full frontend suite (68 files / 442 tests) passes unmodified; no consuming hook/component test needed changes since none of them stub `fetch` directly (all mock their feature's API module).

### File List

- `client/src/lib/apiClient.ts` (modified)
- `client/src/lib/apiClient.test.ts` (modified)

### Review Findings

- [x] [Review][Patch] Bare `catch {}` discards the original fetch-rejection reason, losing diagnostic info (no `cause` chain) [client/src/lib/apiClient.ts:15-20] — fixed: bound the caught value as `cause` and passed it via `new Error(message, { cause })`
- [x] [Review][Defer] Bare catch treats every fetch rejection identically, so an `AbortError` from a caller-supplied `AbortSignal` (e.g. React Query cancellation) would be mislabeled as a generic network error [client/src/lib/apiClient.ts:15-20] — deferred, dormant (no caller currently wires an `AbortSignal`/`signal` into `apiClient`; revisit if request cancellation is added)
- [x] [Review][Defer] Reshaped errors use `Object.assign` to bolt an untyped `detail` property onto a plain `Error`, requiring unsafe casts at call sites [client/src/lib/apiClient.ts:17-18, 23-24] — deferred, pre-existing pattern (already present in the `!res.ok` branch before this change; this diff only mirrors it, as instructed)

## Change Log

- 2026-07-29: Story 11.6 created — frontend network-error reshaping in `apiClient.ts`, reusing the existing `common:errors.networkError` i18n key; closes the deferred item from Story 1.1's pass-2 code review (`deferred-work.md:129`).
- 2026-07-30: Implemented — wrapped `fetch()` in `try`/`catch` in `apiClient.ts`'s `request()`, reshaping network-level exceptions into the `Error & { detail?: string }` contract; added matching test coverage. All tasks/subtasks complete, full frontend suite green, story moved to review.
- 2026-07-30: Code review complete — 0 AC violations, 1 patch applied (added `cause` chaining to the network-error catch for debuggability), 2 items deferred (dormant `AbortError` misclassification risk; pre-existing untyped `Object.assign` error shape). Story moved to done.
