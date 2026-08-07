# Validation Report — energy-tracker

- **PRD:** `_bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md`
- **Rubric:** `.claude/skills/bmad-prd/assets/prd-validation-checklist.md`
- **Run at:** 2026-08-07T00:00:00
- **Grade:** Fair

## Overall verdict

This is a mature, disciplined PRD: FR/consequence coupling is close to exhaustive (no vague adjectives found anywhere in 55 FRs), trade-offs are named with what was given up, and the single-persona/light-UJ shape matches a solo developer-owner tool built for downstream AI-agent consumption. What the rubric flags as the main risk is not the PRD's own prose but its two traceability companions falling behind: `.decision-log.md` stopped at 2026-06-21 and never logged the Release 3/4 decisions (FR-44 through FR-55) the PRD narrates inline, and `SPEC.md` — declared canonical in §0 — still only covers Release 1/2 and is silent on everything shipped since.

The adversarial pass materially shifts the picture from "traceability lag" to "the drift has produced real content defects." It surfaces a genuine required/optional contradiction on the one field FR-11/FR-13 call load-bearing (contract start date), a specification bug where FR-37's euro-denominated budget alert reintroduces the exact Tariff-price noise FR-14 was explicitly designed to avoid, and at least two shipped, user-facing behaviors (the Epic 9 meter-reset chart indicator, decimal-precision input rejection) with zero corresponding FR — meaning the PRD's own claim to be the "single source of requirements truth" is measurably false for parts of the shipped product. None of this rises to a broken dimension or a critical finding on its own, but combined with the rubric's traceability findings it argues for a dedicated reconciliation pass before this PRD is trusted as an unread source of truth for a new repo.

## Dimension verdicts

- Decision-readiness — adequate
- Substance over theater — strong
- Strategic coherence — adequate
- Done-ness clarity — strong
- Scope honesty — strong
- Downstream usability — adequate
- Shape fit — strong

## Findings by severity

### Critical (0)

None.

### High (5)

**[Decision-readiness]** — Decision log stops before Release 3/4 (§10, §11 vs `.decision-log.md`)
§10 says "Full decision history in `.decision-log.md`," but the log's last entry (D-30) predates every FR-44 through FR-55 decision by weeks; those decisions exist only as inline provenance notes in §6.3/§6.4/feature descriptions.
Fix: Append D-31+ entries for the Release 3 and Release 4 decisions, or rescope §10's claim to the original PRD finalization.

**[Downstream usability]** — SPEC.md governance clause stale for Release 3/4 (§0 vs `SPEC.md`)
The canonical-contract statement in §0 has no referent for FR-44 through FR-55; SPEC.md was never updated past the original two releases.
Fix: Update SPEC.md's Capabilities list to cover Release 3/4, or scope §0's governance clause explicitly.

**[Adversarial]** — Contract start date required/optional contradiction (FR-4, FR-6 vs FR-10, §4.4, FR-11, FR-13)
FR-4/FR-6 call the Onboarding Tariff's contract start date optional; FR-10/§4.4 call it required and the sole anchor for FR-11/FR-13 costing logic — no resolution for the missing-field case.
Fix: Align FR-4/FR-6 with FR-10's "required" rule, or define fallback behavior in FR-11/FR-13 when the field is absent.

**[Adversarial]** — Budget Pressure Alert reintroduces the Tariff-noise problem FR-14 was designed around (FR-37 vs FR-14)
FR-37 compares a euro-denominated rolling cost projection against a static euro "planned annual spend," so a mid-year Tariff price increase alone can fire a false budget-pressure alert — the exact noise FR-14 anchors to kWh specifically to avoid.
Fix: Anchor FR-37 to kWh-vs-baseline consistent with FR-14/FR-43, or explicitly state the accepted trade-off.

**[Adversarial]** — Shipped meter-reset chart indicator has no corresponding FR (Epic 9 Story 9.8 vs FR-16/FR-17)
The trend chart ships a third visual state (meter-reset indicator) that FR-16/FR-17 don't describe — the PRD's own account of the product is stale relative to what shipped.
Fix: Add an FR (or amend FR-16/FR-17) documenting the meter-reset visual state and its trigger condition.

### Medium (8)

**[Strategic coherence]** — Success Metrics untouched by Release 3/4 (§7 vs §6.3/§6.4)
SM-3 validates FR-27/FR-32 only; 12 of 46 FRs (FR-44–55) have no Success Metric coverage at all.
Fix: Extend SM-3 for FR-53's temporal correctness, or note that Release 3/4 items are process/quality fixes not measured by new SMs.

**[Downstream usability]** — "Planned annual spend" absent from Glossary (§3 vs FR-4, FR-7, FR-37, §10)
A first-class, multiply-referenced domain concept with no formal §3 definition.
Fix: Add a §3 entry analogous to Annual kWh Baseline.

**[Adversarial]** — Decision-log citations resolve to unrelated entries (§10 Q-3/Q-4/Q-6 vs `.decision-log.md` D-13/D-17/D-18)
The cited IDs point to unrelated decisions (Eve Home timestamps, locale-formats.md authority, §1 Vision framing); the actual matches appear to be D-22/D-23/D-24, with two numbering schemes never disambiguated.
Fix: Correct the citations and disambiguate the "D-N" numbering scheme.

**[Adversarial]** — Undocumented decimal-precision input validation (Epic 9 Stories 9.7/9.11 vs FR-8, FR-24–31)
The system rejects excess-precision kWh input in production; no FR specifies any decimal-place constraint.
Fix: Add a Consequences clause to FR-8 (or a new FR) specifying accepted precision and rejection behavior.

**[Adversarial]** — Reconciliation invariant asserted as fact, not a handled case (FR-27 vs §1, FR-33)
"Attributed kWh never exceeds Main Meter total" is stated unconditionally, but measurement error can produce a negative Residual with no stated handling.
Fix: Add a Consequences clause specifying handling of over-reporting (clamp, flag, or surface a data-quality warning).

**[Adversarial]** — "status: final" doesn't reflect four rounds of post-finalization amendment (frontmatter vs §6)
Frontmatter has said "final" since 2026-06-20 through at least four subsequent rounds of scope change (Release 3, Release 4, two same-day correct-course insertions).
Fix: Update frontmatter to reflect living status, or log each post-finalization change as a formal revision.

**[Adversarial]** — No-notification spike detection sits against the product's core emotional promise (FR-17 vs §2.1, §1)
FR-17's silent, chart-only spike encoding sits against the stated "stop being surprised by my invoice" JTBD, with no rationale for the asymmetry.
Fix: Add a lightweight non-intrusive notification, or state explicitly why silent encoding is intentional.

**[Adversarial]** — Date-aware attribution vs. interpolation interaction is unspecified (FR-52/FR-53 vs FR-26)
Neither FR states whether date-gating happens before or after interpolation fills a gap, nor how a boundary-spanning interpolated day should be split.
Fix: Add a Consequences clause specifying the ordering between attribution and interpolation.

### Low (4)

**[Downstream usability]** — Register drift toward code identifiers in Release 4 FRs (FR-50/51/52/53/55)
Not wrong, but later FRs read more like an engineering spec than earlier Glossary-first ones.
Fix: None required unless a UX-only audience is added.

**[Adversarial]** — "Hub-free" principle is literally false of the product as worded (§1)
§1's "no cloud subscription" claim is contradicted by the product running entirely on paid Azure services.
Fix: Reword to explicitly exclude the app's own hosting from the claim.

**[Adversarial]** — Household-size kWh presets are uncited and inconsistently scaled (FR-5)
Unlike every other numeric constant in the document, FR-5's presets have no citation and an uneven progression.
Fix: Cite a source, or mark explicitly as a placeholder assumption in §11.

**[Adversarial]** — Multi-user architecture asserted with no FR ever exercising it (§9, NFR-2, §1 vs §2.2, §5)
Tenant isolation and "additional users" are promised architecturally with no registration/invitation FR after 13 shipped epics of single-owner deployment.
Fix: Add a placeholder FR/Non-Goal deferring multi-user explicitly, or drop the promise if unplanned.

## Mechanical notes

- Terminology drift: "v1/v2" vs "Release 1-4" — §2.2, §5, §9, §11 still use pre-Release-3/4 vocabulary. Flagged independently by both reviewers.
- Glossary case/term consistency is otherwise clean.
- ID continuity confirmed: FR-1 through FR-55 present exactly once each, no gaps or duplicates.
- Assumptions Index roundtrip: no inline `[ASSUMPTION: ...]` tags remain; §11 is now a historical record, not a live checkable index.
- Epics without FR mapping (Epic 9, 11, 13 — hardening/bugfix work) are, by design, absent from §6's Release table; a PRD-only reader has no way to know these shipped epics exist.
- The SPEC-governs-on-conflict rule (§0) has no maintained tripwire — nothing detects when SPEC.md and the PRD actually diverge, which is how the Release 3/4 staleness went unnoticed.

## Reviewer files

- `review-rubric.md`
- `review-adversarial-general.md`
