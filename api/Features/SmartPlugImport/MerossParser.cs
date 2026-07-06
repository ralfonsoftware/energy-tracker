using System.Globalization;
using System.Text.RegularExpressions;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnergyTracker.Api.Features.SmartPlugImport;

public class MerossParser(AppDbContext db, ILogger<MerossParser> logger)
{
    private static readonly Regex DeviceNamePattern =
        new(@"^Power Monitor Day Data - (.+) - \d{8}\.csv$", RegexOptions.IgnoreCase);

    public async Task ParseAndStoreAsync(Guid flatId, string plugId, string fileName, Stream fileStream, CancellationToken ct)
    {
        var (_, rows) = await ParseFileAsync(fileName, fileStream, logger, ct);

        if (rows.Count == 0)
            return;

        var byDate = new Dictionary<DateOnly, decimal>();
        foreach (var group in rows.GroupBy(r => r.Date))
        {
            if (group.Count() > 1)
                logger.LogWarning("Meross file has {Count} rows for date {Date}; using the last value.", group.Count(), group.Key);
            byDate[group.Key] = group.Last().KwhValue;
        }
        var dates = byDate.Keys.ToList();
        var existing = await db.SmartPlugDailyData
            .Where(d => d.FlatId == flatId && d.PlugId == plugId && dates.Contains(d.Date))
            .ToDictionaryAsync(d => d.Date, ct);

        foreach (var (date, kwh) in byDate)
        {
            if (existing.TryGetValue(date, out var daily))
            {
                daily.KwhValue = kwh;
                daily.IsInterpolated = false;
            }
            else
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
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex,
                "Concurrent import detected for plug {PlugId}: another import already committed a conflicting row. Skipping this invocation.",
                plugId);
        }
    }

    internal static async Task<(string DeviceName, List<(DateOnly Date, decimal KwhValue)> Rows)> ParseFileAsync(
        string fileName, Stream stream, ILogger? logger = null, CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync(ct);

        if (content.Trim().Length == 0)
            throw new UnreadableFileException("Meross export is empty.");

        var match = DeviceNamePattern.Match(fileName);
        var deviceName = match.Success ? match.Groups[1].Value : fileName;
        if (!match.Success)
            logger?.LogWarning("Meross filename '{FileName}' did not match the expected pattern; using it as the device name.", fileName);
        logger?.LogInformation("Meross file device name: {DeviceName}", deviceName);

        var lines = content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        if (lines.Length > 0 && !lines[0].Contains("Power Consumption", StringComparison.OrdinalIgnoreCase))
            logger?.LogWarning("Meross file header line did not match the expected shape: '{HeaderLine}'.", lines[0]);

        var rows = new List<(DateOnly, decimal)>();

        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Trim().Length == 0)
                continue;

            var normalized = line.Replace("\t,", "\t").Trim();
            var parts = normalized.Split('\t', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
            {
                logger?.LogWarning("Meross row skipped: unexpected shape '{RawLine}'.", line);
                continue;
            }

            if (!DateOnly.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                logger?.LogWarning("Meross row skipped: unparseable date '{RawValue}'.", parts[0]);
                continue;
            }

            if (!decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var kwh))
            {
                logger?.LogWarning("Meross row skipped: unparseable value '{RawValue}'.", parts[1]);
                continue;
            }

            if (kwh < 0)
            {
                logger?.LogWarning("Meross row skipped: negative value {KwhValue}.", kwh);
                continue;
            }

            rows.Add((date, kwh));
        }

        return (deviceName, rows);
    }
}
