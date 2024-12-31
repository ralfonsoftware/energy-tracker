using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.WebApi.Data;

public sealed class EnergyTrackerContext : DbContext
{
    public DbSet<Apartment> Apartments => Set<Apartment>();
    public DbSet<Meter> Meters => Set<Meter>();
    public DbSet<MeterReading> MeterReadings => Set<MeterReading>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<EnergyReading> EnergyReadings => Set<EnergyReading>();
    
    public EnergyTrackerContext(DbContextOptions<EnergyTrackerContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.ApplyConfiguration(new ApartmentEntityConfiguration());
        modelBuilder.ApplyConfiguration(new MeterEntityConfiguration());
        modelBuilder.ApplyConfiguration(new MeterReadingEntityConfiguration());
        modelBuilder.ApplyConfiguration(new RoomEntityConfiguration());
        modelBuilder.ApplyConfiguration(new DeviceEntityConfiguration());
        modelBuilder.ApplyConfiguration(new EnergyReadingEntityConfiguration());
    }
}