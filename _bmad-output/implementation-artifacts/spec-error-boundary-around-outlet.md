---
title: 'Error Boundary Around AppShell <Outlet />'
type: 'feature'
created: '2026-07-22'
status: 'done'
context: []
baseline_commit: 'a062cb0fe17d650b712695afecc82a00b2f64e43'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `AppShell.tsx` wraps `<Outlet />` with no error boundary. A runtime throw in any lazily-loaded child page unmounts the entire shell (Header, SidebarNav, BottomTabBar, EuroBurnGradient all disappear), not just the broken page. Flagged since Epic 8's retro, still open per Epic 9's retro and `project-context.md`'s Known Gaps.

**Approach:** Add a class-based `ErrorBoundary` wrapping only `<Outlet />` in `AppShell.tsx`, rendering a `NotFoundPage`-styled `ErrorFallback` on catch. Chrome outside `<Outlet />` stays mounted and functional. The boundary resets when the route changes OR the user clicks the fallback's CTA — without forcing an unmount/remount of unrelated, un-errored subtree state on every ordinary navigation.

## Boundaries & Constraints

**Always:**
- `ErrorBoundary` must be a class component (`static getDerivedStateFromError` + `componentDidCatch`) — React has no hook-based boundary as of React 19.
- `ErrorFallback` visual style mirrors `NotFoundPage.tsx`: dark background, centered icon/heading/body/CTA, `useTranslation('common')`.
- `ErrorFallback`'s CTA uses `useNavigate('/', { replace: true })`, same as `NotFoundPage.tsx` — `ErrorFallback` is a function component rendered inside the router tree, so it has full router context despite being rendered by a class-based boundary; only `componentDidCatch` itself (the class method) cannot use hooks.
- `ErrorBoundary` accepts a `resetKey: string` prop (NOT React's `key` — a plain prop). In `componentDidUpdate(prevProps)`, if `this.state.hasError && prevProps.resetKey !== this.props.resetKey`, call `this.setState({ hasError: false })`. This resets on navigation without unmounting the subtree when no error occurred — `key={pathname}` was tried and rejected (see Spec Change Log) because it forces a full remount of everything under `<Outlet />` on every navigation, silently discarding in-progress state (e.g. unsaved form edits) even when nothing ever errored.
- In `AppShell.tsx`, pass `resetKey={pathname}` (from the existing `useLocation()` destructure) to `<ErrorBoundary>`.
- `ErrorBoundary` also exposes an internal `reset = () => this.setState({ hasError: false })` method and passes it to `ErrorFallback` as an `onRecover` prop, so the CTA can recover even when the error occurred on `/` itself (where navigating to `/` again wouldn't change `pathname`, so the `resetKey` comparison alone wouldn't fire). The CTA calls `onRecover()` AND navigates, in that order.
- New i18n keys (`errorBoundary.heading`/`body`/`cta`) added to both `en-US` and `de-DE` `common.json`, mirroring the existing `notFound` key shape.
- `componentDidCatch` logs via `console.error` only — no telemetry hook exists in this codebase to call instead.

**Ask First:**
- None expected.

**Never:**
- Do not touch the other 2 remaining Epic 9 retro tech-debt items (router structural test, TrendChart SVG id scoping) — tracked separately in `deferred-work.md`.
- Do not use `key={pathname}` (or any `key` prop) on `<ErrorBoundary>` — this was the root cause of a rejected earlier version; see Spec Change Log. Reset must go through the `resetKey` prop + `componentDidUpdate` comparison, not React's reconciliation `key`.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Child page throws during render | Any lazily-loaded page component throws | `ErrorBoundary` catches it; `ErrorFallback` renders in place of `<Outlet />` | Chrome (`Header`/`SidebarNav`/`BottomTabBar`) stays mounted; error logged via `console.error` |
| Child page renders normally | No throw | `children` render unchanged; `ErrorBoundary` is a transparent passthrough; no remount occurs on navigation since `resetKey` changes are only acted on when `hasError` is true | N/A |
| User clicks fallback CTA (error occurred on a non-root route) | Fallback is shown | `onRecover()` clears `hasError`; client-side navigation to `/` via `useNavigate` also fires; `pathname` change would independently reset too | N/A |
| User clicks fallback CTA (error occurred on `/` itself) | Fallback is shown, already at `/` | `onRecover()` clears `hasError` directly — works even though `pathname` doesn't change | N/A |
| User navigates away via SidebarNav/BottomTabBar after an error was caught | Fallback is shown, user clicks any other nav item | `pathname` changes, `componentDidUpdate` sees `resetKey` differ while `hasError` is true, clears it; the newly-selected page renders normally | N/A |

</frozen-after-approval>

## Code Map

- `client/src/components/AppShell.tsx` -- wrap `<Outlet />` (line 28) in `<ErrorBoundary resetKey={pathname}>`, reusing the existing `useLocation()` destructure
- `client/src/components/ErrorBoundary.tsx` -- new class component, catch + fallback render + `componentDidUpdate` reset-on-`resetKey`-change + `reset()` method passed to fallback
- `client/src/components/ErrorFallback.tsx` -- new presentational component, styled like `NotFoundPage.tsx`, CTA calls `onRecover()` then `useNavigate`
- `client/src/components/AppShell.test.tsx` -- add throw-and-recover test, a nav-after-error reset test, and a same-route CTA-recovery test (a 4th test for non-root-route CTA recovery was attempted and dropped — see Spec Change Log)
- `client/src/locales/en-US/common.json`, `client/src/locales/de-DE/common.json` -- add `errorBoundary` key block

## Tasks & Acceptance

**Execution:**
- [x] `client/src/components/ErrorBoundary.tsx` -- class component: `Props = { children: ReactNode; resetKey: string }`, `state = { hasError: boolean; resetKey: string }`, `static getDerivedStateFromError()` sets `hasError`, `componentDidCatch(error, info)` calls `console.error`, a `reset = () => this.setState({ hasError: false })` class field, renders `this.props.children` or `<ErrorFallback onRecover={this.reset} />`. Correction during implementation: used `static getDerivedStateFromProps` (comparing `props.resetKey` to `state.resetKey`, resetting `hasError` when they differ) instead of `componentDidUpdate` + `setState` — oxlint's `react(no-did-update-set-state)` flagged the latter as a new warning; `getDerivedStateFromProps` achieves the identical reset behavior without a lint-visible setState-in-lifecycle pattern, and is React's documented approach for deriving state from prop changes.
- [x] `client/src/components/ErrorFallback.tsx` -- create presentational component accepting `onRecover: () => void`: icon + `t('errorBoundary.heading')` + `t('errorBoundary.body')` + CTA button whose `onClick` calls `onRecover()` then `navigate('/', { replace: true })` -- reuses `NotFoundPage.tsx`'s exact layout/classes for visual consistency
- [x] `client/src/components/AppShell.tsx` -- import `ErrorBoundary`, wrap only `<Outlet />` (not the whole `<main>`) so `Header` stays outside the boundary; pass `resetKey` as a plain prop, NOT React's `key`. Patch during review round 3: extended to `` resetKey={`${pathname}${search}${hash}`} `` (was `resetKey={pathname}` alone) — `AppShell` already tracks `search`/`hash` together with `pathname` for its scroll-reset effect on the same line; a query-string-driven error wouldn't have cleared on same-path navigation otherwise.
- [x] `client/src/locales/en-US/common.json` -- add `"errorBoundary": { "heading": "Something went wrong", "body": "An unexpected error occurred. Try going back to the dashboard.", "cta": "Back to Dashboard" }`
- [x] `client/src/locales/de-DE/common.json` -- add matching German `errorBoundary` block
- [x] `client/src/components/AppShell.test.tsx` -- add test: render `AppShell` with a child route whose element throws during render, assert fallback text is shown AND `screen.getByTestId('sidebar-nav')`/`'bottom-tab-bar'`/`'header'` remain in the document. `AppShell.test.tsx` does NOT mock `react-i18next` — assert against the real resolved English string, not the raw key. Restore the `console.error` spy at the end.
- [x] `client/src/components/AppShell.test.tsx` -- add test: after the fallback is shown, navigate to a different (healthy) route via `router.navigate(...)`, assert the fallback is gone and the new route's content renders — proves the `resetKey`-based reset works without a `key`-forced remount
- [x] `client/src/components/AppShell.test.tsx` -- add test: render at `/` with an element that throws only while a module-scoped `shouldThrow` flag is true, click the CTA button (`fireEvent.click`), flipping `shouldThrow` to `false` beforehand, assert the fallback clears and the recovered content renders — proves `onRecover()` works even when the destination pathname doesn't change

**Acceptance Criteria:**
- Given a child route element throws during render, when `AppShell` is mounted, then `ErrorFallback` renders in place of the broken page and `SidebarNav`/`BottomTabBar`/`Header` remain mounted.
- Given no throw occurs, when the user navigates between routes, then no component under `<Outlet />` unmounts/remounts due to the error boundary (existing `AppShell.test.tsx` tests and app behavior unchanged from before this change).
- Given the fallback is shown on a non-root route, when the user clicks its CTA, then `hasError` clears via `onRecover()` and the app navigates client-side to `/`.
- Given the fallback is shown while already on `/`, when the user clicks its CTA, then `hasError` still clears via `onRecover()` even though `pathname` doesn't change.
- Given the fallback is shown, when the user navigates to a different route via the nav chrome (not the CTA), then the new route renders normally — the boundary does not stay stuck on the fallback.

## Spec Change Log

- **2026-07-22, loopback 1 (after first review pass):** Blind Hunter and Edge Case Hunter both found that `ErrorBoundary`'s `hasError` state never reset, so any caught error left every subsequent route stuck on the fallback until a hard reload. Root cause: the original frozen Boundaries wrongly claimed `ErrorFallback` "has no router context available from `componentDidCatch`" and forbade any reset mechanism. Amended to `useNavigate()` + `key={pathname}` on `ErrorBoundary`.
- **2026-07-22, loopback 2 (after second review pass):** Edge Case Hunter found the `key={pathname}` fix from loopback 1 was itself flawed two ways: (1) it forces a full unmount/remount of everything under `<Outlet />` on *every* navigation — not just after an error — silently discarding in-progress state (e.g. unsaved form edits in `SettingsPage`'s internal sub-routing) even when nothing errored; (2) if the error occurs on `/` itself, navigating to `/` again doesn't change `pathname`, so the CTA becomes a no-op. Amended to a `resetKey` prop + `componentDidUpdate` comparison (resets `hasError` on navigation without forcing a remount) plus an explicit `reset()` method wired to the CTA via an `onRecover` callback (works regardless of whether `pathname` changes). KEEP: class-component shape, `console.error`-only logging, `NotFoundPage.tsx` visual styling, `Header` staying outside the boundary, `useNavigate()` for the CTA (all still correct, unaffected by this amendment).
- **2026-07-22, review round 3 (patches, no further loopback):** Acceptance Auditor confirmed both loopback-2 problems genuinely fixed. Edge Case Hunter found `resetKey={pathname}` alone ignores `search`/`hash` — patched to include all three, matching `AppShell`'s existing scroll-reset effect's dependency shape. Blind Hunter flagged untested CTA-on-non-root-route coverage; adding that test (mounting/navigating into a route that throws) reliably reproduced a React 19 internal diagnostic (`"error during concurrent rendering... recovered by synchronously rendering"`) tied to `react-router`'s `navigate()` transitioning into a throwing destination — confirmed via isolation that it fires regardless of call order (`onRecover()`/`navigate()` swapped, no change) and even on a bare `router.navigate` with no CTA involved at all. All assertions passed in every variant tried; this is React/react-router's own recoverable-error diagnostic, not an application defect. Test dropped rather than chased further — test 3 (CTA-click-on-root-route) already proves `onRecover()` works independent of `pathname`. Logged as a known, code-inspection-verified-but-not-integration-tested combination in `deferred-work.md`.

## Design Notes

React logs uncaught errors to the console by default even when a boundary catches them (this is React's own dev-mode behavior, not an app bug) — do not attempt to suppress this; it's expected during the test and in production dev builds.

`resetKey` is a plain prop compared in `componentDidUpdate`, deliberately NOT React's `key` — `key` forces full unmount/remount of the subtree on every change (too broad, wipes unrelated state on every navigation), whereas a `componentDidUpdate` comparison only resets the boundary's own `hasError` state, and only when it was actually `true`.

## Verification

**Commands:**
- `cd client && npm run test -- AppShell.test.tsx` -- expected: all pass, including new throw-and-recover test
- `cd client && npx tsc --noEmit` -- expected: no new type errors
- `cd client && npm run lint` -- expected: no new warnings

## Suggested Review Order

**Reset mechanism (the part that went through 2 loopbacks)**

- Entry point: `resetKey` is a plain prop, not React's `key` — this is what avoids remounting unrelated state on every navigation
  [`ErrorBoundary.tsx:4`](../../client/src/components/ErrorBoundary.tsx#L4)

- `getDerivedStateFromProps` resets `hasError` when `resetKey` changes — side-effect-free, avoids the oxlint `no-did-update-set-state` warning a `componentDidUpdate` version hit
  [`ErrorBoundary.tsx:14`](../../client/src/components/ErrorBoundary.tsx#L14)

- Explicit `reset()` method, passed to the fallback as `onRecover` — the second half of the reset story, covers the "error on `/` itself" case where `resetKey` alone can't fire
  [`ErrorBoundary.tsx:25`](../../client/src/components/ErrorBoundary.tsx#L25)

- CTA calls `onRecover()` then navigates — order matters less than it seems (see Spec Change Log round 3) but this is the wiring
  [`ErrorFallback.tsx:21`](../../client/src/components/ErrorFallback.tsx#L21)

- `resetKey={pathname}{search}{hash}` — the actual value passed in, extended in round 3 to match `AppShell`'s existing scroll-reset dependency shape
  [`AppShell.tsx:29`](../../client/src/components/AppShell.tsx#L29)

**Boundary shape and wiring**

- Class component with `getDerivedStateFromError` + `componentDidCatch` — the catch itself, unchanged since the very first version
  [`ErrorBoundary.tsx:7`](../../client/src/components/ErrorBoundary.tsx#L7)

- `ErrorFallback` visual layout, mirrors `NotFoundPage.tsx`
  [`ErrorFallback.tsx:1`](../../client/src/components/ErrorFallback.tsx#L1)

- Only `<Outlet />` is wrapped — `Header` stays outside, so chrome survives a child-page crash
  [`AppShell.tsx:27`](../../client/src/components/AppShell.tsx#L27)

**Tests**

- Core fix: throw shows fallback, chrome (`sidebar-nav`/`bottom-tab-bar`/`header`) stays mounted
  [`AppShell.test.tsx:65`](../../client/src/components/AppShell.test.tsx#L65)

- Proves `resetKey` reset works on ordinary navigation, without a `key`-forced remount
  [`AppShell.test.tsx:90`](../../client/src/components/AppShell.test.tsx#L90)

- Proves `onRecover()` works even when `pathname` doesn't change (the dead-end case from loopback 2)
  [`AppShell.test.tsx:121`](../../client/src/components/AppShell.test.tsx#L121)

**Peripherals**

- New i18n keys, both locales
  [`en-US/common.json:29`](../../client/src/locales/en-US/common.json#L29)
  [`de-DE/common.json:29`](../../client/src/locales/de-DE/common.json#L29)
