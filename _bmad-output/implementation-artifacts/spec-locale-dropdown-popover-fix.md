---
title: 'Migrate LocaleDropdown to Radix Popover (consistency fix)'
type: 'refactor'
created: '2026-07-04'
status: 'done'
route: 'one-shot'
---

# Migrate LocaleDropdown to Radix Popover (consistency fix)

## Intent

**Problem:** `LocaleDropdown.tsx` shared `FlatSwitcher`'s hand-rolled `position: absolute` dropdown pattern (manual outside-click/Escape listeners) that caused the flat switcher's dropdown to be clipped by an ancestor's `backdrop-filter` in WebKit. Not currently broken (its 3 onboarding usages have no `backdrop-filter`/`filter`/`transform` ancestor), but a latent risk tracked in `deferred-work.md`.

**Approach:** Proactively migrated onto the same Radix `Popover`/`PopoverTrigger`/`PopoverContent` pattern used by `FlatSwitcher.tsx`/`CostGapBadge.tsx`. Added a dedicated `LocaleDropdown.test.tsx` (none existed before). Caught and fixed one behavior regression during review: the `dimmed` prop's `opacity: 0.7` previously reached the open menu via a shared DOM wrapper; since Radix portals the content elsewhere, the style now needs to be applied explicitly on both the trigger and `PopoverContent`.

## Suggested Review Order

1. [LocaleDropdown.tsx](../../client/src/components/LocaleDropdown.tsx) — the refactor itself, including the `dimmed`-on-`PopoverContent` fix.
2. [LocaleDropdown.test.tsx](../../client/src/components/LocaleDropdown.test.tsx) — new test file: open/select/Escape/outside-dismiss, plus the dimmed-menu regression test and an `aria-expanded` toggle assertion.
3. [deferred-work.md](./deferred-work.md) — three entries marked resolved (latent clipping risk, missing `aria-haspopup`, missing touch-dismiss); one entry partially resolved (Escape-to-close now works, arrow-key listbox navigation still open).
