using System.Text.Json;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Shared;

public static class InsightDeduplication
{
    private const decimal RelativeTolerance = 0.05m;

    private static readonly Dictionary<InsightType, string> PrimaryValueProperty = new()
    {
        [InsightType.Standby] = "estimatedMonthlyCost",
        [InsightType.Replacement] = "estimatedSavingsEur",
        [InsightType.Budget] = "overspendEur",
        [InsightType.InvoiceDeviation] = "impliedDeltaEur"
    };

    /// <summary>
    /// Returns <see langword="true"/> when the most recently stored <see cref="Insight"/> for the
    /// given <paramref name="flatId"/>/<paramref name="type"/>/<paramref name="deviceId"/> identity has
    /// a primary quantified figure within 5% (symmetric relative tolerance) of <paramref name="newPrimaryValue"/>.
    /// Returns <see langword="false"/> when no prior row exists for that identity, or when the prior
    /// row's <see cref="Insight.Data"/> JSON is missing or has a non-numeric value for the expected
    /// property — a parse failure is never a duplicate and never throws.
    /// </summary>
    public static async Task<bool> IsNearDuplicateOfMostRecentAsync(
        AppDbContext db, Guid flatId, InsightType type, Guid? deviceId, decimal newPrimaryValue, CancellationToken ct)
    {
        var mostRecent = await db.Insights.AsNoTracking()
            .Where(i => i.FlatId == flatId && i.Type == type && i.DeviceId == deviceId)
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.InsightId)
            .FirstOrDefaultAsync(ct);

        if (mostRecent is null)
            return false;

        if (mostRecent.IsDismissed)
            return true;

        var existingValue = ExtractPrimaryValue(mostRecent.Data, type);
        if (existingValue is null)
            return false;

        var reference = Math.Max(Math.Abs(newPrimaryValue), Math.Abs(existingValue.Value));
        return Math.Abs(newPrimaryValue - existingValue.Value) <= RelativeTolerance * reference;
    }

    private static decimal? ExtractPrimaryValue(string data, InsightType type)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            if (!PrimaryValueProperty.TryGetValue(type, out var propertyName) ||
                !doc.RootElement.TryGetProperty(propertyName, out var property))
                return null;

            return property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var value) ? value : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
