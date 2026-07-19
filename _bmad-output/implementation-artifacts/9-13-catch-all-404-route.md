---
baseline_commit: 33be6a897d45e25ab0eedbd4210eb0ac88aacd7d
---

# Story 9.13: Catch-All 404 Route

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to see a clear "page not found" message if I navigate to a URL that doesn't exist,
so that I'm not left staring at a blank screen wondering if the app is broken.

## Acceptance Criteria

1. **Given** no catch-all `path: '*'` route exists in `client/src/router.tsx`'s `createBrowserRouter` config, so unknown URLs currently render nothing (React Router logs a "No routes matched" console error and the last-rendered DOM stays as-is, effectively a blank page) — flagged three separate times since Epic 1 and never picked up (`deferred-work.md:53` "No catch-all `path: '*'` route in the React Router — unknown URLs render a blank page with no feedback. **→ Promoted to Story 9.13 (Epic 9)**", and `deferred-work.md:387`'s "Known gaps" entry in `project-context.md`), **when** this story is implemented, **then** a catch-all route renders a simple "Page not found" view with a link back to the Dashboard, styled consistent with the app's existing empty-state visual pattern (`DecompositionUnavailable.tsx` — icon, heading, body text, CTA).
2. **Given** this route, **when** a user navigates to any unmatched path, whether authenticated or unauthenticated, **then** the 404 view renders correctly in both auth states without crashing or infinite-redirecting.

## Tasks / Subtasks

- [x] Task 1: Add the catch-all route as a top-level router sibling, not a child of `OnboardingGate` (AC: 1, 2)
  - [x] In `client/src/router.tsx`, add `NotFoundPage` to the existing `lazy(() => import(...))` block (after the `OnboardingPage` import at line 10): `const NotFoundPage = lazy(() => import('@/components/NotFoundPage'))`.
  - [x] Add a new top-level array entry to the `createBrowserRouter([...])` call, as a sibling to the existing `{ path: '/onboarding', element: <Wrap Page={OnboardingPage} /> }` entry (line 35) — **not** nested inside the `OnboardingGate` element's `children` array (lines 20-34): `{ path: '*', element: <Wrap Page={NotFoundPage} /> }`.
  - [x] **Why it must be a top-level sibling, not a child of `OnboardingGate`:** `OnboardingGate.tsx:6` redirects to `/onboarding` for *any* path when `!settings?.hasFlat`, regardless of whether that path matched a real route. If the catch-all were nested under `OnboardingGate`'s `children`, a user without a flat visiting a nonexistent URL would be redirected to `/onboarding` before the catch-all is ever reached — the 404 would never render, masked by the onboarding redirect (not an infinite loop, but AC1 would silently fail for that auth/onboarding state). Placing `path: '*'` as a second top-level sibling (mirroring the existing `/onboarding` entry's placement) means it is matched directly by the router before `OnboardingGate`'s own children are evaluated, so it renders identically regardless of `hasFlat`, satisfying AC2's "both auth states" requirement structurally, not just by accident.
  - [x] Do not add an `errorElement` — this story is scoped to unmatched paths (`path: '*'`), not to render-time errors thrown by matched routes; conflating the two is out of scope.

- [x] Task 2: Create the `NotFoundPage` component (AC: 1)
  - [x] Create `client/src/components/NotFoundPage.tsx` (default export, matching this project's page-level-route-component convention — the only components with a default export besides other page components, per `project-context.md`'s Component Conventions rule). Place it in `client/src/components/` (not under `features/`) because, like `AppShell.tsx`, `Header.tsx`, and `BottomTabBar.tsx`, it is cross-cutting and not owned by a single feature slice.
  - [x] Implement it as a plain default-exported function component, same as every other page component (`DashboardPage.tsx`, `SettingsPage.tsx`, etc.) — the `lazy()` wrapping (Task 1) happens entirely at the `router.tsx` import site, not inside this file; this file itself has no special-casing for lazy loading.
  - [x] Full-page layout: this route renders **outside** `AppShell` (same as `OnboardingPage`, which is also a top-level sibling outside the `AppShell`/`OnboardingGate` subtree) — so it gets no `Header`, `SidebarNav`, `BottomTabBar`, or `EuroBurnGradient` for free. Give it its own full-viewport container: `<div className="flex flex-col items-center justify-center gap-3.5 h-[100dvh] px-6 text-center" style={{ background: '#111827' }}>` — `100dvh` matches `OnboardingPage.tsx:45`'s standalone full-page pattern; `#111827` matches the base app background used elsewhere as an inline style for a standalone page (`TariffList.tsx:61`), since there is no Tailwind token for this raw hex in a route with no `EuroBurnGradient` ancestor.
  - [x] Content, mirroring `DecompositionUnavailable.tsx:10-21`'s icon/heading/body/CTA anatomy (card chrome — `rounded-card border border-glass-border bg-glass-surface`, `px-6 py-9` — is dropped since this is a full page, not an inline card; the icon/heading/body/CTA classes and structure are kept):
    - Icon: `import { Compass } from 'lucide-react'`; render `<Compass size={48} className="text-text-tertiary" aria-hidden="true" />` (same `size={48}`/`text-text-tertiary`/`aria-hidden` pattern as `DecompositionUnavailable.tsx:11`). `Compass` is unused elsewhere in this codebase (confirmed by `grep -rh "from 'lucide-react'"` across `client/src`) but is part of the already-installed `lucide-react` package, so no new dependency is introduced.
    - Heading: `<h1 className="text-body text-white">{t('notFound.heading')}</h1>` — use a real `<h1>` (unlike `DecompositionUnavailable`'s `<span>`, which is correct for an inline card but not for a full page's primary heading), same `text-body text-white` typography token pair.
    - Body: `<p className="text-body-sm text-white/55">{t('notFound.body')}</p>` (same classes as `DecompositionUnavailable.tsx:13`).
    - CTA: a button using `useNavigate()` to go back to `/`, not a bare `<Link>` — matches this codebase's established convention of `useNavigate` + `onClick` for every other in-app "go somewhere" action (`TariffList.tsx:2,64-66`, `FlatSettingsCard.tsx`, `FlatBaselineEdit.tsx`, `ImportProgressCard.tsx`, `CostGapBadge.tsx`, `DecompositionPage.tsx`, `DecompositionTab.tsx` — no component in this codebase renders a bare react-router `<Link>`). Style: `className="mt-1 w-full max-w-xs rounded-card border border-white/[0.18] bg-white/10 px-4 py-3.5 text-body-sm font-semibold text-white"` (same button classes as `DecompositionUnavailable.tsx:14-20`, with `max-w-xs` added since this button is no longer constrained by a parent card's width).
  - [x] i18n: `const { t } = useTranslation('common')` — no new namespace needed (see Task 3).

- [x] Task 3: Add `notFound` translation keys to the existing `common` namespace (AC: 1)
  - [x] In `client/src/locales/en-US/common.json`, add a new top-level `"notFound"` block alongside the existing `app`/`nav`/`actions`/`errors`/`flatSwitcher` blocks: `"notFound": { "heading": "Page not found", "body": "The page you're looking for doesn't exist or may have moved.", "cta": "Back to Dashboard" }`.
  - [x] In `client/src/locales/de-DE/common.json`, add the matching block: `"notFound": { "heading": "Seite nicht gefunden", "body": "Die gesuchte Seite existiert nicht oder wurde verschoben.", "cta": "Zurück zum Dashboard" }`.
  - [x] Do **not** create a new `notFound.json` namespace file or register a new entry in `client/src/lib/i18n.ts`'s `ns: [...]` array (`i18n.ts:23-34`) — `common` is already loaded on every route and already holds the app's other generic/cross-cutting strings (its existing `errors` block), so this is the path of least friction and matches this project's "only truly shared labels go in `common`" i18n rule (`project-context.md`).

- [x] Task 4: Frontend tests (AC: 1, 2)
  - [x] Create `client/src/components/NotFoundPage.test.tsx` following this project's component-test convention (`AppShell.test.tsx`'s `createMemoryRouter`/`RouterProvider` pattern; `vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (k: string) => k }) }))` per `project-context.md`'s i18n-test rule). Cover:
    - `NotFoundPage_Rendered_ShowsHeadingAndBodyText`: renders `notFound.heading` and `notFound.body` (the raw keys, since `t` is mocked to return the key itself).
    - `NotFoundPage_CtaClicked_NavigatesToDashboard`: render inside a `createMemoryRouter` with routes `[{ path: '/nonexistent', element: <NotFoundPage /> }, { path: '/', element: <div>dashboard-stub</div> }]`, `initialEntries: ['/nonexistent']`; click the `notFound.cta` button; assert `dashboard-stub` is now in the document (proves the navigation actually lands on `/`, not just that a click handler fired).
  - [x] Create `client/src/router.test.tsx` — no router test file currently exists for `router.tsx` (confirmed by `find client/src -iname "*router*"` returning only `router.tsx` itself), so this is net-new coverage, not an extension of an existing suite. Build a test-local route tree that mirrors `router.tsx`'s real structure (the same approach `AppShell.test.tsx:19-31` already uses — a fresh `createMemoryRouter` built inline in the test file, not an import of the real `router` singleton, since `router.tsx`'s `createBrowserRouter` + real lazy imports of full feature pages are impractical to mount directly in a unit test). Mock `@/features/settings/hooks/useUserSettings` (the hook `OnboardingGate` depends on) to control `hasFlat` per test. Cover:
    - `Router_UnmatchedPathWithFlat_RendersNotFoundPage`: mock `useUserSettings` to return `{ settings: { hasFlat: true }, isLoading: false, isError: false }`; navigate to an unmatched path (e.g. `/this-does-not-exist`); assert the catch-all renders (a stub element is fine — the test's job is to prove routing reaches the catch-all, not to re-test `NotFoundPage`'s own content, which `NotFoundPage.test.tsx` already covers).
    - `Router_UnmatchedPathWithoutFlat_RendersNotFoundPageNotOnboardingRedirect`: same, but mock `useUserSettings` to return `{ settings: { hasFlat: false }, isLoading: false, isError: false }`; navigate to the same unmatched path; assert the catch-all still renders (proving the fix for the masking risk described in Task 1) and that no navigation to `/onboarding` occurred (e.g. assert the onboarding stub element is absent). This is the test that pins AC2 — without the top-level-sibling placement from Task 1, this test would fail (the request would be redirected to `/onboarding` instead).
    - `Router_KnownPath_StillRendersItsOwnPage`: sanity check that adding the catch-all didn't shadow a real route — navigate to `/` with `hasFlat: true` and assert the (stubbed) dashboard page renders, not the 404.

- [x] Task 5: Update `project-context.md` to reflect the implemented behavior (AC: 1, 2)
  - [x] Remove the now-resolved "No catch-all 404 route in React Router — unknown URLs render blank (deferred)" line from the `#### Known gaps — do not re-implement or work around` section, since the gap is closed by this story.
  - [x] Bump the file's `_Last updated: ..._` footer date to reflect this change.

## Dev Notes

### Existing code being modified — current state and what's preserved

- **`client/src/router.tsx`** (full file, 37 lines currently): a `createBrowserRouter` config with two top-level array entries — (a) `OnboardingGate` wrapping `AppShell` wrapping the four "real" app pages (`/`, `/insights`, `/decomposition/*`, `/settings/*`), and (b) a sibling top-level `/onboarding` entry outside the gate. This story adds a **third** top-level sibling entry, `{ path: '*', element: <Wrap Page={NotFoundPage} /> }`, using the exact same `lazy`/`Wrap` mechanism already used for every other route in the file. No existing route, the `Wrap` helper, or the `OnboardingGate`/`AppShell` nesting is changed.
- **`client/src/features/onboarding/components/OnboardingGate.tsx`** (full file, 8 lines): unchanged. Its redirect-to-`/onboarding`-for-any-unmatched-path-under-its-subtree behavior is exactly why the new route must sit outside it (see Task 1's rationale) — this story works around that behavior structurally rather than modifying it, since `OnboardingGate` itself is correct for its own purpose (gating the real app pages, not 404 handling).
- **`client/src/locales/{en-US,de-DE}/common.json`**: additive only — a new `notFound` key block is added alongside the existing `app`/`nav`/`actions`/`errors`/`flatSwitcher` blocks. No existing key changed or removed.
- **`client/src/lib/i18n.ts`**: no change — `common` is already in the `ns: [...]` array and already the `defaultNS`.

### Testing Standards Summary

- Frontend: Vitest (`globals: true`, no `describe`/`it`/`expect` imports) + `@testing-library/react`, query by role/text, `jsdom` environment — per `project-context.md`.
- Mock `react-i18next` to return the raw key (`t: (k: string) => k`) in every new test file — established pattern (e.g. `RoomCard.test.tsx`, `SmartStripCard.test.tsx`).
- Router/navigation tests use `createMemoryRouter` + `RouterProvider` from `react-router-dom`, following `AppShell.test.tsx`'s existing pattern exactly (including `initialEntries` and `router.navigate(...)` inside `act(async () => ...)` where a programmatic navigation is asserted).
- No backend changes in this story — no `dotnet test` impact.
- Full verification bar before marking review: `npx vitest run`, `npx tsc --noEmit`, `npm run lint` in `client/`.

### Project Structure Notes

- Two new files: `client/src/components/NotFoundPage.tsx` and its test `client/src/components/NotFoundPage.test.tsx` — same directory as `AppShell.tsx`/`Header.tsx`/`BottomTabBar.tsx`, following the existing convention that cross-cutting, non-feature-owned UI lives in `client/src/components/`, not under `features/`.
- One new test file: `client/src/router.test.tsx`, co-located with `router.tsx` (no existing test file for it to extend).
- No new i18n namespace — only additive keys in the existing `common.json` files (both locales).
- No VSA slice-isolation concerns — `NotFoundPage` and `router.tsx` are both cross-cutting/app-shell-level, not inside any feature slice; no cross-slice hook imports are introduced.
- No backend, database, or infrastructure changes — this is a frontend-only story.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-9-unified-save-affordance-fix-and-pre-epic-10-hardening.md#Story 9.13] — original epic AC text (verbatim source for AC1-2).
- [Source: _bmad-output/implementation-artifacts/deferred-work.md:53] — "Deferred from: code review of 1-1-monorepo-scaffold-and-cicd-pipeline pass 2" entry this story resolves ("→ Promoted to Story 9.13 (Epic 9)"); mark closed once shipped.
- [Source: _bmad-output/project-context.md#Known gaps — do not re-implement or work around] — "No catch-all 404 route in React Router — unknown URLs render blank (deferred)" — this line should be removed from `project-context.md`'s Known Gaps list once this story ships (a human/maintenance task, not part of this story's file list, per that file's own "Update when..." usage guideline — flagging here so it isn't missed).
- [Source: client/src/router.tsx] — full current router config this story extends (all line numbers above verified against the file as of this story's creation).
- [Source: client/src/features/onboarding/components/OnboardingGate.tsx] — the redirect behavior that makes top-level sibling placement structurally necessary, not merely stylistic.
- [Source: client/src/features/onboarding/OnboardingPage.tsx:45] — precedent for a standalone (outside-`AppShell`) full-page layout's `h-[100dvh]` + inline `style={{ background: ... }}` pattern.
- [Source: client/src/features/tariffs/components/TariffList.tsx:61] — precedent for the `#111827` base background hex used as an inline style on a standalone page.
- [Source: client/src/features/decomposition/components/DecompositionUnavailable.tsx] — full file; the empty-state icon/heading/body/CTA visual pattern this story's 404 view mirrors (card chrome dropped for a full-page context).
- [Source: client/src/components/AppShell.test.tsx] — the `createMemoryRouter`-based test pattern this story's two new test files follow.
- [Source: client/src/locales/en-US/common.json, client/src/locales/de-DE/common.json] — existing `common` namespace structure this story adds a `notFound` block to.
- [Source: client/src/lib/i18n.ts:23-34] — namespace registry confirming `common` is already registered and is `defaultNS`, so no registration change is needed.
- [Source: _bmad-output/project-context.md#Critical Implementation Rules] — component-conventions (default export only for page-level components), i18n-per-feature-namespace rule, and `useNavigate`-over-`Link` convention this story follows.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

- `npx vitest run` (full suite): 62 files, 400 tests passed
- `npx tsc --noEmit`: clean, no errors
- `npm run lint` (oxlint): 0 errors; 7 pre-existing `only-export-components` warnings on `router.tsx`'s lazy-import consts (confirmed via git stash diff that this warning category predates this story — it now also fires once more for the new `NotFoundPage` const, consistent with the existing pattern)

### Completion Notes List

- Added a third top-level sibling route (`path: '*'`) to `createBrowserRouter` in `router.tsx`, outside `OnboardingGate`'s subtree, so the 404 page renders regardless of onboarding/auth state (AC2) rather than being masked by the `/onboarding` redirect.
- Created `NotFoundPage.tsx` as a full-page, default-exported route component (no `AppShell`), mirroring `DecompositionUnavailable.tsx`'s icon/heading/body/CTA pattern with card chrome dropped; CTA uses `useNavigate()` to `/`.
- Added `notFound.{heading,body,cta}` keys to the existing `common` namespace in both `en-US` and `de-DE` locale files; no new i18n namespace registered.
- Added `NotFoundPage.test.tsx` (heading/body render + CTA navigation) and net-new `router.test.tsx` (unmatched path with/without `hasFlat`, known path still routes correctly) — 5 new tests, all passing. Followed red-green-refactor: tests written and confirmed failing (missing module / wrong routing) before implementation.
- Full verification bar passed: `npx vitest run` (400/400), `npx tsc --noEmit` (clean), `npm run lint` (0 errors).
- Removed the now-resolved "No catch-all 404 route in React Router" line from `project-context.md`'s Known Gaps list and bumped its `Last updated` footer date, so the project context doc no longer describes fixed behavior as an outstanding gap.

### File List

- `client/src/router.tsx` (modified)
- `client/src/components/NotFoundPage.tsx` (new)
- `client/src/components/NotFoundPage.test.tsx` (new)
- `client/src/router.test.tsx` (new)
- `client/src/locales/en-US/common.json` (modified)
- `client/src/locales/de-DE/common.json` (modified)
- `_bmad-output/project-context.md` (modified — removed resolved Known Gap entry)

## Change Log

- 2026-07-19: Implemented catch-all 404 route (Story 9.13) — added `NotFoundPage` component, top-level `path: '*'` router entry outside `OnboardingGate`, `notFound` i18n keys (en-US/de-DE), and full test coverage (`NotFoundPage.test.tsx`, `router.test.tsx`).
- 2026-07-19: Updated `project-context.md` to remove the resolved "No catch-all 404 route" Known Gap entry, reflecting the implemented behavior.

### Review Findings

- [x] [Review][Patch] Contradictory `last_updated` bookkeeping in `sprint-status.yaml` — comment header says "→ review", structured field says "created — ready-for-dev" for the same date [_bmad-output/implementation-artifacts/sprint-status.yaml:2,38]
- [x] [Review][Patch] `NotFoundPage` CTA uses `navigate('/')` (push) instead of `replace` — leaves the unmatched URL in browser history, so pressing Back after the CTA returns the user to the 404 page [client/src/components/NotFoundPage.tsx:19]
- [x] [Review][Defer] `router.test.tsx` builds an inline stub route tree instead of importing the real `router` from `router.tsx` — a regression in the real file's route order/placement would not be caught by these tests [client/src/router.test.tsx:9-28] — deferred, pre-existing test-strategy tradeoff (mirrors `AppShell.test.tsx`'s established pattern per this story's own Dev Notes)
- [x] [Review][Defer] CTA relies on `OnboardingGate`, whose pre-existing `isLoading || isError → return null` behavior can render blank if clicked during a slow/failed settings fetch [client/src/features/onboarding/components/OnboardingGate.tsx:6] — deferred, pre-existing behavior shared by every in-app navigation to `/`, already tracked via the separate "No React Error Boundary around `<Outlet />`" Known Gap
- [x] [Review][Defer] CTA copy "Back to Dashboard" is imprecise for users with `hasFlat: false` — `OnboardingGate` redirects `navigate('/')` to `/onboarding` instead [client/src/components/NotFoundPage.tsx:19] — deferred, arguably correct product behavior, just an imprecise label; needs product input if changed
- [x] [Review][Defer] `NotFoundPage` is lazy-loaded like every other route; a chunk-load failure on this fallback path has no error boundary and could reproduce the "blank page" symptom this story closes [client/src/router.tsx:11] — deferred, pre-existing risk shared by all lazy routes, already tracked via the separate Error Boundary Known Gap
- [x] [Review][Defer] No `document.title` update or aria-live announcement when landing on the 404 route [client/src/components/NotFoundPage.tsx] — deferred, no existing per-route title convention in this codebase to extend
- [x] [Review][Defer] `NotFoundPage.test.tsx` mocks `react-i18next` to return raw keys, so it does not verify the `notFound.*` keys actually resolve in the locale JSON files [client/src/components/NotFoundPage.test.tsx:7-9] — deferred, same testing convention used project-wide, not unique to this diff
- [x] [Review][Defer] SPA catch-all returns HTTP 200 for genuinely dead links — no server/hosting-level 404 status [client/src/router.tsx] — deferred, inherent SPA architecture characteristic, unrelated to this story's scope
