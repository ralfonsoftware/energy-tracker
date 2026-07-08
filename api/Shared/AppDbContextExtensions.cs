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

    // Loads every Flat-scoped child row into the change tracker before the Flat is removed, so EF Core's
    // configured OnDelete(Cascade) fires deterministically under the InMemory test provider (which, unlike
    // real SQL Server, only cascades to rows already tracked in the current DbContext). Extend this method
    // when a new Flat-scoped child table is added.
    public static async Task LoadFlatCascadeChildrenAsync(this AppDbContext db, Guid flatId, CancellationToken ct)
    {
        await db.MeterReadings.Where(r => r.FlatId == flatId).LoadAsync(ct);
        await db.Tariffs.Where(t => t.FlatId == flatId).LoadAsync(ct);
        await db.Rooms.Where(r => r.FlatId == flatId).LoadAsync(ct);
        await db.LoadPowerPointsAndDevicesAsync(flatId, ct);
        await db.ImportJobs.Where(j => j.FlatId == flatId).LoadAsync(ct);
        await db.SmartPlugDailyData.Where(d => d.FlatId == flatId).LoadAsync(ct);
        await db.SmartPlugIntervalData.Where(d => d.FlatId == flatId).LoadAsync(ct);
    }
}
