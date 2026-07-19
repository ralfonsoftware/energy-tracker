using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Shared;

public static class ConcurrencyExtensions
{
    public static bool TryParseRowVersion(string? base64, out byte[] rowVersion)
    {
        rowVersion = [];
        if (string.IsNullOrWhiteSpace(base64)) return false;
        try { rowVersion = Convert.FromBase64String(base64); return true; }
        catch (FormatException) { return false; }
    }

    public static void ApplyRowVersionCheck<TEntity>(this DbContext db, TEntity entity, byte[] rowVersion)
        where TEntity : class =>
        db.Entry(entity).Property("RowVersion").OriginalValue = rowVersion;
}
