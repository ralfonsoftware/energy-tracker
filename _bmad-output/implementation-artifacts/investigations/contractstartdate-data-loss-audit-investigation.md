# Investigation: Audit of `ContractStartDate` migration for silent tariff data loss

> **Folded into `architecture.md`** (2026-07-22 doc consolidation, Epic 9 retro Action Item #2): the underlying platform fact — Azure SQL Basic-tier PITR is hard-capped at 7 days, no LTR fallback unless separately provisioned — is now a permanent note in Technical Constraints & Dependencies, independent of this specific incident. This investigation itself remains closed as an accepted, unrecoverable risk (no code fix); this file remains as the historical record.

## Hand-off Brief

1. **What was asked.** Confirm whether any flat's tariff history was silently altered by migration
   `20260703114416_ConsolidateTariffContractStartDate.cs`, which backfilled `ContractStartDate` from
   `EffectiveDate` only `WHERE ContractStartDate IS NULL` — silently discarding the old `EffectiveDate`
   value for any tariff that already had a non-null `ContractStartDate` before the migration ran.
2. **Where the case stands. CLOSED — accepted risk, unauditable.** The audit this item calls for
   (compare pre-migration `EffectiveDate` history against current `ContractStartDate` values) requires
   either a database backup/point-in-time restore from before 2026-07-03, or some other independent
   record of the pre-migration values. Neither exists, and — critically — **neither can ever exist
   again**: the audit window has structurally and permanently closed (see Deduction 1). This is not a
   "not yet done" gap; it is a defect class that can no longer be investigated by any means this
   migration's own data provides.
3. **What's needed next.** Nothing further from this migration's own data. A prior, unrelated
   investigation's check of one specific flat (Deduction 2) was initially thought to provide positive
   evidence that flat was unaffected — it does not (see Deduction 2's corrected conclusion below): the
   check has no power to distinguish the true pre-migration value from any other value, so that flat
   is in the same undetermined-risk state as every other flat. No further diagnostic work is possible
   here; this document exists to close the item honestly rather than leave it silently open.

## Case Info

| Field            | Value                                                                                   |
| ---------------- | ---------------------------------------------------------------------------------------- |
| Ticket           | `deferred-work.md` — "Deferred from: code review of cost-gap false-positive fix (2026-07-04)", `EffectiveDate`→`ContractStartDate` migration entry, promoted to Story 9.12 (Epic 9) |
| Date opened      | 2026-07-04 (originally deferred); audited 2026-07-19                                     |
| Status           | Closed — accepted risk, unauditable                                                      |
| System           | Production Azure SQL Database, `energytracker-db` (Basic tier), `Tariffs` table          |
| Evidence sources | `api/Data/Migrations/20260703114416_ConsolidateTariffContractStartDate.cs`, `infra/main.bicep`, Microsoft Learn documentation on Azure SQL Database automated backups, `cost-gap-badge-mismatch-investigation.md` |

## Problem Statement

Migration `20260703114416_ConsolidateTariffContractStartDate.cs:17` (commit `686a2f9`, 2026-07-03) ran:

```sql
UPDATE Tariffs SET ContractStartDate = EffectiveDate WHERE ContractStartDate IS NULL
```

before dropping the `EffectiveDate` column entirely (verified verbatim against the migration file, not
paraphrased). This backfill rule only touches rows where `ContractStartDate` was `NULL`. For any
`Tariff` row that already had a non-null `ContractStartDate` before this migration ran, its
`EffectiveDate` value — whatever it was — was silently discarded without comparison, reconciliation,
or record. Whether that discarded value ever differed from the row's `ContractStartDate` in a way
that mattered has never been meaningfully checked for any row, including the one flat a prior
investigation looked at in passing (see Deduction 2 — that check turns out to have no power to
answer this question either).

## Evidence Inventory

| Source                                                                 | Status    | Notes                                                                 |
| ------------------------------------------------------------------------ | --------- | ---------------------------------------------------------------------- |
| `api/Data/Migrations/20260703114416_ConsolidateTariffContractStartDate.cs` | Available | Confirms the exact backfill rule and its `WHERE ContractStartDate IS NULL` scope |
| `infra/main.bicep`                                                     | Available | `sqlDatabase` resource, lines 174–186; `sku.tier: 'Basic'` at line 180; no LTR policy resource anywhere in the file |
| Microsoft Learn — [Automated backups in Azure SQL Database](https://learn.microsoft.com/azure/azure-sql/database/automated-backups-overview?view=azuresql#backup-retention) | Available | Authoritative source for Basic-tier PITR retention ceiling |
| `_bmad-output/implementation-artifacts/investigations/cost-gap-badge-mismatch-investigation.md` (Deduction 2, Finding 4) | Available | The one prior check touching this defect class — re-examined in Deduction 2 below and found to carry no discriminating evidence either way |
| A pre-2026-07-03 database backup or point-in-time restore              | **Does not exist / unobtainable** | See Deduced Conclusions — this is the central finding of this audit |

## Deduced Conclusions

### Deduction 1: The audit window has structurally and permanently closed — this is not recoverable by any means

**Based on:** `infra/main.bicep:174-186`, Microsoft Learn's Azure SQL Database backup retention documentation.

**Reasoning:** The `sqlDatabase` resource (`infra/main.bicep:174-186`) is provisioned with `sku.tier: 'Basic'`
(line 180). Per Microsoft's own documentation, Azure SQL Database point-in-time restore (PITR) retention
is configurable only between **1 and 7 days** for Basic-tier databases — unlike Standard/Premium tiers,
which allow 1–35 days. Critically, this is not merely what the committed Bicep declares: Basic tier's
7-day ceiling is enforced by Azure at the SKU level — the platform does not accept a longer retention
value for a Basic-tier database under any configuration, whether set via Bicep, the Portal, or the CLI.
So even in the hypothetical case where the live database's actual configuration had drifted from what
`infra/main.bicep` declares, no such drift could produce a retention window longer than 7 days for this
SKU. No Long-Term Retention (LTR) policy resource exists anywhere in `infra/main.bicep` (grepped for
`LongTermRetention` / `backupLongTermRetentionPolicies` — zero matches), so there is no secondary
Azure-native backup path beyond the 7-day PITR window either.

The migration ran on 2026-07-03. As of this audit (2026-07-19), 16 days have elapsed — already past
even the theoretical maximum 7-day Basic-tier PITR window, regardless of what retention value was
actually configured at the time. Note this isn't purely a platform-imposed inevitability: the underlying
data-loss risk was first identified on 2026-07-04 (one day after the migration, while at least some
restore window may still have been open) but sat in `deferred-work.md`'s backlog for 15 days before
this story picked it up — a prioritization gap contributed to the window closing, not only Basic
tier's ceiling. The point in time needed to inspect pre-migration `EffectiveDate` values (immediately
before 2026-07-03) fell out of the retention window days ago and cannot be reached by any Azure-native
restore mechanism today, regardless of how it got there.

**Conclusion:** This is not a "the audit hasn't been run yet" situation. It is a permanently closed
window — the specific evidence needed (pre-migration `EffectiveDate` values, for any tariff whose
`ContractStartDate` was already non-null) no longer exists anywhere Azure retains data, and no future
action can recover it. A "no data loss confirmed" conclusion would overclaim certainty this evidence
gap does not support.

### Deduction 2: A prior check of one flat does not actually clear it either — that comparison has no power to detect this defect

**Based on:** `cost-gap-badge-mismatch-investigation.md`, Deduction 2, Finding 4, and Investigation Backlog item #4; `api/Features/Dashboard/KpiCalculator.cs:158-163` (`ResolveTariff`).

**Reasoning:** A prior, unrelated investigation (the cost-gap badge mismatch, 2026-07-04) re-derived
`DailyAvgCost`, `WeeklyAvgCost`, and `ProjectedMonthlyCost` from the current `ContractStartDate`
(2024-10-01) and found they matched the dashboard figures the user had observed. This was originally
read (by that investigation, and initially by this one) as evidence the current `ContractStartDate` is
"correct." **On closer inspection, it is not:** two things undercut it.

First, `ResolveTariff` (`KpiCalculator.cs:158-163`) only requires `ContractStartDate <= readingDate` to
treat a reading as covered by a tariff — the *exact* value of `ContractStartDate` has no effect on
`DailyAvgCost`/`WeeklyAvgCost`/`ProjectedMonthlyCost`/`CoveredDays`/`HasCostGap` as long as it precedes
every reading, which this flat's earliest reading (2025-12-31) does regardless of whether
`ContractStartDate` is 2024-10-01 or any other earlier date. Reproducing the observed figures from
2024-10-01 would have succeeded identically had the true, undiscarded `EffectiveDate` instead been,
say, 2023-06-01 or 2024-01-15 — the check cannot distinguish between them.

Second, the "observed" screenshot being reproduced was itself captured on **2026-07-04, one day after**
the migration ran (2026-07-03) — per that investigation's own Timeline of Events. Both the "reproduction"
and the "observation" it's checked against use the same *post-migration* `ContractStartDate`. This is
not an independent before/after comparison; it compares current state to current state and will always
match, carrying no evidentiary weight about what `EffectiveDate` looked like before the migration.

**Conclusion:** This check does not clear the one real flat. It is in the same undetermined-risk state
as every other row in the `Tariffs` table. There is, in fact, **no positive evidence for any flat** —
only the absence of any way to check, for any of them, ever again (Deduction 1).

## Residual Risk Statement

Any `Tariff` row — on **any** flat, with no exceptions, since Deduction 2 found the one flat previously
thought to be checked is not actually distinguishable from the rest — that already had a non-null
`ContractStartDate` before 2026-07-03 has had its original `EffectiveDate` value permanently and
unrecoverably discarded by this migration. Whether that discarded value would have revealed a
meaningful divergence — and therefore whether any flat's historical cost/coverage figures are subtly
wrong — cannot be determined now, and cannot be determined at any point in the future, through this
migration's own data. The number of `Tariff` rows this could affect is itself unknown — no query was
run against production to count them (decision: 2026-07-19, keep this story's zero-production-access
boundary clean rather than run even a narrow, read-only scoping query). This is accepted as a closed,
unauditable residual risk rather than pursued further.

## Recommended Next Steps

None from this migration's own data — the evidence needed no longer exists. The reproduction technique
used in `cost-gap-badge-mismatch-investigation.md` (recomputing KPI figures from current DB rows and
comparing against user-observed values) is **not** a valid future sanity check for this specific defect
class, per Deduction 2 — it cannot detect a `ContractStartDate` that is merely a different-but-still-
earlier-than-all-readings date, which is exactly the shape this defect takes. No targeted diagnostic
exists for this defect class; only a pre-migration backup would have worked, and none exists.

## Side Findings

- This project's `deferred-work.md` already carries one other Basic-tier limitation note against this
  same migration — an unrelated one, about unbatched schema rewrites at scale, not backup retention
  (`deferred-work.md`, "Migration `20260703114416_...` is a single unbatched schema rewrite... fine at
  this project's current tiny data scale"). This audit's finding (7-day PITR ceiling) is a distinct
  Basic-tier constraint, not a recurrence of that one. Between the two, Basic tier's limits are worth
  checking explicitly for any future story that assumes a backup, restore, or batching headroom is
  available — verify against the current SKU/tier first, not assumed.
