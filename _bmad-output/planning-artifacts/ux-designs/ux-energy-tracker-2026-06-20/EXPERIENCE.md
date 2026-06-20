---
name: energy-tracker
project: energy-tracker
status: final
created: 2026-06-20
updated: 2026-06-20
sources:
  - _bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md
  - .decision-log.md
---

# EXPERIENCE.md — energy-tracker behavioral spine

Visual identity lives in DESIGN.md. Cross-references use `{colors.token-name}` and `{components.component-name}` syntax. Visual specs (colors, fonts, radii, shadows) are never duplicated here.

---

## Foundation

**Form factor:** Responsive web, mobile-first. Phone (375px) is the primary surface — meter reading almost always happens on a phone while standing at a basement meter. Tablet (768px+) and desktop (1024px+) are supported as responsive layouts.

**UI system:** shadcn/ui on a responsive web stack. DESIGN.md is the visual identity reference. shadcn defaults are inherited for everything not in the brand layer.

**Authentication:** OIDC gate on all routes. Sessions persist across browser restarts. Unauthenticated deep links redirect to the OIDC login flow and return to the originally requested route after successful authentication.

---

## Information Architecture

| Surface | Reached from | Purpose | Release |
| --- | --- | --- | --- |
| Dashboard | App root; bottom tab bar | KPI tiles (daily/weekly kWh + €, monthly projection) plus Enter Reading CTA; ambient Euro Burn gradient background encodes consumption state | R1 |
| Enter Reading (bottom sheet) | Dashboard "Enter Reading" button; tablet header icon | Submit a Meter Reading: kWh value + date/time | R1 |
| Reading History (bottom sheet) | Clock icon in trend chart card header (Dashboard or Insights) | Chronological list of Meter Readings; tap entry to edit | R1 |
| Insights | Bottom tab bar | 30-day trend chart (with spike detection), Generated Insights cards (standby offenders, replacement candidates, budget pressure, invoice deviation) | R1 (trend + spike); R2 (Generated Insights) |
| Decomposition | Bottom tab bar | Consumption breakdown by Room and Device; Residual always shown; period selector | R2 |
| Import | Decomposition header icon; Decomposition empty state CTA | Multi-file smart plug upload surface | R2 |
| Flat Structure | Settings → Flat card → Flat Structure quick link; Decomposition empty state prompt | Four-level hierarchy editor: Flat → Rooms → Power Points → Devices | R2 |
| Settings (root) | Bottom tab bar | Flat cards with quick links; Locale section; Account section | R1 |
| Settings — Flat config | Settings → Flat card | Full flat configuration; tariff history; kWh baseline | R1 |
| Settings — Tariff | Settings → Flat card → Tariff quick link | Tariff entry list; add/edit tariff with contract period | R1 |
| Settings — kWh Baseline | Settings → Flat card → kWh Baseline quick link | Annual kWh Baseline entry with presets or custom value | R1 |
| Settings — Flat Structure | Settings → Flat card → Flat Structure quick link | Same as Flat Structure surface above | R2 |
| Settings — Locale | Settings → Language & Region | Language and region selection; affects all number, date, time, and currency formatting | R1 |
| Settings — Account | Settings → Account section | Sign out; Flat deletion (type-to-confirm) | R1 |
| Onboarding (gate) | First use only, before any main feature | Intro screen → Step 1: Name your Flat → Step 2: Energy contract (kWh baseline + Tariff) | R1 |

**Bottom tab bar:** Dashboard · Insights · Decomposition *(Release 2)* · Settings

**Flat switcher:** Tap the active Flat name in the header from any surface. A dropdown reveals all Flats plus an "Add flat" option. *(Release 2 for multi-Flat; header present in R1 with single Flat name displayed)*

→ Mockup reference: euro-burn-dashboard.html (Dashboard), enter-reading-sheet.html (Enter Reading sheet), insights-tab.html (Insights), decomposition-tab.html (Decomposition), import-surface.html (Import), flat-structure-editor.html (Flat Structure), settings-screens.html (Settings + Onboarding), onboarding-flow.html (Onboarding gate)

---

## Voice and Tone

**Register:** Precise, calm instrument. The app reports facts; it does not coach, encourage, or celebrate. Every string is a measurement or a label — never a motivational prompt.

No exclamation marks anywhere. No streak language. No "Great job!" No "You're doing amazing." If the user is over budget, the UI states that as a number, not a judgment.

**Microcopy — Do / Don't:**

| Context | Do | Don't |
| --- | --- | --- |
| Under budget delta | ↓ 0.8 kWh under budget | You're doing great! 0.8 kWh below target! |
| Over budget delta | ↑ 9.3 kWh over budget | Watch out — you've exceeded your daily budget |
| At budget | — at daily budget | Right on track! |
| Save error | Couldn't save — try again. | Oops! Something went wrong |
| Contract locked | Locked — contract active until Dec 2026 | This field is protected |
| Flat deletion | Type "Wohnung 3B" to delete | Are you sure? This cannot be undone. |
| No smart plug data | No smart plug data for this period. | Decomposition data is unavailable |
| Interpolated data notice | Some values in this period are interpolated. | Data may not be fully accurate |
| Below-last-reading warning | Lower than your last reading (42,187 kWh) — is this correct? | Invalid entry |
| Import gap notification | Gap detected: 3–7 Jun. Missing days have been interpolated. | Data imported with errors |
| Onboarding value prop | Know what your energy costs, every day. | Track your energy and save money! |
| Reading history entry hint | Date and time will be saved with your reading. | Don't forget to set the date! |
| Empty Decomposition | No data for this period. Import smart plug data to see a breakdown. | Nothing here yet — get started! |
| Processing | Processing import… | Hang tight, this may take a moment! |

**Key pattern rules:**
- Numeric deltas always show the arrow symbol first (↓ / ↑), then quantity, then unit, then label.
- Error messages always include an action ("try again", "retry later") when a retry is possible.
- Lock state is a factual label with the contract end date, not an explanation.
- Deletion confirmation copies the resource name verbatim from the UI (no paraphrase).

---

## Component Patterns

Behavioral rules only. Visual specifications (color, typography, radius, shadow, motion curve) live in DESIGN.md.

| Component | Use | Behavioral rules |
| --- | --- | --- |
| KPI tile | Dashboard: daily avg kWh, weekly avg kWh, daily cost, weekly cost, monthly projection | Pairs kWh headline with € subline in one card. Daily tile adds budget delta as tertiary text. On post-reading save: pulses with count animation (number counts up/down to new value). Reduce Motion: skip pulse, show updated values immediately. |
| Enter Reading button | Dashboard CTA (full-label "Enter Reading"); tablet content header (lightning bolt icon only) | Single tap opens the Enter Reading bottom sheet. Label only — no secondary hint text. |
| Enter Reading bottom sheet | Triggered from Dashboard CTA and tablet header icon | Two fields: (1) numeric kWh, required, auto-focused, numeric keyboard on open; (2) date + time, pre-filled with now, editable. Hint text: "Date and time will be saved with your reading." On save success: sheet closes. On save failure: sheet stays open, value preserved, error toast appears near Save button. |
| Trend chart | Dashboard sparkline; Insights 30-day chart | Bars encode daily consumption. Spike day: amber bar (distinctly styled). Non-spike day: standard bar. Reading history icon in card header triggers Reading History bottom sheet. Period dropdown on Insights chart. |
| Reading history icon | Trend chart card header (Dashboard + Insights) | Small clock/list icon. Tap opens Reading History bottom sheet. |
| Bottom sheet | Enter Reading; Reading History; any sheet-pattern surface | Slides up from bottom. Drag handle at top for dismissal. Blocks background interaction while open. |
| Euro Burn Gradient Background | Dashboard full-bleed background | Encodes current-day kWh consumption state. See Euro Burn Gradient System section. Shifts continuously — no discrete mode switch. |
| Import upload zone | Import surface | File picker on all platforms. Drag-and-drop additionally available on desktop/tablet where platform supports it. Shows selected files list below zone with auto-detected type and device association dropdown per file. |
| Decomposition room card | Decomposition tab | Grouped by Room. Device cards within a Room vary in size by data richness: smart plug devices get a larger card with trend/usage detail; EU label or self-measured devices get a compact card showing the estimated value. |
| Decomposition device card — rich (single smart plug) | Decomposition tab, smart plug devices | Large card. "Measured" badge. Sparkline or usage detail. Direct attribution — the import file is the device's own measured kWh. |
| Decomposition Card — Smart Power Strip Sub-device Rows | Decomposition tab, smart power strip | Large card. "Smart strip" badge (measured). Card header shows the strip's measured total — authoritative. Inner sub-section lists per-device rows: configured devices (EU label or self-measured values present) at full opacity with proportional estimated share; unconfigured devices (no estimates configured) at `opacity: 0.45` with equal remainder share and inline hint "Configure device profile for a more accurate split." All sub-device rows are labelled estimated; the strip header total alone is measured. |
| Decomposition device card — compact | Decomposition tab, estimated devices (EU label or self-measured, no plug) | Small compact card. "Estimated" badge. Shows estimated kWh value only. |
| Residual card | Top of Decomposition view | Always rendered first, above all Room cards. Shows unattributed kWh and % of total. Never suppressed, including when Residual is zero. |
| Progress card | Decomposition tab | Appears after import upload is initiated. Persists until background processing completes. User is not blocked — app remains fully navigable. |
| Flat switcher dropdown | Header, all surfaces | Tapping the active Flat name opens a dropdown listing all Flats plus an "Add flat" action. *(R2)* |
| Onboarding step indicator | Onboarding flow | Shows step position (Intro / Step 1 / Step 2). Not shown after onboarding completes. |
| Tariff lock indicator | Tariff edit form | Lock icon inline with locked price fields. Read-only state renders as greyed out. Non-price fields (provider name, contract dates) remain editable. |
| Flat deletion confirm | Account settings | Text input requiring exact Flat name entry. "Delete" action enabled only when input matches. |
| Choice step | Device energy approach selection | Two options presented: EU energy label or self-measured. Selecting one reveals only that approach's fields. The other remains hidden. |
| Toggle | Self-measured device input | Daily / Weekly period selection. "Daily" pre-selected. Controls which kWh input label appears. |

→ Mockup reference: euro-burn-dashboard.html (KPI tiles, Enter Reading button, gradient background), enter-reading-sheet.html (bottom sheet fields and states), gradient-states.html (gradient encoding across consumption range), insights-tab.html (trend chart, period selector, spike bar), decomposition-tab.html (room cards, device cards, Residual card, progress card), import-surface.html (upload zone, file list, device association), settings-screens.html (tariff lock, flat deletion, choice step, toggle)

---

## State Patterns

| State | Surface | Treatment |
| --- | --- | --- |
| **Dashboard — cold open (no readings)** | Dashboard | KPI tiles show empty state (dashes or zero with no delta). Enter Reading CTA is prominent. Euro Burn gradient at neutral midpoint. "Last read:" shows never. |
| **Dashboard — post-reading-submit** | Dashboard | Sheet closes. KPI tiles pulse with count animation as values update to reflect new Reading. "Last read:" timestamp updates to new Reading's date/time. Three ambient signals; no success toast. Reduce Motion: skip pulse, show updated values immediately. |
| **Dashboard — spike day** | Dashboard | Trend sparkline contains an amber bar for the spike day. No banner or badge generated. Full spike context accessible from Insights tab. |
| **Dashboard — under budget** | Dashboard | Euro Burn gradient at cool end. Daily tile tertiary text: "↓ X kWh under budget". |
| **Dashboard — at budget** | Dashboard | Euro Burn gradient at warm neutral midpoint. Daily tile tertiary text: "— at daily budget". |
| **Dashboard — over budget** | Dashboard | Euro Burn gradient at warm/amber end. Daily tile tertiary text: "↑ X kWh over budget". |
| **Enter Reading — default (sheet open, empty)** | Enter Reading sheet | kWh field empty, auto-focused, numeric keyboard displayed. Date/time pre-filled with current timestamp. Save button inactive until kWh value entered. |
| **Enter Reading — valid value entered** | Enter Reading sheet | Save button active. No validation feedback shown for a valid above-last-reading value. |
| **Enter Reading — below-last-reading warning** | Enter Reading sheet | Inline warning below kWh field: "Lower than your last reading (X kWh) — is this correct?" Save button remains available — user can proceed (handles meter replacement or corrections). |
| **Enter Reading — save failed** | Enter Reading sheet | Sheet stays open. User's typed value is preserved. Error toast appears near Save button: "Couldn't save — try again." with Retry action. User retries without re-entering the value. |
| **Reading History — default** | Reading History bottom sheet | Chronological list of all Meter Readings for the active Flat: date/time + kWh value. Entries that were edited show a "corrected" note. |
| **Reading History — edit** | Reading History bottom sheet → edit form | Tap any entry to open edit form pre-populated with its values. Saving records "Original value was X kWh" as a note on the entry. |
| **Insights — data available** | Insights tab | 30-day trend chart at top. Spike bars amber. Generated Insights cards below in scrollable list (R2). |
| **Insights — Generated Insights processing** | Insights tab | Progress indicator visible during discovery run. Prior Insights cards remain visible below the indicator. |
| **Decomposition — data available** | Decomposition tab | Period selector at top. Residual card first. Room cards with device cards below. Interpolated data periods show info banner: "Some values in this period are interpolated." |
| **Decomposition — no data (empty)** | Decomposition tab | "No smart plug data for this period." message with Import CTA button. No zeros or partial figures shown. "Set up your flat" prompt if Flat Structure is not configured. |
| **Decomposition — interpolated data** | Decomposition tab | Info banner at top of content area (below period selector): "Some values in this period are interpolated." Data shown normally below banner. |
| **Decomposition — processing (post-import)** | Decomposition tab | Progress card persists at top of content until background processing completes. Existing data (if any) visible below. App remains navigable. |
| **Strip — partially configured** | Decomposition card | Configured sub-devices show proportional estimated split at full opacity. Unconfigured sub-devices show equal remainder split at reduced opacity with configure hint. Strip measured total unchanged. |
| **Strip — no devices configured** | Decomposition card | Strip card shows measured total only, no sub-device rows. Inner area shows: "Configure device profiles for a breakdown." No sub-device data shown. |
| **Import — empty** | Import surface | Upload zone with file picker. Drag-and-drop hint on desktop/tablet. No file list shown. |
| **Import — files selected** | Import surface | File list appears below upload zone. Each file shows: filename, auto-detected type (Eve Home / Meross), device association dropdown. If filename contains device name (case-insensitive), device is auto-pre-selected. "Upload Files" button active only when all files have a device association. |
| **Import — processing** | Import surface + Decomposition tab | Upload initiated. Progress card appears on Decomposition tab. Import surface can be dismissed. User not blocked. |
| **Import — gap detected** | Import surface or notification | Gap notification: "Gap detected: [date range]. Missing days have been interpolated." Shown as categorized message; does not block further import actions. |
| **Onboarding — new user gate** | Onboarding screens | OIDC auth completes → intro screen (app name + value prop + locale dropdown + "Get Started"). → Step 1: Name your flat. → Step 2: Energy contract (Annual kWh Baseline with presets or custom; Tariff fields; derived euro budget with transparent calculation). Cannot reach main app until both steps complete. |
| **Tariff form — contract period active (locked)** | Settings → Tariff edit | Price fields render inline as read-only, greyed out, with lock icon and label: "Locked — contract active until [month year]." Non-price fields (provider name, contract dates) remain editable. No dialog or tap-to-reveal — lock state immediately visible on form open. |
| **Flat deletion — type-to-confirm** | Settings → Account | Input field: `Type "[Flat name]" to delete`. Delete action enabled only when typed value matches exactly. Friction matches irreversibility: destroys all Readings, Tariff history, Smart Plug Data, and Flat Structure for the Flat. |
| **Flat Structure — empty (no rooms)** | Flat Structure | Shows default 5-room template pre-populated (FR-22). Prompt: "These rooms were pre-filled — edit names or add your own." |
| **Flat Structure — room empty (no power points)** | Room detail | "No power points yet." + Add Power Point button. |
| **Flat Structure — device unconfigured (no energy approach)** | Decomposition card | Device appears in Flat Structure but contributes zero to Decomposition. Prompt to configure consumption approach shown inline. |
| **Import error — unreadable file** | Import surface | Inline error on the file row: "Data cannot be read." File row highlighted with `{colors.accent-error}` left border. User can remove the file and try another. |
| **Import error — processing failed** | Import surface / progress card | Progress card updates: "Processing failed — try again." Retry action available. |
| **Import error — service unavailable** | Import surface / progress card | Progress card updates: "Service temporarily unavailable — try again later." No retry countdown; user initiates retry manually. |
| **Insights — insufficient data** | Insights tab — Generated Insights section | "Not enough data for insights. Add readings and import smart plug data to generate insight cards." No insight cards shown. Trend chart still visible if readings exist. |
| **Reading History — load failed** | Reading History sheet | "Couldn't load reading history." Retry link. Sheet stays open. |

---

## Interaction Primitives

**Touch-first. Tap to act.** No hover-dependent interactions on primary paths. All primary actions reachable with one thumb on a 375px phone.

**Bottom sheet** — slides up from CTA. Drag handle at top for dismissal. Closes on drag below threshold or tap outside (where safe). Does not close on accidental tap — requires deliberate gesture.

**Numeric keyboard** — `inputmode="numeric"` auto-triggers the numeric keyboard for all kWh inputs. No manual keyboard type selection required.

**Period dropdown** — present on the Insights trend chart and the Decomposition period selector. Options: This week · This month · Last month · This year · Custom. Default for Decomposition: This month. Default for Insights: 30 days (not a dropdown option label — 30-day rolling window).

**Date/time field** — pre-filled with current timestamp on every sheet open. Editable for retroactive Reading entry. No separate "retroactive mode" toggle; the pre-filled field makes the capability transparent.

**File picker** — primary file selection on all platforms. Opens the platform's native file chooser. Accepts `.xlsx` (Eve Home) and `.csv` (Meross).

**Drag-and-drop** — available on desktop and tablet where the platform supports it. Dragging files onto the upload zone triggers the same file-selection flow as the file picker. Mobile: file picker only.

**Choice step** — used for device energy approach selection. Two mutually exclusive options appear as visible cards or radio-style selectors. Selecting one reveals only that approach's fields below. The other approach's fields are hidden (not disabled).

**Toggle** — used for self-measured device period selection (Daily / Weekly). Binary. "Daily" pre-selected. Switching updates the input field label instantly.

**Flat switcher** — tap the active Flat name in the header. Reveals a dropdown with all Flats plus "Add flat". Tap any Flat to switch; all surfaces reload for the selected Flat. *(R2)*

**Reading history** — clock/list icon in the trend chart card header (present on both the Dashboard sparkline and the Insights 30-day chart). Tap opens the Reading History bottom sheet.

**Edit-with-log** — Meter Readings are editable after submission. Editing an existing Reading stores "Original value was X kWh" as a note on that entry, visible in the Reading History sheet.

**Locale dropdown** — small `EN ▾` / `DE ▾` style selector in the top-right of the Onboarding intro screen and in Settings → Language & Region. Persists to browser-local storage.

---

## Accessibility Floor

**Target:** WCAG 2.2 AA.

**Numeric inputs:** `inputmode="numeric"` on all kWh fields. Ensures correct mobile keyboard without requiring `type="number"` (which has problematic UX on some platforms).

**Interactive elements:** Every button, icon button, dropdown trigger, and sheet handle carries an explicit `aria-label` or visible text label. State is communicated via `aria-expanded`, `aria-disabled`, `aria-invalid` as appropriate.

**Tap targets:** Minimum 44 × 44 pt for all interactive elements. Icon-only buttons (chart history icon, tablet Enter Reading icon) meet this floor regardless of visual icon size.

**Reduce Motion:** When `prefers-reduced-motion: reduce` is active, the KPI tile beat animation on post-reading-submit is skipped entirely. Updated values appear immediately. No other motion is conditionally suppressed because no other motion carries data state.

**Focus traversal:** Follows reading order (top-to-bottom, left-to-right within each section). Bottom sheet focus is trapped while open; returns to the triggering element on close.

**Screen reader:** Each tab in the bottom tab bar announces the surface name on focus and on activation. Bottom sheet opening announces its title and first interactive element. Validation messages use `role="alert"` or `aria-live="polite"` as appropriate to severity.

**Contrast:** Specified in DESIGN.md — see `{colors.foreground}`, `{colors.muted-foreground}`, and gradient foreground tokens. Not duplicated here.

---

## Key Flows

### Flow 0 — First-time setup (Ralf, day one, just moved in)

**Protagonist:** Ralf. Day one. Just moved in; no readings, no tariff, no flat configured. The app has never been opened.

1. Ralf navigates to the app for the first time. Onboarding gate intercepts; main tabs not accessible.
2. Intro screen: app name, value prop, tariff note. Ralf selects `DE` from the locale dropdown (overriding the browser's English default). Taps "Get Started."
3. Step 1: enters flat name "Wohnung 3B." Taps Continue.
4. Step 2: selects "2 persons ~2,500 kWh" preset. Enters base fee €12/month and price per kWh €0.2285. Adds contract end date Dec 2026.
5. Annual budget auto-calculates: €1,473/year. Ralf adjusts it to €1,400 (his personal target). Taps "Complete Setup."
6. **Climax beat:** The Dashboard opens for the first time — Euro Burn Gradient Background at neutral (no reading yet), Enter Reading CTA prominent. Ralf immediately taps it, enters his first meter reading. The gradient shifts to cool-teal as the KPI tiles populate with the first data. The app has a heartbeat.

Failure path: if network is unavailable during setup, onboarding completes locally; data syncs on next connection.

→ Mockup reference: onboarding-flow.html

---

### Flow 1 — UJ-1: Ralf reads the meter (phone, basement, under 60 seconds)

**Protagonist:** Ralf. Phone in hand, standing at the basement electricity meter. Wants to record the reading and know today's cost before walking back upstairs.

1. Opens the app on his phone. OIDC session is active; he lands directly on the Dashboard.
2. The Euro Burn gradient background and KPI tiles from his last reading are visible. He taps **Enter Reading**.
3. The Enter Reading bottom sheet slides up. The kWh field is auto-focused; the numeric keyboard is already displayed.
4. He reads the meter display and types the five-digit kWh value. The date/time field shows the current timestamp — he doesn't touch it.
5. He taps **Save**.
6. **Climax beat:** The sheet closes. The KPI tiles pulse — numbers count up/down to their new values. The "Last read:" timestamp in the header updates to the current time. The Euro Burn gradient may shift (warmer or cooler) reflecting today's consumption against budget.
7. Ralf can see his daily average kWh, today's euro cost, and the budget delta without any further action. He pockets his phone and walks upstairs.

Total elapsed time: under 60 seconds.

→ Mockup reference: euro-burn-dashboard.html, enter-reading-sheet.html

---

### Flow 2 — UJ-2: Ralf imports a week of smart plug data *(Release 2)*

**Protagonist:** Ralf. He has downloaded exports from the Eve Home app (`.xlsx`) and the Meross app (`.csv`) covering the past week. He wants to see a Decomposition for that week.

1. Opens the app on his phone or tablet. Navigates to the **Decomposition** tab.
2. The Decomposition tab is in its empty/unavailable state for the selected period: "No smart plug data for this period." An **Import** button is visible.
3. He taps Import. The Import surface opens.
4. He taps the upload zone to open the file picker. He selects the Eve Home `.xlsx` and the Meross `.csv` in one selection.
5. Both files appear in the file list below the upload zone. The Eve Home file shows auto-detected type "Eve Home" and a device association dropdown — the filename contains the device name, so it is auto-pre-selected. The Meross file shows auto-detected type "Meross" with its device also auto-pre-selected.
6. He confirms the associations and taps **Upload Files**.
7. A progress card appears on the Decomposition tab. The app is fully navigable; Ralf can check his Dashboard while processing runs.
8. **Gap notification step:** If either export contains a mid-period date gap, a notification appears: "Gap detected: [date range]. Missing days have been interpolated." He acknowledges it.
9. Processing completes. The progress card disappears.
10. The Decomposition tab now shows: Residual card at top (unattributed kWh + %), then Room cards with device cards. A week's data reveals the living room smart plug as the dominant consumer. An info banner appears if any interpolated values are present in the period.
11. Ralf taps a device card to see its weekly total and notes that one device is drawing more than expected.

→ Mockup reference: decomposition-tab.html, import-surface.html

---

### Flow 3 — UJ-3: Ralf reviews before the monthly invoice *(Release 2 for Generated Insights)*

**Protagonist:** Ralf. It is late in the month. He wants to know whether he is on track for his annual budget and whether any device is worth investigating before the invoice arrives.

1. Opens the app. Navigates to the **Insights** tab.
2. The 30-day trend chart loads at the top. He sees his consumption pattern for the month — one amber spike bar mid-month catches his eye. He notes it but does not act on it yet.
3. He scrolls down to the Generated Insights cards.
4. A **Budget pressure** card shows: rolling monthly projection × 12 exceeds his planned annual spend by a quantified euro amount.
5. A **Standby offender** card names a specific device — "TV living room" — with a quantified monthly standby cost of, for example, "€2.40/month in standby."
6. **Climax beat:** Ralf now knows the name of the device responsible for unnecessary cost and the exact monthly amount. He decides to investigate the TV's standby settings tonight.
7. He taps the Budget pressure card. The Insight detail shows the rolling annual kWh projection vs his Annual kWh Baseline, and the implied euro difference at the current Tariff.
8. He navigates back to the Dashboard. The "Last read:" timestamp confirms his most recent Reading is current.

Failure path: discovery run fails (service unavailable) → progress card shows "Processing failed — try again." Prior insights remain visible. Ralf can retry manually or wait for the next scheduled run at 02:00 UTC.

→ Mockup reference: insights-tab.html

---

## Responsive & Platform

| Breakpoint | Behavior |
| --- | --- |
| **Mobile (< 768px)** | Single-column layout throughout. Bottom tab bar for navigation. Enter Reading button full-label ("Enter Reading") as a prominent CTA on Dashboard. Bottom sheet for Reading entry. Trend chart full-width within its card. Decomposition room cards in single column. Import: file picker only, no drag-and-drop. Phone-frame layouts as shown in mockups. |
| **Tablet (768–1023px)** | Trend chart full-width (more chart real estate than phone). Insights tab: chart full-width top, Insights cards in 2-column grid below. Enter Reading: compact icon-only button (lightning bolt, Lucide `Zap`) in top-right of content header — frees main content area for the Dashboard. Sidebar nav appears (bottom tab bar hidden or replaced). Import: file picker + drag-and-drop on upload zone. Flat switcher in header. |
| **Desktop (1024px+)** | Full sidebar nav. Wider chart and Decomposition layouts. Dashboard KPI tiles in a wider row. Decomposition: room cards in multi-column grid. Import: file picker + drag-and-drop. Settings sub-screens may use a two-panel layout (list left, detail right). |

Bottom sheet behavior is consistent across all breakpoints — it slides up from the bottom on all form factors. On desktop, a modal dialog alternative may be considered but the bottom sheet pattern is preserved for consistency.

---

## Euro Burn Gradient System

The Dashboard background gradient is a live instrument — it encodes current-day kWh consumption state continuously, without a separate indicator, marker, or mode switch. The background color IS the data. Color token definitions, stop positions, and the design rationale for the midpoint, kWh anchoring, and no-marker decisions are in DESIGN.md → Colors → Euro Burn Gradient System.

**Anchor:** Daily kWh budget = Annual kWh Baseline ÷ 365.

**Gradient range:**
- Consumption at −50% of daily budget (half the budget used): gradient clips to cool edge — `{colors.gradient-cool-start}` through `{colors.gradient-cool-end}`.
- Consumption at +50% of daily budget (budget exceeded by half): gradient clips to warm edge — `{colors.gradient-warm-start}` through `{colors.gradient-warm-end}`.
- Values outside this range clip to the respective edge without further shift.

**Acceptable zone:** ±10% of the daily budget midpoint produces no visual alarm. The gradient shifts are subtle within this band.

**KPI correlation:** The daily tile's budget delta text (e.g., "↓ 0.8 kWh under budget") provides explicit precision for the state the gradient encodes. They are correlated signals — the gradient sets the ambient mood; the tile gives the number.

→ Mockup reference: gradient-states.html (full range from cool to warm), euro-burn-dashboard.html (gradient in context)

---

## Reading Correction Flow

**Entry point:** Clock/list icon in the trend chart card header. Present on both the Dashboard sparkline card and the Insights 30-day chart card.

**Flow:**
1. Tap the clock/list icon in the chart card header.
2. Reading History bottom sheet slides up. Shows chronological list of Meter Readings for the active Flat: date/time + kWh value. Entries that were previously edited show a "corrected" note inline.
3. Tap any entry in the list.
4. Edit form opens, pre-populated with the entry's recorded values (kWh value + date/time).
5. User modifies the value(s) and taps Save.
6. Save stores the corrected Reading. The original value is stored as a note: "Original value was X kWh" — visible in the Reading History sheet on that entry.
7. The Reading History sheet reflects the correction immediately. The Dashboard and Insights trend chart update to reflect the corrected Reading.

**Rationale:** Readings are editable because single-user, non-regulatory context makes direct editing the lowest-friction correction path. The lightweight log (original value note) satisfies the "days later reconciliation against annual bill" use case without adding a complex versioning layer.

→ Mockup reference: enter-reading-sheet.html (edit form state), insights-tab.html (chart card header icon placement)

---

## Inspiration & Anti-patterns

**Lifted — Apple Weather:** Ambient gradient encodes data state without requiring a separate legend or indicator. Bold numerics dominate the information hierarchy. The background is not decorative — it carries meaning. The energy-tracker gradient applies this principle directly: background color = consumption state.

**Lifted — Apple Home:** Variable-size device cards proportional to data richness. Quick-link access from settings to related sub-screens. Icon-only action buttons in the navigation header (tablet Enter Reading lightning bolt). These patterns make dense information scannable without requiring a separate drill-down UI.

**Lifted — shadcn/ui:** Component foundation. The brand layer (DESIGN.md) is what energy-tracker adds on top of shadcn defaults — not a component system built from scratch. This keeps maintenance overhead low and ensures accessible baseline behavior.

**Rejected — Thermometer / temperature progress bar:** A vertical or horizontal thermometer encoding energy consumption was considered as an alternative to the full-bleed gradient. Rejected because the metaphor is indirect (temperature ≠ energy) and the progress-bar shape implies a fixed maximum. The gradient has no implied ceiling — it is an ambient continuous field, not a progress indicator.

**Rejected — Success toast on Reading save:** A "Reading saved!" toast was considered. Rejected because three ambient signals already confirm the loop closed: (1) sheet closes, (2) KPI tiles pulse, (3) "Last read:" timestamp updates. Adding a fourth confirmation signal is noise. The ambient signals are the reward for completing the loop.

**Rejected — Separate budget section in Settings:** An independent "Budget" settings row was considered. Rejected because the Annual kWh Baseline and the Annual euro budget are both "how much do I expect to use / spend" inputs — they belong together. In Settings, the euro budget lives near the Tariff configuration because tariff and budget are financially related (price per kWh × baseline = spending expectation). Onboarding captures them together in Step 2 for the same reason.

**Rejected — Separate Trends and Insights tabs:** A dedicated "Trends" tab for the usage chart and a separate "Insights" tab for AI-generated findings were considered. Rejected because trends (chart + spike detection) are themselves usage-pattern insights — the same mental category as standby offenders and budget alerts. One tab named "Insights" covers both and avoids a navigation split that doesn't match how the user thinks about the information.

---

## Smart Power Strip Model *(Release 2)*

Two smart hardware types are supported in Flat Structure:

| Type | Outlets | Import data | Attribution |
| --- | --- | --- | --- |
| Smart Plug | 1 | Device's measured kWh (direct) | Measured — fully attributed |
| Smart Power Strip | N | Strip total kWh (all outlets combined) | Measured total; per-device split estimated |

Strip decomposition split algorithm:
1. Configured devices (EU label or self-measured) receive a proportional share: `(device_estimate / sum_of_configured_estimates) × strip_total`
2. Unconfigured devices receive an equal share of the remaining proportion: `(strip_total − configured_total) / unconfigured_count`
3. Unconfigured sub-device rows render at reduced opacity with a configure hint; configured sub-device rows render at full opacity.
4. The strip card header renders the measured total — authoritative, never estimated.

→ Mockup reference: decomposition-tab.html (Decomposition card patterns)
