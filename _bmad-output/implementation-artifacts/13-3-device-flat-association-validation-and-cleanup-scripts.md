---
baseline_commit: c4428db3fd13ad96cdab5b548a0a8e9c491c7078
---

# Story 13.3: Device–Flat Association Validation & Cleanup Scripts

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the team maintaining this app,
I want a repeatable way to find and remove any device that no longer has a valid chain up to a real flat,
so that a future bug, incomplete migration, or manual DB intervention can't leave silently-orphaned device data behind unnoticed.

## Acceptance Criteria

1. **Given** no existing script or tooling validates device–flat referential integrity in production, **when** implemented, **then** a new validation script (`scripts/db/validate-device-flat-associations.sql`, plain T-SQL runnable via `sqlcmd` against the production connection) reports, without modifying any data: devices whose `PowerPointId` has no matching `PowerPoint` row, power points whose `RoomId` has no matching `Room` row, rooms whose `FlatId` has no matching `Flat` row (each surfaced per-device via a chain of `LEFT JOIN`s from `Devices` outward so every anomaly is reported in terms of the affected `DeviceId`), `DeviceAssignmentPeriod` rows referencing a non-existent `DeviceId`, devices with zero `DeviceAssignmentPeriod` rows, devices with more than one *open* (`To IS NULL`) period, and `DeviceAssignmentPeriod` rows whose `FlatId` no longer matches the device's current Room→Flat chain.
2. **Given** the validation script's findings, **when** a device is confirmed to have no valid chain up to a real `Flat`, **then** a separate cleanup script (`scripts/db/cleanup-orphaned-devices.sql`) exists that deletes exactly those devices (and, for each, its own `DeviceAssignmentPeriod` rows first) — scoped by an explicit `DeviceId` list the operator pastes in from the validation script's output, never an implicit "delete everything found" auto-wiring between the two scripts.
3. **Given** these are destructive-capable scripts, **when** implemented, **then** the cleanup script is a plain `.sql` file with no wrapper automation — never invoked by CI/CD, an Azure Function, an EF Core migration, or any other automated path; a human runs it manually via `sqlcmd`/Azure Portal Query Editor after reviewing the validation output.
4. **Given** the current production baseline is confirmed clean (zero anomalies of every kind above, verified 2026-08-02 during this story's scoping), **when** delivered, **then** the validation script is run once against production as this story's own verification, its output (all-clear) is recorded in this story's Dev Agent Record / Completion Notes, and the cleanup script is **not** executed as part of this story — it's delivered ready-to-use for whenever it's next needed.
5. **Given** these are standalone SQL scripts, not EF Core migrations, **when** implemented, **then** neither script is added to `api/Data/Migrations/` or referenced anywhere the `dotnet ef database update` deploy step (`.github/workflows/azure-static-web-apps.yml:105-110`) would pick it up — they live only in the new `scripts/db/` folder, and a short `scripts/db/README.md` documents what each script does, how to run it, and the manual-review-before-cleanup expectation.

## Tasks / Subtasks

- [x] Task 1: Create `scripts/db/` folder and README (AC: #5)
  - [x] Create `scripts/db/README.md` documenting: purpose of each script, the exact `sqlcmd` invocation to run them (`sqlcmd -S energytracker-sqlsrv.database.windows.net -d energytracker-db -G -i <file>.sql`, matching this project's connection string host/database name from `.github/workflows/azure-static-web-apps.yml:107` and the Azure AD `-G` auth flag), a note that `-G` requires the operator to already be authenticated via `az login` under an identity with Azure SQL access (exactly as used during this epic's investigation), and an explicit warning that `cleanup-orphaned-devices.sql` is destructive and must only be run after reviewing `validate-device-flat-associations.sql`'s output
- [x] Task 2: Write the validation script (AC: #1)
  - [x] `scripts/db/validate-device-flat-associations.sql` — a sequence of `SELECT` queries (read-only, no `BEGIN TRAN`/writes), one per anomaly class, each producing a clearly-labeled result set (e.g. via `PRINT` banners between queries, or a leading literal column like `SELECT 'orphan-device-no-powerpoint' AS Check, d.DeviceId, d.Name FROM ...`) covering exactly the seven checks in AC #1. Base the join logic on the queries already proven against this production database during this epic's investigation (chain: `Devices d LEFT JOIN PowerPoints pp ON d.PowerPointId = pp.PowerPointId LEFT JOIN Rooms r ON pp.RoomId = r.RoomId LEFT JOIN Flats f ON r.FlatId = f.FlatId`, plus the `DeviceAssignmentPeriods`-focused checks joining back to `Devices`/`Rooms`)
  - [x] Each result set must include enough columns to act on directly (at minimum `DeviceId`, `Name`, and whichever FK is broken) so the operator can paste `DeviceId`s straight into the cleanup script without a second lookup
- [x] Task 3: Write the cleanup script (AC: #2, #3)
  - [x] `scripts/db/cleanup-orphaned-devices.sql` — starts with a clearly-marked block (e.g. `DECLARE @DeviceIdsToDelete TABLE (DeviceId uniqueidentifier); INSERT INTO @DeviceIdsToDelete VALUES (...);`) the operator fills in by hand from the validation script's output; then `DELETE FROM DeviceAssignmentPeriods WHERE DeviceId IN (SELECT DeviceId FROM @DeviceIdsToDelete)` followed by `DELETE FROM Devices WHERE DeviceId IN (SELECT DeviceId FROM @DeviceIdsToDelete)`, both wrapped in a single `BEGIN TRAN` / `COMMIT` (with the transaction left uncommitted / a `-- COMMIT` comment line the operator uncomments deliberately, so a copy-paste run doesn't silently commit) — the exact "human confirms before commit" mechanics are a judgment call for whichever pattern is clearest; document the chosen mechanic in the script's own header comment
  - [x] No `WHERE` clause in the cleanup script may derive its target rows from a live re-query of "broken chain" logic — it must only ever act on the explicit, operator-provided `DeviceId` list, so a cleanup run can never delete a device that wasn't specifically reviewed and listed
- [x] Task 4: Run and record verification (AC: #4)
  - [x] Run `validate-device-flat-associations.sql` against production via `sqlcmd`
  - [x] Record the (expected all-clear) output in this story's Dev Agent Record / Completion Notes List, confirming the baseline matches what was found during this epic's scoping (zero results for every check)
  - [x] Do not run `cleanup-orphaned-devices.sql` — there is nothing to clean up

## Dev Notes

- **Not an active-incident cleanup.** A live check against production during this epic's scoping (documented in this story's parent epic file) found **zero** anomalies across every check this story specifies: zero orphaned devices, power points, or rooms at any level of the `Device → PowerPoint → Room → Flat` chain; zero `DeviceAssignmentPeriod` rows referencing a non-existent device; zero devices with no assignment period; zero devices with more than one open period; zero `FlatId` drift between a `DeviceAssignmentPeriod` and its device's current chain. This story is defense-in-depth, not a bug fix — frame the Completion Notes accordingly.
- **Why this is even possible to be defense-in-depth rather than moot:** `Device.PowerPointId`, `PowerPoint.RoomId`, and `Room.FlatId` are all `IsRequired()` with `OnDelete(DeleteBehavior.Cascade)` (`api/Data/Configurations/DeviceConfiguration.cs:14,28-31`, and the equivalent `PowerPointConfiguration.cs`/`RoomConfiguration.cs` — verify current file names/line numbers, this codebase's convention is one `{Entity}Configuration.cs` per entity in `api/Data/Configurations/`). Under normal EF Core-mediated writes, a structural orphan is unreachable — these scripts exist for the cases EF Core's FK enforcement doesn't cover: a raw SQL admin action, a future migration that loosens a constraint, or (directly relevant to this epic) a bug in Story 13.1's new device-scoped endpoints.
- **These are plain `.sql` files, not C# tooling, not an EF Core migration, and not a Function.** Do not create a new Azure Function, console app, or migration for this — the project has no existing convention for ops tooling beyond shell scripts in `scripts/` (currently `dev-up.sh`/`dev-down.sh` for local dev orchestration), so `scripts/db/*.sql` run via `sqlcmd` is the right level of ceremony: no build step, no deploy step, nothing for CI to pick up by accident.
- **Never let these scripts become reachable by automation.** `.github/workflows/azure-static-web-apps.yml`'s "Run EF Core migrations" step (`dotnet ef database update --project api/energy-tracker-api.csproj`) only ever applies files under `api/Data/Migrations/` — these new scripts must never be moved there, referenced from there, or added to any workflow YAML. This is the same "dev agents/automation never execute destructive operations against live infra" boundary this project already enforces for Bicep/deploy changes (`infra/` — do not modify without an explicit infra story) and EF Core migrations against real infra — extend that same boundary to these scripts explicitly.
- **The proven `sqlcmd` invocation from this epic's investigation:** `sqlcmd -S energytracker-sqlsrv.database.windows.net -d energytracker-db -G -i <file>.sql`, run under an identity already authenticated via `az login` with Azure SQL access (matching the exact connection details in `.github/workflows/azure-static-web-apps.yml:107`'s `SqlConnectionString`: `Server=tcp:energytracker-sqlsrv.database.windows.net,1433;Database=energytracker-db;Authentication=Active Directory Default;...`). Confirm this still works when writing the README — it was directly exercised (read-only queries only) during this epic's investigation session.
- **Cleanup script safety is the load-bearing requirement, not the query logic.** The validation queries are straightforward `LEFT JOIN ... IS NULL` anomaly detection. The part that needs real care is making sure `cleanup-orphaned-devices.sql` can never be run "as-is" against a live database and delete something unreviewed — it must require the operator to actively paste in specific `DeviceId`s (not re-derive "everything broken" itself) and take a deliberate action to actually commit. Don't optimize this script for convenience; optimize it for making an accidental mass-delete structurally difficult.
- **Do not attempt to also validate/clean up `Room`/`PowerPoint`/`Flat`-level orphans beyond what's needed to explain a device anomaly.** This story is device-scoped per its own title and the user's explicit request ("all devices without any valid association to a flat") — a `Room` with no `Flat`, if one ever existed, is a `Room`-level cleanup concern for a future story, not this one; this story's checks surface it only insofar as it's a *cause* of a device being unassociated.

### Project Structure Notes

- New top-level `scripts/db/` subfolder, sibling to the existing `scripts/dev-up.sh`/`scripts/dev-down.sh` — this is the first non-shell, non-dev-orchestration content in `scripts/`; keep it clearly separated (its own subfolder, its own README) rather than mixing with the local-dev scripts.
- No changes to `api/`, `client/`, or `infra/` — this story touches only the new `scripts/db/` folder.

### References

- [Source: `_bmad-output/planning-artifacts/epics/epic-13-flat-structure-save-integrity-device-and-room-scoped-saves.md`, Story 13.3 section] — the epic-level AC set this story file expands
- [Source: `_bmad-output/implementation-artifacts/investigations/structure-editor-device-not-persisted-investigation.md`] — the investigation this whole epic (and the `sqlcmd`/Azure SQL access pattern this story reuses) originates from
- [Source: `api/Data/Configurations/DeviceConfiguration.cs`] — `Device`'s FK/cascade configuration to `PowerPoint`
- [Source: `.github/workflows/azure-static-web-apps.yml:105-110`] — the EF Core migrations deploy step this story's scripts must never be reachable from
- [Source: `scripts/dev-up.sh`, `scripts/dev-down.sh`] — existing `scripts/` folder convention (shell scripts, no build step) this story's `scripts/db/` subfolder follows the spirit of

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- `sqlcmd -S energytracker-sqlsrv.database.windows.net -d energytracker-db -G -i scripts/db/validate-device-flat-associations.sql` initially failed with a T-SQL syntax error (`Incorrect syntax near the keyword 'From'`/`'To'`) because `DeviceAssignmentPeriod.From`/`.To` are SQL Server reserved keywords — fixed by bracket-quoting (`dap.[From]`, `dap.[To]`) everywhere those columns are referenced.
- First run against production also failed with an Azure SQL firewall error (client IP not allowlisted); resolved after Ralf added a firewall rule for the operator's IP — not something the dev agent does itself (see Dev Notes' "never let these scripts become reachable by automation" boundary, extended here to "never modify live Azure firewall rules").

### Completion Notes List

- Implemented as three plain files under the new `scripts/db/` folder: `README.md`, `validate-device-flat-associations.sql`, `cleanup-orphaned-devices.sql`. No `api/`, `client/`, or `infra/` changes, per this story's Project Structure Notes.
- This story's deliverable is ops tooling (SQL scripts), not application code, so there are no unit/integration tests to author per the project's testing conventions — the story's own AC #4 (running the validation script against production and recording the all-clear) is the verification step, executed below.
- Confirmed with Ralf before executing anything against the production database, per this project's "dev agents never touch live Azure without explicit go-ahead" convention (previously applied to `infra/deploy.sh`/Bicep, extended here to a read-only prod DB query). Ralf explicitly authorized the dev agent to run the read-only validation script directly.
- **Production verification (AC #4) — run 2026-08-07:** `validate-device-flat-associations.sql` executed against `energytracker-sqlsrv.database.windows.net` / `energytracker-db` via `sqlcmd -G`. All seven checks returned **zero rows**:
  - `orphan-device-no-powerpoint`: 0
  - `orphan-device-no-room`: 0
  - `orphan-device-no-flat`: 0
  - `assignment-period-orphan-device`: 0
  - `device-no-assignment-period`: 0
  - `device-multiple-open-periods`: 0
  - `assignment-period-flatid-drift`: 0

  This matches the all-clear baseline found during this epic's scoping (2026-08-02). `cleanup-orphaned-devices.sql` was **not** executed — there is nothing to clean up. It ships ready-to-use for whenever it's next needed, per AC #4.

### File List

- `scripts/db/README.md` (new)
- `scripts/db/validate-device-flat-associations.sql` (new)
- `scripts/db/cleanup-orphaned-devices.sql` (new)

### Change Log

| Date | Change |
|---|---|
| 2026-08-07 | Added `scripts/db/` with a read-only Device→Flat association validation script, a manual-review-gated cleanup script, and a README covering usage/safety. Ran validation against production as this story's own verification: zero anomalies across all seven checks (baseline confirmed clean). |
| 2026-08-07 | Code review completed — zero findings. Reviewer verified table/column names against EF Core entities/migrations, confirmed the cleanup script's redundant-but-harmless `DeviceAssignmentPeriods` delete matches AC #2, and traced Check 7's drift logic against all `api/Features` code to confirm no false-positive risk (no code path reassigns a device across flats). Story marked done. |
