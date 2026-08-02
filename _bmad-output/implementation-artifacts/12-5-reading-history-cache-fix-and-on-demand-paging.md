---
baseline_commit: 1fea6fe50649b86c356573a44952dbe07c16060a
---

# Story 12.5: Reading History Cache Fix & On-Demand Paging

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want the Reading History sheet to always show my most recent readings and to load older ones on demand,
so that I can trust the list is current and don't have to wait for my entire reading history to load at once.

## Acceptance Criteria

1. **Given** `useSubmitReading`'s mutation succeeds, **when** `onSuccess` runs, **then** it invalidates both `['dashboard', flatId]` (existing) and `['readings', flatId]` (new) via `queryClient.invalidateQueries`, so the Reading History sheet reflects newly submitted readings the next time it is visible.

2. **Given** `GET /api/v1/flats/{flatId}/readings`, **when** called with optional `skip` and `take` query params (`skip` default `0`, `take` default `20`), **then** `GetReadingHistoryFunction` returns `{ items: ReadingResponse[], totalCount: number }` — `items` reverse-chronological by `ReadingDate` (unchanged sort), `totalCount` the Flat's total reading count regardless of paging window; HTTP 200; ≤ 2s response time (unchanged NFR-1 budget).

3. **Given** `skip` or `take` is present but non-numeric or negative, or `take` exceeds `100`, **when** the request is parsed, **then** HTTP 400 Problem Details is returned, matching `GetInsightsFunction.cs`'s `status`-param validation style; no query executes.

4. **Given** `useReadingHistory` (converted from `useQuery` to `useInfiniteQuery`, key `['readings', flatId]`, page size 20), **when** the Reading History sheet first opens, **then** the first page (20 most recent readings) loads and renders exactly as today; a "Load more" button appears below the list when `hasNextPage` is true, minimum 44×44pt tap target per UX-DR11, and is absent once all readings are loaded.

5. **Given** the "Load more" button is tapped, **when** `fetchNextPage()` resolves, **then** the next 20 (or fewer, on the final page) readings append to the bottom of the existing list; the button shows a pending/disabled state via `isFetchingNextPage` while the request is in flight.

6. **Given** the Reading Edit flow's existing error-recovery path (`refetch().then(result => result.data?.find(...))` in `ReadingHistorySheet.tsx`), **when** updated for the new infinite-query shape, **then** it searches `result.data.pages.flatMap(p => p.items)` instead of a flat array; behavior on Patch failure (re-fetch and re-open the edit view with fresh data) is otherwise unchanged.

7. **Given** backend and frontend test suites, **when** run, **then** tests cover: default paging (skip=0, take=20) returns correct first page and `totalCount`; a second page via `skip=20` returns the next slice; invalid/negative `skip`/`take` and `take>100` return 400 (`GetReadingHistoryFunctionTests.cs`); `useSubmitReading`'s `onSuccess` invalidates both `['dashboard', flatId]` and `['readings', flatId]` (`useSubmitReading.test.ts`, extending the existing invalidation assertion); `useReadingHistory` fetches subsequent pages and exposes `hasNextPage`/`fetchNextPage` correctly (`useReadingHistory.test.ts`); the Reading History sheet renders the "Load more" button, appends items on click, and hides the button once exhausted (`ReadingHistorySheet.test.tsx`).

## Tasks / Subtasks

- [x] **Task 1: Cache-invalidation bug fix** (AC: #1)
  - [x] 1.1 In `client/src/features/readings/hooks/useSubmitReading.ts`, add `queryClient.invalidateQueries({ queryKey: ['readings', flatId] })` to the `onSuccess` handler, alongside the existing `['dashboard', flatId]` invalidation (use `Promise.all([...])`, mirroring `usePatchReading.ts`'s exact two-invalidation shape).

- [x] **Task 2: Backend paging — `GetReadingHistoryFunction.cs`** (AC: #2, #3)
  - [x] 2.1 In `api/Features/Readings/ReadingModels.cs`, add `public record ReadingHistoryResponse(List<ReadingResponse> Items, int TotalCount);`.
  - [x] 2.2 In `GetReadingHistoryFunction.cs`, after the tenant check, parse `skip`/`take` from `req.Query`: default `skip = 0`, default `take = 20`. For each present param, `int.TryParse` — non-numeric or negative → 400 Problem Details (same anonymous-object shape as the existing `Invalid flatId format.` response, `detail` e.g. `"skip must be a non-negative integer."` / `"take must be a non-negative integer."`). If `take > 100` → 400 with `detail: "take must not exceed 100."`. No query executes until all params validate.
  - [x] 2.3 Query total count first: `var totalCount = await db.MeterReadings.AsNoTracking().CountAsync(r => r.FlatId == flatGuid, ct);` (unfiltered by skip/take — whole Flat).
  - [x] 2.4 Add `.Skip(skip).Take(take)` to the existing `OrderByDescending(r => r.ReadingDate)` query chain, before `.Select(...)` — sort and projection otherwise unchanged.
  - [x] 2.5 Return `new OkObjectResult(new ReadingHistoryResponse(readings, totalCount));` instead of the bare list.

- [x] **Task 3: Backend tests** (AC: #7)
  - [x] 3.1 In `api.Tests/Features/Readings/GetReadingHistoryFunctionTests.cs`, add a `MakeGetRequest(string? skip = null, string? take = null)` overload building `ctx.Request.QueryString = new QueryString($"?skip={skip}&take={take}")` conditionally per param present (mirror `GetInsightsFunctionTests.cs`'s `MakeRequest(string? status)` helper exactly — only append params that are non-null).
  - [x] 3.2 Update the 3 existing tests (`RunAsync_MultipleReadings_ReturnsReverseChronologicalOrder`, `RunAsync_ReadingWithCorrection_IncludesIsCorrectedAndOriginalKwhValue`, `RunAsync_NoReadings_ReturnsEmptyArray`) to assert against `ReadingHistoryResponse` instead of `List<ReadingResponse>`: `ok.Value.ShouldBeOfType<ReadingHistoryResponse>()`, then assert on `.Items` and `.TotalCount`.
  - [x] 3.3 Add `RunAsync_DefaultPaging_ReturnsFirstTwentyAndTotalCount` — seed 25 readings with distinct dates, call with no skip/take, assert `Items.Count == 20` (the 20 most recent) and `TotalCount == 25`.
  - [x] 3.4 Add `RunAsync_SecondPageViaSkip_ReturnsNextSlice` — same 25-reading seed, call with `skip=20`, assert `Items.Count == 5` and `TotalCount == 25`, and that the returned items are the 5 oldest.
  - [x] 3.5 Add 400 tests: `RunAsync_NegativeSkip_Returns400`, `RunAsync_NonNumericSkip_Returns400`, `RunAsync_NegativeTake_Returns400`, `RunAsync_TakeExceedsMax_Returns400` (`take=101`) — each asserts `BadRequestObjectResult` and that no exception is thrown (i.e., the query never runs).

- [x] **Task 4: Frontend API + hook conversion** (AC: #4)
  - [x] 4.1 In `client/src/features/readings/api/readingApi.ts`, add `export type ReadingHistoryPage = { items: ReadingResponse[]; totalCount: number }`. Change `getReadingHistory` to `export const getReadingHistory = (flatId: string, params: { skip: number; take: number }) => apiClient.get<ReadingHistoryPage>(\`/flats/${flatId}/readings?skip=${params.skip}&take=${params.take}\`)` (manual query-string interpolation — `apiClient` has no query-param helper, matches `insightsApi.ts`'s `getInsights` convention).
  - [x] 4.2 In `client/src/features/readings/hooks/useReadingHistory.ts`, convert to `useInfiniteQuery`:
    ```ts
    import { useInfiniteQuery } from '@tanstack/react-query'
    import { getReadingHistory } from '@/features/readings/api/readingApi'

    const PAGE_SIZE = 20

    export function useReadingHistory(flatId: string | undefined) {
      return useInfiniteQuery({
        queryKey: ['readings', flatId],
        queryFn: ({ pageParam }) => getReadingHistory(flatId as string, { skip: pageParam, take: PAGE_SIZE }),
        initialPageParam: 0,
        getNextPageParam: (lastPage, allPages) => {
          const loaded = allPages.reduce((sum, page) => sum + page.items.length, 0)
          return loaded < lastPage.totalCount ? loaded : undefined
        },
        enabled: !!flatId,
      })
    }
    ```
    `pageParam` is the `skip` value for that page — page 1 uses `initialPageParam: 0`, subsequent pages use the running count of already-loaded items (`getNextPageParam`'s return value), naturally producing `0, 20, 40, ...`.

- [x] **Task 5: Frontend UI — flatten pages, "Load more" button, error-recovery update** (AC: #4, #5, #6)
  - [x] 5.1 In `client/src/features/readings/components/ReadingHistorySheet.tsx`, destructure `fetchNextPage`, `hasNextPage`, `isFetchingNextPage` alongside the existing `data, isLoading, isError, refetch` from `useReadingHistory(flatId)`.
  - [x] 5.2 Replace every `(data ?? [])` usage (empty check, `.map(...)`) with a flattened list: `const readings = data?.pages.flatMap(page => page.items) ?? []`, then use `readings` in place of `(data ?? [])` throughout the list-rendering branch.
  - [x] 5.3 Update the edit-error-recovery `onError` callback: `refetch().then(result => { const fresh = result.data?.pages.flatMap(p => p.items).find(r => r.readingId === editingReading.readingId); if (fresh) setEditingReading(fresh) })` — same fallback behavior, new page-aware lookup.
  - [x] 5.4 Below the `<ul>` (inside the same non-loading/non-error/non-empty render branch), add a "Load more" button, shown only when `hasNextPage`: `<button type="button" onClick={() => fetchNextPage()} disabled={isFetchingNextPage} className="mt-2 min-h-11 w-full text-body-sm text-text-secondary underline disabled:opacity-40">{t('history.loadMore')}</button>` (reuses the retry button's tap-target/style convention from this same file).
  - [x] 5.5 Add `history.loadMore` key to both `client/src/locales/en-US/readings.json` and `de-DE/readings.json`, in the existing `history` block.

- [x] **Task 6: Frontend tests** (AC: #7)
  - [x] 6.1 In `client/src/features/readings/hooks/useReadingHistory.test.ts`, rewrite `mockGetReadingHistory` responses as `ReadingHistoryPage` shape (`{ items: [...], totalCount: N }`). Update the existing "resolves with mocked list" test to assert `result.current.data?.pages[0]` instead of a flat array, and that `getReadingHistory` was called with `('flat-1', { skip: 0, take: 20 })`. Add a test that a second page loads correctly via `fetchNextPage()` when `totalCount` exceeds the first page's item count, and one confirming `hasNextPage` is `false` once all items are loaded (`totalCount` equals the sum of loaded items).
  - [x] 6.2 In `client/src/features/readings/hooks/useSubmitReading.test.ts`, extend `useSubmitReading_OnSuccess_InvalidatesDashboardQuery` (or add a sibling assertion in the same test) to also assert `invalidateQueries` was called with `{ queryKey: ['readings', 'flat-1'] }`.
  - [x] 6.3 In `client/src/features/readings/components/ReadingHistorySheet.test.tsx`, update `setupReadingHistory`'s mock return shape to the new hook contract: wrap `data` as `{ pages: [{ items: options?.data ?? [], totalCount: (options?.data ?? []).length }] }` (or `undefined` when no data), add `fetchNextPage: vi.fn()`, `hasNextPage: options?.hasNextPage ?? false`, `isFetchingNextPage: options?.isFetchingNextPage ?? false` to the mock's return value and the helper's `options` param; also update `refetch`'s mocked resolved value to the same `{ data: { pages: [...] } }` shape used by production code's error-recovery path (Task 5.3), since the existing `ReadingHistorySheet_SaveInEditView_...` test's `refetch` mock return value must match what the real code now reads. Add tests: `ReadingHistorySheet_HasNextPage_RendersLoadMoreButton`, `ReadingHistorySheet_NoNextPage_LoadMoreButtonAbsent`, `ReadingHistorySheet_TapLoadMore_CallsFetchNextPage`.

## Dev Notes

### Scope and origin

This story combines two independent, small defects discovered together and shipped as one story per `sprint-change-proposal-2026-08-02-reading-history-paging.md`: (1) a confirmed cache-invalidation bug — `useSubmitReading`'s `onSuccess` never invalidated `['readings', flatId]`, so the Reading History sheet silently kept showing a stale snapshot after new readings were submitted (sorting itself was already correct); and (2) a new, previously-deferred capability — on-demand paging instead of one unbounded fetch (`deferred-work.md:295` explicitly logged this as an acceptable simplification at the time; this story revisits it). Both touch the same hook/component/endpoint and ship together. No dependency on Stories 12.1–12.4.

### Why offset (`skip`/`take`) pagination, not cursor-based

Considered and rejected in the sprint-change-proposal: cursor-based pagination (by `ReadingId`/`ReadingDate`) is unnecessary at this data volume — manual meter readings, not high-frequency data (same reasoning already used for a related KPI-calculator concern in `deferred-work.md:272`). Offset pagination is simpler and matches the `{ items, totalCount }` shape `architecture.md:448` already documents.

### `ReadingHistoryResponse` is a new, additive contract — exactly one producer, one consumer

This is an internal API contract change (bare `ReadingResponse[]` → `{ items, totalCount }`) with exactly one producer (`GetReadingHistoryFunction`) and one consumer (`useReadingHistory`/`readingApi.ts`), both updated in this story — no versioning or backward-compatibility shim needed. Do not preserve the old bare-array shape alongside the new one.

### `useInfiniteQuery` is new to this codebase — no existing usage to copy

TanStack Query v5.101 is already a project dependency, but `useInfiniteQuery` has zero prior usages anywhere in `client/src` — this is the first. Key v5 API points (breaking vs. v4, do not use v4-era patterns from memory or docs): `initialPageParam` is **required** (no implicit default); `getNextPageParam(lastPage, allPages)` receives both the last page and the full pages array; the hook result exposes `data.pages` (array of whatever `queryFn` resolves to) and `data.pageParams`, plus `fetchNextPage`, `hasNextPage`, `isFetchingNextPage` alongside the normal `isLoading`/`isError`. `invalidateQueries({ queryKey: ['readings', flatId] })` (already used by `usePatchReading.ts`, and added to `useSubmitReading.ts` in this story) refetches **all** currently-loaded pages of an infinite query automatically in v5 — no special handling needed for `usePatchReading.ts` to keep working against the new infinite-query shape.

### `GetInsightsFunction.cs` is the validation-style reference for Task 2

Copy its query-param parsing shape (parse from `req.Query["..."].ToString()`, validate, return the same anonymous Problem Details object literal used everywhere else in this function — `type`/`title: "Bad Request"`/`status: 400`/`detail`). Do not introduce a shared validation helper; this codebase's established convention is per-function inline Problem Details literals (confirmed in Story 12.4's Dev Notes: "no shared helper exists anywhere in the codebase").

### `GetInsightsFunctionTests.cs`'s `MakeRequest` helper is the exact test-setup pattern for Task 3.1

```csharp
private static HttpRequest MakeRequest(string? status = null)
{
    var ctx = new DefaultHttpContext();
    if (status is not null)
        ctx.Request.QueryString = new QueryString($"?status={status}");
    return ctx.Request;
}
```
Replicate this shape with `skip`/`take` instead of `status`, building a combined query string only from the params actually passed (both, one, or neither).

### No entity/migration changes in this story

Unlike Stories 12.1/12.2/12.4, this story touches no `Data/Entities/` or `Data/Configurations/` files and requires no EF Core migration — `skip`/`take` are request-time query params, and the response shape change is a DTO-only addition (`ReadingHistoryResponse` in `ReadingModels.cs`). Do not run `dotnet ef migrations add`.

### `ReadingHistorySheet.tsx` current file fully read — exact touch points

The file (`client/src/features/readings/components/ReadingHistorySheet.tsx`) has two render branches reading `useReadingHistory`'s `data`: the empty-state check (`(data ?? []).length === 0`, line 76) and the list `.map()` (`(data ?? []).map(reading => ...)`, line 81), plus the error-recovery `refetch().then(...)` inside `onError` (lines 39–42). All three must move from flat-array `data` semantics to `data?.pages.flatMap(p => p.items) ?? []` semantics (Task 5.1–5.3). The edit view (`ReadingEditView`) and `usePatchReading` usage are untouched by this story.

### Testing Rules (from project context)

- Backend: xUnit + EF Core `InMemory`, `Shouldly` assertions. Test placement mirrors `api/Features/{Feature}/`. Run `dotnet test` manually (no CI gate exists yet — known gap).
- Frontend: Vitest (`globals: true`, no `describe`/`it`/`expect` imports), `@testing-library/react`. Mock API modules (`vi.mock('@/features/readings/api/readingApi')`), not `apiClient`. Mock hooks (`vi.mock('@/features/readings/hooks/useReadingHistory')`) in component tests, not the underlying fetch. Run `npm test -- --run`, `npx tsc -b`, `npm run lint` (all from `client/`) — `npx tsc --noEmit` is a silent no-op in this repo, don't use it.
- Query by role/label/text in component tests, not CSS class or `data-testid`.

### Project Structure Notes

- No conflicts with unified project structure — all changed files are existing files in `api/Features/Readings/`, `api.Tests/Features/Readings/`, and `client/src/features/readings/{api,hooks,components}/`; no new feature folders needed.
- `ReadingHistoryResponse` follows the codebase's `{Entity}Summary`/`{Entity}Response` DTO naming convention (closest precedent: `DecompositionResponse` wrapping multiple sub-fields including totals).

### References

- [Source: `_bmad-output/planning-artifacts/epics/epic-12-device-lifecycle-and-date-aware-decomposition-attribution.md#Story 12.5`] — epic-level AC, used verbatim.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-02-reading-history-paging.md`] — full origin/rationale; confirms the two-defects-one-story bundling, offset-vs-cursor decision, "Load more" button vs. infinite scroll decision.
- [Source: `_bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md:168-174`] — FR-48, including the new incremental-loading consequence bullet already added by the sprint-change-proposal.
- [Source: `_bmad-output/planning-artifacts/architecture.md:448`] — `{ items, totalCount }` paginated-collection response-shape convention, first real consumer.
- [Source: `_bmad-output/implementation-artifacts/deferred-work.md:295`] — original deferral entry this story resolves (no other list endpoint paginates — no longer true after this story, but only for this one endpoint).
- [Source: `api/Features/Readings/GetReadingHistoryFunction.cs`, `ReadingModels.cs`] — full files read; exact current shape being extended.
- [Source: `api.Tests/Features/Readings/GetReadingHistoryFunctionTests.cs`] — full file read; existing `MakeDb`/`MakeFunctionContext`/`SeedFlatAsync`/`MakeGetRequest` helpers and `Fact` naming convention to extend.
- [Source: `api/Features/Insights/GetInsightsFunction.cs`] — query-param validation style (parse/validate/Problem-Details-400) modeled directly.
- [Source: `api.Tests/Features/Insights/GetInsightsFunctionTests.cs`] — `MakeRequest(string? status)` query-string-building test helper pattern, replicated for `skip`/`take`.
- [Source: `client/src/features/readings/hooks/useSubmitReading.ts`, `useSubmitReading.test.ts`] — full files read; current single-invalidation `onSuccess`, and the existing test proving the `['readings', flatId]` gap.
- [Source: `client/src/features/readings/hooks/usePatchReading.ts`] — `Promise.all([...])` dual-invalidation pattern mirrored for Task 1.
- [Source: `client/src/features/readings/hooks/useReadingHistory.ts`, `useReadingHistory.test.ts`] — full files read; current `useQuery` shape being converted.
- [Source: `client/src/features/readings/api/readingApi.ts`] — full file read; `getReadingHistory`/`ReadingResponse` current shape.
- [Source: `client/src/features/readings/components/ReadingHistorySheet.tsx`, `ReadingHistorySheet.test.tsx`] — full files read; exact render branches and existing mock/test conventions to extend.
- [Source: `client/src/features/insights/api/insightsApi.ts`] — `getInsights`'s manual query-string interpolation, confirming `apiClient` has no query-param helper (same convention used for `getReadingHistory`'s `skip`/`take`).
- [Source: `client/src/lib/apiClient.ts`] — full file read; confirmed `get<T>` takes only a path, no query-param helper.
- [Source: `client/src/locales/en-US/readings.json`, `de-DE/readings.json`] — full files read; existing `history.*` key structure extended with `loadMore`.
- [Source: `_bmad-output/implementation-artifacts/12-4-insight-dismiss-and-reactivate.md`] — most recent prior story in this epic; confirms per-function inline Problem Details convention (no shared helper), one-hook-per-mutation precedent, and that this story (unlike 12.1/12.2/12.4) needs no EF migration or dual SQL-Server/SQLite migration generation since no entities change.
- [Source: `_bmad-output/project-context.md`] — mutation-hook pattern (`invalidateQueries` scoped to `['resource', flatId]`), TanStack Query v5 gotchas (`isPending` not `isLoading` on mutations, object-form `useQuery`), testing rules, i18n namespace convention.

### Review Findings

- [x] [Review][Patch] "Load more" failure hides the already-loaded reading list behind the full-page error state [`client/src/features/readings/components/ReadingHistorySheet.tsx:63,77,80,103`] — fixed: the full-page error block (`isError`) and the list-rendering block (`readings.length > 0`) are no longer mutually exclusive — the error block now only shows when there's no data yet (`isError && readings.length === 0`), the list renders whenever `readings.length > 0` regardless of `isError`, and the "Load more" button similarly no longer hides on `isError` (so a failed page fetch can be retried by tapping it again). Covered by 2 new tests: `ReadingHistorySheet_ErrorWithAlreadyLoadedData_StillRendersListNotFullErrorScreen`, `ReadingHistorySheet_ErrorWithNoData_ShowsFullErrorScreen`.
- [x] [Review][Defer] Non-atomic `CountAsync` + paged `Select` in `GetReadingHistoryFunction` creates a race window under concurrent inserts [`api/Features/Readings/GetReadingHistoryFunction.cs:208-217`] — deferred, pre-existing design tradeoff (offset pagination was deliberately chosen over cursor-based in the sprint-change-proposal, accepting this exact class of risk given manual-meter-reading data volume/cadence). A concurrent insert between the count and paged queries can shift the skip window, causing a duplicate or skipped row across pages, or a stale `totalCount` mis-reporting `hasNextPage`. Revisit if this endpoint is ever exposed to higher-frequency/concurrent writers.
- [x] [Review][Defer] `take=0` is accepted by validation but can never produce a terminating "Load more" sequence for a direct API caller [`api/Features/Readings/GetReadingHistoryFunction.cs`] — unreachable via the frontend (hardcoded `PAGE_SIZE=20`), so no user-facing impact today; only a latent contract gap for a future non-frontend consumer. Add a `take >= 1` lower bound if/when this endpoint gains another consumer.

### Dismissed as noise (8)

- "`useSubmitReading` invalidating `['readings', flatId]` triggers a refetch of every already-loaded infinite-query page" — this is the documented, intended TanStack Query v5 behavior (Dev Notes call it out explicitly) and is the entire point of AC1's cache fix; cost is trivial at this data volume.
- "No upper bound on `skip`" — scoped to one tenant's own Flat readings, bounded by realistic manual-entry volume; not an exploitable/DoS-relevant unbounded scan.
- "`useSubmitReading.test.ts`'s `callOrder` assertion can't distinguish the two `invalidateQueries` calls (both push the literal `'invalidate'`)" — the property under test (immediate-callback-before-invalidation ordering) is still correctly verified; each invalidation target is already independently asserted in its own dedicated test.
- "Four inline duplicated Problem Details literals in `GetReadingHistoryFunction.cs`" — matches this codebase's established per-function inline-literal convention (no shared helper exists anywhere, per Story 12.4's Dev Notes).
- "Unverified JSON casing for `ReadingHistoryResponse`" — confirmed camelCase via the shared `JsonSerializationDefaults` applied to `Mvc.JsonOptions`, same as every other response type.
- "Unencoded query-string construction in `getReadingHistory`" — matches `insightsApi.ts`'s existing `getInsights` convention; inputs are numeric-typed at the call site.
- "Test helper `MakeGetRequest` always emits both `skip=`/`take=` keys instead of building the query string conditionally per param" — functionally harmless (`GetReadingHistoryFunction.cs` treats an empty string as absent), no test or behavior gap.
- "`useSubmitReading.ts` invalidates `['dashboard', flatId]` before `['readings', flatId]`, reversed from `usePatchReading.ts`'s order" — functionally inert under `Promise.all`; AC1 doesn't require an order.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- No entity/migration changes needed for this story (confirmed in Dev Notes) — `dotnet ef migrations add` was not run.
- `TrendChart.test.tsx` (pre-existing, not listed in the story's Task 6 file list) mocked `useReadingHistory` with the old flat-array `data` shape and broke once `ReadingHistorySheet.tsx` switched to `data?.pages.flatMap(...)`. Fixed by updating its mock to the new `{ data: { pages: [{ items, totalCount }] }, fetchNextPage, hasNextPage, isFetchingNextPage }` shape — same class of gap Story 12.4 hit with `useInsights.test.ts`.

### Completion Notes List

- Task 1: `useSubmitReading.ts`'s `onSuccess` now invalidates both `['dashboard', flatId]` and `['readings', flatId]` via `Promise.all`, matching `usePatchReading.ts`'s existing dual-invalidation shape. Fixes the confirmed production bug where new readings never appeared in an already-cached Reading History sheet.
- Task 2: `GetReadingHistoryFunction.cs` now accepts optional `skip`/`take` query params (defaults 0/20), validates them (non-numeric/negative/`take>100` → 400 Problem Details, modeled on `GetInsightsFunction.cs`), and returns `ReadingHistoryResponse { Items, TotalCount }` instead of a bare array. `TotalCount` is queried unfiltered by paging window.
- Task 3: Updated the 3 pre-existing `GetReadingHistoryFunctionTests.cs` tests for the new response shape; added 6 new tests covering default paging, second-page-via-skip, and all 4 validation-failure branches (negative skip, non-numeric skip, negative take, take>100).
- Task 4: `readingApi.ts`'s `getReadingHistory` now takes `{ skip, take }` and returns `ReadingHistoryPage`; `useReadingHistory.ts` converted from `useQuery` to `useInfiniteQuery` (`initialPageParam: 0`, `getNextPageParam` computes the next `skip` from the running loaded-item count vs. `totalCount`).
- Task 5: `ReadingHistorySheet.tsx` flattens `data.pages` into a single `readings` list for both the empty-state check and the `<ul>` map; the edit-flow error-recovery path searches across all flattened pages; a "Load more" button (44×44pt tap target, `disabled` while `isFetchingNextPage`) renders below the list when `hasNextPage` is true. Added `history.loadMore` to both locale files.
- Task 6: Rewrote `useReadingHistory.test.ts` for the infinite-query contract (first page, second-page fetch, `hasNextPage` false once exhausted); extended `useSubmitReading.test.ts` with a new invalidation assertion and fixed the call-order test's expected sequence (now two invalidate calls); rewrote `ReadingHistorySheet.test.tsx`'s `setupReadingHistory` mock for the new hook shape and added 3 new tests for the "Load more" button's visibility and click behavior.
- Full regression: backend `dotnet test` → 544/544 passed (13 new vs. Story 12.4's 531 baseline); frontend `npm test -- --run` → 502/502 passed (after fixing the pre-existing `TrendChart.test.tsx` mock — see Debug Log); `npx tsc -b` → clean; `npm run lint` → clean (only pre-existing unrelated `router.tsx` warnings).

### File List

**Backend**
- `api/Features/Readings/ReadingModels.cs` (modified)
- `api/Features/Readings/GetReadingHistoryFunction.cs` (modified)
- `api.Tests/Features/Readings/GetReadingHistoryFunctionTests.cs` (modified)

**Frontend**
- `client/src/features/readings/hooks/useSubmitReading.ts` (modified)
- `client/src/features/readings/hooks/useSubmitReading.test.ts` (modified)
- `client/src/features/readings/api/readingApi.ts` (modified)
- `client/src/features/readings/hooks/useReadingHistory.ts` (modified)
- `client/src/features/readings/hooks/useReadingHistory.test.ts` (modified)
- `client/src/features/readings/components/ReadingHistorySheet.tsx` (modified)
- `client/src/features/readings/components/ReadingHistorySheet.test.tsx` (modified)
- `client/src/features/dashboard/components/TrendChart.test.tsx` (modified — pre-existing mock updated for new hook shape)
- `client/src/locales/en-US/readings.json` (modified)
- `client/src/locales/de-DE/readings.json` (modified)
