-- validate-device-flat-associations.sql
--
-- Read-only integrity check for the Device -> PowerPoint -> Room -> Flat chain
-- and the DeviceAssignmentPeriod rows that track device history.
--
-- Runs SEVEN independent checks, each producing its own labeled result set
-- (via a leading literal CheckName column). No data is modified — SELECT
-- statements only, no BEGIN TRAN, no writes.
--
-- Usage (see scripts/db/README.md for full details):
--   sqlcmd -S energytracker-sqlsrv.database.windows.net -d energytracker-db -G -i scripts/db/validate-device-flat-associations.sql
--
-- Any non-empty result set below indicates an anomaly. Take the DeviceId
-- values from a non-empty result set and paste them into
-- scripts/db/cleanup-orphaned-devices.sql to remove the affected devices —
-- do not run cleanup automatically off the back of this script.

SET NOCOUNT ON;

-- Shared chain used by checks 1-3: every Device, left-joined outward through
-- PowerPoint -> Room -> Flat, so a break at any link is attributable to a
-- specific DeviceId.

-- Check 1: devices whose PowerPointId has no matching PowerPoint row.
SELECT
    'orphan-device-no-powerpoint' AS CheckName,
    d.DeviceId,
    d.Name,
    d.PowerPointId AS BrokenPowerPointId
FROM Devices d
LEFT JOIN PowerPoints pp ON d.PowerPointId = pp.PowerPointId
WHERE pp.PowerPointId IS NULL;

-- Check 2: devices whose PowerPoint exists but that PowerPoint's RoomId has
-- no matching Room row.
SELECT
    'orphan-device-no-room' AS CheckName,
    d.DeviceId,
    d.Name,
    pp.PowerPointId,
    pp.RoomId AS BrokenRoomId
FROM Devices d
JOIN PowerPoints pp ON d.PowerPointId = pp.PowerPointId
LEFT JOIN Rooms r ON pp.RoomId = r.RoomId
WHERE r.RoomId IS NULL;

-- Check 3: devices whose Room exists but that Room's FlatId has no matching
-- Flat row.
SELECT
    'orphan-device-no-flat' AS CheckName,
    d.DeviceId,
    d.Name,
    pp.PowerPointId,
    r.RoomId,
    r.FlatId AS BrokenFlatId
FROM Devices d
JOIN PowerPoints pp ON d.PowerPointId = pp.PowerPointId
JOIN Rooms r ON pp.RoomId = r.RoomId
LEFT JOIN Flats f ON r.FlatId = f.FlatId
WHERE f.FlatId IS NULL;

-- Check 4: DeviceAssignmentPeriod rows referencing a non-existent DeviceId.
SELECT
    'assignment-period-orphan-device' AS CheckName,
    dap.Id AS DeviceAssignmentPeriodId,
    dap.DeviceId,
    dap.FlatId,
    dap.[From],
    dap.[To]
FROM DeviceAssignmentPeriods dap
LEFT JOIN Devices d ON dap.DeviceId = d.DeviceId
WHERE d.DeviceId IS NULL;

-- Check 5: devices with zero DeviceAssignmentPeriod rows.
SELECT
    'device-no-assignment-period' AS CheckName,
    d.DeviceId,
    d.Name
FROM Devices d
WHERE NOT EXISTS (
    SELECT 1 FROM DeviceAssignmentPeriods dap WHERE dap.DeviceId = d.DeviceId
);

-- Check 6: devices with more than one open (To IS NULL) assignment period.
SELECT
    'device-multiple-open-periods' AS CheckName,
    dap.DeviceId,
    d.Name,
    COUNT(*) AS OpenPeriodCount
FROM DeviceAssignmentPeriods dap
JOIN Devices d ON dap.DeviceId = d.DeviceId
WHERE dap.[To] IS NULL
GROUP BY dap.DeviceId, d.Name
HAVING COUNT(*) > 1;

-- Check 7: DeviceAssignmentPeriod rows whose FlatId no longer matches the
-- device's current Room -> Flat chain (drift between recorded history and
-- current structure). Only evaluated for devices with an intact chain today
-- — a device with a broken chain is already reported by checks 1-3.
SELECT
    'assignment-period-flatid-drift' AS CheckName,
    dap.Id AS DeviceAssignmentPeriodId,
    dap.DeviceId,
    d.Name,
    dap.FlatId AS RecordedFlatId,
    r.FlatId AS CurrentFlatId,
    dap.[From],
    dap.[To]
FROM DeviceAssignmentPeriods dap
JOIN Devices d ON dap.DeviceId = d.DeviceId
JOIN PowerPoints pp ON d.PowerPointId = pp.PowerPointId
JOIN Rooms r ON pp.RoomId = r.RoomId
WHERE dap.FlatId <> r.FlatId;
