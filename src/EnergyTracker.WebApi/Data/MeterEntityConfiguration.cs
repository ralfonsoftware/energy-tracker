using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyTracker.WebApi.Data;

internal sealed class MeterEntityConfiguration : IEntityTypeConfiguration<Meter>
{
    public void Configure(EntityTypeBuilder<Meter> builder)
    {
        builder.HasKey(m => m.MeterId);
        builder.Property(m => m.Name).IsRequired().HasMaxLength(100);
        builder.Property(m => m.Location).HasMaxLength(100);
        builder.Property(m => m.Manufacturer).HasMaxLength(100);

        builder.HasMany(m => m.MeterReadings)
            .WithOne(mr => mr.Meter)
            .HasForeignKey(mr => mr.MeterId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasIndex(m => new { m.ApartmentId, m.Name }).IsUnique();
        builder.HasIndex(m => m.ApartmentId).HasDatabaseName("IX_Meter_ApartmentId");
    }
}