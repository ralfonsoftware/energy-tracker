# PRD Quality Review — energy-tracker

## Overall verdict

This PRD is well-crafted for a developer-owner personal tool: the glossary is tight, every FR carries testable consequences, the counter-metrics are a genuine rarity, and scope boundaries are precise. The main risks are two open ambiguities that will block downstream work — the standby-offender detection algorithm is undefined (making FR-35 untestable and the UX designer left guessing), and the "relaxed tolerance" phrase for interpolated reconciliation is vague enough to break SM-3. A handful of lower-priority gaps (no inter-flat Tariff isolation clause, silent `[NOTE FOR PM]` on DB choice, budget-settings location deferred without a forcing event) are manageable. No critical showstoppers, two high-priority items to resolve before UX and architecture sessions.

---

## Decision-readiness — adequate

The PRD is broadly decision-ready for a personal tool: the scope is set, trade-offs between synchronous and background processing are explicit (NFR-1's three tiers), and the release split is clearly reasoned. Open Questions §10 are genuinely open — Q-3, Q-4, and Q-6 are cleanly deferred to UX with no pretense of resolution, which is honest.

Two things undercut readiness at the margin: FR-35's standby offender algorithm is not defined anywhere (not in the PRD, not in the SPEC), so a UX designer cannot design the insight card and an architect cannot estimate the query complexity. FR-37 says "rolling monthly projection × 12 exceeds the user's planned annual spend" but FR-37's consequence doesn't state the formula for "rolling monthly projection" — which is the same formula driving SM-5. Without it, SM-5 ("within 10% of the eventual invoice") cannot be independently verified.

The persistent data store is left TBD with a `[NOTE FOR PM]` (NFR-4) and the note is honest and appropriately scoped. The problem is that FR-23 (Flat deletion cascade), FR-27 (reconciliation), and FR-32 (decomposition) all depend on relational vs. document tradeoffs. This isn't a blocker for the PRD, but it is a forcing event that should be sequenced before architecture, not after.

### Findings

- **high** Standby offender algorithm undefined (§4.11 FR-35) — "disproportionate standby draw" is not defined: no threshold, no comparison baseline (24-hour profile? overnight window? % of total?). A UX designer cannot design the insight card; an architect cannot estimate query cost. *Fix:* Define the standby detection rule — e.g., "standby draw > X% of total device consumption, or nightly draw exceeds Y kWh average over the past 30 days" — or add an explicit `[ASSUMPTION A-8]` with the intended heuristic and flag it for resolution before architecture.
- **high** "Tolerance relaxed" for interpolated reconciliation is not quantified (§FR-27, §FR-32, §SM-3) — The phrase "tolerance relaxed when interpolation is used" appears three times but the relaxed value is never stated. SM-3 targets ±0.1 kWh without interpolation; the interpolated tolerance is unknown. This makes SM-3 partially untestable and the reconciliation acceptance criterion ambiguous for engineers. *Fix:* State the relaxed tolerance (e.g., ±0.5 kWh per interpolated day, or ±5% of the interpolated period total) or add an `[ASSUMPTION]` that it will be set during architecture.
- **medium** Rolling monthly projection formula not defined (§FR-37, §SM-5) — The budget pressure alert and the 10% invoice accuracy metric both depend on this formula, but it is not stated. If "rolling monthly projection" means different things to the alert (FR-37) and the accuracy metric (SM-5), the two will diverge. *Fix:* Define the formula in the Glossary or as an FR consequence — e.g., "trailing 30-day total × (days-in-month / 30)."
- **low** Q-1 (DB selection) is marked resolved but the resolution is "deferred to architecture" — the open-question entry reads "Resolved: Deferred to architecture session." This is a contradiction: a deferred decision is not resolved. *Fix:* Change Q-1 status to "Deferred — will be resolved in architecture session" and remove "Resolved" label.

---

## Substance over theater — strong

No persona theater: §2 has Jobs To Be Done and Non-Users rather than fictional personas, which is appropriate for a single developer-owner. The vision (§1) is specific enough that it could not swap into a generic energy-app PRD without rewriting it — the basement meter scenario, the sixty-second loop, and the "named standby offenders" are product-specific. NFRs are not boilerplate: NFR-1's three-tier performance model is derived from actual product behavior (background blob processing vs. synchronous dashboard load), NFR-2's fixed-decimal requirement is a real constraint, and NFR-3 cross-references an external companion file. The only thin section is the Device Registry (§4.9): FR-29 through FR-31 are accurate but thin — the "prompt to configure consumption" mentioned in the description has no corresponding FR or testable consequence.

### Findings

- **medium** "Prompt to configure consumption" unverifiable (§4.9 description, §FR-29) — The description says "Devices without a consumption approach configured appear in the Flat Structure but contribute zero to Decomposition with a prompt to configure consumption." There is no FR or testable consequence for the prompt. The SPEC (CAP-8) lists this as a success criterion. *Fix:* Add an FR-29a consequence: "A Device with no consumption approach configured displays a prompt to configure consumption in the Decomposition view."
- **low** FR-38 consequence for scheduled discovery is not directly testable in a unit/integration context (§FR-38) — "A manually triggered discovery run updates the Insights page" is testable; the scheduled 02:00 UTC run has no stated verification mechanism. *Fix:* Add a consequence: "The scheduled discovery job can be triggered via a test invocation endpoint or a clock-injected test harness and produces the same output as a manual trigger."

---

## Strategic coherence — strong

The PRD has a clear thesis: replace a spreadsheet with a mobile-first app whose irreducible core is read-enter-see, layered upward with decomposition and insights. Every feature group traces to this arc. The release split (R1 = core tracking, R2 = decomposition + insights) reflects the dependency order correctly. Success Metrics validate the thesis directly: SM-1 validates the 60-second core, SM-2 and SM-3 validate correctness, SM-4 validates insight quality. Counter-metrics (SM-C1, SM-C2) are genuine guardrails — SM-C1 against insight quantity gaming, SM-C2 against UI complexity creep in the reading flow. This is unusually well-structured for a personal project PRD.

### Findings

- **low** SM-4 ("at least one named device") is a one-time reachability test, not an ongoing quality signal — it will be satisfied after the first successful insight run and never reconfirm value. *Fix:* Consider adding a companion metric: "Insight actionability: user acts on (unplugs / replaces) at least one device within 90 days of first insight." For a personal tool this is self-evident, but naming it makes the thesis explicit.

---

## Done-ness clarity — strong

This is the PRD's clearest strength. Every FR has a "Consequences (testable)" block, and the blocks are specific: redirect URLs, field-level validations, arithmetic equalities, named UI states. Vague language ("handles gracefully," "reasonable," "user-friendly") is absent from FRs. A few edge cases:

- FR-8 references "the performance budget defined in §NFR-1" rather than inlining the value — this is acceptable since NFR-1 is clear, but a future reader skimming FR-8 must follow the cross-reference.
- FR-26's gap detection says "within its covered period (first date to last date in the export)" — "first date" and "last date" are well-defined by the file format, but it is not stated what happens when a file contains only one data point (no interpolation possible). This is an edge case, not a gap in the FR.

### Findings

- **medium** Single-data-point import edge case unhandled (§FR-26) — A Smart Plug export with exactly one day of data has no gap-detection range. The FR is silent on this. It is unlikely in practice but is a valid error path. *Fix:* Add a consequence: "An export with only one data point is imported without gap detection; no interpolation is performed."
- **low** FR-11 "rejected" is underspecified (§FR-11) — The consequence says "An attempt to edit the price fields of a Tariff entry whose Contract Period has started is rejected." "Rejected" needs a behavior: HTTP 400? Disabled fields with a tooltip? Q-3 defers this to UX, but the FR consequence should acknowledge the deferral explicitly. *Fix:* Add "(UX pattern deferred — see Q-3)" to the consequence.

---

## Scope honesty — strong

Non-Goals (§5) do real work: they each rule out something a reasonable user might ask for (native app, real-time monitoring, export, cross-user admin). The multi-tenant hosted version non-goal is paired with an architecture constraint ("architecture must support it, UI must not target it"), which is the right framing. `[ASSUMPTION]` tags are present (§11) and most are sourced. Two gaps:

- No assumption tags the user's sole reliance on Azure — if the owner's Azure subscription lapses, the app is dead. For a personal tool this is fine, but it is an implicit constraint that could be named.
- There is no `[ASSUMPTION]` for the Eve Home locale: FR-24 specifies the sheet name as `Gesamtverbrauch` (German), which implies the owner's Eve Home app is in German. If the locale is ever changed, the parser breaks. This is an untaged constraint buried in a functional requirement.

### Findings

- **medium** Eve Home locale dependency untagged (§FR-24) — The sheet name `Gesamtverbrauch` is hardcoded as the parse target. If the Eve Home app locale changes, the import breaks silently. *Fix:* Add `[ASSUMPTION A-8]` (or renumber): "The Eve Home app is configured in German; the export sheet name is always `Gesamtverbrauch`. If the app locale changes, the parser must be updated."
- **low** No assumption for Tariff currency alignment — The Tariff is stored and displayed in the active Locale's currency. But currency and Locale are the same setting; the PRD never explicitly states that only one currency is supported per Flat at a time. If the user switches Locale mid-month, historical cost displays will change currency symbol. *Fix:* Add a note or assumption: "Currency symbol is derived from the active Locale at render time; switching Locale changes the displayed symbol for all historical costs."

---

## Downstream usability — adequate

**Glossary:** Tight and consistent. Terms introduced in §3 are used consistently throughout FRs. Cross-references to companion files (`locale-formats.md`, `smart-plug-formats.md`) are named but not anchored — a downstream agent must know where to find them. The SPEC is the canonical source for companion file locations; this is acceptable given §0's "SPEC governs" declaration.

**FR/UJ/SM ID continuity:** FR-1 through FR-42 are contiguous, no gaps. SM-1 through SM-6 and SM-C1 through SM-C2 are clean. UJ-1 through UJ-3 are defined and cross-referenced in feature descriptions. Assumptions A-1 through A-7 are contiguous with no gaps. Cross-references resolve: FR-8 → NFR-1, FR-35 → SM-4, FR-37 → FR-37 (self-referential in §10 Q-4 is fine).

**UX extraction:** UJ-1, UJ-2, UJ-3 give enough narrative for a UX designer to start wireframes. Q-3, Q-4, Q-6 correctly flag UX-decision-required items. The Onboarding flow (§4.2) is complete enough for screen design. The missing piece is the Insights card format: no UJ covers the Insights page experience; UJ-3 gets the user to the page but doesn't describe the card structure (does a standby offender insight show a chart, a number, a call-to-action button?). This is a UX decision, but the PRD doesn't flag it as one.

**Architecture extraction:** The tech stack (§9) is clear. NFR-4 explicitly names the TBD as a `[NOTE FOR PM]` and sequences it before Release 2. The async processing model (blob trigger → queue → Function) is stated in NFR-4 and referenced in FR-28. One gap: there is no NFR or FR for data retention — how long are Meter Readings kept? Is there a maximum Reading history limit? For a personal tool this is probably "forever," but an architect designing the data store needs to know.

**Story generation:** FR IDs are stable and testable consequences are present. A story-generation agent can produce an acceptance criterion from each consequence block directly. The one friction point is FR-35 (standby offender algorithm undefined), which will produce a story with an AC that says "identifies devices with disproportionate standby draw" — an untestable story.

### Findings

- **medium** No data retention NFR (§8) — An architect designing the persistent store needs to know the expected Read history volume and retention policy. "Two years of Readings" appears in NFR-1 as a dashboard load benchmark but is never established as a retention requirement. *Fix:* Add an NFR-5 or a note in §9: "Meter Readings and Smart Plug Data are retained indefinitely (personal tool; storage cost is negligible at expected volume)."
- **medium** Insights page UX gap not flagged (§4.11, §10) — The PRD defers Tariff lock UX (Q-3), Flat deletion confirmation (Q-6), and budget settings placement (Q-4) to UX, but does not flag the Insights card structure as a UX open question. A UX designer will need to decide this and currently has no signal that it's theirs to decide. *Fix:* Add Q-7: "Insight card design: what does a standby-offender insight card show beyond device name and monthly cost? (CTA button, chart, history?) — Deferred to UX design session."
- **low** Companion file locations not anchored (§0, §4.6 description) — `locale-formats.md` and `smart-plug-formats.md` are referenced by name without a relative path. The SPEC frontmatter anchors them, but the PRD's §0 says "Source documents are referenced for traceability" without a pointer to where they live. *Fix:* Add a note in §0 or §9: "Companion files `locale-formats.md` and `smart-plug-formats.md` are co-located with the SPEC at `_bmad-output/specs/spec-energy-tracker/`."

---

## Shape fit — strong

For a developer-owner personal tool feeding a downstream chain (UX → architecture → stories), the formalization level is appropriate and unusually well-calibrated. The PRD is specific enough to drive story generation (testable consequences per FR) without over-specifying implementation (no class diagrams, no API contracts, no DB schema). The "SPEC governs" declaration in §0 is the correct safety valve — it prevents the PRD from needing to be a complete specification.

The choice to omit fictional personas and replace them with Jobs To Be Done is the right shape for a single-user tool; it avoids theater without losing decision signal. The release 2 annotation on features is a clean scope-management device that prevents scope creep into story generation.

The one shape concern is that several features in §4.11 (Insights) are Release 2 but their success metrics (SM-4, SM-5) and counter-metrics (SM-C1) are not labelled R2. A story-generation agent consuming SM-4 for a Release 1 sprint would be misdirected.

### Findings

- **medium** SM-4 and SM-5 not labelled Release 2 (§7) — These metrics validate FR-35 through FR-37, which are all Release 2 features, but the Success Metrics section has no release labelling. A downstream sprint-planning agent or story-generator running against Release 1 would incorrectly include SM-4 and SM-5 as acceptance benchmarks. *Fix:* Add a "*(Release 2)*" label to SM-4, SM-5, SM-C1 in §7, parallel to the release annotations on the feature descriptions.

---

## Mechanical notes

**Glossary drift:** None found. Terms introduced in §3 are used consistently throughout. "Interpolated Value" (§3) is correctly cross-referenced as "interpolated values" (lowercase) in FRs — intentional convention, not drift. "Smart Plug Data" (§3, capitalized compound) is used consistently.

**ID continuity:** FR-1 through FR-42 contiguous, no gaps or duplicates. UJ-1 through UJ-3 defined and back-referenced. SM-1 through SM-6, SM-C1 through SM-C2 clean. NFR-1 through NFR-4 present. Q-1 through Q-6 present. A-1 through A-7 present.

**Assumptions Index roundtrip:** All seven assumptions are resolvable to a source location. A-1 resolved (NFR-1); A-2 sourced to §5; A-3 sourced to SPEC; A-4 sourced to SPEC; A-5 sourced to NFR-3; A-6 sourced to smart-plug-formats.md; A-7 resolved (FR-40). The roundtrip is complete. Note: A-1 and A-7 are marked "Resolved" and struck through — this is good hygiene, but "Resolved" should be used consistently (Q-1 in §10 is ambiguously "resolved as deferred" — see Decision-readiness finding above).

**Broken cross-refs:** None found. FR-8 → NFR-1 resolves. FR-9 → Tariff resolves. FR-37 "budget settings" is undefined as a screen (see Q-4 deferral, acceptable). FR-35's consequence references "sufficient Smart Plug Data" — this is a real ambiguity but is a substance issue (covered in Decision-readiness), not a broken cross-ref.

**Release annotation consistency:** FRs in §4.7 through §4.11 are annotated "*(Release 2)*" in section descriptions. §6 (MVP Scope) correctly maps FRs to releases. §7 (Success Metrics) lacks release annotations — see Shape fit finding.
