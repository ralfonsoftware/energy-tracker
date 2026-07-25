---
title: 'Fix multiple-cascade-paths deploy failure on Insights table'
type: 'bugfix'
created: '2026-07-25'
status: 'done'
context: ['{project-root}/_bmad-output/implementation-artifacts/investigations/story-10-1-deploy-failure-investigation.md']
baseline_commit: '0bec76568ed878dd8729416a6a190d2cde9b820b'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Story 10.1's deploy failed with SQL Server Error 1785 ("multiple cascade paths") because `Insights` is reachable from `Flats` via **three** referential-action paths, not two as originally diagnosed: the direct `FlatId` `Cascade` edge, an indirect `Flats --Cascade--> InsightRuns --SetNull--> Insights` edge via `RunId`, and an indirect `Flats --Cascade--> Room --Cascade--> PowerPoint --Cascade--> Device --SetNull--> Insights` edge via `DeviceId`. SQL Server's Error 1785 counts `SetNull` the same as `Cascade` when computing conflicting paths, so both indirect edges independently conflict with the direct edge.

**Approach:** Change **both** `Insight.Run`'s and `Insight.Device`'s `OnDelete` from `DeleteBehavior.SetNull` to `DeleteBehavior.ClientSetNull` (EF-managed, compiles to `NO ACTION` in SQL, removes both indirect paths from SQL Server's cascade graph). Regenerate migration `20260725133706_AddInsightsTables` in place (confirmed via `dotnet ef migrations list` to still be `(Pending)` everywhere — never successfully applied, so no other environment depends on its current shape). Verify by applying the full migration chain against a disposable local SQL Server (not the live Azure instance).

## Boundaries & Constraints

**Always:**
- Preserve the epic-specified behavior: deleting an `InsightRun` must not delete the `Insight` rows it produced (only null their `RunId`); deleting a `Device` must not delete the `Insight` rows referencing it (only null their `DeviceId`).
- Regenerate the existing migration in place — do not add a new migration on top of a broken one (confirmed `Pending` everywhere, safe to edit).
- Keep the in-code comments on both `Insight.Device`'s and `Insight.Run`'s config accurate and cross-consistent: both must explain why `ClientSetNull` (not `SetNull`) is required for their respective path.

**Ask First:**
- If `dotnet ef migrations list` no longer shows `20260725133706_AddInsightsTables` as fully `Pending` (i.e., it appears applied somewhere) when re-checked at implementation time — HALT, this changes the safe regeneration approach.

**Never:**
- Do not touch `RoomConfiguration.cs` or `PowerPointConfiguration.cs` — that part of the cascade chain is unaffected and out of scope. (`DeviceConfiguration.cs` itself is also untouched — the `Device` fix is on `Insight.Device`, in `InsightConfiguration.cs`.)
- Do not run migrations against the live Azure SQL instance — verification uses a disposable local container, never the shared/live database (infra/deploy is human-owned).
- Do not modify the unrelated pending changes already in the working tree (`sprint-status.yaml`, the `10-2` story draft).

</frozen-after-approval>

## Code Map

- `api/Data/Configurations/InsightConfiguration.cs:26-45` -- the `Insight.Device` AND `Insight.Run` FK configs, both changed from `SetNull` to `ClientSetNull`; comments updated on both
- `api/Data/Migrations/20260725133706_AddInsightsTables.cs` -- migration regenerated in place (currently `Pending` everywhere, confirmed via `dotnet ef migrations list`); final regenerated file is `20260725140716_AddInsightsTables.cs` (re-timestamped twice as the fix was extended)
- `api/Data/Migrations/20260725140716_AddInsightsTables.Designer.cs` -- migration snapshot, regenerated alongside
- `api/Data/Migrations/AppDbContextModelSnapshot.cs` -- EF model snapshot, auto-updated by `dotnet ef migrations` commands
- `api/Features/Flats/DeleteFlatFunction.cs:57` -- confirmed this is the only current caller path that cascades a `Flat` delete; it calls `LoadFlatCascadeChildrenAsync` first
- `api/Shared/AppDbContextExtensions.cs:8-29` -- confirms `Devices`, `InsightRuns`, and `Insights` (all by `FlatId`) are loaded into the change tracker before a `Flat` delete, so `ClientSetNull` behaves identically to `SetNull` in the only path that exists today (no independent `InsightRun` or `Device` deletion code exists yet in Story 10.1 scope)
- `api.Tests/Data/InsightConfigurationTests.cs` -- new file; direct tests of the `ClientSetNull` behavior for both `Run` and `Device` (gap identified during adversarial review)

## Tasks & Acceptance

**Execution:**
- [x] `api/Data/Configurations/InsightConfiguration.cs` -- change **both** `Insight.Device` and `Insight.Run`'s `OnDelete(DeleteBehavior.SetNull)` to `OnDelete(DeleteBehavior.ClientSetNull)`; update both comments to explain the shared root cause (Error 1785 counts SetNull same as Cascade) -- fixes the full root cause (initial one-edge fix was insufficient; see Spec Change Log)
- [x] Regenerate migration: remove and re-add `AddInsightsTables` twice (once per fix iteration) -- final file `20260725140716_AddInsightsTables.cs`, whose `FK_Insights_InsightRuns_RunId` and `FK_Insights_Devices_DeviceId` both now omit `onDelete` entirely (default `NO ACTION`), while `FK_Insights_Flats_FlatId` stays `Cascade`
- [x] Verify migration end-to-end against a disposable local SQL Server (Docker `mcr.microsoft.com/mssql/server:2022-latest`, throwaway container, destroyed after use) -- applied the full migration chain from scratch; **first attempt (Run-only fix) failed with the same Error 1785** on the Device path, confirming the blind-review finding; second attempt (both fixes) succeeded cleanly. This is stronger evidence than the originally-planned local `dotnet ef database update` (real SQL Server semantics, not just generated-script inspection) and required no changes to the live Azure SQL instance
- [x] Add `api.Tests/Data/InsightConfigurationTests.cs` -- direct tests for both `ClientSetNull` edges (gap flagged independently by two review passes)

**Acceptance Criteria:**
- Given the current model with `Insight.Run` and `Insight.Device` set to `ClientSetNull`, when `dotnet ef migrations script` is generated, then `FK_Insights_InsightRuns_RunId` and `FK_Insights_Devices_DeviceId` both omit `ON DELETE` (default `NO ACTION`) and `FK_Insights_Flats_FlatId` still specifies `CASCADE` — verified
- Given a disposable local SQL Server instance at the pre-10.1 migration baseline, when the full migration chain including `AddInsightsTables` is applied, then it completes without Error 1785 and `Insights`/`InsightRuns` exist with the expected FK delete actions (`CASCADE`/`NO_ACTION`/`NO_ACTION` per `sys.foreign_keys`) — verified against a throwaway Docker SQL Server 2022 container
- Given the existing `DeleteFlatFunction` flow (which loads `Devices`, `InsightRuns`, and `Insights` for the flat before removing it), when a `Flat` with `Device`/`InsightRun`/`Insight` rows is deleted, then all of them are cascade-deleted exactly as before (behavior unchanged for the only caller that exists today) — verified via existing `DeleteFlatFunctionTests` passing unchanged
- Given a tracked `InsightRun` (or `Device`) removed directly without removing its `Flat`, when its dependent `Insight` rows are loaded first, then `SaveChangesAsync` nulls `RunId` (or `DeviceId`) without deleting the `Insight` row — verified by new `InsightConfigurationTests`

## Spec Change Log

- **Triggering finding (bad_spec, blind adversarial review):** the original Approach only fixed `Insight.Run`'s FK (`SetNull` → `ClientSetNull`) to resolve the `InsightRuns` cascade path, leaving `Insight.Device`'s FK as DB-level `SetNull`. The reviewer noted the `Device` path (`Flat -> Room -> PowerPoint -> Device -> Insight`) is structurally identical to the `InsightRuns` path and would independently trigger the same Error 1785 conflict — the original in-code comment's claim that `SetNull` on `Device` was already safe had never been empirically verified, only assumed.
- **What was amended:** Intent/Problem (now names all three paths, not two) and Approach (fixes both `Run` and `Device` edges); Boundaries `Always`/`Never` updated to match; Tasks extended to include the `Device` fix, empirical Docker-based verification, and new regression tests; Acceptance Criteria rewritten to reflect what was actually verified.
- **Known-bad state avoided:** shipping a "fix" that still fails Error 1785 on redeploy. This was reproduced live during implementation — applying the full migration chain to a disposable SQL Server with only the `Run` edge fixed failed with the identical error on `FK_Insights_Flats_FlatId`; fixing both edges resolved it.
- **KEEP:** the `ClientSetNull` mechanism choice (vs. an additive/alternate migration strategy) was correct and is retained. The empirical verification method (disposable Docker SQL Server, destroyed after use — never the live Azure SQL instance) worked well and is the recommended default for verifying EF Core cascade-path fixes in this repo going forward; `dotnet ef migrations script` alone is insufficient since it only emits DDL text without invoking real SQL Server validation.

## Design Notes

`ClientSetNull` vs `SetNull`: `SetNull` is enforced by the database itself (`ON DELETE SET NULL` in the FK), which SQL Server's Error 1785 check counts the same as `CASCADE` when computing conflicting referential-action paths to the same table. `Insights` has two indirect paths from `Flats` in addition to the direct one — via `InsightRuns` (`RunId`) and via `Room -> PowerPoint -> Device` (`DeviceId`) — so both edges needed `ClientSetNull`, not just one. `ClientSetNull` tells EF Core to null the FK in the change tracker when a tracked parent is deleted, and to declare `NO ACTION` at the database level — so the database sees only the single direct cascading path, while EF still nulls `RunId`/`DeviceId` in memory whenever the related `InsightRun`/`Device` is loaded and removed via `SaveChangesAsync`. This is safe today because the only code path that deletes an `InsightRun` or `Device` (`DeleteFlatFunction` cascading from a `Flat` delete) already loads `Devices`, `InsightRuns`, and `Insights` for that flat first (`AppDbContextExtensions.LoadFlatCascadeChildrenAsync`) — and in that path, the `Insight` rows are deleted anyway via the direct `FlatId` cascade, so the nulling is moot. If a future story deletes an `InsightRun` or `Device` independently of its `Flat` without loading dependent `Insight` rows first, that delete would fail on the FK constraint instead of nulling the FK — deferred to `deferred-work.md` as a flag for whoever builds that code path.

## Verification

**Commands:**
- `cd api && dotnet ef migrations remove --project energy-tracker-api.csproj` then `dotnet ef migrations add AddInsightsTables --project energy-tracker-api.csproj` -- regenerates the migration; confirmed both `FK_Insights_InsightRuns_RunId` and `FK_Insights_Devices_DeviceId` omit `onDelete`
- `docker run -d --name et-cascade-test -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=..." -p 15433:1433 mcr.microsoft.com/mssql/server:2022-latest` then `dotnet ef database update --project energy-tracker-api.csproj --connection "Server=tcp:localhost,15433;..."` -- expected and confirmed: full migration chain applies with no Error 1785; container destroyed afterward (`docker rm -f et-cascade-test`)
- `cd api && dotnet build` -- expected and confirmed: builds clean, 0 warnings/errors
- `cd api.Tests && dotnet test` -- expected and confirmed: 395/395 pass (393 pre-existing + 2 new)

## Suggested Review Order

**Root cause fix**

- Both FK edges causing the multiple-cascade-paths conflict, switched from DB-level `SetNull` to EF-managed `ClientSetNull`; comments explain why each one was needed.
  [`InsightConfiguration.cs:26`](../../api/Data/Configurations/InsightConfiguration.cs#L26)

**Regenerated migration (mechanical, EF-generated)**

- `FK_Insights_Devices_DeviceId` and `FK_Insights_InsightRuns_RunId` now omit `onDelete` (default `NoAction`); `FK_Insights_Flats_FlatId` unchanged as `Cascade`.
  [`20260725140716_AddInsightsTables.cs:51`](../../api/Data/Migrations/20260725140716_AddInsightsTables.cs#L51)
- Snapshot updated to match — single mechanical diff, no other schema drift.
  [`AppDbContextModelSnapshot.cs:495`](../../api/Data/Migrations/AppDbContextModelSnapshot.cs#L495)

**Confirms the fix is safe for the one existing caller**

- The only code path that deletes an `InsightRun`/`Device` today already loads all dependent rows before delete, so `ClientSetNull` behaves identically to `SetNull` here.
  [`AppDbContextExtensions.cs:18`](../../api/Shared/AppDbContextExtensions.cs#L18)
- Confirms `LoadFlatCascadeChildrenAsync` is called before the cascading delete.
  [`DeleteFlatFunction.cs:57`](../../api/Features/Flats/DeleteFlatFunction.cs#L57)

**New tests (peripheral)**

- Direct regression coverage for the `ClientSetNull` behavior on both edges — the gap two review passes independently flagged.
  [`InsightConfigurationTests.cs:44`](../../api.Tests/Data/InsightConfigurationTests.cs#L44)
