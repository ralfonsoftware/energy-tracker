# PRD Quality Review — prd-energy-tracker-2026-06-20

## Overall verdict

This PRD holds up well under a fresh read. The FR/Consequences discipline is applied almost universally, the three-principle thesis (cost-first, residual-aware, hub-free) actually drives feature sequencing and Success Metrics rather than decorating them, and the FR-37 budget-kWh-anchor redefinition — the highest-scrutiny change in this edit round — is handled with real rigor: a concrete formula, a worked numeric example, an explicit edge case, and a dated, unambiguous "not yet implemented" consequence rather than a silently-updated spec pretending the code already matches. The residual gaps are narrow and specific rather than structural: one Insight-detection FR (FR-36) lacks the numeric trigger its siblings have, a term central to this very edit round ("budget kWh anchor") didn't make it into the Glossary alongside its sibling term, and assumption-honesty lives only in the §11 index rather than inline at the point where a reader would need it. None of these threaten the PRD's usefulness to downstream UX/architecture/story workflows; they are precision fixes on an otherwise strong document.

## Decision-readiness — strong

Trade-offs are named with what was given up, not smoothed. §1's cost-first principle carries a stated exception ("one deliberate exception: the KPI dashboard's budget-delta comparison anchors to kWh... since euro figures shift with every Tariff change") with the reasoning inline, not deferred to a footnote. FR-17 states the no-notification design choice for spike detection and names the metric it protects ("protecting SM-C2") rather than asserting it "balances" usability and completeness. FR-37's Consequences block explicitly documents a live contradiction between spec and shipped code (`BudgetAlertDetector.cs`) — a real tension surfaced, not buried. §10 Open Questions are genuinely resolved-with-citation (UX/architecture session references, decision-log IDs), not rhetorical questions answered in the next breath.

No findings.

## Substance over theater — strong

No persona theater: a single named protagonist (the developer-owner) carries all three UJs; no persona roster added to look thorough. No differentiation/competitive section exists — appropriate, since none was asked for. NFRs carry product-specific bounds instead of boilerplate: NFR-1's three performance tiers have explicit second-counts (≤2s, ≤30s), NFR-4 gives a concrete, reasoned infrastructure choice (Azure SQL Basic DTU over Cosmos DB, with the specific query shape that motivates it) rather than "must be reliable and scalable." The Vision (§1) is specific to this product (basement meter, Eve Home/Meross brands, the 60-second loop) and would not swap cleanly into another PRD in this category.

No findings.

## Strategic coherence — strong

The thesis is stated and load-bearing: three named design principles (cost-first, residual-aware, hub-free) in §1, and the Release structure in §6 explicitly argues from that thesis ("Release 1 is a self-contained spreadsheet replacement... Release 2 layers attribution on top"). Success Metrics validate the thesis, not activity: SM-1 (speed), SM-2 (cost accuracy), SM-3 (decomposition correctness), SM-4 (insight actionability) each map to a named FR cluster, and counter-metrics are present and specific (SM-C1 guards against insight-volume gaming; SM-C2 guards the core loop against feature creep). Release 3/4 are transparently labeled as later, externally-sourced additions (post-retro, post-architecture-review) rather than folded in as if original — this strengthens rather than weakens coherence, since scope evolution is traceable instead of silent.

No findings.

## Done-ness clarity — adequate

The default posture is strong: nearly every FR carries a "Consequences (testable)" block with concrete, checkable conditions, and there is no instance of "gracefully," "reasonable," "user-friendly," or "intuitive" anywhere in the document (verified by search). FR-37 is the standout case the task flagged for scrutiny, and it earns the treatment: the budget-kWh-anchor formula is given exactly — `(planned annual spend − annualized monthly base fee) ÷ price per kWh` — with a worked example (€1,200 spend, €0.35/kWh, €10/month base fee → ≈3,085.7 kWh), an explicit degenerate case (base fee ≥ spend → anchor is 0, alert fires immediately), and a bolded, dated "Not yet implemented as of this PRD revision (2026-08-07)" consequence naming the exact file (`BudgetAlertDetector.cs`) and the exact discrepancy (cost-vs-cost comparison the FR replaces). That is the rubric's bar for done-ness clarity met, not dodged — an engineer picking this up knows both the target behavior and that it diverges from what ships today.

Two FRs fall short of that bar:

### Findings
- **high** FR-36 (Replacement candidate detection, §4.11) has no testable trigger condition — (§ location) FR-36 says the app "identifies high-consumption Devices where replacement offers a quantifiable payback," but unlike its neighbors (FR-35's "> 2 W," FR-43's "±10%"), no threshold defines "high-consumption" or how "quantifiable payback" is computed (payback period? minimum euro savings? comparison baseline?). The Consequence only checks the output shape ("names a specific Device with a quantified savings figure"), not when the detector should fire. An engineer cannot write an acceptance test for the detection logic itself from this FR alone. *Fix:* add an explicit numeric threshold or defer the exact formula to architecture.md the way FR-32's strip-sharing math does ("see architecture.md AD-8a"), but state that a formula exists and where.
- **low** FR-47 (Responsive device card grid, §4.13) doesn't define the tablet/desktop breakpoint — "On tablet and desktop viewports" is directional, not a testable boundary (what viewport width triggers the grid vs. the single-column layout?). Lower stakes: this is a Release 3 UI-consistency fix with no dedicated Success Metric by design (§7 note). *Fix:* state the breakpoint(s) in pixels or reference a design-system token.

## Scope honesty — strong

§5 Non-Goals does real work with specific, non-generic entries (e.g., "Multi-tenant hosted version for other households (architecture must support it, UI must not target it)" — nuanced, not a blanket exclusion). §11's Assumptions Index is substantive: 11 entries, several explicitly marked *Resolved* with a source, and [A-11] is a genuine honesty fix — flagging the FR-5 household-size presets as "a rough PM judgment call... not sourced from an external dataset" rather than letting them read as researched data. Open-items density is low and matches the stakes (single developer-owner, not a multi-stakeholder green-light document).

### Findings
- **medium** Assumption tags live only in the §11 index, never inline at the point of use (§3, throughout §4) — the PRD uses no `[ASSUMPTION: …]` convention in body text; all 11 assumptions are declared once, in §11, with no back-reference from the FR or Glossary entry that actually makes the assumption. Concretely: FR-5's household-size presets (1,500/2,500/3,500/4,250 kWh) carry only an "≈" hedge inline; a reader of FR-5 alone would not know these are an uncited PM guess (per [A-11]) unless they separately consult §11. This weakens the rubric's "each section makes sense pulled out alone" test for downstream extraction. *Fix:* add a light inline pointer at FR-5 (e.g., "(see [A-11])") — the convention doesn't need to change everywhere, just at the handful of assumption-bearing FRs.

## Downstream usability — strong

The Glossary (§3) is comprehensive (21 terms) and used consistently — no case/plural drift found across FRs. FR IDs are fully contiguous: every number FR-1 through FR-56 appears exactly once (verified by extraction), with no gaps or duplicates, despite non-monotonic physical placement from incremental additions (FR-48–56 inserted into their thematic home sections rather than appended at the end) — the ID space survived several edit rounds cleanly. All three UJs carry a named protagonist ("the developer-owner"), and most feature descriptions carry an explicit "Realizes UJ-X" tag, giving a clean cross-reference chain for downstream UX/architecture work. Cross-references checked (FR-37→FR-14, FR-4→FR-6/FR-10, FR-50→FR-32, FR-56→FR-8/16/17) all resolve to real, matching content.

### Findings
- **medium** "Budget kWh anchor" is undefined in the Glossary (§3) despite being the central new term this edit round introduced — it's used in FR-7 ("re-derives its budget kWh anchor") and is the load-bearing concept of the redefined FR-37, but §3 stops at "Locale" without a matching entry. Notably, its sibling term "Planned Annual Spend" *was* added to the Glossary in this same round (per decision log D-43) — the omission of "budget kWh anchor" looks like an oversight rather than a deliberate choice, since the two terms are defined together in FR-37 and one got the Glossary treatment while the other didn't. *Fix:* add a Glossary entry mirroring the Planned Annual Spend entry, e.g. "Budget kWh Anchor — a derived kWh figure, frozen at the moment Planned Annual Spend is set or edited, used to detect budget pressure independent of later Tariff changes (FR-37)."

## Shape fit — strong

This is a chain-top, single-operator PRD (§0: "any downstream workflow agents consuming it for UX design, architecture, or epic/story generation"), and the rigor level matches that stated purpose rather than over-formalizing a solo/hobby tool: only 3 UJs (appropriately light — no UJ-density inflation), but FR-level testable-consequence discipline throughout (appropriately heavy, since story generation depends on it). Brownfield accuracy is good where it matters most — FR-37's "not yet implemented" note cites the actual file (`BudgetAlertDetector.cs`); FR-32's D-34-sourced FR-56 cites `KpiCalculator.cs`'s `WasMeterReset`. Release framing (§6) correctly separates original-PRD scope (Releases 1–2) from later, differently-sourced additions (Release 3 from retro, Release 4 from architecture review/brainstorming), so a downstream reader isn't misled about provenance.

No findings.

## Mechanical notes

- **Glossary gap:** "budget kWh anchor" undefined (see Downstream usability finding above) — the one real drift found; no case/plural/synonym drift detected elsewhere in a 21-term glossary checked against FR usage.
- **ID continuity:** Clean. FR-1 through FR-56 each appear exactly once; no gaps, no duplicates, despite five distinct edit rounds (Release 1–4 plus this validation-reconciliation pass) inserting IDs into thematic sections out of numeric order.
- **Assumptions Index roundtrip:** Cannot fully verify in the rubric's expected sense because the PRD's convention departs from it — there are no inline `[ASSUMPTION: …]` tags anywhere in the body; all 11 assumptions are declared once, only in §11. Every §11 entry does trace to real content elsewhere in the PRD (checked A-2 through A-11), so nothing is orphaned in the index — the gap is the missing inline half of the roundtrip, not a mismatch (see Scope honesty finding).
- **No `[NOTE FOR PM]` tags anywhere in the document.** Deferred-decision content that would typically carry this tag instead lives inline within the relevant FR's Consequences (FR-37's implementation-gap note) or in `.decision-log.md`'s Open Items (OI-1). Functionally equivalent for a solo developer-owner PRD, but a reader scanning specifically for `[NOTE FOR PM]` markers will find none despite real open items existing.
- **UJ protagonist naming:** Clean — all three UJs open with "The developer-owner," carrying context inline; no floating UJs.
- **Required sections:** All present for a chain-top, single-operator capability spec (Vision, Target User incl. JTBD/Non-Users/UJs, Glossary, Features/FRs, Non-Goals, MVP Scope, Success Metrics, Cross-Cutting NFRs, Platform, Open Questions, Assumptions Index).
