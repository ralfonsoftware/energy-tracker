---
title: 'Reading History sheet overflow fix'
type: 'bugfix'
created: '2026-08-02'
status: 'done'
route: 'one-shot'
context: []
---

# Reading History sheet overflow fix

## Intent

**Problem:** The Reading History bottom sheet had no height cap or internal scroll container. Once its content
(title + reading rows + "Mehr laden") grew taller than the viewport, the `position: fixed` sheet grew upward past
`top: 0`, permanently hiding the newest readings and the close (X) button off-screen with no way to scroll to them.

**Approach:** Wrap the sheet's scrollable content (loading/error/empty states, the reading list, and the load-more
button) in a `max-h-[65dvh] overflow-y-auto` inner container, keeping the title and drag handle outside it so the
close button (rendered by the parent Sheet) never overlaps the scrollbar's vertical band.

## Suggested Review Order

1. [ReadingHistorySheet.tsx](../../client/src/features/readings/components/ReadingHistorySheet.tsx#L52) — the fix: new scrollable wrapper, unchanged conditional branches
2. [reading-history-sheet-overflow-investigation.md](investigations/reading-history-sheet-overflow-investigation.md) — root-cause investigation this fix resolves
3. [deferred-work.md](deferred-work.md#deferred-from-code-review-of-reading-history-sheet-overflow-fix-2026-08-02) — sibling `ReadingEditView` has the same latent (unreproduced) gap, logged not fixed
