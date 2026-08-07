-- cleanup-orphaned-devices.sql
--
-- DESTRUCTIVE. Deletes specific devices (and their DeviceAssignmentPeriod
-- rows) by an explicit DeviceId list that YOU fill in below, after reviewing
-- the output of validate-device-flat-associations.sql. This script never
-- re-derives "which devices are broken" itself — it only ever acts on the
-- exact IDs you paste in, so a copy-paste run cannot delete anything that
-- wasn't specifically reviewed.
--
-- Safety mechanic: everything runs inside BEGIN TRAN. The final COMMIT is a
-- comment (`-- COMMIT`) that you must uncomment and re-run deliberately (or
-- copy-paste just that line into the same session) after checking the
-- "rows affected" counts below match what you expect. If you do nothing,
-- the transaction stays open in your session; closing the session or
-- running ROLLBACK discards it — nothing commits by accident.
--
-- Usage (see scripts/db/README.md for full details):
--   sqlcmd -S energytracker-sqlsrv.database.windows.net -d energytracker-db -G -i scripts/db/cleanup-orphaned-devices.sql
--
-- Note: sqlcmd runs this whole file as one batch/session, so the BEGIN TRAN
-- below stays open until you explicitly COMMIT or ROLLBACK in that same
-- session (e.g. via Azure Portal Query Editor or an interactive sqlcmd
-- session) — do not treat a single non-interactive `sqlcmd -i` run as
-- "safe by default" unless you've uncommented the COMMIT line first.

SET NOCOUNT ON;

-- ============================================================================
-- STEP 1: Fill in the exact DeviceId values to delete, taken from
-- validate-device-flat-associations.sql's output. Do not paste anything you
-- have not personally reviewed.
-- ============================================================================
DECLARE @DeviceIdsToDelete TABLE (DeviceId uniqueidentifier);
INSERT INTO @DeviceIdsToDelete (DeviceId) VALUES
    -- ('00000000-0000-0000-0000-000000000000'),
    ('REPLACE-WITH-REAL-DEVICE-ID');

BEGIN TRAN;

DELETE FROM DeviceAssignmentPeriods
WHERE DeviceId IN (SELECT DeviceId FROM @DeviceIdsToDelete);

PRINT 'Deleted DeviceAssignmentPeriods rows: ' + CAST(@@ROWCOUNT AS varchar(20));

DELETE FROM Devices
WHERE DeviceId IN (SELECT DeviceId FROM @DeviceIdsToDelete);

PRINT 'Deleted Devices rows: ' + CAST(@@ROWCOUNT AS varchar(20));

-- ============================================================================
-- STEP 2: Review the PRINT output above. Only if the row counts match what
-- you expect from the validation script's output, uncomment the line below
-- and re-run it (in the same transaction/session) to make the delete permanent.
-- ============================================================================
-- COMMIT;

-- If anything looks wrong, run this instead of COMMIT:
-- ROLLBACK;
