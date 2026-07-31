---
baseline_commit: e3b2dec1b4e1de7add73a36c7ee811433fcf005a
---

# Story 11.9: Accessible Spike-Bar Indicator — Design-Gated

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user relying on assistive technology,
I want to know which days had a consumption spike without relying on bar color alone,
so that the trend chart's spike information isn't invisible to me.

## Acceptance Criteria

1. **UX design gate — this story ships no visual/markup change to spike encoding until Sally's design is approved.** `TrendChart.tsx`'s spike bars are communicated via color alone today: a spike day's `<Cell>` renders `fill: 'var(--color-accent-spike)'` (amber, `#f59e0b`), a normal day renders `fill: 'rgba(255,255,255,0.5)'` (translucent white) — no pattern, icon, or accessible text equivalent exists for spikes (`TrendChart.tsx:113-119`). This is a WCAG 1.4.1 (Use of Color) concern first flagged during Story 3.5's second review round (`deferred-work.md:259`) and never resolved. Before any implementation, invoke the UX designer (Sally — `bmad-agent-ux-designer` / `bmad-ux` skill) with the current-state context in Dev Notes below (including the existing meter-reset hatch-pattern precedent and the `CostGapBadge.tsx` Popover-based precedent) and get a concrete accessible-treatment proposal. Present it to Ralf for approval before writing any implementation code. **Do not invent the visual treatment yourself** — this is an explicit epic requirement (`epic-11-post-epic-10-hardening-and-technical-debt-resolution.md` Story 11.9: "this story cannot proceed until Ralf (with Sally's input) decides the accessible treatment").

2. **Given** three illustrative options are named in the epic text — a pattern/hatch fill reusing the visual language already shipped for the meter-reset indicator (Story 9.8), a small icon/badge on spike bars, or a text-based summary below the chart — and whichever is chosen must also carry a screen-reader-accessible text equivalent (mirroring how Story 9.8's meter-reset indicator did it: a `sr-only` `<span>` listing affected, locale-formatted dates), **when** Sally proposes a design, **then** the proposal explicitly resolves two things that are currently undecided: (a) the exact visual encoding for a spike day that is *not* also a meter-reset day, and (b) the **combined case** — `TrendChart.tsx`'s current fill ternary (`:113-119`) gives `wasMeterReset` priority over `spikeSet.has(...)`, so a day that is both a spike and a meter reset renders only the reset hatch today and never surfaces as a spike at all. The approved design must state whether this combined-day behavior changes (e.g., both indicators visible) or stays exactly as-is (reset takes visual priority, spike-ness omitted for that day) — this cannot be left for the implementer to improvise.

3. **Given** the approved design, **when** implemented in `TrendChart.tsx`, **then** spike days are distinguishable without relying on color alone, with a screen-reader-accessible text equivalent for each spike day, and existing tests (`TrendChart_NoSpikeDays_AllBarsUseNonSpikeFillColor`, `TrendChart_OneSpikeDayMatchingDailyConsumption_ThatBarUsesSpikeFillColor`) are updated to assert the new encoding (not just the untouched color) rather than deleted, plus new regression tests cover both the visual marker and the accessible text equivalent — mirroring the existing `TrendChart_OneMeterResetDay_ThatBarUsesResetHatchFillAndOthersDoNot` / `TrendChart_HasMeterResetDay_RendersAccessibleSummaryTextWithLocaleFormattedDate` test pairs.

4. **Given** `TrendChart.tsx` is a single shared component rendered in both `DashboardPage.tsx` (7-day view) and `InsightsTab.tsx` (30-day view, per decision D-30), **when** implemented, **then** the accessible treatment applies identically in both contexts with no component-specific special-casing and no prop-API change to either caller (unless the approved design explicitly requires a new prop, in which case both call sites are updated).

## Tasks / Subtasks

- [x] Task 1: UX design pass — accessible spike-encoding proposal and approval (AC: 1, 2)
  - [x] Invoke Sally (`bmad-agent-ux-designer` skill, or `bmad-ux` if that's the active planning skill) with: the current `TrendChart.tsx` fill logic (read in full during story creation — see Dev Notes), the existing meter-reset hatch-pattern + `sr-only` summary precedent shipped in Story 9.8 (`TrendChart.tsx:90-101,126-128`), and the alternative `CostGapBadge.tsx` Popover-based accessible-detail precedent (`client/src/features/dashboard/components/CostGapBadge.tsx`) as prior art.
  - [x] Get an explicit proposal for: the spike-day visual encoding, its accessible text equivalent, and the combined spike+meter-reset day behavior (AC2).
  - [x] Present to Ralf; do not proceed to Task 2 until approved. Record the approved design (concrete class names / pattern spec / copy for the accessible text) in Dev Notes before implementing.
  - [x] **Halt and ask Ralf if this gate is reached without a clear answer** — do not default to guessing (e.g. silently reusing the meter-reset hatch's exact markup) to keep moving.

- [x] Task 2: Implement the approved design in `TrendChart.tsx` (AC: 3, 4)
  - [x] Apply the approved visual encoding to spike-day `<Cell>`s. If a pattern/hatch approach is chosen, follow the existing `resetHatchId` pattern: a uniquely-`useId()`-scoped `<pattern>` in `<defs>` (do not hardcode a static `id` — the current `resetHatchId` was itself a fix for a prior unscoped-`id` collision defer, `deferred-work.md`/Story 9.8 review; do not reintroduce that bug for the new pattern).
  - [x] Resolve the combined spike+reset day case exactly as Task 1's approved design specifies — do not leave the current silent `wasMeterReset`-wins ternary behavior in place unless the approved design explicitly confirms that's the desired outcome.
  - [x] Add the accessible text equivalent (e.g. a second `sr-only` `<span>` alongside the existing `resetDates`-derived one, or a merged summary — per the approved design), using the same `Intl.DateTimeFormat`-localized date formatting already used for `resetDates` (`TrendChart.tsx:42-50`) — do not emit raw ISO date strings.
  - [x] Do not modify `DashboardPage.tsx` or `InsightsTab.tsx` unless the approved design requires a new prop on `TrendChart`.

- [x] Task 3: Add i18n keys (AC: 3)
  - [x] Add the new translation key(s) under the existing `"trend"` block in `client/src/locales/en-US/dashboard.json` and `client/src/locales/de-DE/dashboard.json`, following the exact convention of the existing `trend.meterResetSummary` key (interpolated `{{dates}}` placeholder, same tone/register per D-28). No new i18n namespace registration is needed — `dashboard` is already registered in `client/src/lib/i18n.ts`.

- [x] Task 4: Regression pass (AC: 3, 4)
  - [x] Run `npx tsc --noEmit`, `npx vitest run`, and `npm run lint` from `client/` — zero regressions.
  - [x] Manually verify (or explicitly note if unable, per Story 8.4's precedent — do not claim an unverified visual as confirmed) that the new spike indicator renders correctly alongside the existing meter-reset hatch, on both the Dashboard (7-day) and Insights tab (30-day) surfaces. **Unable to verify live in-browser**: `swa` CLI (required for Easy Auth simulation per project-context.md) is not installed locally, and `api/local.settings.json`'s `SqlConnectionString` points to the live Azure SQL instance rather than a local dev DB — spinning up the full stack against production data was out of scope for this check. Verification basis instead: (1) the approved design was pixel-faithfully mocked up (exact dark-glass tokens, gradient, card radius/blur from `index.css`/`EuroBurnGradient.tsx`) and visually reviewed/approved by Ralf before implementation; (2) `TrendChart.test.tsx` asserts the exact SVG fill/pattern structure (combined-day pattern distinct from reset-only pattern, correct line counts per pattern, correct `fill` per day type) rather than just DOM presence, which is a stronger guarantee for this kind of encoding than a single manual screenshot would be.

- [x] Task 5: Close out the tracked deferred-work entry (AC: n/a — hygiene)
  - [x] Mark the `deferred-work.md:259` entry ("Spike days are communicated via bar color alone with no secondary indicator or accessible text equivalent for the underlying values — WCAG 1.4.1 concern, needs UX/accessibility design input.") as closed, following this file's established strikethrough + "Closed by Story X.Y (date)" convention (see `deferred-work.md:325` for the exact format).

### Review Findings

- [x] [Review][Patch] Hardcoded bar indices in combined-pattern test are fragile [client/src/features/dashboard/components/TrendChart.test.tsx:112-113]
- [x] [Review][Patch] Non-discriminating `patterns.toHaveLength(2)` assertion [client/src/features/dashboard/components/TrendChart.test.tsx:110]
- [x] [Review][Patch] Stale header comment contradicts actual `last_updated` field in sprint-status.yaml [_bmad-output/implementation-artifacts/sprint-status.yaml:2]
- [x] [Review][Defer] sr-only spike/reset summaries lack `aria-live` wiring [client/src/features/dashboard/components/TrendChart.tsx:167-172] — deferred, pre-existing
- [x] [Review][Defer] Locale-formatted date lists use a hardcoded comma separator [client/src/features/dashboard/components/TrendChart.tsx:52-60] — deferred, pre-existing
- [x] [Review][Defer] Duplicate chart-data dates would produce duplicate entries in accessible summaries [client/src/features/dashboard/components/TrendChart.tsx:52-60] — deferred, pre-existing
- [x] [Review][Defer] Accessible summary text has no length cap for wide day windows [client/src/features/dashboard/components/TrendChart.tsx:52-60,167-172] — deferred, pre-existing
- [x] [Review][Defer] Combined-day detection depends on exact string match between spikeDays and chartData dates [client/src/features/dashboard/components/TrendChart.tsx:150-157] — deferred, pre-existing

## Dev Notes

### Why this story starts with a design gate, not code

Unlike most stories in this epic, the epic text explicitly withholds the visual spec: "**This story cannot proceed until Ralf (with Sally's input) decides the accessible treatment** — options include a pattern/hatch fill..., a small icon/badge..., or a text-based summary below the chart" (`epic-11-post-epic-10-hardening-and-technical-debt-resolution.md`, Story 11.9 Note). This story file therefore does not prescribe the exact markup — Task 1 is a hard gate, matching the established pattern from Stories 8.4/9.1/9.6. Skipping it and silently copying the meter-reset hatch pattern verbatim would satisfy AC3's letter but violate AC1's explicit gate and the epic's stated intent — Sally/Ralf may choose the hatch approach, but that choice must be made deliberately (and must also resolve the combined-day question, which the meter-reset precedent alone doesn't answer).

**Contrast with Story 9.8:** that story added the meter-reset indicator *without* a formal Sally/Ralf design-decision doc — its own Dev Notes state "No design-decision doc exists for the exact visual treatment of this story... Task 4 proposes one concrete, low-complexity implementation... If Ralf wants a different visual, treat as a starting point to adjust." Story 11.9's epic text is written more strongly ("cannot proceed until... decides") — treat this as a genuine, harder gate than 9.8's, not as license to repeat 9.8's looser pattern.

### Approved design (design gate closed 2026-07-31 — Sally + Ralf)

Sally proposed a hatch-pattern-only-for-the-ambiguous-case design (visual mockup reviewed by Ralf as a rendered artifact before approval); Ralf simplified the spike-only treatment during review. Final approved design:

- **Spike-only day** (spike, not also a meter-reset day): **no change** — stays `fill: 'var(--color-accent-spike)'` (solid amber), exactly as it renders today. No new pattern for this case (an earlier mirrored-diagonal "spikeHatch" proposal was explicitly rejected — the pattern was hard to distinguish from the reset hatch at 30-day/Insights-tab bar density).
- **Meter-reset-only day**: **no change** — stays the existing Story 9.8 `resetHatchId` slate hatch.
- **Combined day** (both a spike AND a meter-reset): **behavior changes**. Today this silently renders only the reset hatch and the spike-ness is invisible. New: a dedicated `combinedHatchId` pattern — `useId()`-scoped like `resetHatchId` (no static id), 4×4 `<pattern>`, `rect fill="var(--color-accent-spike)"` (amber, not slate — ties it visually to "spike" while staying distinct from the plain solid amber spike-only bar), with **two** diagonal `<line>` strokes (`rgba(255,255,255,0.4)`, `stroke-width: 1.5`) — one at `rotate(45deg)`, one at `rotate(-45deg)` — producing a crosshatch, visually distinct from both the plain spike-only bar (no pattern) and the reset-only bar (single-diagonal slate hatch). This is the **only new pattern** introduced by this story.
- **Accessible text equivalent**: a new `sr-only` `<span>`, sibling to the existing meter-reset one, listing spike dates via the same `Intl.DateTimeFormat` locale formatting already used for `resetDates` (day `2-digit`/month `2-digit`/year `numeric`, `timeZone: 'UTC'`). New i18n key `trend.spikeSummary` (interpolated `{{dates}}` placeholder), added to both `en-US` and `de-DE` `dashboard.json`, following the exact convention of `trend.meterResetSummary`. A day that is both spike and reset appears in **both** the new spike summary and the existing reset summary — no merged/special-cased combined-day copy needed; this is deliberate and covers the combined case for screen readers without extra logic.
- **Explicitly accepted trade-off**: plain amber-only for spike-only days still technically relies on color for a colorblind *sighted* user (WCAG 1.4.1's letter), even though the new `sr-only` text closes the gap for screen-reader users. Ralf reviewed this trade-off explicitly (an optional small non-color marker was offered and declined) and confirmed plain color + the accessible text equivalent is the accepted final treatment for spike-only days in this story — this is a deliberate scope decision by Ralf, not an oversight.
- **New fill ternary** (`TrendChart.tsx:113-119` today):
  ```tsx
  fill={
    entry.wasMeterReset && spikeSet.has(entry.date)
      ? `url(#${combinedHatchId})`
      : entry.wasMeterReset
        ? `url(#${resetHatchId})`
        : spikeSet.has(entry.date)
          ? 'var(--color-accent-spike)'
          : 'rgba(255,255,255,0.5)'
  }
  ```

### Current `TrendChart.tsx` state (read in full during story creation)

- Spike detection: `spikeSet = new Set(dashboard?.spikeDays ?? [])` (`:22`), computed from the `DashboardSummary.spikeDays` array (backend-computed via `KpiCalculator.DetectSpikes`, unrelated to this story — no backend changes here).
- Fill ternary (`:113-119`, the exact code this story changes):
  ```tsx
  fill={
    entry.wasMeterReset
      ? `url(#${resetHatchId})`
      : spikeSet.has(entry.date)
        ? 'var(--color-accent-spike)'
        : 'rgba(255,255,255,0.5)'
  }
  ```
  Meter-reset (`wasMeterReset`) is checked first and wins — a day that is both a meter reset and a spike currently shows only the reset hatch, never the spike color. This is the exact ambiguity AC2 requires the design to resolve.
- Existing meter-reset accessible-indicator precedent (Story 9.8), to offer Sally as prior art:
  - `resetHatchId = \`meterResetHatch-${useId()}\`` (`:20`) — uniquely scoped per component instance (a prior unscoped-`id` collision was flagged and is now fixed this way; do not regress to a static id for the new spike pattern).
  - SVG `<pattern>` in `<defs>` (`:90-101`): 4×4 diagonal stripe, `rotate(45)`, background `var(--color-accent-reset)` (`#94a3b8`, slate) with a `rgba(255,255,255,0.4)` diagonal line.
  - `resetDates` memo (`:42-50`): filters `chartData` for `wasMeterReset`, formats each date via a locale-aware `Intl.DateTimeFormat` (`day/month/year: '2-digit'/'2-digit'/'numeric'`, `timeZone: 'UTC'`).
  - `sr-only` summary span (`:126-128`): `{resetDates.length > 0 && <span className="sr-only">{t('trend.meterResetSummary', { dates: resetDates.join(', ') })}</span>}`.
  - `minPointSize={3}` on `<Bar>` (`:109`) — added in Story 9.8's own review pass because a 0-kWh reset bar would otherwise render at zero height, hiding the hatch. If the approved spike design also needs to render on a very low (but non-zero) kWh day, this existing prop already covers minimum visible height — no change needed there.
- CSS accent variables (`client/src/index.css:18,24`): `--color-accent-spike: #f59e0b` (amber), `--color-accent-reset: #94a3b8` (slate) — already visually distinct from each other; any new pattern for spikes should keep using `--color-accent-spike` as its base color so spike and reset stay distinguishable from one another, not just from normal bars.
- Alternative precedent to offer Sally: `client/src/features/dashboard/components/CostGapBadge.tsx` — a `Popover`-based icon+text accessible-detail pattern (`⚠` icon button, `aria-hidden` on the glyph, translated label, `PopoverContent` with explanatory text). This is a heavier interaction pattern (click-to-reveal) than the meter-reset's passive hatch+sr-only approach — a real design-space alternative Sally may weigh, not a rhetorical mention.
- Both call sites (`DashboardPage.tsx:48`, `InsightsTab.tsx:47`) pass only `dashboard`/`flatId`/`days`/`headerExtra` — no spike-specific props exist today; this story only needs to add one if the approved design requires it (e.g., a threshold value for a tooltip), which is unlikely given `spikeDays` already arrives fully computed from the backend.

### Origin of this WCAG concern

First flagged during Story 3.5's second code-review round (`3-5-trend-chart-and-spike-detection.md:107`, `deferred-work.md:259`) and traced back to design decision **D-31** (`ux-designs/ux-energy-tracker-2026-06-20/.decision-log.md:98-99`: "Spike detection — amber bar only, no separate alert"), which pre-dates this project's later accessibility-floor requirement (`UX-DR11`, WCAG 2.2 AA). This story resolves the tension between D-31 (as originally written) and UX-DR11 — Sally's proposal in Task 1 should be understood as superseding/refining D-31's "amber bar only" framing for the accessibility dimension specifically, not its underlying decision to keep spike encoding lightweight (no separate banner/notification, full detail lives in Insights tab) — that part of D-31 is not in question here.

### Testing standards summary

- Frontend: Vitest + `@testing-library/react`, `globals: true` (no `describe`/`it`/`expect` imports), colocated `TrendChart.test.tsx`. `react-i18next` is mocked to return raw keys with interpolation options appended (`vi.mock` at the top of the test file) — assert against the key string plus JSON-stringified options (see existing `TrendChart_HasMeterResetDay_RendersAccessibleSummaryTextWithLocaleFormattedDate` for the exact technique), not translated prose.
- Query bars via `container.querySelectorAll('.recharts-bar-rectangle path')` and assert on the `fill` attribute — the same technique the existing spike-color and reset-hatch tests already use. If the approved design adds a pattern, extract its `id` via `container.querySelector('pattern')` the same way `TrendChart_OneMeterResetDay_...` does — do not hardcode an expected id string, since it's `useId()`-generated and non-deterministic across renders.
- Query by role/label/text for any new interactive element (e.g. if the approved design is icon/Popover-based); do not add snapshot tests for Tailwind class strings.

### Project Structure Notes

- Modify: `client/src/features/dashboard/components/TrendChart.tsx`, `client/src/features/dashboard/components/TrendChart.test.tsx`, `client/src/locales/en-US/dashboard.json`, `client/src/locales/de-DE/dashboard.json`.
- No backend changes — `spikeDays`/`wasMeterReset` are already computed and delivered by the existing `DashboardSummary` API contract; this is a pure frontend rendering/accessibility story.
- No new dependencies — the meter-reset precedent proves SVG `<pattern>` fills work with the existing `recharts` version with no added library; a Popover-based alternative would reuse the already-present `@/components/ui/popover` (shadcn), also no new dependency either way.
- Do not modify `DashboardPage.tsx` or `InsightsTab.tsx` unless Task 1's approved design explicitly requires a new prop.

### Previous story intelligence (Story 11.8)

Story 11.8 touched an unrelated feature slice (`flat-structure/`, per-row save state) — no shared surface area with `TrendChart.tsx`. Its main transferable lesson: when review feedback reveals a test asserting a *count* should use exact `toHaveLength(n)` rather than a loose `.length > 0`/`toBeGreaterThan` check — apply the same rigor to any new spike-count assertions this story adds (e.g. asserting exactly N spike-styled bars, not just "at least one"). `deferred-work.md` was checked for a `blocks: Story 11.9` tag — none found; the only related open item is the WCAG entry this story itself resolves (`:259`, see Task 5).

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-11-post-epic-10-hardening-and-technical-debt-resolution.md#Story 11.9] — original epic AC text and the "cannot proceed until Ralf/Sally decide" design-gate framing this story's AC1/Task 1 carry forward as a hard gate.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md:259] — the original WCAG 1.4.1 finding this story resolves; also the item Task 5 closes out.
- [Source: _bmad-output/implementation-artifacts/3-5-trend-chart-and-spike-detection.md:107] — the original code-review finding that first surfaced this concern.
- [Source: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/.decision-log.md#D-31] — "amber bar only, no separate alert" decision this story refines for the accessibility dimension.
- [Source: _bmad-output/planning-artifacts/epics/requirements-inventory.md#UX-DR11] — accessibility floor (WCAG 2.2 AA) this story brings spike encoding into compliance with.
- [Source: _bmad-output/implementation-artifacts/9-8-meter-reset-handling-in-kpi-calculations.md] — precedent for an accessible bar-chart indicator (hatch pattern + `sr-only` summary) in this exact component; also documents the unscoped-pattern-`id` bug this story must not reintroduce.
- [Source: _bmad-output/implementation-artifacts/8-4-responsive-device-card-grid-room-card-layout-on-tablet-and-desktop.md] — precedent for this project's UX-design-gate story format (Task 1 as a hard gate, halt-and-ask if unresolved).
- [Source: client/src/features/dashboard/components/TrendChart.tsx] — full current implementation read during story creation; fill ternary (`:113-119`) and meter-reset precedent (`:20,42-50,90-101,126-128`) are the exact code this story extends.
- [Source: client/src/features/dashboard/components/TrendChart.test.tsx] — existing spike-color and reset-hatch test pairs this story's new tests mirror.
- [Source: client/src/features/dashboard/components/CostGapBadge.tsx] — alternative Popover-based accessible-indicator precedent, offered to Sally as prior art.
- [Source: client/src/features/dashboard/DashboardPage.tsx:48] and [Source: client/src/features/insights/components/InsightsTab.tsx:47] — the two call sites `TrendChart` is shared across (7-day and 30-day views, per D-30); both must render the new treatment identically per AC4.
- [Source: client/src/index.css:18,24] — `--color-accent-spike`/`--color-accent-reset` values, confirmed already visually distinct from each other.
- [Source: client/src/locales/en-US/dashboard.json] and [Source: client/src/locales/de-DE/dashboard.json] — existing `trend.meterResetSummary` key convention Task 3's new key(s) follow.
- [Source: _bmad-output/implementation-artifacts/11-8-room-list-per-row-save-state-consistency.md] — previous story in this epic; confirmed no shared surface area, and the "use exact-count assertions, not loose ones" testing lesson.
- [Source: _bmad-output/project-context.md] — Vitest/testing-library conventions, i18n namespace rules, Tailwind v4 conventions applied above.

## Change Log

- 2026-07-31: Design gate closed (Sally + Ralf) — combined spike+meter-reset days get a new crosshatch pattern; spike-only days keep today's plain amber fill (deliberate, Ralf-accepted trade-off); new `sr-only` spike text equivalent added. Implemented in `TrendChart.tsx`, i18n keys added, `deferred-work.md:259` closed. Status → review.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

None — implementation proceeded without needing scratch debug logging; `npx tsc --noEmit`, `npx vitest run` (full suite), and `npm run lint` all passed clean on first post-implementation run.

### Completion Notes List

- Task 1 (design gate): Invoked Sally (`bmad-agent-ux-designer`) with the story's Dev Notes context. Sally's initial proposal used a mirrored-diagonal hatch pattern for spike-only days plus a crosshatch for the combined spike+meter-reset case; built and published an interactive HTML mockup (recreated at true scale from the app's actual dark-glass tokens/gradient) for Ralf to review visually before approving. Ralf simplified the proposal: spike-only days keep today's plain solid-amber fill (no new pattern — the mirrored hatch was hard to distinguish from the reset hatch at 30-day/Insights density); the crosshatch pattern is reserved solely for the combined-day case. Ralf was also given an explicit WCAG 1.4.1 trade-off flag (plain color for spike-only still doesn't fully cover colorblind sighted users even with the new `sr-only` text) and explicitly accepted it as a deliberate scope decision rather than an oversight. Approved design recorded in Dev Notes → "Approved design" subsection before any implementation code was written.
- Task 2: Implemented per the approved design. Added a `combinedHatchId` (`useId()`-scoped, matching the `resetHatchId` convention) crosshatch `<pattern>` used only when a day is both `wasMeterReset` and in `spikeSet`; spike-only fill (`var(--color-accent-spike)`) and reset-only fill (`url(#resetHatchId)`) are unchanged from pre-story behavior. Added a `spikeDates` memo (mirrors the existing `resetDates` memo exactly — same `Intl.DateTimeFormat` locale formatting) and a second `sr-only` `<span>` (`trend.spikeSummary`) rendered alongside the existing meter-reset one. A combined day naturally appears in both summaries with no special-cased merge logic. TDD: wrote 4 new/updated test cases first (confirmed 3 failing before implementation, 1 pre-existing test unaffected since spike-only fill didn't change), then implemented until green.
- Task 3: Added `trend.spikeSummary` key to both `en-US` and `de-DE` `dashboard.json`, following `trend.meterResetSummary`'s exact interpolation/tone convention (D-28 register). Both files validated as parseable JSON.
- Task 4: `npx tsc --noEmit` — clean. `npx vitest run` (full suite) — 469/469 passed, 0 regressions. `npm run lint` — clean except 7 pre-existing `react(only-export-components)` warnings in `router.tsx`, unrelated to this story. Manual in-browser verification was not possible in this environment (`swa` CLI not installed locally; `api/local.settings.json`'s `SqlConnectionString` points to the live Azure SQL instance, not a local dev DB) — noted explicitly per Story 8.4's precedent rather than claimed. Verification instead rests on the Ralf-approved pixel-faithful mockup and on tests that assert the actual SVG fill/pattern structure per day-state (not just DOM presence).
- Task 5: Closed the `deferred-work.md:259` WCAG 1.4.1 spike-encoding entry using the file's established strikethrough + "Closed by Story X.Y (date)" convention, noting the accepted spike-only color-alone trade-off explicitly so it isn't misread as a full WCAG 1.4.1 close.

### File List

- `client/src/features/dashboard/components/TrendChart.tsx` (modified)
- `client/src/features/dashboard/components/TrendChart.test.tsx` (modified)
- `client/src/locales/en-US/dashboard.json` (modified)
- `client/src/locales/de-DE/dashboard.json` (modified)
- `_bmad-output/implementation-artifacts/deferred-work.md` (modified)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified)
