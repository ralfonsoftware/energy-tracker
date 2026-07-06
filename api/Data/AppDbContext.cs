using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Flat> Flats => Set<Flat>();
    public DbSet<Tariff> Tariffs => Set<Tariff>();
    public DbSet<MeterReading> MeterReadings => Set<MeterReading>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<PowerPoint> PowerPoints => Set<PowerPoint>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<SmartPlugDailyData> SmartPlugDailyData => Set<SmartPlugDailyData>();
    public DbSet<SmartPlugIntervalData> SmartPlugIntervalData => Set<SmartPlugIntervalData>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
