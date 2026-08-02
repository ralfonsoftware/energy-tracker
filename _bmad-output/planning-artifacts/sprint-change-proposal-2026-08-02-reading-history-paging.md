# Sprint Change Proposal — 2026-08-02 (Story 12.5: Reading History Cache Fix & On-Demand Paging)

## Section 1: Issue Summary

**Trigger:** Ralf reported the Reading History sheet on production (`energytracker.ralfonsoftware.de`) showing a desc-sorted list that stops at 23.07.2026, 16:22 even though the database has newer readings up to the current date (2026-08-02). He also requested on-demand loading of older entries instead of the sheet always fetching the full history at once.

**Problem — two distinct defects, one story:**

1. **Confirmed bug (missing cache invalidation):** `useSubmitReading.ts:11-14`'s `onSuccess` handler only invalidates `['dashboard', flatId]`. It never invalidates `['readings', flatId]`. Once the Reading History sheet has been opened and its query cached, entering new readings afterward never refreshes that cache — the sheet keeps showing the snapshot from whenever it first fetched, silently missing every reading submitted since. Only editing a reading (`usePatchReading.ts:19-23`) invalidates `['readings', flatId]`; submitting a new one does not. This exactly reproduces the screenshot: correctly desc-sorted, but stale.
2. **Not a bug — sorting is already correct.** `GetReadingHistoryFunction.cs:42` already does `OrderByDescending(r => r.ReadingDate)`. No change needed here.
3. **New capability (deliberately deferred, never scoped):** `GetReadingHistoryFunction` returns the entire reading history in one unbounded query with no `skip`/`take`. A `deferred-work.md` entry from the Story 3.6 code review (2026-07-03) explicitly logged this as an acceptable simplification at the time ("no other list endpoint paginates"). No epic or story has revisited it since. Ralf now wants on-demand loading of older readings instead of a single unbounded fetch.

**Evidence:**
- `client/src/features/readings/hooks/useSubmitReading.ts:11-14` (missing invalidation)
- `client/src/features/readings/hooks/usePatchReading.ts:19-23` (the invalidation that *does* exist, for comparison)
- `client/src/features/readings/hooks/useSubmitReading.test.ts:33-42` — the existing test only asserts `['dashboard', 'flat-1']` was invalidated, proving the gap was never covered
- `api/Features/Readings/GetReadingHistoryFunction.cs:40-44` (unbounded query, correct sort)
- `_bmad-output/implementation-artifacts/deferred-work.md:295` ("`GetReadingHistoryFunction` has no pagination/limit — consistent with... no other list endpoint paginates")
- `architecture.md:448` — the API response-shape convention already anticipates this: `"Paginated collection (if needed): { "items": [...], "totalCount": N }"` — documented but never used until now
- Screenshot: list runs 23.07.2026 → 31.12.2025, missing all readings between 24.07.2026 and today (2026-08-02)

## Section 2: Impact Analysis

**Epic Impact:** No existing epic's plan is disrupted. Epic 12 (Device Lifecycle & Date-Aware Decomposition Attribution, `in-progress`) gains a fifth story, **12.5**. Thematically unrelated to FR-52/53/54 (device lifecycle / decomposition), same as Stories 12.3/12.4 — bucketed here per Ralf's explicit choice (confirmed for this proposal), continuing the established precedent rather than reopening the closed Epic 3 or the closed-with-retrospective Epic 11.

**Story Impact:** One new story — **12.5**, combining the cache-invalidation bug fix and the new paging capability, since both touch the same hook/component/endpoint and ship together.

**Artifact Conflicts:**
- `epic-12-device-lifecycle-and-date-aware-decomposition-attribution.md`: new **Story 12.5** appended.
- `epic-list.md`: Epic 12 summary paragraph and story count updated.
- PRD (`prd.md`): FR-48 ("Meter Reading correction and history") gets one new testable consequence bullet noting incremental loading for long histories. No new FR number — this is a refinement of FR-48's existing "viewable" requirement, not a new capability, so it doesn't warrant its own FR (unlike FR-55, which added a wholly new dismiss/reactivate capability).
- `requirements-inventory.md`: no changes — FR-48 already listed, no new FR/UX-DR introduced.
- Architecture (`architecture.md`): no changes needed — the paginated-collection response shape (`{ items, totalCount }`) is already documented at line 448; this story is simply its first real consumer. Worth noting in the story's Dev Notes, not worth a new AD entry for using an already-documented pattern as intended.
- `sprint-status.yaml`: `12-5-reading-history-cache-fix-and-on-demand-paging: backlog` added under the Epic 12 block.

**Technical Impact:**
- Backend: `GetReadingHistoryFunction.cs` gains optional `skip`/`take` query params (default `skip=0`, `take=20`, capped at `take=100`; malformed/negative values → 400 Problem Details, same validation style as `GetInsightsFunction.cs`'s `status` param). Response shape changes from a bare `ReadingResponse[]` array to `{ items: ReadingResponse[], totalCount: number }`.
- Frontend: `readingApi.ts`'s `getReadingHistory` signature changes to accept `{ skip, take }` and return the new paged shape. `useReadingHistory.ts` converts from `useQuery` to `useInfiniteQuery` (TanStack Query v5), page size 20, exposing `fetchNextPage`/`hasNextPage`/`isFetchingNextPage`. `ReadingHistorySheet.tsx` flattens `data.pages` into a single list, adds a "Load more" button below the list (min 44×44pt tap target per UX-DR11, hidden when `hasNextPage` is false), and its existing `refetch().then(result => result.data?.find(...))` error-recovery path (used when a Patch fails) updates to search across `result.data.pages.flatMap(p => p.items)` instead of a flat array. `useSubmitReading.ts`'s `onSuccess` gains `queryClient.invalidateQueries({ queryKey: ['readings', flatId] })` alongside the existing dashboard invalidation.
- Since `usePatchReading.ts` already invalidates `['readings', flatId]` and TanStack Query v5's `invalidateQueries` refetches all currently-loaded pages of an infinite query (not just the first), no change is needed there beyond it continuing to work correctly against the new infinite-query shape.
- This is an internal contract change (bare array → `{ items, totalCount }`) with exactly one producer and one consumer, both updated in the same story — no versioning or backward-compatibility concern.

## Section 3: Recommended Approach

**Selected: Option 1 — Direct Adjustment.** Add Story 12.5 to the existing Epic 12; one small PRD consequence-bullet addition to FR-48; no rollback; no MVP scope change.

**Rationale:** The bug fix is a one-line addition to an existing mutation hook, already following an established pattern (`usePatchReading.ts` does exactly this today). The paging feature reuses a response shape the architecture doc already anticipated and a TanStack Query v5 mechanism (`useInfiniteQuery`) the project stack already includes — no new library, no new architectural pattern. Effort: **Medium** (the bug fix is trivial; the paging change touches API contract, hook, component, and tests across both layers). Risk: **Low** — additive/internal contract change, single producer/consumer, no data migration.

**Alternatives considered:**
- *Split into two items* (quick fix + separate story) — rejected per your preference; both changes touch the exact same files and ship together more cleanly as one story.
- *Infinite scroll instead of "Load more" button* — rejected per your preference; a button is simpler to implement and test, and avoids scroll-container edge cases inside a bottom sheet.
- *Cursor-based pagination (by `ReadingId`/`ReadingDate`)* instead of offset (`skip`/`take`) — considered, but offset pagination is simpler, sufficient at this data volume (manual meter readings, not high-frequency data — same reasoning `deferred-work.md:272` already used for a related KPI-calculator concern), and matches the `{ items, totalCount }` shape the architecture doc already committed to.

## Section 4: Detailed Change Proposals

### `epic-12-device-lifecycle-and-date-aware-decomposition-attribution.md`
Append after Story 12.4:

```
## Story 12.5: Reading History Cache Fix & On-Demand Paging

As a user,
I want the Reading History sheet to always show my most recent readings and to load older ones on demand,
So that I can trust the list is current and don't have to wait for my entire reading history to load at once.

**Acceptance Criteria:**

**Given** `useSubmitReading`'s mutation succeeds,
**When** `onSuccess` runs,
**Then** it invalidates both `['dashboard', flatId]` (existing) and `['readings', flatId]` (new) via `queryClient.invalidateQueries`, so the Reading History sheet reflects newly submitted readings the next time it is visible.

**Given** `GET /api/v1/flats/{flatId}/readings`,
**When** called with optional `skip` and `take` query params (`skip` default `0`, `take` default `20`, `take` capped at `100`),
**Then** `GetReadingHistoryFunction` returns `{ items: ReadingResponse[], totalCount: number }` — `items` reverse-chronological by `ReadingDate` (unchanged sort), `totalCount` the Flat's total reading count regardless of paging window; HTTP 200; ≤ 2s response time (unchanged NFR-1 budget).

**Given** `skip` or `take` is present but non-numeric or negative, or `take` exceeds `100`,
**When** the request is parsed,
**Then** HTTP 400 Problem Details is returned, matching `GetInsightsFunction.cs`'s `status`-param validation style; no query executes.

**Given** `useReadingHistory` (converted from `useQuery` to `useInfiniteQuery`, key `['readings', flatId]`, page size 20),
**When** the Reading History sheet first opens,
**Then** the first page (20 most recent readings) loads and renders exactly as today; a "Load more" button appears below the list when `hasNextQuery` is true, minimum 44×44pt tap target per UX-DR11, and is absent once all readings are loaded.

**Given** the "Load more" button is tapped,
**When** `fetchNextPage()` resolves,
**Then** the next 20 (or fewer, on the final page) readings append to the bottom of the existing list; the button shows a pending/disabled state via `isFetchingNextPage` while the request is in flight.

**Given** the Reading Edit flow's existing error-recovery path (`refetch().then(result => result.data?.find(...))` in `ReadingHistorySheet.tsx`),
**When** updated for the new infinite-query shape,
**Then** it searches `result.data.pages.flatMap(p => p.items)` instead of a flat array; behavior on Patch failure (re-fetch and re-open the edit view with fresh data) is otherwise unchanged.

**Given** backend and frontend test suites,
**When** run,
**Then** tests cover: default paging (skip=0, take=20) returns correct first page and `totalCount`; a second page via `skip=20` returns the next slice; invalid/negative `skip`/`take` and `take>100` return 400 (`GetReadingHistoryFunctionTests.cs`); `useSubmitReading`'s `onSuccess` invalidates both `['dashboard', flatId]` and `['readings', flatId]` (`useSubmitReading.test.ts`, extending the existing invalidation assertion); `useReadingHistory` fetches subsequent pages and exposes `hasNextPage`/`fetchNextPage` correctly (`useReadingHistory.test.ts`); the Reading History sheet renders the "Load more" button, appends items on click, and hides the button once exhausted (`ReadingHistorySheet.test.tsx`).
```

### `epic-list.md`
- Epic 12 entry: story count and summary updated to include Story 12.5; note (matching the existing 12.3/12.4 note) that 12.5 is also thematically unrelated to FR-52/53/54 but bucketed here per Ralf's choice.

### `prd.md`
- FR-48 (§4.3, after the existing two consequence bullets): new bullet — *"For Flats with long reading histories, the Reading History view loads readings incrementally (most recent first) rather than fetching the entire history at once."*

### `sprint-status.yaml`
- `12-5-reading-history-cache-fix-and-on-demand-paging: backlog` added under the Epic 12 block, after `12-4-insight-dismiss-and-reactivate`.

## Section 5: Implementation Handoff

**Change scope: Minor-to-Moderate.** The bug fix alone is a one-line, low-risk change following an existing pattern verbatim. The paging addition touches an API contract, one hook, one component, and tests across both layers, but reuses existing stack capabilities (TanStack Query v5 `useInfiniteQuery`, the architecture doc's already-documented paginated-collection shape) with no new architecture or library introduced.

**Routed to:** Developer agent (`bmad-agent-dev` / `bmad-dev-story` or `bmad-quick-dev`) for direct implementation of Story 12.5. No dependency on Stories 12.1–12.4; can be implemented in any order within Epic 12. Given the cache-invalidation bug is live and user-visible today (same "live production bug" precedent as Story 11.13), recommend picking this up promptly rather than deferring to the end of Epic 12.

**Success criteria:** `useSubmitReading` invalidates `['readings', flatId]` on success; `GetReadingHistoryFunction` correctly pages via `skip`/`take` with accurate `totalCount` and validates malformed params; `useReadingHistory` loads pages on demand via `fetchNextPage`; `ReadingHistorySheet` shows/hides the "Load more" button correctly and appends pages without losing existing edit/error-recovery behavior; full test coverage as specified in Story 12.5's final AC.

---

*All edits described in Section 4 have been applied directly to `epic-12-device-lifecycle-and-date-aware-decomposition-attribution.md`, `epic-list.md`, `prd.md`, and `sprint-status.yaml` as part of this `bmad-correct-course` pass, per Ralf's approval.*
