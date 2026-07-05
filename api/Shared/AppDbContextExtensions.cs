using EnergyTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Shared;

public static class AppDbContextExtensions
{
    public static async Task LoadPowerPointsAndDevicesAsync(this AppDbContext db, Guid flatId, CancellationToken ct)
    {
        await db.PowerPoints.Where(pp => pp.Room.FlatId == flatId).LoadAsync(ct);
        await db.Devices.Where(d => d.PowerPoint.Room.FlatId == flatId).LoadAsync(ct);
    }
}
