# scripts/db/

Plain T-SQL scripts for validating and (if ever needed) cleaning up
Device -> PowerPoint -> Room -> Flat referential integrity in production.

These are standalone `.sql` files run manually via `sqlcmd` or Azure Portal
Query Editor. They are **not** EF Core migrations, are **not** referenced
from `api/Data/Migrations/`, and are **not** invoked by any CI/CD pipeline,
Azure Function, or other automation — see "Why these are plain SQL, not
automated" below.

## Scripts

### `validate-device-flat-associations.sql`

Read-only. Runs seven checks for orphaned/inconsistent data in the
Device -> PowerPoint -> Room -> Flat chain and `DeviceAssignmentPeriod`
history, each producing its own labeled result set. Safe to run any time;
makes no changes.

### `cleanup-orphaned-devices.sql`

**Destructive.** Deletes an explicit, operator-provided list of `DeviceId`s
(and their `DeviceAssignmentPeriod` rows) taken from
`validate-device-flat-associations.sql`'s output. Never run this without
first running the validation script and reviewing its output — see the
warning below.

## How to run

Both scripts assume you're already authenticated via `az login` under an
identity with Azure SQL access to the production database (the same access
used during this epic's production investigation). `-G` tells `sqlcmd` to
use that Azure AD session rather than a SQL login/password.

```bash
az login   # if not already authenticated

sqlcmd -S energytracker-sqlsrv.database.windows.net -d energytracker-db -G -i scripts/db/validate-device-flat-associations.sql

sqlcmd -S energytracker-sqlsrv.database.windows.net -d energytracker-db -G -i scripts/db/cleanup-orphaned-devices.sql
```

Server and database name match the connection string used by this project's
deploy pipeline (`.github/workflows/azure-static-web-apps.yml`).

## Warning: manual review before cleanup

`cleanup-orphaned-devices.sql` is destructive and must only be run after:

1. Running `validate-device-flat-associations.sql` and reading its output.
2. Manually pasting the specific `DeviceId`s you've decided to remove into
   the `@DeviceIdsToDelete` table at the top of `cleanup-orphaned-devices.sql`.
3. Reviewing the row counts it prints before uncommenting the `COMMIT` line
   inside the script (see that script's header comment for the exact
   mechanic).

There is no automatic wiring between the two scripts — the cleanup script
never re-derives "everything broken" itself, only ever acting on the IDs you
explicitly list.

## Why these are plain SQL, not automated

`Device.PowerPointId`, `PowerPoint.RoomId`, and `Room.FlatId` are all
required FKs with cascade delete, so under normal EF Core-mediated writes a
structural orphan should be unreachable. These scripts exist for the cases
EF Core's enforcement doesn't cover (a raw SQL admin action, a future
migration that loosens a constraint, etc.), not for a routine workflow —
so they deliberately have no build step, no deploy step, and are never
invoked by CI/CD, an Azure Function, or an EF Core migration. This follows
the same "dev agents/automation never run destructive operations against
live infra" boundary this project already applies to `infra/` and EF Core
migrations — a human runs these manually, after reviewing the output.
