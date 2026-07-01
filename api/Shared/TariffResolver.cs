using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Shared;

public class TariffResolver(AppDbContext db)
{
    public async Task<Tariff?> ResolveAsync(Guid flatId, DateTimeOffset date, CancellationToken ct)
        => await db.Tariffs
            .Where(t => t.FlatId == flatId && t.EffectiveDate <= date)
            .OrderByDescending(t => t.EffectiveDate)
            .FirstOrDefaultAsync(ct);
}
