using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyTracker.WebApi.Data;

internal sealed class DeviceEntityConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.HasKey(d => d.DeviceId);
        builder.Property(d => d.Name).IsRequired().HasMaxLength(100);
        builder.Property(d => d.Type).IsRequired().HasMaxLength(50);
        builder.Property(d => d.Manufacturer).HasMaxLength(100);
        builder.Property(d => d.Specifications).HasColumnType("nvarchar(max)");

        builder.HasMany(d => d.EnergyReadings)
            .WithOne(er => er.Device)
            .HasForeignKey(er => er.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasIndex(d => d.ApartmentId).HasDatabaseName("IX_Device_ApartmentId");
        builder.HasIndex(d => d.RoomId).HasDatabaseName("IX_Device_RoomId");
        builder.HasIndex(d => new { d.RoomId, d.Name }).IsUnique();
    }
}