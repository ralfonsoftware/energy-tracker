using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace api.Tests.Data;

// SQLite-targeted sibling of AppDbContext, used only by the api.Tests/Integration/ tier
// (see api.Tests/Integration/SqliteIntegrationTestBase.cs). Lives in the test project so
// SQLite-only packages and generated migrations never ship in the deployed Function App.
public class SqliteAppDbContext(DbContextOptions<SqliteAppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Flat> Flats => Set<Flat>();
    public DbSet<Tariff> Tariffs => Set<Tariff>();
    public DbSet<MeterReading> MeterReadings => Set<MeterReading>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<PowerPoint> PowerPoints => Set<PowerPoint>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceAssignmentPeriod> DeviceAssignmentPeriods => Set<DeviceAssignmentPeriod>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<SmartPlugDailyData> SmartPlugDailyData => Set<SmartPlugDailyData>();
    public DbSet<SmartPlugIntervalData> SmartPlugIntervalData => Set<SmartPlugIntervalData>();
    public DbSet<InsightRun> InsightRuns => Set<InsightRun>();
    public DbSet<Insight> Insights => Set<Insight>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // SQLite has no database-generated concurrency tokens (IsRowVersion() relies on
        // ValueGeneratedOnAddOrUpdate(), unsupported by this provider — see Microsoft Learn
        // "SQLite EF Core Database Provider Limitations"). Downgrade to a manually-managed
        // concurrency token; none of this test tier's scenarios exercise concurrency conflicts.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var rowVersion = entityType.FindProperty("RowVersion");
            if (rowVersion is null)
                continue;

            rowVersion.ValueGenerated = ValueGenerated.Never;
            rowVersion.IsConcurrencyToken = true;
        }

        // SQLite does not enforce decimal column precision/scale natively. Round-trip through
        // a rounding converter for the columns this test tier exercises so truncation behavior
        // matches production's decimal(18,4)/decimal(18,6) column types.
        modelBuilder.Entity<MeterReading>()
            .Property(r => r.KwhValue)
            .HasConversion(v => Math.Round(v, 4), v => v);

        modelBuilder.Entity<Tariff>()
            .Property(t => t.PricePerKwh)
            .HasConversion(v => Math.Round(v, 6), v => v);
    }
}
