# Reconciliation Report — validation-report.md findings vs. prd.md / .decision-log.md

_Run: 2026-08-07 — cross-checks all 17 findings from `validation-report.md` against the current `prd.md` text and the claimed fix in `.decision-log.md` (D-31–D-46, OI-1)._

Legend: [H]=High, [M]=Medium, [L]=Low, matching the original severity buckets.

---

## Confirmed fixed (13 of 17)

- **[H] SPEC.md governance clause stale for Release 3/4** — §0 (line 12) now explicitly scopes "SPEC governs" to Release 1/2 only, naming Release 3/4 as PRD-authoritative until SPEC.md is reconciled. Matches D-42.
- **[H] Contract start date required/optional contradiction** — FR-4 and FR-6 consequences (lines 126, 140) now state the field defaults to Onboarding-completion date when omitted, so it's never actually left unset, reconciling with FR-10's "required." Matches D-32.
- **[H] FR-37 reintroduces Tariff-noise FR-14 was designed to avoid** — FR-37 (lines 480–488) fully redefined around a frozen "budget kWh anchor" derived from planned annual spend; worked example (€1,200/€0.35/€10 → 3,085.7 kWh) checks out arithmetically; explicitly notes the Tariff-price-alone non-trigger case. Matches D-37. Implementation gap (`BudgetAlertDetector.cs` still euro-vs-euro) is honestly flagged inline and in OI-1, not concealed.
- **[H] Meter-reset chart indicator has no FR** — New FR-56 (lines 257–262) added, precedence over FR-17 spike styling stated explicitly, cross-references FR-8's separate warning purpose, added to §6.3 Release 3 list (line 643). Matches D-34.
- **[M] "Planned annual spend" absent from Glossary** — §3 entry added (line 78), consistent with FR-4/FR-7/FR-37 usage. Matches D-43.
- **[M] Decision-log citations resolve to unrelated entries** — §10 Q-3/Q-4/Q-6 (lines 738–740) now cite D-22/D-23/D-24, the actual matching entries. Matches D-31. (See residual note under Gaps — the fix is real but incomplete relative to its own "disambiguate the numbering scheme" clause.)
- **[M] Undocumented decimal-precision validation** — FR-8 consequence added (line 164): >4 decimal places rejected pre-storage. Matches D-33.
- **[M] Reconciliation invariant asserted as fact** — FR-27 consequence added (line 353): negative Residual is possible, unclamped, displayed as a data-quality signal. Matches D-35. No conflict introduced with FR-33 ("Residual always shown, including zero").
- **[M] No-notification spike detection vs. core promise** — FR-17 (line 250) now carries explicit rationale (protects SM-C2, routes through UJ-3 Insights-review habit). Matches D-40; SM-C2 cross-reference verified to exist (§7, line 677).
- **[M] Date-aware attribution vs. interpolation interaction unspecified** — FR-53 consequence added (line 419) specifying pipeline ordering (interpolation resolved at import time; attribution reads the resulting daily series by calendar date) and the boundary-day case. Matches D-36.
- **[L] Register drift in Release 4 FRs** — Accepted as-is per original "no fix required" option; D-46 explicitly records this as accepted, no PRD change needed.
- **[L] "Hub-free" literally false** — §1 (line 22) reworded to exclude the app's own Azure hosting from the claim. Matches D-38.
- **[L] Household-size presets uncited** — §11 [A-11] added (line 762), explicitly marks presets as an unsourced PM judgment call. Matches D-41.

## Findings still counted "fixed" but with a caveat — see Gaps

- **[L] Multi-user architecture asserted with no FR** — §1 (line 22) genuinely softened ("architecturally accommodated... not a committed roadmap item"). Core finding is resolved. D-39's claim that §9 was *also* edited does not check out — see Gaps.

---

## Gaps found (3 of 17 findings + 3 additional issues surfaced by cross-checking)

### 1. [H] Decision log staleness for Release 3/4 — not actually fixed

**What's wrong:** The finding was "log's last entry (D-30) predates every FR-44 through FR-55 decision by weeks; those decisions exist only as inline provenance notes." Its two suggested remedies were: (a) append D-31+ entries documenting the actual Release 3/4 decisions themselves, or (b) rescope §10's "full decision history" claim.

Neither happened. D-31 through D-46 exclusively document *this validation-reconciliation pass* (2026-08-07) — none of them retroactively log why FR-44–47, FR-52–56 were originally added. §10 (line 734) still reads "All open questions resolved. Full decision history in `.decision-log.md`" — unchanged, unscoped. The gap this finding identified (decision log silent on Release 3/4's origin decisions) still exists after the reconciliation pass.

**Where:** `.decision-log.md` (no new entries pre-dating D-31); `prd.md` §10 line 734.

**Compounding issue:** D-46 states "All findings from `validation-report.md`... triaged: applied as PRD updates D-31 through D-45, except FR-37's implementation work... and the register-drift... finding." This is an overclaim — this finding is a third, silent exception not disclosed in D-46's own exception list.

**Suggested correction:** Either add decision-log entries (even backdated, clearly marked as retroactive) capturing the Epic 8/9/12 rationale for FR-44–47/52–56, or edit §10's line to read "Full decision history from 2026-06-20 onward in `.decision-log.md`; Release 3/4 feature rationale is captured in the epic files, not this log" — and correct D-46's exception list to admit this gap rather than imply full coverage.

### 2. [M] Success Metrics note (D-45) doesn't cover the full Release 3/4 FR set

**What's wrong:** The original finding was that 12 of 46 FRs (loosely "FR-44–55") lack Success Metric coverage. The fix (§7 note, line 679, added per D-45) only names FR-44–47 and FR-55 as "process/quality fixes with no dedicated Success Metric by design," and separately defers FR-53. It says nothing about **FR-52** (device existence window) or **FR-54** (period total summary) — both Release 4, both functionally significant, neither covered by any SM — nor about the newly-added **FR-56** (meter-reset indicator, also uncovered). A reader relying on §7's note to believe "every FR without an SM has been accounted for" would be wrong for three FRs.

**Where:** `prd.md` §7, line 679 (the note added by D-45); §6.4 (FR-52, FR-54) and §6.3 (FR-56) for the omitted FRs.

**Suggested correction:** Extend the §7 note to explicitly include FR-52, FR-54, and FR-56 in the "process/quality, no dedicated SM by design" bucket (or justify why they're different from FR-44–47/55 and deserve future SM coverage).

### 3. [M] "status: final" vs. four rounds of post-finalization amendment — missed entirely

**What's wrong:** This finding has no corresponding D-3x entry in the reconciliation batch, and no PRD change addresses it. Frontmatter (lines 1–5) still reads `status: final`, `updated: 2026-08-07` — the same pattern already used at D-21 (2026-06-20 finalization) and D-30 (2026-06-21 update), i.e., bumping the date without ever discussing whether "final" is still the right word after Release 3, Release 4, and now a validation-reconciliation pass. Neither of the finding's two suggested remedies (change status, or log each change as a formal revision) was newly applied here — the "log each change" pattern already existed before this finding was raised, so nothing changed in response to it.

**Where:** `prd.md` frontmatter, lines 1–5.

**Suggested correction:** Either change `status: final` to something like `status: living` / `status: final (amended)`, or add an explicit decision-log entry (e.g., a D-4x) that consciously re-affirms "final" as an intentional choice and states the rationale (e.g., "final" means "finalized for implementation," not "immutable"), rather than letting the date bump imply resolution.

### 4. [Bonus — new inconsistency from the FR-37 rewrite] SM-5's "Validates FR-37" mapping now reads as a mismatch

**What's wrong:** Not one of the 17 findings, but flagged per the task's instruction to check every FR-37 reference. §7 SM-5 (line 671): "Rolling monthly projection is within 10% of the eventual annual invoice... Validates FR-37." This description — a €-denominated projection-vs-actual-invoice accuracy metric — fits **FR-43** (invoice deviation hint, which explicitly computes "the implied euro difference at the current Tariff," line 508) far better than the newly kWh-anchored FR-37, which is now a threshold/alert mechanism comparing projected kWh against a frozen kWh anchor, not a "how close is the projection to the eventual invoice" accuracy measure. This mismatch likely pre-existed the FR-37 rewrite in weaker form, but the rewrite (moving FR-37 further from euro-denominated invoice comparison) makes the SM-5→FR-37 mapping less defensible than before.

**Where:** `prd.md` §7 SM-5, line 671.

**Suggested correction:** Re-point SM-5 to validate FR-43 (or both FR-37 and FR-43), or reword SM-5 to describe what FR-37 actually measures (budget-kWh-anchor breach lead time / false-positive rate under Tariff changes).

### 5. [Bonus — residual issue from D-31's fix] Dual "D-N" numbering scheme still unresolved

**What's wrong:** The original finding's fix asked to "correct the citations and disambiguate the 'D-N' numbering scheme." D-31 corrected the §10 citations (real fix, counted above), but the underlying confusion remains: the "Source" column of D-22/D-23/D-24 (lines 40–42) still reads "UX session D-13," "UX session D-17/D-24," "UX session D-18" — these are a *different document's* internal decision IDs that collide with this log's own unrelated D-13 (Eve Home timestamps) / D-17 (locale-formats.md) / D-18 (§1 Vision). A future reader following those Source citations without knowing there are two independent "D-N" schemes could easily land on the wrong entry again, exactly as the original finding described.

**Where:** `.decision-log.md`, lines 40–42 (Source column of D-22/D-23/D-24).

**Suggested correction:** Rename the external document's citation format (e.g., "UX-D-13" or "UX output §Q-3, internal ref D-13") so it's visually distinct from this log's own D-N sequence.

### 6. [Bonus — decision-log internal accuracy] D-39 overclaims its own scope

**What's wrong:** D-39 states its section reference as "§1, §9," claiming both were edited to soften the multi-user promise. Only §1 (line 22) shows the claimed softening language ("architecturally accommodated... not a committed roadmap item"). §9's Platform section (line 728: "Single-tenant per authenticated user... No multi-user management UI.") is unchanged and doesn't carry any correlating new wording — it already stated the restrictive position and required no edit. This is a minor decision-log accuracy issue (a section reference that doesn't check out against the diff), of the type the task asked to watch for.

**Where:** `.decision-log.md`, D-39; `prd.md` §9, line 728.

**Suggested correction:** Amend D-39's section reference to "§1" only (or add a one-clause note that §9 needed no change because it was already consistent).

---

## Summary count

- 17 findings total.
- 13 confirmed properly fixed with PRD text matching the decision-log claim.
- 3 findings have real gaps (1 High — decision log/​§10 staleness never actually fixed despite D-46 implying full coverage; 2 Medium — incomplete SM note, and the frontmatter-status finding missed entirely).
- 1 Low finding (multi-user) is substantively fixed but its decision-log entry (D-39) overclaims which sections were touched.
- 3 additional issues surfaced during cross-checking, not among the original 17: SM-5's FR-37 mapping now reads as a mismatch after the redefinition; the dual "D-N" numbering-scheme confusion D-31 was supposed to disambiguate is only half-resolved; D-39's stated section scope doesn't match its actual diff.
- FR ID continuity re-verified independently: FR-1 through FR-56 all present, unique, no gaps — confirmed correct.
