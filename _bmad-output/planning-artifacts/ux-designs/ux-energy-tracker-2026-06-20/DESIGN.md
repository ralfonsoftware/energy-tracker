---
name: energy-tracker
project: energy-tracker
status: final
created: 2026-06-20
updated: 2026-06-20
sources:
  - _bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md
  - _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/.decision-log.md

colors:
  # ── Euro Burn gradient system ─────────────────────────────────────────────
  # Full-screen ambient background. Maps kWh consumption vs daily budget to
  # a continuous color position; cool = under budget, warm = over budget.
  gradient-cool-start: "#1a1f4e"   # deep indigo-blue at −50% budget (cool edge, 0%)
  gradient-cool-end: "#0d4f5c"     # teal at −30% budget (cool edge, 30%)
  gradient-neutral: "#2d2018"      # warm charcoal at 100% of daily budget (midpoint, 60%)
  gradient-warm-start: "#4a2000"   # deep amber at +35% over budget (warm edge, 85%)
  gradient-warm-end: "#6b2d00"     # burnt orange at +50% over budget (warm edge, 100%)

  # ── Glass surface system ──────────────────────────────────────────────────
  # Dark mode: semi-transparent white over the gradient background
  glass-surface: "rgba(255,255,255,0.08)"      # dark mode card background
  glass-border: "rgba(255,255,255,0.14)"       # dark mode card border (thin bright line)
  # Light mode: heavier white fill over lighter gradient
  glass-surface-light: "rgba(255,255,255,0.55)" # light mode card background
  glass-border-light: "rgba(0,0,0,0.08)"        # light mode card border (subtle dark line)

  # ── Semantic accents ──────────────────────────────────────────────────────
  accent-spike: "#f59e0b"          # amber — spike bars in trend chart and standby warnings
  accent-under-budget: "#4ade80"   # cool green (dark mode) — budget delta when under budget
  accent-over-budget: "#f59e0b"    # amber — budget delta when over budget (same hue as spike)
  accent-info: "#60a5fa"           # blue — interpolation hints, info states
  accent-error: "#f87171"          # red — error states (sheet save failure)
  accent-tariff-locked: "#d97706"  # amber-dimmed — inline locked tariff field indication
  residual-tint: "rgba(245,158,11,0.10)" # amber tint overlay on the Residual card background

  # ── Text (dark mode base) ────────────────────────────────────────────────
  text-primary: "rgba(255,255,255,0.90)"    # primary white text on glass or gradient
  text-secondary: "rgba(255,255,255,0.65)"  # KPI subline, secondary labels
  text-tertiary: "rgba(255,255,255,0.35)"   # hint text, section-label caps, bar axis labels

typography:
  # System font stack — no web fonts loaded. Renders as SF Pro on Apple devices,
  # Segoe UI / system-ui on Windows, and Roboto / system-ui on Android/Chrome OS.
  font-family: >-
    -apple-system, BlinkMacSystemFont, "SF Pro Display", "SF Pro Text",
    "Helvetica Neue", sans-serif
  # Type scale roles
  # display-kpi:   22px / 700 / tracking −0.02em  — KPI tile headline (kWh primary value)
  # display-kpi-tablet: 20px / 700 / tracking −0.02em — same role on tablet
  # body:          16px / 600                      — CTA button label
  # body-sm:       13px / 500–600                  — KPI subline (€ value), header flat name
  # label-caps:    11px / 600 / tracking +0.08–0.12em / uppercase — section headers, trend title
  # caption:       10px / 400                      — KPI tertiary hint, CTA sub-hint
  # micro:          9px / 500                      — bar axis labels, tab labels

rounded:
  # Energy-tracker uses larger radii than shadcn defaults (shadcn: 6/8/12px)
  # to support the liquid glass aesthetic and soft atmospheric feel.
  pill: "9999px"   # full pill — Enter Reading CTA button
  sheet: "24px"    # top-left + top-right only — bottom sheets (Enter Reading, reading history)
  card: "18px"     # glass cards (KPI tiles, trend card, decomposition cards)
  input: "12px"    # text inputs inside sheets
  badge: "20px"    # status badges and chips (e.g. "estimated" on decomposition cards)
  sidebar-item: "10px" # tablet sidebar nav item active highlight

spacing:
  # Inherits shadcn/Tailwind spacing scale (4px base unit).
  # App-level overrides:
  screen-padding-x: "16px"  # horizontal inset for all screen content
  card-gap: "10px"           # gap between KPI grid cards
  card-padding: "16px 18px"  # internal padding for glass cards
  scroll-bottom-clearance: "84px" # padding-bottom to clear the fixed tab bar

components:
  # Brand-specific components not covered by shadcn defaults.
  # See component specs in the body section below.
  - euro-burn-gradient-background
  - glass-card
  - kpi-tile
  - enter-reading-button
  - bottom-tab-bar
  - progress-card
---

# energy-tracker — DESIGN.md

Visual identity spine for the energy-tracker web app. This file specifies the brand layer only; all unlisted tokens and component styles inherit from shadcn/ui defaults.

---

## Brand & Style

**Euro Burn** is the design language: a cost-first energy instrument built on top of shadcn/ui, styled with an Apple Weather-inspired liquid glass aesthetic. The name reflects the product's core purpose — making the daily cost of electricity visceral and tangible.

Three principles govern every visual decision:

1. **Ambient state encoding.** The full-screen gradient background is not decorative — it is data. Its position on the cool-to-warm spectrum encodes the user's current kWh consumption relative to their daily budget. The visual field of the entire app communicates budget status before the user reads a single number.

2. **kWh-anchored, cost-surfaced.** The gradient and the budget delta text are anchored to kWh, not euros. kWh usage is stable across tariff changes; euro costs shift with every contract update. The budget delta line on the Daily KPI tile always reads in kWh ("↓ 0.8 kWh under budget"), while the euro equivalent is surfaced as a secondary subline within the same card.

3. **Instrument, not coach.** The visual language is calm and precise. No gamification, no streaks, no encouraging language. The aesthetic is a clean instrument panel — confident, factual, and respectful of the user's intelligence.

shadcn/ui provides the component substrate (form primitives, sheet, dropdown, toast, dialog). The brand layer supplies the gradient background, glass surface system, token overrides, and the brand-specific components documented below.

---

## Colors

### Euro Burn Gradient System

The gradient background spans 160 degrees (or 140 degrees on tablet) across five color stops:

| Token | Hex | Position | Meaning |
| --- | --- | --- | --- |
| `gradient-cool-start` | `#1a1f4e` | 0% | −50% of daily kWh budget or lower |
| `gradient-cool-end` | `#0d4f5c` | 30% | Approaching daily budget from below |
| `gradient-neutral` | `#2d2018` | 60% | Exactly at daily kWh budget |
| `gradient-warm-start` | `#4a2000` | 85% | ~+35% over daily kWh budget |
| `gradient-warm-end` | `#6b2d00` | 100% | +50% of daily kWh budget or higher |

The gradient is **not** re-rendered on every kWh update. It represents the current day's consumption state relative to the configured daily budget (Annual kWh Baseline ÷ 365). The midpoint neutral (`#2d2018`) is a warm charcoal — deliberately not cool — so that "exactly at budget" does not read as "over budget." The light mode gradient mirrors this mapping with sky-blue through warm-sand to amber tints.

**Gradient mapping rule:** −50% of budget clips to the cool edge; +50% clips to the warm edge. A ±10% acceptable zone around the midpoint produces no visual alarm — the gradient shift is imperceptible within normal daily variation.

**No midpoint marker.** A hairline marker at the gradient midpoint was explicitly removed (D-15). The background color shift is itself the unambiguous signal; the KPI delta tile provides explicit numeric precision.

### Glass Surface System

All content cards use a glass morphism treatment: `backdrop-filter: blur(20px) saturate(180%)` over the gradient background. Two sets of tokens cover dark and light mode:

- **Dark mode:** `glass-surface` / `glass-border` — semi-transparent white tint with a bright thin border, creating the illusion of frosted glass over the deep gradient.
- **Light mode:** `glass-surface-light` / `glass-border-light` — heavier white fill with a subtle dark border, maintaining readability over light gradient tints.

### Semantic Accent Palette

| Token | Hex | Usage |
| --- | --- | --- |
| `accent-spike` | `#f59e0b` | Spike bars in trend chart; standby offender warnings |
| `accent-under-budget` | `#4ade80` | Budget delta text when under budget (dark mode) |
| `accent-over-budget` | `#f59e0b` | Budget delta text when over budget |
| `accent-info` | `#60a5fa` | Interpolation hints; info state labels |
| `accent-error` | `#f87171` | Error toast text inside Enter Reading sheet |
| `accent-tariff-locked` | `#d97706` | Inline lock icon + "Locked — contract active" label |
| `residual-tint` | `rgba(245,158,11,0.10)` | Amber tint overlay on the Residual card in Decomposition |

Note: `accent-under-budget` uses `#16a34a` (a darker green) in light mode for adequate contrast over lighter glass surfaces.

Tokens not defined in this file (`foreground`, `muted-foreground`, `background`, `border`, `input`, `ring`, etc.) inherit from shadcn/ui defaults and are not overridden by the energy-tracker brand layer.

### Do's and Don'ts — Colors

| Do | Don't |
| --- | --- |
| Use `accent-spike` amber consistently for both spike bars and standby cost warnings — same semantic meaning | Use red for spike bars — spikes are informational, not errors |
| Use `accent-under-budget` green only for delta text that is genuinely below budget | Apply green to "on track" projected month KPI — use `text-secondary` instead |
| Let the gradient speak first — keep card backgrounds translucent enough that the gradient bleeds through | Set card backgrounds to opaque — this kills the ambient state signal |
| Use `text-tertiary` for hint text and label-caps section headers | Use full-opacity white for tertiary content — the visual hierarchy flattens |

---

## Typography

**No web fonts.** The app loads zero custom typefaces. The system font stack resolves to SF Pro on Apple devices (the primary demographic for evening basement meter reads on iPhone), Segoe UI on Windows, and system-ui on Android.

```
-apple-system, BlinkMacSystemFont, "SF Pro Display", "SF Pro Text", "Helvetica Neue", sans-serif
```

SF Pro Display's tabular numerals and optical sizing at large scales make it ideal for the KPI tile headline role without custom font loading cost.

### Type Roles

| Role | Size | Weight | Tracking | Usage |
| --- | --- | --- | --- | --- |
| `display-kpi` | 22px | 700 | −0.02em | KPI tile headline — primary kWh value (phone) |
| `display-kpi-tablet` | 20px | 700 | −0.02em | Same role on tablet (tighter 4-column grid) |
| `body` | 16px | 600 | +0.01em | CTA button label ("Enter Reading") |
| `body-sm` | 13px | 500–600 | +0.01em | KPI subline (€ value), flat name in header |
| `label-caps` | 11px | 600 | +0.08–0.12em (uppercase) | Section headers, trend chart title, tab labels |
| `caption` | 10px | 400 | default | KPI tertiary hint, CTA sub-hint, bar-label axis |
| `micro` | 9px | 500 | +0.02em | Bar chart day-of-week labels |

**Numeric display style:** KPI headlines use negative letter-spacing (−0.02em) and weight 700. This tightens multi-digit numbers ("86.8 kWh", "~632 kWh") into a single visual unit rather than a sequence of characters. The tilde prefix on projected values ("~632 kWh") is styled identically — the approximation is conveyed by the tilde character, not by visual softening.

---

## Layout & Spacing

**Mobile-first.** The primary design surface is 375px wide (standard iPhone viewport). All spacing decisions are made for this width first.

### Breakpoints

| Breakpoint | Width | Layout change |
| --- | --- | --- |
| Phone (default) | < 768px | Bottom tab bar, full-width CTA button, 2×2 KPI grid |
| Tablet | 768px+ | Sidebar nav (200px), icon-only header CTA (Lucide `Zap`), 4-across KPI grid, full-width trend chart |
| Desktop | 1024px+ | Sidebar nav expanded, two-column Insights layout (chart + insights grid) |

### Spacing Scale

The app inherits the 4px base unit (Tailwind default) from shadcn without override. App-level layout constants:

- **Screen horizontal padding:** 16px on phone, 20–24px on tablet/desktop
- **KPI card gap:** 10px (tighter than shadcn default gap-4 = 16px, to keep 2×2 grid compact)
- **Card internal padding:** 16px vertical, 18px horizontal
- **Scroll bottom clearance:** 84px padding-bottom to clear the fixed tab bar on phone
- **Tab bar height:** 72px fixed at viewport bottom

---

## Elevation & Depth

**Backdrop-filter layering**, not box shadows, produces the depth. The composition has three planes:

1. **Background plane (deepest):** The Euro Burn gradient — a full-screen CSS `linear-gradient` rendered directly on the screen element. It is always visible beneath everything.
2. **Glass mid-plane:** Glass cards (`backdrop-filter: blur(20px) saturate(180%)`). The blur collapses depth to a smear of gradient color beneath each card, creating perceived separation without a hard boundary.
3. **Surface plane (topmost):** Interactive elements — CTA button, tab bar, sheet — each with their own glass treatment and a brighter border (`rgba(255,255,255,0.40)` on the CTA vs `rgba(255,255,255,0.14)` on cards) to signal interactability.

The tab bar and CTA button use a slightly brighter border (`1.5px solid rgba(255,255,255,0.40)`) than standard cards (`1px solid rgba(255,255,255,0.14)`), which establishes their surface-plane identity without box shadows.

Box shadows appear only on the Enter Reading sheet (via shadcn sheet defaults) and the phone/tablet demo frames in design artifacts — never on in-app content cards.

**Glass surface text contrast:** `{colors.text-primary}` (rgba 255,255,255,0.90) over `{colors.glass-surface}` (rgba 255,255,255,0.08) over the darkest gradient point (`{colors.gradient-cool-start}` `#1a1f4e`) achieves approximately 7:1 contrast ratio — WCAG AA compliant. Verify at the warm edge (`{colors.gradient-warm-end}` `#6b2d00`) where contrast is lower; `{colors.text-primary}` over warm glass yields approximately 5:1.

---

## Shapes

The glass aesthetic requires softer radii than shadcn's defaults (which are typically 6–8px) to support the atmospheric, Apple Weather-inspired character.

| Token | Radius | Applied to |
| --- | --- | --- |
| `pill` | 9999px | Enter Reading CTA button (full-width pill) |
| `sheet` | 24px (top corners only) | Bottom sheets: Enter Reading, reading history |
| `card` | 18px | All glass content cards: KPI tiles, trend card, decomposition cards |
| `input` | 12px | Text inputs inside sheets |
| `badge` | 20px | Status chips: "estimated", "R2 locked" |
| `sidebar-item` | 10px | Tablet sidebar nav item active state highlight |

**Rationale:** Card radii at 18px (vs shadcn's ~8px) are the most visible departure from shadcn defaults. At 18px, cards read as soft, floating objects rather than rectangles with clipped corners. The full-pill CTA at 9999px is a deliberate contrast to the cards — it communicates "action" through its distinct shape vocabulary.

---

## Components

### Euro Burn Gradient Background

The full-screen ambient background that encodes consumption state.

**Visual spec:**
- CSS `linear-gradient(160deg, ...)` on the `.screen` element (phone) or the content root (tablet/desktop)
- Five stops: `gradient-cool-start` → `gradient-cool-end` → `gradient-neutral` → `gradient-warm-start` → `gradient-warm-end`
- Angle `140deg` on tablet (shallower than the phone's `160deg`)
- Angle and stop positions are fixed design constants; stop positions shift to reflect the current day's consumption percentage (re-applied each update)
- Budget clamp: consumption ≤ −50% of daily budget clips to stop 0%; consumption ≥ +50% clips to stop 100%
- Renders behind all other content; `z-index` layering keeps cards and nav above it

**Light mode:** A separate cool-to-warm gradient using lighter tints: `#e8f4fd` (sky blue) → `#daeef9` → `#e8d5b0` (warm sand midpoint) → `#f0c890` → `#e8b870` (warm amber).

### Glass Card

The reusable frosted-glass surface for all content cards (KPI tiles, trend chart, decomposition cards).

**Dark mode:**
```
background: rgba(255,255,255,0.08)   /* glass-surface */
backdrop-filter: blur(20px) saturate(180%)
border: 1px solid rgba(255,255,255,0.14)  /* glass-border */
border-radius: 18px  /* card radius */
padding: 16px 18px
```

**Light mode overrides:**
```
background: rgba(255,255,255,0.55)   /* glass-surface-light */
border: 1px solid rgba(0,0,0,0.08)   /* glass-border-light */
```

All other properties (backdrop-filter, border-radius, padding) are unchanged between modes.

Reading history icon: a 20×20px clock or list SVG, `{colors.text-secondary}` stroke, positioned in the card's top-right header row. No background fill; icon only.

### KPI Tile

A glass card containing a paired kWh + € layout. The daily tile adds a budget delta line.

**Structure (top to bottom):**
1. **Headline** (`display-kpi`, `text-primary`) — the primary kWh value: `"12.4 kWh"`, `"~632 kWh"`, `"15.5 kWh"`
2. **Subline** (`body-sm`, `text-secondary`) — the paired euro equivalent: `"€2.87"`, `"~€87.20"`, `"€3.59/day"`
3. **Delta** (optional, `label-caps` size, `accent-under-budget` or `accent-over-budget`) — budget delta on the daily tile only: `"↓ 0.8 kWh under budget"` / `"↑ 9.3 kWh over budget"` / `"— at daily budget"`; or status text like `"on track"` on the projected tile
4. **Tertiary** (optional, `caption`, `text-tertiary`) — contextual hint on the budget tile: `"based on 5,657 kWh/yr"`

On phone, tiles are arranged 2×2. On tablet, all four tiles are arranged in a single 4-column row. The tile structure is identical across breakpoints; only the headline font size reduces from 22px to 20px on tablet to accommodate the tighter column width.

### Enter Reading Button

The primary full-width CTA on the phone dashboard. The irreducible core action (D-6).

**Phone (full-width pill):**
```
width: 100%
padding: 16px 24px
border-radius: 9999px   /* pill */
background: rgba(255,255,255,0.10)
backdrop-filter: blur(20px) saturate(180%)
border: 1.5px solid rgba(255,255,255,0.40)
box-shadow:
  0 0 0 1px rgba(255,255,255,0.05),
  0 4px 24px rgba(255,255,255,0.06),
  inset 0 1px 0 rgba(255,255,255,0.15)
```

Label: `"Enter Reading"` — `body` type role, `text-primary`. No secondary hint text on the button face (D-11); the hint "Date and time will be saved with your reading." appears inside the sheet.

**Tablet (icon-only compact):** A 44×44px square pill (`border-radius: 14px`) positioned in the top-right of the content header. Contains the Lucide `Zap` icon at 20×20px, `currentColor: #ffffff`. Same glass treatment as the phone variant. (D-12, D-14)

**Light mode:** `background: rgba(255,255,255,0.65)`, `border: 1.5px solid rgba(0,0,0,0.12)`, `box-shadow: 0 4px 20px rgba(0,0,0,0.08)`. Label color: `#1a1f2e`.

### Bottom Tab Bar

Fixed glass navigation bar anchored to the bottom of the phone viewport. Transitions to a sidebar on tablet.

**Phone:**
```
position: fixed, bottom: 0, left: 0, right: 0
height: 72px
background: rgba(10,15,25,0.75)   /* dark semi-transparent */
backdrop-filter: blur(20px) saturate(180%)
border-top: 1px solid rgba(255,255,255,0.10)
padding-top: 10px
```

Four tabs: Dashboard · Insights · Decomposition · Settings. Each tab: icon (22×22px SVG, `stroke: white`, `stroke-width: 1.8`) + label (`micro` type role). Active tab: icon opacity 1.0, label `text-primary`. Inactive: icon opacity 0.4, label `text-tertiary`.

**Light mode:** `background: rgba(255,255,255,0.75)`, `border-top: 1px solid rgba(0,0,0,0.08)`. Icons and labels use `#1a1f2e` in place of white.

**Tablet:** Replaced by a 200px-wide sidebar (`background: rgba(0,0,0,0.25)`, `backdrop-filter: blur(20px) saturate(180%)`, `border-right: 1px solid rgba(255,255,255,0.08)`). Nav items are rows with icon + label; active item gets `background: rgba(255,255,255,0.12)`, `border-radius: 10px`.

### Progress Card

An amber-tinted glass card used for background processing states — specifically the Smart Plug import progress indicator (D-37) and insight discovery progress (FR-39).

**Spec:**
- Inherits glass card base styles (18px radius, `backdrop-filter`)
- Applies `residual-tint` (`rgba(245,158,11,0.10)`) as an additional overlay on the glass surface
- Uses `accent-tariff-locked` (`#d97706`) at low opacity on the border for a warm amber tint
- Contains: a brief status label (`label-caps`), a progress description (`body-sm`, `text-secondary`), and optionally a spinner or indeterminate progress bar
- Persists on the Decomposition tab until processing completes; user is not blocked from navigating

### Decomposition Card — Smart Power Strip Sub-device Rows

Within a smart power strip card, sub-device rows follow a two-tier opacity treatment. Configured sub-device rows (device has EU label or self-measured values) use `{colors.text-primary}` for the kWh value and `{colors.text-secondary}` for the label — identical to standard card content. Unconfigured sub-device rows render at `opacity: 0.45` relative to the card background, with hint text ("Configure device profile for a more accurate split") rendered in `{colors.text-tertiary}` to reinforce reduced confidence without suppressing the value entirely.

---

## Do's and Don'ts

| Do | Don't |
| --- | --- |
| Let the gradient background carry the ambient budget state signal — it speaks before the user reads any text | Add any annotation, hairline, or marker to the gradient background (D-15) |
| Use `"↓ 0.8 kWh under budget"` / `"↑ 9.3 kWh over budget"` — kWh delta, arrow prefix, explicit direction (D-28) | Show euro delta on the budget delta line — tariff changes would make the number stale (D-8) |
| Keep glass card opacity low enough that the gradient bleeds through (`rgba(255,255,255,0.08)` dark / `0.55` light) | Make cards opaque or near-opaque — this breaks the ambient encoding entirely |
| Use `accent-spike` amber for both spike bars and standby warnings — consistent semantic color for "unexpected elevated consumption" | Use a separate warning color (orange, red) for standby offenders — the amber is already the established spike language |
| Write voice-and-tone copy as: `"Couldn't save — try again."` / `"Locked — contract active until Dec 2026"` (D-28) | Add exclamation marks, motivational copy, streak language, or coaching text anywhere in the app |
| Preserve the three post-save signals: sheet closes + KPI pulse animation + "Last read:" timestamp update (D-22) | Show a success toast on save — the ambient KPI update is the confirmation |
| On save failure: keep the sheet open, preserve the typed value, show inline error toast near Save button (D-22) | Dismiss the sheet on save failure — the user must not lose their typed reading |
| Use the Lucide `Zap` icon on the tablet compact CTA (D-14) | Use a text label on the tablet CTA — it dominates the header at compact sizes |
| Apply `text-tertiary` (`rgba(255,255,255,0.35)`) to label-caps section headers — they are wayfinding, not content | Make section header caps full opacity — they compete with the KPI values for attention |
