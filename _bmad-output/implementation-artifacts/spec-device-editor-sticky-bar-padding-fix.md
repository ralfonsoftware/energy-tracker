---
title: 'Device/Room editor: fix sticky action bar covering last form field on mobile'
type: 'bugfix'
created: '2026-07-16'
status: 'done'
route: 'one-shot'
context: []
---

# Device/Room editor: fix sticky action bar covering last form field on mobile

## Intent

**Problem:** On mobile, `DeviceEditor`'s and `RoomEditor`'s shared `StickyActionBar` (Abbrechen/Speichern) covered the last form field at full scroll — the scrollable content's bottom padding (`pb-10` = 40px) was smaller than the sticky bar's actual rendered height (~81-97px), confirmed via a real device screenshot showing the kWh input hidden underneath it. Not a scroll-lock: the page reached its true scroll end (proven by the app's persistent tab bar being fully visible), the last field was just physically occluded.

**Approach:** Increase the bottom padding on both components' scrollable content wrappers to `pb-32` (128px), reliably clearing the sticky bar's rendered height with a safety margin, and document the relationship with an inline comment so the value isn't a silent magic number.

## Suggested Review Order

- Root cause and fix rationale, documented at the point of change.
  [`DeviceEditor.tsx:87`](../../client/src/features/flat-structure/components/DeviceEditor.tsx#L87)

- Same shared-component defect, same fix, in the sibling editor.
  [`RoomEditor.tsx:58`](../../client/src/features/flat-structure/components/RoomEditor.tsx#L58)

- The shared sticky bar whose rendered height drives the padding requirement (unchanged, reference only).
  [`StickyActionBar.tsx:8`](../../client/src/features/flat-structure/components/StickyActionBar.tsx#L8)

- Investigation case file with the confirmed root cause and supporting screenshot evidence.
  [`device-editor-mobile-scroll-investigation.md`](investigations/device-editor-mobile-scroll-investigation.md)
