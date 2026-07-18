---
baseline_commit: 7b71d85c02899bc6fedf4b3836ed63223836091c
---

# Story 9.3: Align Room-List Save Affordance

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want the room-list view's Save actions — the page-level "Speichern" button and each room's inline Save icon — to look and feel consistent with the Power Point and Device edit save actions,
so that saving works the same way everywhere in Flat Structure settings.

## Acceptance Criteria

1. **Given** `FlatStructureEditor.tsx`'s page-level Save button (`:268-276`) and per-room inline Save icon buttons (`:356-373`), which are functionally correct today — not scroll-broken, confirmed by the investigation (Finding 4: this view's save buttons are structurally separate from `StickyActionBar` and were never meant to be sticky) — but visually distinct from the Power Point/Device-edit save pattern (Epic 8 retro Challenge #1), **when** restyled per Story 9.1's approved D-45 pattern, **then** their visual treatment (styling, placement) matches the unified pattern without changing their existing working behavior — autosave-on-room-add/delete (Story 8.1), per-room save-on-dirty, and page-level batched save all continue to function exactly as before.
2. **Given** this story only changes presentation, not behavior, **when** implemented, **then** no changes are made to `handleSave`, `handleSaveRoom`, `isRoomDirty`, or any other logic in `FlatStructureEditor.tsx` — this is a restyle, not a behavior change; existing tests for save/dirty-state logic continue to pass unmodified.

## Tasks / Subtasks

- [x] Task 0: Verify the premise with a live side-by-side comparison before writing any CSS (AC: 1)
  - [x] **Read this Dev Note section fully before touching code — the "visually distinct" premise in AC1 is broader than what the current code actually shows.** A direct diff of hex/token values, done during story creation, found the room-list Save button's core color treatment *already identical* to `RoomEditor`/`DeviceEditor`'s Save buttons (see "What already matches" below). What differs is **placement/shape**, which D-45 explicitly says must be preserved, not unified. Confirm this yourself by running the app locally (`func start` + `swa start`, per project convention) and opening the room list, a Room Power Point list, and the Device edit screen side by side (or via the `claude-in-chrome` browser automation tool available in this environment, following Story 9.2's self-verification precedent) before deciding what — if anything — needs a code change.
  - [x] Do not assume a large rewrite is needed. If the live comparison confirms colors/accents already read as one system, the remaining work may be narrow (see "Known candidate deltas" below) or, in the extreme, limited to a few pixel-level polish tweaks. Do not invent new shared components, wrap the room list in a `StickyActionBar`-style bar container, or extract shared button styling constants — none of that is asked for by the ACs and D-45 explicitly rules out changing room-list placement.

- [x] Task 1: Reskin the page-level "Speichern" button (`FlatStructureEditor.tsx:268-276`) (AC: 1)
  - [x] Compare against `RoomEditor.tsx`'s Save button (`:110-118`) and `DeviceEditor.tsx`'s Save button (`:312-320`) — see exact current values in Dev Notes. Apply only the deltas the live comparison in Task 0 actually surfaces (e.g., a genuine outlier in radius, font-weight, or accent color) — the background/border/color values are already the same literal `rgba(...)` triples as of story creation, so do not touch them speculatively.
  - [x] Preserve the button's existing `disabled={hasPlugIdConflict || hasEmptyName || hasNoRooms || isPending}` logic and the `{isPending ? t('editor.saving') : t('editor.save')}` text-swap pattern exactly as-is — this text-swap (not a spinner) is already the same pending convention `RoomEditor`/`DeviceEditor` use for their own full-width Save buttons.
  - [x] Preserve placement: top-right of the header row, inline with the back button. Do not move it into a bottom bar — D-45 is explicit that room-list placement stays inline, only the border/background/accent/spinner *treatment* is reskinned.

- [x] Task 2: Reskin the per-room inline Save icon button (`FlatStructureEditor.tsx:356-373`) (AC: 1)
  - [x] Compare against the same two reference buttons. The icon button's spinner (`w-4 h-4 rounded-full border-2 border-white/20 border-t-white/70 animate-spin`, line 366-369) already reuses the exact `border-white/20 border-t-white/70` spinner treatment used elsewhere in the app (`SettingsRoot.tsx:29`) — this is the correct pattern for an icon-only button that has no room for a text swap; do not replace it with a text-swap (there's no text in this button) or remove it.
  - [x] Preserve the existing `disabled={!isDirty || isPending || isSaveBlocked}`, `aria-label={saveLabel}`, and `title={saveLabel}` exactly as-is. **Do not change the accessible name computation** (`saveLabel = \`${isPending ? t('editor.saving') : t('editor.save')}: ${room.name.trim()}\`\`) — the existing test suite queries buttons by this exact `getByRole('button', { name: 'editor.save: <RoomName>' })` pattern (see `FlatStructureEditor.test.tsx`); any change to this string or to how the name is composed will break dozens of existing assertions.
  - [x] Preserve placement: inline per-row, next to the delete icon. Do not collapse per-room saves into a single global bar — this is the specific interaction regression D-45 calls out and rejects.

- [x] Task 3: Confirm out-of-scope UI is left untouched (AC: 1, 2)
  - [x] The room-delete confirmation row (`Cancel`/`Delete` text buttons, `FlatStructureEditor.tsx:337-352`) is **not** a Save affordance and is outside this story's two named targets (page-level Speichern button, per-room Save icon). Even though its `Cancel` button visually differs from `DeviceEditor.tsx`'s bordered-pill Cancel button, do not restyle it here — AC1 names only the Save button and Save icon; touching the delete-confirmation row is scope creep this story's AC2 ("no changes... to any other logic") is designed to prevent, and delete-affordance consistency was never raised in Epic 8 retro Challenge #1 or D-45.
  - [x] The "add room" button (`FlatStructureEditor.tsx:410-422`) uses the app's established *secondary*-button treatment (`rgba(255,255,255,0.10)` bg / `rgba(255,255,255,0.12)` border / `rgba(255,255,255,0.75)` text) — the same secondary pattern `RoomEditor.tsx`'s "add power point" button and `DeviceEditor.tsx`'s "configure profile" button already use verbatim. It is not a Save action and is already consistent; leave it unchanged.

- [x] Task 4: Regression pass (AC: 2)
  - [x] Run `npm run lint` in `client/`.
  - [x] Run the full Vitest suite in `client/`. `FlatStructureEditor.test.tsx` (940 lines, ~40+ assertions) queries exclusively by `getByRole`/`getByText`/`getByLabelText` against translation-key text and accessible names — never by className or inline style — so a pure CSS/token restyle that doesn't touch text content, `aria-label`, or DOM structure is expected to pass unmodified. Confirm this holds; if any test fails, the change touched something other than presentation and needs to be reconsidered against AC2.
  - [x] Do not add new tests for this story — it is a visual-only change with no new behavior to cover, and the existing suite already pins the behavior (save/dirty/autosave logic) this story must not alter.

### Review Findings

- [x] [Review][Defer] Per-room Save icon state is scoped to the whole page's `isPending`, not per-row [`client/src/features/flat-structure/components/FlatStructureEditor.tsx:41,359,365`] — deferred, pre-existing
- [x] [Review][Defer] Per-room save success confirmation isn't co-located with the row that was saved [`client/src/features/flat-structure/components/FlatStructureEditor.tsx:289-292` vs `RoomEditor.tsx:105-108`] — deferred, pre-existing
- [x] [Review][Defer] Blocked per-room Save icon gives no visible reason, unlike RoomEditor's always-shown blocked-reason text [`client/src/features/flat-structure/components/FlatStructureEditor.tsx:315,362` vs `RoomEditor.tsx:98-104`] — deferred, pre-existing
- [x] [Review][Defer] Delete-confirmation row's Cancel button visual inconsistency (acknowledged in Task 3) was never logged to `deferred-work.md` [`client/src/features/flat-structure/components/FlatStructureEditor.tsx:337-352` vs `DeviceEditor.tsx`'s bordered-pill Cancel button] — deferred, pre-existing, now filed

## Dev Notes

### What already matches (confirmed by direct file comparison during story creation — do not re-derive, verify live instead)

| Property | `FlatStructureEditor.tsx` page-level Save (`:268-276`) | `FlatStructureEditor.tsx` per-room Save icon (`:356-373`) | `RoomEditor.tsx` Save (`:110-118`) | `DeviceEditor.tsx` Save (`:312-320`) |
|---|---|---|---|---|
| Background | `rgba(255,255,255,0.12)` | `rgba(255,255,255,0.12)` | `rgba(255,255,255,0.12)` | `rgba(255,255,255,0.12)` |
| Border | `1px solid rgba(255,255,255,0.40)` | `1px solid rgba(255,255,255,0.40)` | `1px solid rgba(255,255,255,0.40)` | `1px solid rgba(255,255,255,0.40)` |
| Text color | white | white | white | white |
| Radius | `rounded-full` | `rounded-full` | `rounded-full` | `rounded-full` |
| Disabled state | `disabled:opacity-40` | `disabled:opacity-40` | `disabled:opacity-40` | `disabled:opacity-40` |
| Success accent | `#60a5fa` (`editor.saveSuccess`, line 290) | n/a (no per-row success text) | `#60a5fa` (line 106) | n/a |
| Error accent | `text-accent-error` token (lines 285, 295, 300, 305) | n/a | `text-accent-error` token (line 95) | n/a |
| Pending indicator | text-swap `editor.saving`/`editor.save` | spinner (`animate-spin`, icon-only button) | text-swap `editor.saving`/`editor.save` | n/a — `DeviceEditor`'s Save has no pending state at all (`{t('device.save')}` is unconditional, line 319); it's a synchronous draft commit to parent state, not an async mutation — the actual persistence/pending state happens later when the room is saved |

These values were already byte-identical before this story — the app's "primary action pill" convention (`bg 0.12` / `border 0.40` / white text / rounded-full) and its success/error accent tokens were shared across these files from the start, independent of Epic 9. **What genuinely differs — and is the one thing D-45 says to preserve, not unify — is placement and shape:** a small top-right text pill (page-level), a `min-h-11 min-w-11` circular icon button (per-room), vs. a full-width `h-12`/`h-14` bottom-bar button (`RoomEditor`/`DeviceEditor`). Do not change placement or convert the per-row icon button into a full-width labeled button — D-45: "collapsing many independent per-room save actions into one global bar would be an interaction regression."

Given this, treat AC1's "visually distinct" framing as the epic author's read of the *whole screen* (per Epic 8 retro Challenge #1, Ralf's actual complaint was two save mechanisms coexisting on one screen reading as unrelated systems, not necessarily a color mismatch) rather than a literal claim that every hex value differs. Your job is to close whatever gap the live comparison in Task 0 actually reveals — which may be small (e.g., a subtle sizing/weight tweak that makes the small pill read more clearly as "the same control family," or nothing beyond confirming parity) — not to redesign these two buttons from scratch.

### D-45 (the approved design decision this story implements)

> **Room list** (`FlatStructureEditor.tsx`'s per-room saves + page-level "Speichern"): placement stays inline/per-row — collapsing many independent per-room save actions into one global bar would be an interaction regression (forces selecting a row before saving it). Reskin only: same border/background treatment, accent colors, and pending-spinner styling as the fixed bar, so it reads as the same system without forcing an ill-fitting placement.
>
> [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/.decision-log.md#D-45]

### Explicitly out of scope (per Story 9.2's Dev Notes, still authoritative)

- **Do not touch `StickyActionBar.tsx`, `RoomEditor.tsx`, or `DeviceEditor.tsx`** — Story 9.2 already fixed their structural (position: fixed) defect and reskinned them to D-45's full-screen-context pattern; they are the *reference* for this story, not a target. Re-reading their current code is fine (and required, for comparison); editing them is not part of this story.
- **Do not touch `PowerPointEditor.tsx`** — not part of the room-list Save affordance.
- Confirmed via story creation: no other component imports the room-list's Save styling; this is a self-contained change to `FlatStructureEditor.tsx` alone.

### Project Structure Notes

- **Single file touched:** `client/src/features/flat-structure/components/FlatStructureEditor.tsx`. No new files, no new i18n keys (pure CSS/className/inline-style restyle — no new user-visible strings), no backend changes, no new dependencies.
- VSA slice isolation unaffected — this is a component-level CSS change within the existing `flat-structure` feature folder.
- `client/src/components/ui/` is untouched — none of the affected elements are shadcn-generated.

### Testing Standards Summary

- No new automated tests. This is a pure presentation change; the existing suite already pins all the behavior (save/dirty/autosave logic, error/success state transitions) this story must not alter.
- `FlatStructureEditor.test.tsx` queries by `getByRole('button', { name: ... })`, `getByText`, and `getByLabelText` against translation-key strings and computed accessible names — it does not assert on `className` or inline `style` anywhere. This means a correct restyle should not require any test changes. If a test breaks, treat it as a signal you changed text content, DOM structure, or the accessible-name computation, not something to patch around.
- Manual visual verification (Task 0) is the actual verification mechanism for the "reads as the same system" requirement — this is a subjective/visual property `jsdom` cannot check, consistent with this project's established pattern for `StickyActionBar`-adjacent work (Epic 8 retro Challenge #5, Story 9.2 Dev Notes).

### Previous Story Intelligence (Story 9.2)

- Story 9.2 fixed `StickyActionBar.tsx`'s structural defect (`position: sticky` → `fixed`) for `RoomEditor.tsx`/`DeviceEditor.tsx` and explicitly scoped the room-list view out: *"Do not touch `PowerPointEditor.tsx` or `FlatStructureEditor.tsx` — the room-list view's Save affordance is a structurally separate mechanism (page-level button + per-row icons, never wrapped in `StickyActionBar`), confirmed unaffected by this defect (investigation Finding 4) and explicitly out of scope — that's Story 9.3."* This story is that follow-up.
- Story 9.2's exact current `RoomEditor`/`DeviceEditor` Save button values (used as this story's comparison targets) are captured verbatim in the "What already matches" table above — re-derived directly from current source, not copied from 9.2's own Dev Notes, since 9.2 already shipped and its file contents are now ground truth.
- Story 9.2 confirmed its own regression pass found `FlatStructureEditor.test.tsx` unmodified and passing (382/382 suite total) — the same test file this story must also leave passing.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-9-unified-save-affordance-fix-and-pre-epic-10-hardening.md#Story 9.3] — original epic AC text (verbatim source for this story's ACs).
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/.decision-log.md#D-45] — the approved design decision this story implements (room-list clause).
- [Source: _bmad-output/implementation-artifacts/epic-8-retro-2026-07-18.md#Challenges & Patterns, item 1] — Ralf's original complaint (Epic 8 retro Challenge #1) that this story and D-45 respond to.
- [Source: _bmad-output/implementation-artifacts/9-2-fix-roomeditor-and-deviceeditor-save-bar-structural.md] — the sibling story that fixed `StickyActionBar`/`RoomEditor`/`DeviceEditor` and explicitly deferred the room-list view to this story.
- [Source: client/src/features/flat-structure/components/FlatStructureEditor.tsx] — file to modify (Tasks 1-3); page-level Save button at `:268-276`, per-room Save icon at `:356-373`, delete-confirmation row (out of scope) at `:337-352`, add-room button (out of scope, already consistent) at `:410-422`.
- [Source: client/src/features/flat-structure/components/RoomEditor.tsx:110-118] — Save button comparison target.
- [Source: client/src/features/flat-structure/components/DeviceEditor.tsx:312-320] — Save button comparison target.
- [Source: client/src/features/flat-structure/components/StickyActionBar.tsx] — reference-only (do not edit); shows the fixed-bar container treatment the room list is explicitly *not* adopting as a wrapper.
- [Source: client/src/features/settings/components/SettingsRoot.tsx:29] — the app's existing icon-only pending-spinner precedent (`border-2 border-white/20 border-t-white/70 animate-spin`), matching the per-room Save icon's own spinner exactly.
- [Source: client/src/features/flat-structure/components/FlatStructureEditor.test.tsx] — existing test suite (940 lines) this story must leave passing unmodified; queries by role/text/label only, never by class/style.
- [Source: _bmad-output/planning-artifacts/epics/requirements-inventory.md#FR-45] — the functional requirement this story continues to satisfy (already met for this view per the investigation's Finding 4 — this story is presentation-only, not a re-fix of FR-45).
- [Source: _bmad-output/project-context.md] — Tailwind v4 conventions, VSA feature-folder rules, no-comments-unless-non-obvious rule, i18n per-namespace convention.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- Local dev stack stood up per Story 9.2's precedent: Azurite (blob emulator, `/tmp/azurite-data-93`) + `func start` (from `api/`, against the real shared dev Azure SQL DB via `local.settings.json`'s `SqlConnectionString`) + `vite` (client, port 5173) + `npx @azure/static-web-apps-cli start` (port 4280, auth emulation).
- `claude-in-chrome` browser extension was not connected in this environment, so live visual verification used a scripted Playwright (Chromium) session instead: logged in via the SWA CLI mock-auth form (`/.auth/login/aad`), completed onboarding with a throwaway account/flat ("Story 9.3 Test Flat"), then navigated to `/settings/structure` (room list), drilled into a room (`RoomEditor`) and a device (`DeviceEditor`) to screenshot all three Save affordances side by side.
- Screenshots confirmed byte-for-byte what the Dev Notes' pre-derived hex table stated: the room-list page-level Save pill and per-room circular Save-icon button already use the identical `rgba(255,255,255,0.12)` background / `rgba(255,255,255,0.40)` border / white text / `rounded-full` / `disabled:opacity-40` treatment as `RoomEditor`'s and `DeviceEditor`'s full-width Save buttons, and the same `#60a5fa` success-accent color and `border-white/20 border-t-white/70 animate-spin` spinner convention used elsewhere. Triggering an actual per-room save (rename + click) confirmed the success banner ("Structure saved.") renders in the same blue accent as `RoomEditor`'s inline success text. No visual delta was found beyond the placement/shape difference D-45 explicitly preserves (small top-right pill + per-row icon vs. one full-width bottom bar).
- Cleaned up the throwaway test flat via the Settings > Account > Delete Flat flow (typed-confirmation) before tearing down the local stack — `DeleteFlat` function completed successfully (~20s, cascade delete over the real dev DB), confirmed by the account redirecting to `/onboarding` afterward (no flat left). Unlike Story 9.2's leftover test flat, no test data remains in the shared dev DB from this story.
- Local dev stack (Azurite, `func start`, SWA CLI, Vite) was torn down after verification; regression pass (lint + Vitest) was run standalone afterward and needed no running backend.

### Completion Notes List

- **No source changes were required.** Task 0's live comparison (screenshots of the room list, `RoomEditor`, and `DeviceEditor` side by side) confirmed the premise already flagged in this story's Dev Notes: `FlatStructureEditor.tsx`'s page-level Save button and per-room Save icon already share the exact same background/border/text-color/radius/disabled-opacity/success-accent/spinner treatment as the D-45 reference buttons in `RoomEditor.tsx` and `DeviceEditor.tsx`. The only differences are placement and shape (small top-right pill + per-row circular icon vs. one persistent full-width bottom bar), which D-45 explicitly requires to be preserved, not unified. This is the "in the extreme, limited to... nothing beyond confirming parity" outcome the Dev Notes anticipated.
- AC1 is satisfied as-is: the visual treatment already matches the unified pattern, and no existing behavior (autosave-on-room-add/delete, per-room save-on-dirty, page-level batched save) was touched.
- AC2 is satisfied trivially: zero lines of `FlatStructureEditor.tsx` (or any other file) were changed, so `handleSave`, `handleSaveRoom`, `isRoomDirty`, and all other logic are untouched by construction.
- Regression pass: `npm run lint` (oxlint) — no new warnings/errors (pre-existing `router.tsx` fast-refresh warnings only, unrelated to this story). Full Vitest suite: 59 test files / 382 tests, all passing — matching the exact 382/382 baseline Story 9.2 recorded, confirming no regression.
- No new tests were added, per the story's explicit instruction — there is no behavior change to cover, and this story made no code change at all.

### File List

No files were modified. Live verification confirmed `client/src/features/flat-structure/components/FlatStructureEditor.tsx` already satisfies AC1's visual-consistency requirement without any change.

## Change Log

- 2026-07-18: Verified (no code change) — live Playwright-driven comparison of the room list's Save affordances against `RoomEditor`/`DeviceEditor` confirmed the D-45 visual pattern was already fully matched; both ACs satisfied without touching `FlatStructureEditor.tsx`. Full regression pass (lint + 382/382 Vitest) green.
