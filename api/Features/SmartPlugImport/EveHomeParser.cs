using System.Globalization;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using ExcelDataReader;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnergyTracker.Api.Features.SmartPlugImport;

public class EveHomeParser(AppDbContext db, ILogger<EveHomeParser> logger)
{
    static EveHomeParser()
    {
        // ExcelDataReader needs this registered before any Excel file is read (see api/Program.cs for the
        // primary startup-time registration). Duplicated here as a static constructor so parsing is also
        // correct in hosts that never execute Program.cs, e.g. the test project — registering the same
        // provider instance twice is a documented no-op, not an error.
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }

    public async Task ParseAndStoreAsync(Guid flatId, string plugId, Stream fileStream, CancellationToken ct)
    {
        var (deviceName, rows) = ParseFile(fileStream, logger);
        logger.LogInformation("Eve Home file device name: {DeviceName}", deviceName);

        var seen = new HashSet<DateTimeOffset>(await db.SmartPlugIntervalData
            .Where(d => d.FlatId == flatId && d.PlugId == plugId)
            .Select(d => d.Timestamp)
            .ToListAsync(ct));

        var newRows = new List<SmartPlugIntervalData>();
        var affectedDates = new HashSet<DateOnly>();
        foreach (var (timestamp, whValue) in rows)
        {
            if (!seen.Add(timestamp))
                continue;

            newRows.Add(new SmartPlugIntervalData
            {
                FlatId = flatId,
                PlugId = plugId,
                Timestamp = timestamp,
                WhValue = whValue
            });
            affectedDates.Add(DateOnly.FromDateTime(timestamp.DateTime));
        }

        if (newRows.Count == 0)
            return;

        db.SmartPlugIntervalData.AddRange(newRows);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex,
                "Concurrent import detected for plug {PlugId}: another import already inserted overlapping intervals. Skipping this invocation.",
                plugId);
            return;
        }

        foreach (var date in affectedDates)
        {
            var dayStart = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var dayEnd = dayStart.AddDays(1);

            var totalWh = await db.SmartPlugIntervalData
                .Where(d => d.FlatId == flatId && d.PlugId == plugId && d.Timestamp >= dayStart && d.Timestamp < dayEnd)
                .SumAsync(d => d.WhValue, ct);
            var kwh = totalWh / 1000m;

            var daily = await db.SmartPlugDailyData
                .SingleOrDefaultAsync(d => d.FlatId == flatId && d.PlugId == plugId && d.Date == date, ct);

            if (daily is null)
            {
                db.SmartPlugDailyData.Add(new SmartPlugDailyData
                {
                    FlatId = flatId,
                    PlugId = plugId,
                    Date = date,
                    KwhValue = kwh,
                    IsInterpolated = false
                });
            }
            else
            {
                daily.KwhValue = kwh;
                daily.IsInterpolated = false;
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex,
                "Concurrent import detected for plug {PlugId} while upserting daily totals: another import already committed a conflicting row. Skipping this invocation.",
                plugId);
        }
    }

    internal static (string DeviceName, List<(DateTimeOffset Timestamp, decimal WhValue)> Rows) ParseFile(Stream stream, ILogger? logger = null)
    {
        try
        {
            using var reader = ExcelReaderFactory.CreateReader(stream);

            if (!reader.Read())
                throw new UnreadableFileException("Eve Home export is missing the device name row (row 1).");
            var deviceName = StripPrefix(reader.GetValue(0)?.ToString(), "Gerät: ") ?? string.Empty;

            if (!reader.Read())
                throw new UnreadableFileException("Eve Home export is missing the room name row (row 2).");
            _ = StripPrefix(reader.GetValue(0)?.ToString(), "Raum: "); // informational only per AC1, not persisted anywhere

            if (!reader.Read())
                throw new UnreadableFileException("Eve Home export is missing the home name row (row 3).");
            // Row 3 (A3, "Zuhause: ") — not mentioned in any AC, discarded

            if (!reader.Read())
                throw new UnreadableFileException("Eve Home export is missing the header row (row 4).");
            // Row 4 — header row (Datum / Gesamtverbrauch (Wh)), discarded

            var rows = new List<(DateTimeOffset, decimal)>();
            while (reader.Read())
            {
                var rawTimestamp = reader.GetValue(0);
                var rawWh = reader.GetValue(1);

                if (rawTimestamp is null || rawWh is null)
                    continue;

                var timestamp = ExtractWallClockTimestamp(rawTimestamp);

                decimal whValue;
                switch (rawWh)
                {
                    case double d when !double.IsNaN(d) && !double.IsInfinity(d)
                        && d >= (double)decimal.MinValue && d <= (double)decimal.MaxValue:
                        whValue = (decimal)d;
                        break;
                    case int i:
                        whValue = i;
                        break;
                    case decimal dec:
                        whValue = dec;
                        break;
                    case string s when decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedValue):
                        whValue = parsedValue;
                        break;
                    default:
                        logger?.LogWarning("Eve Home row skipped: unparseable Wh value '{RawValue}' ({RawType}).", rawWh, rawWh.GetType().Name);
                        continue; // stray non-numeric/out-of-range value — treat as an empty-value row, skip
                }

                if (whValue < 0)
                {
                    logger?.LogWarning("Eve Home row skipped: negative Wh value {WhValue}.", whValue);
                    continue;
                }

                var offsetTimestamp = new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Unspecified), TimeSpan.Zero);
                rows.Add((offsetTimestamp, whValue));
            }

            return (deviceName, rows);
        }
        catch (UnreadableFileException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new UnreadableFileException($"Unable to parse Eve Home export: {ex.Message}");
        }
    }

    private static string? StripPrefix(string? value, string prefix) =>
        value is not null && value.StartsWith(prefix, StringComparison.Ordinal)
            ? value[prefix.Length..]
            : value;

    // ExcelDataReader returns either a plain DateTime or a DateTimeOffset for t="d" cells, depending on the
    // cell's style — confirmed by inspecting the real sample files, where date cells with a style reference
    // (s="4") come back as DateTimeOffset tagged with the reading machine's local system offset, not a real
    // UTC/plug-timezone claim. Either way only the wall-clock component is kept; any attached offset or Kind
    // is discarded here, never applied as an adjustment, so the local time actually encoded in the cell is
    // preserved unchanged for the caller to re-wrap with the fixed zero offset (see AC1).
    internal static DateTime ExtractWallClockTimestamp(object rawTimestamp) => rawTimestamp switch
    {
        DateTime dt => dt,
        DateTimeOffset dto => dto.DateTime,
        string s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) => parsed,
        _ => throw new UnreadableFileException(
            $"Unable to parse Eve Home export: unreadable timestamp value '{rawTimestamp}'.")
    };
}
