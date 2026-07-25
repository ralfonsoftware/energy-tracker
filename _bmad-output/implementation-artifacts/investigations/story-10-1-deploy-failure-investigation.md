# Investigation: Story 10.1 deployment failure — SQL Server multiple cascade paths error on Insights table

## Hand-off Brief

1. **What happened.** The `AddInsightsTables` EF Core migration failed during CI/CD deploy with SQL Server error 1785 ("may cause cycles or multiple cascade paths") on `FK_Insights_Flats_FlatId`, because `Insights` is reachable from `Flats` via two referential-action paths: the direct `FlatId` cascade, and an indirect path through `InsightRuns` (`Flats`→`InsightRuns` is `Cascade`, `InsightRuns`→`Insights` is `SetNull`).
2. **Where the case stands.** Root cause is Confirmed directly from the failed job log and the current EF configuration/migration source — no further evidence needed.
3. **What's needed next.** Change one edge in the `Insights.RunId` → `InsightRuns` relationship from database-enforced `SetNull` to `ClientSetNull` (EF-managed, compiles to `NO ACTION` in SQL) to break the second path, then regenerate the migration. Recommend `bmad-quick-dev` or `bmad-correct-course` to implement.

## Case Info

| Field            | Value                                                                      |
| ---------------- | -------------------------------------------------------------------------- |
| Ticket           | Story 10.1 — Insights Infrastructure                                       |
| Date opened      | 2026-07-25                                                                 |
| Status           | Concluded                                                                  |
| System           | GitHub Actions CI/CD → Azure SQL (`energytracker-sqlsrv`), EF Core 10, `dotnet ef database update` migration step |
| Evidence sources | GitHub Actions job log (run 30160156098), migration `20260725133706_AddInsightsTables.cs`, EF entity configurations (`InsightConfiguration.cs`, `InsightRunConfiguration.cs`, `DeviceConfiguration.cs`, `FlatConfiguration.cs`, `RoomConfiguration.cs`, `PowerPointConfiguration.cs`) |

## Problem Statement

User-reported: "the deployment of story 10.1 failed due new introduced constraints" — pointing to
https://github.com/ralfonsoftware/energy-tracker/actions/runs/30160156098/job/89684238542

## Evidence Inventory

| Source                              | Status    | Notes                                                                 |
| ------------------------------------ | --------- | ---------------------------------------------------------------------- |
| GitHub Actions failed job log        | Available | Full "Run EF Core migrations" step log fetched via `gh run view --log-failed` |
| Migration `20260725133706_AddInsightsTables.cs` | Available | Matches current entity config exactly — not a stale-migration issue |
| EF entity configurations (Insight, InsightRun, Device, Flat, Room, PowerPoint) | Available | Read in full |

## Timeline of Events

| Time (UTC)           | Event                                                              | Source                          | Confidence |
| --------------------- | ------------------------------------------------------------------- | -------------------------------- | ---------- |
| 2026-07-25T13:44:01.103Z | Migration `20260725133706_AddInsightsTables` begins applying       | Job log                          | Confirmed  |
| 2026-07-25T13:44:01.474Z | `CREATE TABLE [Insights]` issued with 3 FKs: Devices (SetNull), Flats (Cascade), InsightRuns (SetNull) | Job log | Confirmed |
| 2026-07-25T13:44:01.653Z | SQL Server rejects `FK_Insights_Flats_FlatId`: "may cause cycles or multiple cascade paths" (Error 1785) | Job log | Confirmed |
| 2026-07-25T13:44:01.690Z | Step exits with code 1; deploy job fails                            | Job log                          | Confirmed  |

## Confirmed Findings

### Finding 1: The failing statement and exact SQL Server error

**Evidence:** GitHub Actions run 30160156098, job 89684238542, step "Run EF Core migrations", log lines 2026-07-25T13:44:01.4738976Z–13:44:01.6903074Z.

**Detail:** Applying migration `20260725133706_AddInsightsTables`, the `CREATE TABLE [Insights]` statement (containing `FK_Insights_Devices_DeviceId` SetNull, `FK_Insights_Flats_FlatId` Cascade, `FK_Insights_InsightRuns_RunId` SetNull) fails with:
> `Introducing FOREIGN KEY constraint 'FK_Insights_Flats_FlatId' on table 'Insights' may cause cycles or multiple cascade paths. Specify ON DELETE NO ACTION or ON UPDATE NO ACTION, or modify other FOREIGN KEY constraints.`
Error Number 1785 (SQL Server's standard "multiple cascade paths" restriction). Migration aborts; deploy fails at the migration step.

### Finding 2: Current `OnDelete` configuration exactly matches the migration (not a drift issue)

**Evidence:** api/Data/Configurations/InsightConfiguration.cs:21-40, api/Data/Migrations/20260725133706_AddInsightsTables.cs (FK declarations at lines 28-67).

**Detail:** `Insight.Flat` → `Cascade` (api/Data/Configurations/InsightConfiguration.cs:24), `Insight.Device` → `SetNull` (line 33), `Insight.Run` → `SetNull` (line 40). The generated migration's `ReferentialAction` values are identical (`Cascade`, `SetNull`, `SetNull` respectively). This rules out a stale/regenerated-migration mismatch — the failure is inherent to the current model as designed.

### Finding 3: Author was aware of one multi-path risk (Device) but not the second (InsightRuns)

**Evidence:** api/Data/Configurations/InsightConfiguration.cs:26-29 (comment): *"SetNull (not Cascade): Insight already has a direct cascade path from Flat via FlatId. Device also cascades from Flat via Room -> PowerPoint -> Device. If DeviceId cascaded too, SQL Server would reject the model at migration/deploy time for multiple cascade paths reaching Insights from Flat."*

**Detail:** This comment shows deliberate mitigation of exactly one second path (via `Device`), confirmed by the full cascade chain: `Flat`→`Room` (Cascade, RoomConfiguration.cs:20-23) →`PowerPoint` (Cascade, PowerPointConfiguration.cs:17-20) →`Device` (Cascade, DeviceConfiguration.cs:26-29). No comment or mitigation addresses the `InsightRuns` path.

### Finding 4: The unmitigated second path — via InsightRuns

**Evidence:** api/Data/Configurations/InsightRunConfiguration.cs:18-21 (`InsightRun.Flat` → `Cascade`); api/Data/Configurations/InsightConfiguration.cs:37-40 (`Insight.Run` → `SetNull`).

**Detail:** `Flats` → `InsightRuns` is `Cascade` (deleting a Flat cascades to delete its InsightRuns). `InsightRuns` → `Insights` (via `RunId`) is `SetNull`. This forms a second referential-action path from `Flats` to `Insights`: `Flats --Cascade--> InsightRuns --SetNull--> Insights`. Combined with the direct `Flats --Cascade--> Insights` (`FlatId`) path, `Insights` is reachable from `Flats` via two distinct paths carrying referential actions other than `NO ACTION`.

## Deduced Conclusions

### Deduction 1: Root cause is the InsightRuns-mediated second path, not the Device path

**Based on:** Finding 1, Finding 3, Finding 4.

**Reasoning:** SQL Server's "multiple cascade paths" restriction (Error 1785) is not limited to two `CASCADE` actions — it triggers whenever a table is reachable from the same ancestor via more than one path that each contain a referential action other than `NO ACTION` (this includes `SET NULL` and `SET DEFAULT`, not just `CASCADE`). The Device path was correctly mitigated to `SetNull` on `Insight.Device`, removing it as a conflict source (its remaining chain `Flat→Room→PowerPoint→Device` still cascades down to `Device`, but the leaf edge into `Insights` is `SetNull`/non-conflicting once isolated — the actual conflict per the log is specifically flagged on `FK_Insights_Flats_FlatId`, which is only in conflict with one other path). The `InsightRuns` path was never mitigated: `Flats→InsightRuns` is `Cascade` and `InsightRuns→Insights` is `SetNull`, which is itself a second live path from `Flats` to `Insights`, distinct from the direct `FlatId` edge. This is the path SQL Server is rejecting.

**Conclusion:** The deploy failure is caused by the combination of `InsightRunConfiguration.cs:21` (`OnDelete(DeleteBehavior.Cascade)` on `InsightRun.Flat`) and `InsightConfiguration.cs:40` (`OnDelete(DeleteBehavior.SetNull)` on `Insight.Run`) coexisting with the direct `InsightConfiguration.cs:24` (`OnDelete(DeleteBehavior.Cascade)` on `Insight.Flat`).

## Hypothesized Paths

None — root cause is Confirmed; no open hypotheses remain.

## Missing Evidence

None. All evidence needed to reach a Confirmed root cause was available in the job log and current source.

## Source Code Trace

| Element       | Detail                                                                                          |
| ------------- | ------------------------------------------------------------------------------------------------ |
| Error origin  | `api/Data/Configurations/InsightConfiguration.cs:21-24` (`Insight.Flat` FK, `Cascade`) in conflict with `api/Data/Configurations/InsightRunConfiguration.cs:18-21` (`InsightRun.Flat` FK, `Cascade`) + `api/Data/Configurations/InsightConfiguration.cs:37-40` (`Insight.Run` FK, `SetNull`) |
| Trigger       | `dotnet ef database update` during the "Run EF Core migrations" CI step applying `20260725133706_AddInsightsTables` |
| Condition     | Model has 2+ referential-action paths (any of Cascade/SetNull/SetDefault) from `Flats` to `Insights`: direct via `FlatId`, and indirect via `InsightRuns.RunId` |
| Related files | `api/Data/Migrations/20260725133706_AddInsightsTables.cs`, `api/Data/Migrations/20260725133706_AddInsightsTables.Designer.cs`, `api/Data/Configurations/DeviceConfiguration.cs`, `api/Data/Configurations/RoomConfiguration.cs`, `api/Data/Configurations/PowerPointConfiguration.cs` (confirm the mitigated Device path, not the cause) |

## Conclusion

**Confidence:** High

Root cause is Confirmed directly from the CI job log (SQL Server Error 1785 naming `FK_Insights_Flats_FlatId`) cross-referenced with the current EF Core model. `Insights` has two live referential-action paths from `Flats`: the direct `FlatId` Cascade edge, and an indirect `Flats --Cascade--> InsightRuns --SetNull--> Insights` edge via `RunId`. SQL Server disallows this regardless of the second edge being `SetNull` rather than `Cascade`. The developer's in-code comment shows the `Device` path was deliberately mitigated but the `InsightRuns` path was overlooked — this is a gap in the original design, not an environment or CI issue. The migration file is confirmed to match current source exactly, ruling out a stale-migration explanation.

## Recommended Next Steps

### Fix direction

Break the second path by changing exactly one edge from a database-enforced action to an EF-only (in-memory) action, which compiles to `NO ACTION` in SQL and removes it from SQL Server's cascade-path graph:

- In `api/Data/Configurations/InsightConfiguration.cs:37-40`, change `Insight.Run`'s `OnDelete(DeleteBehavior.SetNull)` to `OnDelete(DeleteBehavior.ClientSetNull)`. This preserves the epic-specified behavior (an `InsightRun`'s deletion must not remove the `Insight` rows it produced) when EF has the related `Insight` rows loaded/tracked, but SQL Server no longer sees a second cascading path — it enforces `NO ACTION` at the DB level.
  - Trade-off to flag: `ClientSetNull` only nulls `RunId` for entities EF has loaded into the change tracker at delete time. If `InsightRun` rows are ever deleted without loading their dependent `Insight` rows (e.g., a raw SQL delete or a delete via a detached/untracked context), orphaned FK values would violate the FK constraint at delete time instead of silently nulling. Confirm whether `InsightRun` deletion always goes through a code path that loads dependents, or add an explicit bulk `UPDATE Insights SET RunId = NULL WHERE RunId = @runId` before the `InsightRun` delete if not.
- Then regenerate the migration (`dotnet ef migrations remove` on `20260725133706_AddInsightsTables` if not yet applied anywhere, or add a new migration if it must not be edited retroactively — check `dotnet ef migrations list` against any environment where it may have partially applied) and verify `dotnet ef database update` succeeds locally before re-pushing, per this project's standing rule (project-context.md: "Always test `dotnet ef database update` locally before pushing").

### Diagnostic

Not needed — root cause is Confirmed. Optional verification: after the fix, run `dotnet ef migrations script` locally and grep the output for `FK_Insights_InsightRuns_RunId` to confirm `NO ACTION` is emitted instead of `SET NULL`.

## Reproduction Plan

1. On a local/dev SQL Server or Azure SQL instance with an empty (or migrated-to-prior-migration) database, run `dotnet ef database update --project api/energy-tracker-api.csproj` from the current `main` branch state.
2. Observe migration `20260725133706_AddInsightsTables` fail with the same SQL Server Error 1785 on `FK_Insights_Flats_FlatId` — confirms the failure is deterministic and environment-independent (not an Azure SQL-specific quirk).
3. Apply the fix direction above, regenerate the migration, and re-run `dotnet ef database update` to confirm it completes cleanly.

## Side Findings

- The deploy pipeline has no test/lint gate that would have caught this before the migration step runs against production infrastructure (per project-context.md: "No `dotnet test` / `npm test` in CI"). A `dotnet ef migrations script` dry-run step (or applying migrations against a throwaway CI database) before touching `energytracker-db` would catch this class of failure pre-merge instead of mid-deploy. Worth flagging as a CI hardening item, separate from this fix.
