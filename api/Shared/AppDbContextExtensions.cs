using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Shared;

public static class AppDbContextExtensions
{
    // Typed against the base DbContext (rather than AppDbContext) so the SQLite integration test
    // tier's SqliteAppDbContext can exercise this exact production loading logic too — see
    // api.Tests/Integration/FlatCascadeDeleteTests.cs.
    public static async Task LoadPowerPointsAndDevicesAsync(this DbContext db, Guid flatId, CancellationToken ct)
    {
        await db.Set<PowerPoint>().Where(pp => pp.Room.FlatId == flatId).LoadAsync(ct);
        await db.Set<Device>().Where(d => d.PowerPoint.Room.FlatId == flatId).LoadAsync(ct);
    }

    // Loads every Flat-scoped child row into the change tracker before the Flat is removed, so EF Core's
    // configured OnDelete(Cascade) fires deterministically under the InMemory test provider (which, unlike
    // real SQL Server, only cascades to rows already tracked in the current DbContext). Extend this method
    // when a new Flat-scoped child table is added.
    public static async Task LoadFlatCascadeChildrenAsync(this DbContext db, Guid flatId, CancellationToken ct)
    {
        await db.Set<MeterReading>().Where(r => r.FlatId == flatId).LoadAsync(ct);
        await db.Set<Tariff>().Where(t => t.FlatId == flatId).LoadAsync(ct);
        await db.Set<Room>().Where(r => r.FlatId == flatId).LoadAsync(ct);
        await db.LoadPowerPointsAndDevicesAsync(flatId, ct);
        await db.Set<DeviceAssignmentPeriod>().Where(p => p.FlatId == flatId).LoadAsync(ct);
        await db.Set<ImportJob>().Where(j => j.FlatId == flatId).LoadAsync(ct);
        await db.Set<SmartPlugDailyData>().Where(d => d.FlatId == flatId).LoadAsync(ct);
        await db.Set<SmartPlugIntervalData>().Where(d => d.FlatId == flatId).LoadAsync(ct);
        await db.Set<InsightRun>().Where(r => r.FlatId == flatId).LoadAsync(ct);
        await db.Set<Insight>().Where(i => i.FlatId == flatId).LoadAsync(ct);
    }
}
