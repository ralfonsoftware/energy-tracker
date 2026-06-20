# Spine Pair Review — energy-tracker

## Overall verdict

The spine pair is strong and substantially ready for downstream consumption. EXPERIENCE.md is exceptionally thorough — all three UJs have well-formed Key Flows, states are comprehensively mapped, and component behavioral rules are genuine rather than boilerplate. The main gaps are: two unresolved token references in EXPERIENCE.md point to wrong keys in DESIGN.md's YAML, one .working/ HTML file is completely unreferenced in either spine, DESIGN.md status is still "draft" while EXPERIENCE.md is "final," and light-mode contrast targets for text-over-glass are unspecified for the custom colors. None of these block story-writing; the token mismatches block clean source-extraction.

---

## 1. Flow coverage — strong

Checked: three named UJs in PRD §2.3 — UJ-1 (meter read), UJ-2 (smart plug import), UJ-3 (invoice review). Also checked functional areas without a named UJ but with dedicated FR groups: Onboarding (FR-4–7), Tariff management (FR-10–13), Reading correction (FR-9), Flat deletion (FR-23).

Each UJ has a Key Flow with a named protagonist, numbered steps, a climax beat, and a failure path. Coverage is complete for all three UJs.

### Findings

- **medium** Flow 3 climax beat names "Generated Insights cards" but omits a failure path for the case where insight discovery fails mid-review or returns no findings. The state table covers "Generated Insights processing" but not a "discovery failed" terminal state. (EXPERIENCE.md §Key Flows — Flow 3). *Fix:* Add a failure path sentence: discovery failure → prior cards persist, an error toast offers manual retry.
- **medium** Onboarding flow (FR-4–7) is documented in the IA table and State Patterns but has no Key Flow. The onboarding step is load-bearing (blocks all other features) and involves a three-screen sequence with two required data-entry steps. (EXPERIENCE.md — no Flow 4). *Fix:* Add Flow 4 covering the new-user gate: Intro screen → Step 1 Flat name → Step 2 baseline + tariff → main app; failure path for tariff field validation.
- **low** Reading Correction has a detailed standalone section but is not a named Key Flow. Story-dev can source it from the section, so this is cosmetic. (EXPERIENCE.md §Reading Correction Flow). *Fix:* Retitle the standalone section to "Flow 4 — UJ-1 extension: Reading correction" or promote it to a flow entry for navigational consistency.

---

## 2. Token completeness — adequate

Extracted all YAML keys from DESIGN.md frontmatter and all `{colors.xxx}` / `{components.xxx}` references from both spines.

**YAML keys defined:** gradient-cool-start, gradient-cool-end, gradient-neutral, gradient-warm-start, gradient-warm-end, glass-surface, glass-border, glass-surface-light, glass-border-light, accent-spike, accent-under-budget, accent-over-budget, accent-info, accent-error, accent-tariff-locked, residual-tint, text-primary, text-secondary, text-tertiary. All have hex or rgba values. No color token is missing a value.

**Token references in EXPERIENCE.md body — resolution check:**

- `{colors.foreground}` (Accessibility Floor §Contrast) — **not defined** in DESIGN.md YAML. This appears to be a shadcn inherited token used by name; the spine's own comment says "inherits shadcn defaults for everything not in the brand layer." However the reference implies consumers can source-extract a specific hex from DESIGN.md — they cannot.
- `{colors.muted-foreground}` (Accessibility Floor §Contrast) — same issue as above. Not defined in DESIGN.md YAML.
- `{colors.gradient-cool-edge}` (Euro Burn Gradient System) — **key mismatch.** DESIGN.md defines `gradient-cool-start`, not `gradient-cool-edge`. EXPERIENCE.md references a non-existent key.
- `{colors.gradient-warm-edge}` (Euro Burn Gradient System) — **key mismatch.** DESIGN.md defines `gradient-warm-end`, not `gradient-warm-edge`.
- `{colors.text-primary}`, `{colors.text-secondary}`, `{colors.text-tertiary}` — correctly resolve.
- `{components.component-name}` syntax mentioned in EXPERIENCE.md header as cross-reference pattern, but no `{components.xxx}` references appear in the body — they are always referenced by prose name. Acceptable; the pattern is declared but the spine uses it consistently by prose.

**Contrast targets:** DESIGN.md specifies no explicit WCAG contrast ratios for the custom token combinations (e.g., `text-primary` rgba(255,255,255,0.90) over `glass-surface` rgba(255,255,255,0.08) over gradient). The Drift reference example deferred contrast claims to shadcn defaults; energy-tracker has a fully custom glass stack where the effective contrast depends on the gradient position underneath. No ratio is committed anywhere in the pair.

### Findings

- **critical** `{colors.gradient-cool-edge}` and `{colors.gradient-warm-edge}` in EXPERIENCE.md §Euro Burn Gradient System resolve to no key in DESIGN.md YAML. Correct keys are `gradient-cool-start` and `gradient-warm-end`. Source-extraction would silently fail. (EXPERIENCE.md lines ~279–281). *Fix:* Rename references in EXPERIENCE.md to match DESIGN.md YAML keys exactly.
- **high** `{colors.foreground}` and `{colors.muted-foreground}` in EXPERIENCE.md §Accessibility Floor are shadcn inherited tokens, not defined in DESIGN.md. A downstream consumer resolving the reference would find nothing. (EXPERIENCE.md §Accessibility Floor §Contrast). *Fix:* Either define these as pass-through commentary ("inherits shadcn defaults — see shadcn theme") or remove the token syntax and write prose: "inherits shadcn's foreground and muted-foreground defaults."
- **high** No contrast ratio is specified for `text-primary` (rgba white 0.90) over the glass stack at any gradient position. The glass background is semi-transparent over a dynamic gradient — the effective contrast is not constant and cannot be assumed from shadcn defaults. The pair claims WCAG AA but provides no basis for the claim on the custom surface. (DESIGN.md §Colors; EXPERIENCE.md §Accessibility Floor). *Fix:* Add a note in DESIGN.md specifying the minimum contrast ratio for `text-primary` and `text-secondary` over the glass surface at the lightest gradient position (warm edge), and cite the verified ratio.
- **low** `accent-under-budget` light-mode override is documented only in prose ("uses `#16a34a` in light mode") but is absent from the YAML frontmatter. A tool consuming the YAML would get only the dark-mode value. (DESIGN.md §Colors §Semantic Accent Palette note). *Fix:* Add `accent-under-budget-light: "#16a34a"` to the YAML, or adopt a consistent `token` / `token-dark` / `token-light` naming convention.

---

## 3. Component coverage — strong

Extracted all component names from both files.

**Named in DESIGN.md §Components:** euro-burn-gradient-background, glass-card, kpi-tile, enter-reading-button, bottom-tab-bar, progress-card, decomposition-card–smart-power-strip-sub-device-rows.

**Named in EXPERIENCE.md §Component Patterns (table rows):** KPI tile, Enter Reading button, Enter Reading bottom sheet, trend chart, reading history icon, bottom sheet (generic), Euro Burn gradient background, import upload zone, decomposition room card, decomposition device card — rich (single smart plug), decomposition device card — rich (smart power strip), decomposition device card — compact, residual card, progress card, flat switcher dropdown, onboarding step indicator, tariff lock indicator, flat deletion confirm, choice step, toggle.

**Cross-coverage check:**
- All DESIGN.md brand-layer components have both a visual spec (DESIGN.md) and a behavioral entry (EXPERIENCE.md). No orphaned visual spec.
- Several EXPERIENCE.md components have no DESIGN.md visual spec entry: reading history icon, decomposition room card, decomposition device card variants (rich/compact), residual card, flat switcher dropdown, onboarding step indicator, tariff lock indicator, flat deletion confirm, choice step, toggle. These all inherit from shadcn or are adequately described as layout containers — acceptable for a brand-layer delta document. Not a defect.
- "Enter Reading bottom sheet" is in EXPERIENCE.md Component Patterns but DESIGN.md only covers the "Enter Reading Button" visual spec. The sheet itself inherits shadcn Sheet defaults plus `rounded.sheet` (24px top corners), which is defined in DESIGN.md shapes. Adequate.

### Findings

- **medium** The Decomposition section introduces "smart power strip sub-device rows" as a component in DESIGN.md §Components but it is presented as a subsection of "Decomposition Card" rather than as a standalone component entry. The EXPERIENCE.md component table has a matching "decomposition device card — rich (smart power strip)" entry. The naming is slightly inconsistent: DESIGN.md says "Decomposition Card — Smart Power Strip Sub-device Rows"; EXPERIENCE.md says "decomposition device card — rich (smart power strip)." Downstream consumers may not link these as the same component. (DESIGN.md §Components last subsection; EXPERIENCE.md §Component Patterns row 12). *Fix:* Align the component name — use the same heading in both files, e.g., "Decomposition Device Card — Smart Power Strip."
- **low** "Reading history icon" is named in EXPERIENCE.md §Component Patterns but has no visual spec entry in DESIGN.md — not even an "inherits shadcn Icon" note. Given it appears in two surfaces (Dashboard sparkline header and Insights chart header), a one-line note confirming the icon primitive would close the gap. (EXPERIENCE.md §Component Patterns). *Fix:* Add a one-liner to DESIGN.md §Components: "Reading history icon — Lucide `Clock` or `List`, inherits shadcn default icon sizing."

---

## 4. State coverage — adequate

Walked all IA surfaces from EXPERIENCE.md §Information Architecture table. Listed expected states per surface and verified coverage.

**Dashboard:** empty (no readings) ✓, cold load not explicitly named (skeleton state covered in paragraph form via post-reading behavior), post-save ✓, under/at/over budget ✓, spike day ✓.

**Enter Reading sheet:** default (empty) ✓, valid value ✓, below-last-reading warning ✓, save failed ✓.

**Reading History sheet:** default ✓, edit ✓. No error state for "load failed" (network error when opening history).

**Insights:** data available ✓, Generated Insights processing ✓. No "no data yet" state (user hasn't entered enough readings for a 30-day chart) and no "discovery failed" state.

**Decomposition:** data available ✓, no data (empty) ✓, interpolated data ✓, processing post-import ✓, strip partially configured ✓, strip no devices configured ✓.

**Import:** empty ✓, files selected ✓, processing ✓, gap detected ✓. No "import failed" terminal state (FR-28 defines error categorization, but no corresponding EXPERIENCE.md state entry covers what the UI shows when all files fail to parse).

**Flat Structure editor:** no states covered at all — no entry in State Patterns. The surface is in the IA table but completely absent from State Patterns.

**Settings — Flat config / Tariff / kWh Baseline:** covered via tariff lock state ✓ and flat deletion ✓. kWh Baseline has no state entry (empty, first-entry, edit existing not covered).

**Onboarding:** covered ✓ in a single entry.

### Findings

- **high** Flat Structure editor surface (R2) is in the IA table but has zero state coverage in EXPERIENCE.md §State Patterns. This is a multi-step hierarchy editor (four-level: Flat → Rooms → Power Points → Devices) — downstream story-dev has no behavioral contract for its empty, in-progress, complete, or error states. (EXPERIENCE.md §State Patterns — no entry for Flat Structure). *Fix:* Add minimum states: empty (no rooms added), structure in-progress (rooms added, no devices), device unconfigured (no energy approach set), and device configured. One table row per state with treatment column.
- **high** Import surface has no "import failed — parse error" terminal state. FR-28 specifies three categorized error messages but the State Patterns table only covers the success-path import processing state. (EXPERIENCE.md §State Patterns). *Fix:* Add one state row: "Import — parse error" → treatment: categorized error message inline in Import surface per FR-28; "Upload Files" button re-enabled; files remain listed for correction or removal.
- **medium** Insights tab has no "not enough data" state (e.g., user has fewer than 2 Readings, or all Readings within a single day, so no trend line is renderable). (EXPERIENCE.md §State Patterns). *Fix:* Add: "Insights — insufficient data" → "Chart shows empty state with prompt: 'Enter at least two readings on different days to see a trend.'"
- **medium** Reading History sheet has no "load failed" state — the sheet is opened via a tap and fetches from the server. (EXPERIENCE.md §State Patterns). *Fix:* Add: "Reading History — load failed" → inline error message in the sheet with retry.
- **low** Dashboard cold open (no readings) is covered but the term "cold open" vs "cold load" is inconsistently used. The state table entry is "Dashboard — cold open (no readings)" but does not mention a skeleton loading state before data arrives; the Enter Reading CTA prominence is mentioned but no loading skeleton treatment is specified (contrast: Drift EXPERIENCE.md has explicit skeleton treatment). (EXPERIENCE.md §State Patterns first row). *Fix:* Split into two rows: "Dashboard — initial load (skeleton)" and "Dashboard — empty (no readings submitted)."

---

## 5. Visual reference coverage — adequate

**Files in .working/:**
1. decomposition-tab.html
2. direction-ambient-glass.html
3. direction-clean-instrument.html
4. direction-night-meter.html
5. direction-solar-heat.html
6. enter-reading-sheet.html
7. euro-burn-dashboard.html
8. flat-structure-editor.html
9. gradient-states.html
10. import-surface.html
11. insights-tab.html
12. onboarding-flow.html
13. settings-screens.html

**Spine references (combined):**

EXPERIENCE.md §IA table footer: euro-burn-dashboard.html ✓, enter-reading-sheet.html ✓, insights-tab.html ✓, decomposition-tab.html ✓, import-surface.html ✓, flat-structure-editor.html ✓, settings-screens.html ✓.

EXPERIENCE.md §Component Patterns footer: euro-burn-dashboard.html ✓, enter-reading-sheet.html ✓, gradient-states.html ✓, insights-tab.html ✓, decomposition-tab.html ✓, import-surface.html ✓, settings-screens.html ✓.

EXPERIENCE.md §Euro Burn Gradient System: gradient-states.html ✓, euro-burn-dashboard.html ✓.

EXPERIENCE.md Flow 1: euro-burn-dashboard.html ✓, enter-reading-sheet.html ✓.
EXPERIENCE.md Flow 2: decomposition-tab.html ✓, import-surface.html ✓.
EXPERIENCE.md Flow 3: insights-tab.html ✓.
EXPERIENCE.md §Reading Correction: enter-reading-sheet.html ✓, insights-tab.html ✓.
EXPERIENCE.md §Smart Power Strip Model: decomposition-tab.html ✓.

**Unreferenced files (orphans):**
- `direction-ambient-glass.html` — not linked anywhere in either spine.
- `direction-clean-instrument.html` — not linked anywhere.
- `direction-night-meter.html` — not linked anywhere.
- `direction-solar-heat.html` — not linked anywhere.
- `onboarding-flow.html` — not linked anywhere in either spine.

**"Spines win on conflict" stated:** EXPERIENCE.md §IA table footer contains the statement "Spine wins on conflict." ✓ (Present, though the phrasing is slightly different from the Drift reference which says "Spine wins on conflict" — acceptable.)

### Findings

- **high** `onboarding-flow.html` is in .working/ but is never referenced in either spine. The Onboarding gate is an R1 feature (FR-4–7) with a three-screen sequence. The mockup exists but the spines provide no link to it and no claim about what it illustrates. A downstream UX implementer reading the spines would not know to consult it. (EXPERIENCE.md §IA — Onboarding row has no mockup ref; §State Patterns Onboarding entry has no mockup ref). *Fix:* Add `→ Mockup reference: onboarding-flow.html (Intro screen, Step 1, Step 2)` to the Onboarding entry in §State Patterns and/or as a footnote to the IA table Onboarding row.
- **medium** `direction-ambient-glass.html`, `direction-clean-instrument.html`, `direction-night-meter.html`, and `direction-solar-heat.html` are design direction artifacts in .working/ but are not linked from either spine. If these are rejected directions they should be noted as such (or ignored); if they informed the final direction they could be cited in EXPERIENCE.md §Inspiration. Currently they are invisible to consumers. (DESIGN.md, EXPERIENCE.md — neither references these files). *Fix:* Either add a single footnote to DESIGN.md §Brand & Style ("Design direction candidates: direction-*.html in .working/; ambient-glass and clean-instrument informed the final direction; night-meter and solar-heat were rejected.") or confirm they are scratch artifacts and note them as non-canonical in DESIGN.md.
- **low** The "Spine wins on conflict" statement is in the IA table footer of EXPERIENCE.md only. DESIGN.md contains no equivalent statement. For consumers reading only DESIGN.md, the precedence contract is invisible. *Fix:* Add a one-liner to DESIGN.md §Brand & Style footer or a "Source precedence" note: "EXPERIENCE.md is the behavioral authority; on conflict, spines govern over mockups."

---

## 6. Bloat & overspecification — strong

The spine pair is well-disciplined overall. DESIGN.md stays at the brand-layer delta; it does not duplicate shadcn specs. EXPERIENCE.md does not repeat visual specs — it defers to DESIGN.md tokens. No FR-level requirement text is copied from the PRD into either spine.

### Findings

- **low** EXPERIENCE.md §Smart Power Strip Model contains the full decomposition split algorithm (proportional share formula, equal share formula for unconfigured devices). This is a business logic / backend spec — it belongs in the PRD or architecture doc, not the experience spine. The UX consequence (two-tier opacity treatment) is already captured in §Component Patterns and §State Patterns. (EXPERIENCE.md §Smart Power Strip Model). *Fix:* Reduce to the UX-relevant contract only: "Configured sub-devices render at full opacity with their proportional share displayed. Unconfigured sub-devices render at reduced opacity with an equal-share placeholder and a configure hint." Remove the ratio formulas.
- **low** EXPERIENCE.md §Euro Burn Gradient System duplicates much of what DESIGN.md §Colors §Euro Burn Gradient System and DESIGN.md §Components §Euro Burn Gradient Background already cover (clip bounds, midpoint rationale, kWh vs € anchor decision, no-marker decision). The section in EXPERIENCE.md is legitimately behavioral (it explains the system's live-instrument nature and its correlation with the KPI tile), but the repeated rationale paragraph ("Why kWh, not €" and "No marker or hairline") is sourced from DESIGN.md. (EXPERIENCE.md §Euro Burn Gradient System). *Fix:* Trim the "Why kWh, not €" and "No marker or hairline" paragraphs from EXPERIENCE.md §Euro Burn Gradient System; they live correctly in DESIGN.md. Keep the behavioral description (anchor, range, acceptable zone, KPI correlation).

---

## 7. Inheritance discipline — adequate

**Sources frontmatter:** Both spines cite `_bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md`. EXPERIENCE.md also cites `.decision-log.md` (DESIGN.md does not — possible oversight, or intentional if decisions were only consulted for EXPERIENCE.md).

**UJ / requirement names vs PRD verbatim:**
- PRD: "UJ-1. The developer-owner reads the meter and checks cost." — EXPERIENCE.md Flow 1 heading: "UJ-1: Ralf reads the meter (phone, basement, under 60 seconds)." The label UJ-1 matches; the name is paraphrased, not verbatim. Acceptable.
- PRD: "UJ-2. The developer-owner uploads a week's worth of smart plug data." — EXPERIENCE.md Flow 2 heading: "UJ-2: Ralf imports a week of smart plug data." Matches in substance.
- PRD: "UJ-3. The developer-owner reviews insights before the monthly invoice." — EXPERIENCE.md Flow 3 heading: "UJ-3: Ralf reviews before the monthly invoice." Matches.

**Glossary terms consistency:** PRD defines: Flat, Main Meter, Meter Reading, Tariff, Contract Period, Annual kWh Baseline, Daily kWh Budget, Smart Plug Data, Decomposition, Residual, Interpolated Value, Insight, Flat Structure, Device, Room, Power Point. Both spines use these terms consistently and correctly throughout. No invented synonyms detected.

**Component name consistency across sections:** "Enter Reading button" vs "Enter Reading bottom sheet" vs "Enter Reading CTA" — three names for related but distinct components; this is acceptable (button vs sheet vs label). Within EXPERIENCE.md §Component Patterns and §State Patterns the names are self-consistent.

**`{token}` references resolve to DESIGN.md YAML:** Two critical mismatches documented in Pass 2 above (`gradient-cool-edge` / `gradient-warm-edge`). All other token references resolve correctly.

### Findings

- **critical** (duplicated from Pass 2 — Token completeness) `{colors.gradient-cool-edge}` and `{colors.gradient-warm-edge}` do not resolve. Already documented under Pass 2.
- **medium** DESIGN.md frontmatter `status: draft`; EXPERIENCE.md frontmatter `status: final`. If both are published simultaneously as the pair, the draft status on DESIGN.md will cause downstream consumers to treat the visual spec as provisional while the behavioral spec is final. (DESIGN.md frontmatter line 3). *Fix:* Promote DESIGN.md status to `final` if it is in fact complete, or add a note explaining why the draft status is intentional.
- **medium** EXPERIENCE.md cites `.decision-log.md` as a source but DESIGN.md does not. If design decisions (D-6, D-8, D-10, D-11, D-12, D-14, D-15, D-22, D-28 etc.) are cited by ID throughout DESIGN.md, the decision log should also be listed as a source in DESIGN.md's frontmatter. Downstream consumers who encounter "D-15" in DESIGN.md have no pointer to the file where D-15 is defined. (DESIGN.md frontmatter). *Fix:* Add `.decision-log.md` to DESIGN.md sources frontmatter.

---

## 8. Shape fit — strong

**DESIGN.md canonical section order check:**
1. Brand & Style ✓
2. Colors ✓
3. Typography ✓
4. Layout & Spacing ✓
5. Elevation & Depth ✓
6. Shapes ✓
7. Components ✓
8. Do's and Don'ts ✓

All eight canonical sections present in order.

**EXPERIENCE.md required sections:**
- Foundation ✓
- Information Architecture ✓
- Voice and Tone ✓
- Component Patterns ✓
- State Patterns ✓
- Interaction Primitives ✓
- Accessibility Floor ✓
- Key Flows ✓
- Responsive & Platform ✓
- Inspiration & Anti-patterns ✓

**Required-when-applicable sections:**
- Responsive section: present ✓ (multi-surface: phone/tablet/desktop).
- Inspiration section: present ✓. Coaching path: product explicitly rejects coaching (Drift anti-pattern discipline) — the anti-pattern entries adequately cover this.

**Invented sections:**
- EXPERIENCE.md §Euro Burn Gradient System: warrants its place — the gradient is a novel cross-cutting design primitive not fully captured in either Component Patterns or State Patterns alone.
- EXPERIENCE.md §Reading Correction Flow: warrants its place — it is a distinct user-facing flow covering a non-obvious "edit with log" pattern.
- EXPERIENCE.md §Smart Power Strip Model: partially earned — the UX-relevant card anatomy is warranted; the algorithm formula is overreach (noted in Pass 6).

### Findings

- **low** DESIGN.md has no "Do's and Don'ts — Typography" or "Do's and Don'ts — Components" subsections; the single Do's and Don'ts table at the end is thematically focused on color and gradient behavior only. Several component-level decisions (e.g., Lucide `Zap` on tablet CTA, no secondary hint on button face, three post-save signals) appear in the table but are framed as visual decisions. Typography decisions (no web fonts, `display-kpi` exclusively for KPI headlines) have no corresponding Don't entries. *Fix:* Consider splitting the Do's and Don'ts table into subsections (Colors, Components, Typography) or adding 2–3 typography don'ts ("Don't load web fonts"; "Don't use `display-kpi` for non-numeric headline roles").

---

## Mechanical notes

1. **Key mismatch (critical blocker):** EXPERIENCE.md §Euro Burn Gradient System references `{colors.gradient-cool-edge}` and `{colors.gradient-warm-edge}`. DESIGN.md YAML defines `gradient-cool-start` and `gradient-warm-end`. These must be aligned.

2. **Token resolution for inherited tokens:** EXPERIENCE.md §Accessibility Floor references `{colors.foreground}` and `{colors.muted-foreground}` — shadcn inherited tokens not in DESIGN.md YAML. Remove token syntax for shadcn-pass-through values, or document them as inherited.

3. **Status mismatch:** DESIGN.md `status: draft` vs EXPERIENCE.md `status: final`. Resolve before publishing the pair.

4. **Decision log source missing from DESIGN.md:** D-xx references appear throughout DESIGN.md but `.decision-log.md` is not in its sources frontmatter.

5. **`accent-under-budget` light-mode value in prose only:** `#16a34a` appears in a body paragraph but not in YAML. YAML-consuming tools would not see it.

6. **Orphan mockups:** `onboarding-flow.html`, `direction-ambient-glass.html`, `direction-clean-instrument.html`, `direction-night-meter.html`, `direction-solar-heat.html` — none linked from either spine.

7. **Component name variance:** "Decomposition Card — Smart Power Strip Sub-device Rows" (DESIGN.md) vs "decomposition device card — rich (smart power strip)" (EXPERIENCE.md). Should be normalized to the same label.
