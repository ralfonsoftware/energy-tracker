---
title: 'Fix room-list row overflow on iPhone Safari (flat-structure editor)'
type: 'bugfix'
created: '2026-07-16'
status: 'done'
baseline_commit: 'f8590fd0472677156f1c60f9a2167239b45bae10'
context: ['{project-root}/_bmad-output/project-context.md', '{project-root}/_bmad-output/implementation-artifacts/investigations/flat-structure-room-row-overflow-investigation.md']
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** On iPhone-width Safari viewports, the flat-structure editor's room-list row (`FlatStructureEditor.tsx:322`) is a single non-wrapping flex row whose 4 children (name input, Save, "N Anschlüsse ›" summary, delete icon) can't shrink to fit — the summary text clips mid-word and the delete icon is pushed off-screen. Root-caused in `investigations/flat-structure-room-row-overflow-investigation.md`; pre-existing since Story 6.0, not introduced by Story 8.1/8.2.

**Approach:** Restructure the row into two stacked lines: line 1 keeps the name input plus Save+Delete grouped as icon-only buttons at the trailing edge; line 2 holds the "N Anschlüsse ›" summary button alone. Save becomes icon-only (lucide `Check`), preserving its existing per-room `aria-label` so accessibility and existing tests are unaffected. While `isPending` is true for this room's save, the icon swaps to a small spinning ring — reusing the CSS-ring `animate-spin` convention already established in `SettingsRoot.tsx`/`ImportProgressCard.tsx` — restoring the in-flight visual signal the old text label (`"Speichern"` → `"Speichert…"`) used to provide, which two independent code reviews flagged as lost when the button went icon-only. The button also gets a `min-h-11 min-w-11` touch-target floor (matching this same file's existing convention on the `editor.retry` button) and a `title` attribute mirroring its `aria-label`, so mouse users get a native tooltip an icon-only control otherwise lacks.

## Boundaries & Constraints

**Always:**
- Preserve the Save button's existing `aria-label` pattern (`` `${isPending ? t('editor.saving') : t('editor.save')}: ${room.name.trim()}` ``) unchanged — ~15 existing tests key off it via `getByRole('button', { name: 'editor.save: <RoomName>' })`.
- Preserve the Save button's `disabled={!isDirty || isPending || isSaveBlocked}` and `onClick={() => handleSaveRoom(room)}` unchanged — layout/markup only, no logic change.
- Preserve the delete-confirm branch (`confirmDeleteRoomKey === room.key`) — Cancel/Delete text buttons and the `room.deletePrompt` hint — unchanged in content/behavior; only verify it still fits at iPhone widths.
- Preserve the "N Anschlüsse ›" button's existing `onClick` (list→room navigation, including the `setSaveError(false)/setSaveSuccess(false)` reset) unchanged.
- Add `title={` `${isPending ? t('editor.saving') : t('editor.save')}: ${room.name.trim()}` `}` to the Save button, mirroring its `aria-label` value exactly.
- Give the Save button a `min-h-11 min-w-11` touch-target floor plus `flex items-center justify-center` to keep the icon/spinner centered inside it — matching the 44×44px convention already used by this file's `editor.retry` button.
- While `isPending` is true for the room being saved, render a small `animate-spin` CSS ring (sized `w-4 h-4`, matching the existing `Check`/`Trash2` icon sizing in this row) in place of the `Check` icon — reuse the ring styling already established in `SettingsRoot.tsx` (`border-2 border-white/20 border-t-white/70 rounded-full animate-spin`), do not invent a new spinner style.
- Re-run `npx tsc --noEmit`, `npx vitest run`, `npm run lint` from `client/` after the change; zero regressions.

**Ask First:** If the restructure would require changing the Save button's `aria-label` text itself (not just its surrounding markup) to make a test pass, stop and ask before doing so.

**Never:**
- Do not change `handleSaveRoom`, `handleDeleteRoom`, `isRoomDirty`, `hasBlankNameInRoom`, `hasPlugIdConflictForRoomSave`, or any mutation/state logic.
- Do not touch the page-level top-right Speichern button, `RoomEditor.tsx`, `DeviceEditor.tsx`, or `StickyActionBar.tsx` — out of scope per the investigation's Side Findings.
- Do not add any pending-state visual beyond the specified spinning-ring icon swap — no text label, no color change beyond the existing `disabled:opacity-40`, no additional messaging.
- Do not restructure the delete-confirm branch or change the icon-only treatment for it — it stays text-label buttons, unchanged, per the investigation's scoping.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Normal row, narrow viewport | Room with name + N power points, viewport ≤430px wide | Row wraps to 2 lines: line 1 = name input + Save icon + Delete icon (no horizontal overflow); line 2 = "N Anschlüsse ›" | N/A |
| Delete-confirm row, narrow viewport | `confirmDeleteRoomKey === room.key`, viewport ≤430px | Cancel/Delete text row still fits without overflow (unchanged) | N/A |
| Save pending | `isPending === true` for this room's save | Save button shows an `animate-spin` ring instead of the `Check` icon, stays disabled, `aria-label`/`title` swap to `editor.saving: <name>` | N/A |
| Save idle, mouse hover | Not pending, cursor over the Save icon button | Native browser tooltip shows via `title` (`editor.save: <name>` or the disabled-state text) | N/A |

</frozen-after-approval>

## Code Map

- `client/src/features/flat-structure/components/FlatStructureEditor.tsx` -- room-list row markup (lines ~322-390) to restructure into 2 lines; add `Check` import alongside existing `Trash2` import (line 4)
- `_bmad-output/implementation-artifacts/investigations/flat-structure-room-row-overflow-investigation.md` -- root-cause investigation this spec implements the fix for

## Tasks & Acceptance

**Execution:**
- [x] `client/src/features/flat-structure/components/FlatStructureEditor.tsx` -- import `Check` from `lucide-react`; change the row's outer container (line 322, currently `<div className="flex items-center gap-2">`) to `<div className="flex flex-col gap-2">` wrapping two nested rows: row 1 = the existing `flex items-center gap-2` line with the name input plus (delete-confirm Cancel/Delete OR Save+Delete icon buttons) grouped via `flex items-center gap-2 shrink-0`; row 2 = the "N Anschlüsse ›" button alone, rendered only when not in delete-confirm mode -- fixes the overflow at its root by letting content wrap instead of forcing 4 unshrinkable items onto one line
- [x] `client/src/features/flat-structure/components/FlatStructureEditor.tsx` -- change the Save button from a text pill to an icon-only, 44×44px-minimum button rendering `<Check className="h-4 w-4" aria-hidden="true" />` normally, or a small `animate-spin` ring while `isPending`, plus a `title` mirroring its `aria-label`; keep `aria-label`/`disabled`/`onClick` unchanged (see Design Notes for the exact markup) -- reclaims horizontal space per the reported fix direction while restoring the pending-state signal and touch-target size two independent reviews flagged as lost in the first pass
- [x] `client/src/features/flat-structure/components/FlatStructureEditor.test.tsx` -- run the existing suite unchanged first; only touch a test if the restructure breaks a query relying on incidental DOM structure (e.g. sibling order) rather than role/label -- confirms no hidden coupling to the old single-row layout

**Acceptance Criteria:**
- Given a room-list row rendered at a 390px-wide viewport, when not in delete-confirm mode, then the name input, Save icon, and Delete icon appear on one line with no horizontal overflow, and "N Anschlüsse ›" appears on its own line below.
- Given the same row in delete-confirm mode, when rendered at 390px width, then the Cancel/Delete text-button row still fits without overflow (unchanged from today).
- Given the Save icon button, when queried by its existing `aria-label` (`editor.save: <name>` / `editor.saving: <name>`), then it is found and clickable exactly as before — all existing tests referencing this pattern pass unchanged.
- Given `isPending` is true for a room's save, when the row re-renders, then the Save button shows the spinning ring instead of the checkmark and remains disabled.
- Given `npx tsc --noEmit`, `npx vitest run`, and `npm run lint` run from `client/`, when executed after the change, then all pass with zero regressions.

## Spec Change Log

- **2026-07-16, code review loopback (intent_gap):** Blind Hunter and Edge Case Hunter independently flagged that the first icon-only Save button implementation removed all visual pending-state feedback (previously `"Speichern"` → `"Speichert…"` text swap), and that `isPending` is a shared/global flag so this affected every room row identically. Edge Case Hunter separately flagged the new button's ~32×32px tap target fell below this same file's own established `min-h-11 min-w-11` (44×44px) convention, and Blind Hunter flagged the icon-only button had no hover affordance (`title`) for sighted mouse users. Human decision (Ralf, 2026-07-16): amend intent to allow a scoped spinning-ring icon swap during `isPending` (reusing the existing `SettingsRoot.tsx`/`ImportProgressCard.tsx` `animate-spin` convention, not inventing a new one), plus the `min-h-11 min-w-11` touch target and `title` attribute. Code was reverted to `baseline_commit` before this amendment; re-implementing from the amended spec. **KEEP:** the two-row restructure (input+Save+Delete on row 1, "N Anschlüsse ›" on row 2) and the icon-only Save concept both worked and are unchanged by this amendment — only the button's internal content/sizing/tooltip changes.

- **2026-07-16, round-2 review (patch, no further loopback):** Both reviewers independently confirmed the Delete (Trash2) button never received the same `min-h-11 min-w-11` touch-target treatment as Save — the tap-target gap round 1 fixed for Save just relocated to the adjacent button. Also flagged: `aria-label`/`title` were built from two separately-inlined copies of the same template literal (maintenance risk, not a behavior bug). Both auto-applied as patches (extracted a shared `saveLabel` local const reused for both attributes; added matching `min-h-11 min-w-11` + `title` to the Delete button) without a third full review round, since neither touches frozen behavior (aria-label value, disabled/onClick logic) — pure styling/refactor. Two additional findings were raised but explicitly declined by human decision (Ralf, 2026-07-16), both **kept as originally specified**: (1) the reused spinner ring's resting-arc contrast against this button's own background was flagged as a plausible-but-unmeasured legibility risk — kept verbatim per the "reuse existing convention, don't invent a new style" rule; (2) `title` mirroring `aria-label` exactly was flagged as a possible double-announcement risk on some screen-reader/browser combinations — kept as specified, accepted as low real-world impact. The shared-`isPending`-affects-every-row-identically finding was **not** reopened — it is the same tradeoff already accepted and deferred during the Story 8.2 code review (see `deferred-work.md`); this round just made it more visually obvious via the spinner, addended there rather than re-litigated here.

## Design Notes

The Save icon button mirrors the existing pill's background/border treatment (minus the text/horizontal padding), reuses the app's existing `animate-spin` ring convention for the pending state (see `SettingsRoot.tsx:29`), and gets an explicit touch-target floor plus a native tooltip:

```jsx
<button
  type="button"
  onClick={() => handleSaveRoom(room)}
  disabled={!isDirty || isPending || isSaveBlocked}
  aria-label={`${isPending ? t('editor.saving') : t('editor.save')}: ${room.name.trim()}`}
  title={`${isPending ? t('editor.saving') : t('editor.save')}: ${room.name.trim()}`}
  className="min-h-11 min-w-11 flex items-center justify-center rounded-full disabled:opacity-40 shrink-0"
  style={{ background: 'rgba(255,255,255,0.12)', border: '1px solid rgba(255,255,255,0.40)', color: 'white' }}
>
  {isPending ? (
    <div
      className="w-4 h-4 rounded-full border-2 border-white/20 border-t-white/70 animate-spin"
      aria-hidden="true"
    />
  ) : (
    <Check className="h-4 w-4" aria-hidden="true" />
  )}
</button>
```

Row 2 ("N Anschlüsse ›") keeps its current classes verbatim (`flex items-center gap-1 text-xs text-white/50 shrink-0`) — just moved to its own sibling row; `shrink-0` is harmless there even though no longer strictly needed.

## Verification

**Commands:**
- `npx tsc --noEmit` (from `client/`) -- expected: zero errors
- `npx vitest run` (from `client/`) -- expected: all tests pass, zero regressions (372+ passing baseline)
- `npm run lint` (from `client/`) -- expected: zero new errors (pre-existing unrelated `router.tsx` warnings acceptable)

**Manual checks (if no CLI):**
- Resize a local dev build to ~375-430px width (or real iPhone Safari) and confirm the room-list row no longer overflows and the delete icon is reachable without scrolling/swiping.

## Suggested Review Order

**Row restructure (the overflow fix itself)**

- Entry point — the row's outer container goes from a single non-wrapping flex row to a two-row `flex-col` wrapper, letting content wrap instead of overflow.
  [`FlatStructureEditor.tsx:323`](../../client/src/features/flat-structure/components/FlatStructureEditor.tsx#L323)

- Row 2 — the "N Anschlüsse ›" summary button now renders alone on its own line, only outside delete-confirm mode.
  [`FlatStructureEditor.tsx:387`](../../client/src/features/flat-structure/components/FlatStructureEditor.tsx#L387)

**Save button (icon-only, pending feedback, accessibility)**

- Shared label computed once and reused for both `aria-label`/`title`, avoiding the duplicated-string maintenance trap flagged in review.
  [`FlatStructureEditor.tsx:316`](../../client/src/features/flat-structure/components/FlatStructureEditor.tsx#L316)

- The button itself — icon-only, 44×44 touch target, `aria-label`/`disabled`/`onClick` preserved byte-for-byte from before this fix.
  [`FlatStructureEditor.tsx:355`](../../client/src/features/flat-structure/components/FlatStructureEditor.tsx#L355)

- Pending-state spinner swap — reuses the existing `animate-spin` ring convention from `SettingsRoot.tsx`, restoring the visual feedback lost when the text label was dropped.
  [`FlatStructureEditor.tsx:364`](../../client/src/features/flat-structure/components/FlatStructureEditor.tsx#L364)

**Delete button touch-target parity**

- Delete icon button gets the same `min-h-11 min-w-11` + `title` treatment as Save, closing the tap-target mismatch two reviewers independently flagged in round 2.
  [`FlatStructureEditor.tsx:373`](../../client/src/features/flat-structure/components/FlatStructureEditor.tsx#L373)

**Peripherals**

- New icon import.
  [`FlatStructureEditor.tsx:4`](../../client/src/features/flat-structure/components/FlatStructureEditor.tsx#L4)
