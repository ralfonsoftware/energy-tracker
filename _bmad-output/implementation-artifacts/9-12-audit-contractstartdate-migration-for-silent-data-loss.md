---
baseline_commit: 2555a6b3ee0c7f35ae0ef8e6c6dd45a4e41039cf
---

# Story 9.12: Audit ContractStartDate Migration for Silent Data Loss

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the team maintaining this app,
I want confirmation that no flat's tariff history was silently altered by the ContractStartDate consolidation migration,
so that a known-unaudited risk doesn't sit open indefinitely.

## Rescope Note — Read Before Starting

**This story cannot be completed as its AC is literally written, and no code or SQL query is expected.** The epic's AC assumes a "backup or point-in-time restore, if available" exists to compare pre-migration `EffectiveDate` history against current `ContractStartDate` values. That assumption has been checked and resolved to **not available, and not recoverable**:

- The SQL Database (`infra/main.bicep:174-186`, `sqlDatabase` resource) runs on the **Basic** tier (`sku.tier: 'Basic'`, line 180). Per Microsoft's own documentation on [Azure SQL Database automated backups](https://learn.microsoft.com/azure/azure-sql/database/automated-backups-overview?view=azuresql#backup-retention), Basic-tier point-in-time restore (PITR) retention is capped at a **maximum of 7 days** — unlike Standard/Premium (1–35 days), this ceiling cannot be raised for Basic databases.
- No Long-Term Retention (LTR) policy exists anywhere in `infra/main.bicep` (grepped for `LongTermRetention`/`backupLongTermRetentionPolicies` — zero hits) or elsewhere in the repo, so there is no secondary Azure-native backup path beyond the 7-day PITR window.
- Migration `20260703114416_ConsolidateTariffContractStartDate.cs` ran on **2026-07-03**. As of this story's creation date (**2026-07-19**), that's **16 days ago** — already past even the theoretical maximum 7-day Basic-tier PITR window, regardless of what retention setting was actually configured at the time. The window to inspect the database's pre-migration state has already closed and cannot be reopened.
- **Decision (Ralf, 2026-07-19):** given the above, do not attempt any SQL query against production, and do not attempt manual reconciliation via personal recall. This story's job is solely to **document that the audit is technically impossible, explain why, and close the `deferred-work.md` item as an accepted, unauditable residual risk** — not to produce a "no data loss confirmed" finding, which would overclaim certainty that doesn't exist.
- **No code changes. No SQL execution. No dev-agent access to production required.** This is a documentation-only story, structurally similar in shape (not content) to Story 9.11's test-only rescope.

## Acceptance Criteria

1. **Given** migration `20260703114416_ConsolidateTariffContractStartDate.cs` backfilled `ContractStartDate` from `EffectiveDate` only `WHERE ContractStartDate IS NULL`, silently discarding the old `EffectiveDate` value for any tariff that already had a non-null `ContractStartDate` before the migration (`deferred-work.md` — the "Deferred from: code review of cost-gap false-positive fix (2026-07-04)" section, `EffectiveDate`→`ContractStartDate` migration entry), **when** this story is implemented, **then** a new investigation document at `_bmad-output/implementation-artifacts/investigations/contractstartdate-data-loss-audit-investigation.md` records: the exact backup/PITR unavailability finding (Basic tier 7-day cap, 16+ elapsed days, no LTR), why no query or restore was attempted, and that a byte-for-byte pre/post migration comparison is now permanently impossible — not merely "not yet done."

2. **Given** the cost-gap investigation (`_bmad-output/implementation-artifacts/investigations/cost-gap-badge-mismatch-investigation.md`, Deduction 2 and Open Question 4) already checked **the one real affected Flat's** tariff for this exact defect class and found its `ContractStartDate` self-consistent with 100% real reading coverage — ruling out this specific flat as affected, though it did not (and could not) rule out any other flat, **when** the new investigation document is written, **then** it cites this prior finding accurately as the only concrete evidence available, explicitly scoped to that one flat, and does not extend or generalize it to any other flat's data.

3. **Given** the audit is closed as an accepted, permanently-unauditable residual risk rather than a resolved-clean finding, **when** this story completes, **then** `deferred-work.md`'s entry (the "Deferred from: code review of cost-gap false-positive fix (2026-07-04)" section, `EffectiveDate`→`ContractStartDate` migration line, currently reading "**→ Promoted to Story 9.12 (Epic 9)**") is updated to mark the item closed, replacing the promotion note with a resolution note pointing at the new investigation document and stating the outcome is "accepted risk, unauditable" — not "fixed" or "confirmed clean."

## Tasks / Subtasks

- [x] Task 1: Write the investigation document (AC: #1, #2)
  - [x] Create `_bmad-output/implementation-artifacts/investigations/contractstartdate-data-loss-audit-investigation.md`.
  - [x] Document the migration's exact backfill behavior and the specific data-loss scenario it can cause (quote `migrationBuilder.Sql("UPDATE Tariffs SET ContractStartDate = EffectiveDate WHERE ContractStartDate IS NULL")` from `api/Data/Migrations/20260703114416_ConsolidateTariffContractStartDate.cs:17`).
  - [x] Document the backup/PITR unavailability finding verbatim from this story's Rescope Note (Basic tier 7-day cap, no LTR, 16+ elapsed days) — this is settled fact as of story creation, do not re-derive it from scratch or second-guess it during implementation.
  - [x] Cite the cost-gap investigation's Deduction 2 finding for the one real affected Flat, scoped exactly as described in AC2 — do not imply it covers any flat beyond the one it actually examined.
  - [x] State the residual risk plainly: any flat (other than the one already checked) that had a non-null `ContractStartDate` before 2026-07-03 has had its original `EffectiveDate` permanently and unrecoverably discarded; whether this reflects an actual user-facing defect for any such flat cannot be determined now or in the future through this migration's data alone.
  - [x] Do not attempt, suggest, or scaffold any SQL query, backup restore, or Azure CLI command against the production database — this was explicitly decided against for this story.

- [x] Task 2: Close the deferred-work.md item (AC: #3)
  - [x] Locate the entry in `_bmad-output/implementation-artifacts/deferred-work.md`, under "## Deferred from: code review of cost-gap false-positive fix (2026-07-04)", the bullet currently ending "**→ Promoted to Story 9.12 (Epic 9)**".
  - [x] Replace the promotion note with a closure note: reference the new investigation document, and state the outcome as "accepted risk — audit window closed, permanently unauditable" (not "resolved" or "no data loss found").

### Review Findings

**Note on AC #2's premise:** AC #2 (and this story's original Rescope Note/Task 1 text above) assumed the cost-gap investigation's prior check of the one real flat provided positive evidence that flat was unaffected. Code review found this assumption false — see below. AC #2's *citation* of that prior finding is accurate; its characterization of what that finding *proves* was not. The deliverable (the investigation document) has been corrected; this AC text is left as historical record of the premise this story started from, per this project's convention of not rewriting spec text after the fact.

- [x] [Review][Patch] Deduction 2's "one flat confirmed unaffected" claim is circular and false [investigations/contractstartdate-data-loss-audit-investigation.md] — fixed: `KpiCalculator.cs:158-163`'s `ResolveTariff` only requires `ContractStartDate <= readingDate`, so the exact value is unobservable in `DailyAvgCost`/`CoveredDays`/`HasCostGap` as long as it precedes all readings (true here regardless of what the discarded `EffectiveDate` was); additionally the "observed" screenshot being reproduced was captured 2026-07-04, one day *after* the 2026-07-03 migration, using the same post-migration `ContractStartDate` — comparing current state to current state, not an independent check. Rewrote Deduction 2, the Residual Risk Statement, and Recommended Next Steps to state plainly that no flat — including this one — has been cleared.
- [x] [Review][Patch] Wrong `infra/main.bicep` line-range citation [investigations/contractstartdate-data-loss-audit-investigation.md, 9-12-audit-contractstartdate-migration-for-silent-data-loss.md] — fixed: `sqlDatabase` resource is lines 174-186, not 174-197 (187-197 is the unrelated `keyVault` resource). Corrected in both files, all occurrences.
- [x] [Review][Patch] Migration SQL quote lacked a file:line citation, unlike the bicep citation's exact line numbers [investigations/contractstartdate-data-loss-audit-investigation.md] — fixed: added `:17` and a note that the quote was verified verbatim against the file, not paraphrased.
- [x] [Review][Patch] "Side Findings" claimed this was the "second" Basic-tier backup-retention limitation but cited an unrelated (unbatched-migration) limitation as the "first" [investigations/contractstartdate-data-loss-audit-investigation.md] — fixed: corrected to describe it as a distinct Basic-tier constraint, not a recurrence.
- [x] [Review][Patch] Timeline framing implied the closed audit window was purely Basic-tier's structural limit [investigations/contractstartdate-data-loss-audit-investigation.md] — fixed: added acknowledgment that the item sat in `deferred-work.md`'s backlog for 15 days after being identified (2026-07-04) before this story picked it up (2026-07-19), which also contributed.
- [x] [Review][Patch] `deferred-work.md`'s closure note had a redundant duplicate migration-file-path citation, and repeated the false "one flat confirmed unaffected" claim — fixed: removed the duplicate citation; updated the claim to state the risk applies to every `Tariff` row, including that one.
- [x] [Review][Patch] Deduction 1 didn't address IaC-vs-live-config drift (bicep says Basic tier, but is that still true live?) [investigations/contractstartdate-data-loss-audit-investigation.md] — fixed: added that Basic tier's 7-day PITR ceiling is enforced by Azure at the SKU level regardless of configuration source, so no drift scenario could exceed it.
- [x] [Review][Defer] The "one real affected Flat" / single-tenant framing (inherited from Story 4.4 and the cost-gap investigation) was not independently re-verified as accurate — deferred, pre-existing framing from prior stories, and verifying it would require exactly the production database access this story was explicitly told not to use.
- [x] [Review][Decision] Whether to allow one narrow, read-only `COUNT`-style query to scope the number of potentially-affected `Tariff` rows, given Deduction 2 means the residual risk pool is larger than originally thought — resolved: Ralf chose no query, keep the zero-production-access boundary clean (2026-07-19). Reflected in the investigation document's Residual Risk Statement.

## Dev Notes

- **No production code, no SQL, no migration changes.** `api/Data/Migrations/20260703114416_ConsolidateTariffContractStartDate.cs` is historical and must not be edited (standing project convention — migrations under `api/Data/Migrations/` are never hand-edited after the fact, per `project-context.md`'s EF Core Migrations rule).
- **Do not re-verify the Basic-tier 7-day PITR ceiling by checking live Azure state.** This has already been confirmed against Microsoft's own documentation and the committed Bicep (`infra/main.bicep:174-186`, no retention override present, so the platform default applies — 7 days, the max Basic allows). Re-checking live Azure Portal/CLI state is unnecessary and out of scope; the elapsed-time math alone (16 days > 7-day max) already makes the window closed regardless of what was configured.
- **Do not touch the live production database.** Per this project's established convention, Ralf handles anything touching live Azure directly; this story does not require it anyway, since Task 1/2 are pure documentation.
- **This is the second consecutive Epic 9 story that rescopes an epic-stated AC after the underlying premise no longer held** (9.11 found the "divergence bug" didn't exist; 9.12 finds the "backup exists" assumption doesn't hold). Both are handled the same way: state plainly in a Rescope Note what changed and why, then deliver the narrower, honest scope — not the literal epic text.

### Project Structure Notes

- One new file: `_bmad-output/implementation-artifacts/investigations/contractstartdate-data-loss-audit-investigation.md`, following the existing naming/location convention of every other file in that folder (e.g., `cost-gap-badge-mismatch-investigation.md`).
- One existing file modified: `_bmad-output/implementation-artifacts/deferred-work.md` (close out the promoted item).
- No `api/`, `client/`, or `api.Tests/` files touched by this story.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-9-unified-save-affordance-fix-and-pre-epic-10-hardening.md#Story 9.12] — original epic AC text (superseded in scope by this story's Rescope Note; see above for why).
- [Source: _bmad-output/implementation-artifacts/deferred-work.md] — "Deferred from: code review of cost-gap false-positive fix (2026-07-04)" section, the entry this story closes.
- [Source: _bmad-output/implementation-artifacts/investigations/cost-gap-badge-mismatch-investigation.md#Deduction 2, #Open Question 4] — the one prior, narrowly-scoped check of this exact defect class for the one real affected Flat.
- [Source: api/Data/Migrations/20260703114416_ConsolidateTariffContractStartDate.cs:17] — the exact backfill SQL statement this audit concerns.
- [Source: infra/main.bicep:174-186] — `sqlDatabase` resource; `sku.tier: 'Basic'` (line 180); no LTR policy resource present anywhere in the file.
- [Microsoft Learn: Automated backups in Azure SQL Database — Backup retention](https://learn.microsoft.com/azure/azure-sql/database/automated-backups-overview?view=azuresql#backup-retention) — confirms Basic-tier PITR is configurable only 1–7 days, unlike Standard/Premium's 1–35.
- [Source: _bmad-output/implementation-artifacts/9-11-kwhvalue-precision-divergence-fix.md] — previous story in this epic; same "Rescope Note" pattern used here for a different reason (verified-false premise vs. expired-window premise).

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- No code, build, or test commands applicable — this is a documentation-only story (no `api/`, `client/`, or `api.Tests/` files touched). Verified by re-reading the final story file: File List contains only Markdown.

### Completion Notes List

- Confirmed the Rescope Note's premise firsthand while writing the investigation doc: `infra/main.bicep:174-186` shows `sku.tier: 'Basic'` with no LTR policy resource anywhere in the file, and Microsoft's own documentation confirms Basic-tier PITR is capped at 7 days (vs. 1–35 for Standard/Premium) — combined with the migration's 2026-07-03 date and this story's 2026-07-19 audit date (16 days elapsed), the restore window is provably closed, not merely unconfirmed.
- Wrote `_bmad-output/implementation-artifacts/investigations/contractstartdate-data-loss-audit-investigation.md` following this project's established investigation-doc template (same section structure as `cost-gap-badge-mismatch-investigation.md`), closing the case as "accepted risk, unauditable" rather than "no data loss confirmed" — per the story's explicit instruction not to overclaim certainty the evidence doesn't support.
- Closed the `deferred-work.md` entry (cost-gap false-positive fix review, 2026-07-04 section) with a strikethrough + resolution note pointing at the new investigation doc, explicitly avoiding "fixed"/"resolved clean" language per the story's AC #3.
- No SQL query, backup restore, or Azure CLI command was run or scaffolded against production, per the story's explicit instruction and Ralf's 2026-07-19 decision.
- **Post-review correction (2026-07-19):** the initial pass cited `cost-gap-badge-mismatch-investigation.md`'s Deduction 2 as confirming the one real affected flat's tariff was unaffected. Code review (Blind Hunter + Edge Case Hunter, independently) found this false: `KpiCalculator.ResolveTariff` (`KpiCalculator.cs:158-163`) is insensitive to the exact `ContractStartDate` value as long as it precedes all readings, and the "observed" figures being reproduced were themselves captured one day *after* the migration, using the same post-migration value — the comparison has no power to detect this defect class. Corrected the investigation document and `deferred-work.md`'s closure note accordingly: no flat, including this one, is actually cleared. See Review Findings above.

### File List

- `_bmad-output/implementation-artifacts/investigations/contractstartdate-data-loss-audit-investigation.md` (new)
- `_bmad-output/implementation-artifacts/deferred-work.md` (modified)

## Change Log

- 2026-07-19: Closed the `EffectiveDate`→`ContractStartDate` migration audit as an accepted, permanently-unauditable risk (Basic-tier 7-day PITR ceiling + 16 elapsed days, no LTR configured). Documented in a new investigation file; `deferred-work.md` entry updated accordingly. No production code, SQL, or migration changes.
- 2026-07-19 (code review): Corrected the investigation document's central Deduction 2 — the one flat previously thought cleared by a prior investigation is not actually distinguishable from the rest (the check it relied on has no power to detect this defect class). Also fixed a wrong `infra/main.bicep` line-range citation, added a missing file:line citation, corrected a mislabeled "second limitation" claim, and softened timeline framing. No scope change to Task 1/2 or the "no production access" boundary — reaffirmed by Ralf during review.
