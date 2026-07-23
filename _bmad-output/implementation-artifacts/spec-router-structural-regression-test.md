---
title: 'Router Structural Regression Test'
type: 'chore'
created: '2026-07-23'
status: 'done'
route: 'one-shot'
---

# Router Structural Regression Test

## Intent

**Problem:** `router.test.tsx` exercised a hand-rolled stub route tree instead of the real `router` export from `router.tsx`, so a route-order regression or accidental path removal in the real file wouldn't be caught by any test.

**Approach:** Extracted `router.tsx`'s inline route array into a separate exported `routes` const (used to build `createBrowserRouter(routes)`), then imported it directly in `router.test.tsx` and added structural assertions on real path values/order/nesting. Avoided react-router's `router.routes` getter — it's explicitly marked `@private PRIVATE - DO NOT USE` in the library's own type definitions — in favor of asserting on the plain, pre-`createBrowserRouter` array instead. Kept the existing 3 stub-tree behavior tests as-is; they test `OnboardingGate`/catch-all integration behavior, a different concern.

## Suggested Review Order

- The extraction that makes the real route config importable without touching react-router's private API
  [`router.tsx:20`](../../client/src/router.tsx#L20)

- New structural tests asserting on the real, imported `routes` array
  [`router.test.tsx:80`](../../client/src/router.test.tsx#L80)
