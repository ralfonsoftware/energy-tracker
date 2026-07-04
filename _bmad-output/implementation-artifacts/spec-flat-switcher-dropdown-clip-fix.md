---
title: 'Fix flat switcher dropdown clipped by header backdrop-filter'
type: 'bugfix'
created: '2026-07-04'
status: 'done'
route: 'one-shot'
---

# Fix flat switcher dropdown clipped by header backdrop-filter

## Intent

**Problem:** The flat switcher dropdown was a hand-rolled `position: absolute` `<div>` nested inside `<header>`, whose `backdropFilter: blur(20px)` clips overflowing absolutely-positioned descendants in WebKit/Safari — cutting off the dropdown at the header's bottom edge (see `_bmad-output/implementation-artifacts/investigations/flat-switcher-overlay-clipped-investigation.md`).

**Approach:** Refactored `FlatSwitcher.tsx`'s dropdown onto the Radix `Popover`/`PopoverTrigger`/`PopoverContent` primitive already used elsewhere in the codebase (`CostGapBadge.tsx`), so the panel renders in a portal to `document.body` instead of as a clipped descendant. Checked `LocaleDropdown.tsx` for the same latent risk — its current onboarding-screen usages don't sit inside any `backdrop-filter`/`filter`/`transform` ancestor, so it isn't currently broken; left as-is with a tracked note in `deferred-work.md`.

## Suggested Review Order

1. [FlatSwitcher.tsx](../../client/src/components/FlatSwitcher.tsx) — the actual fix: Popover-based dropdown replacing the hand-rolled absolute div.
2. [FlatSwitcher.test.tsx](../../client/src/components/FlatSwitcher.test.tsx) — two new tests covering Escape-key and outside-pointerdown dismissal, the behavior previously provided by manual `useEffect` listeners.
3. [deferred-work.md](./deferred-work.md) — note on `LocaleDropdown.tsx`'s shared latent risk (last entry, "fix of flat-switcher-overlay-clipped bug").
