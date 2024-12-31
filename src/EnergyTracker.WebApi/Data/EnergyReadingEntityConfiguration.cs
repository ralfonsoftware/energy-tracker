using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyTracker.WebApi.Data;

internal sealed class EnergyReadingEntityConfiguration : IEntityTypeConfiguration<EnergyReading>
{
    public void Configure(EntityTypeBuilder<EnergyReading> builder)
    {
        builder.HasKey(er => er.EnergyReadingId);
        builder.Property(er => er.Timestamp).IsRequired();
        builder.Property(er => er.EnergyUsageInkWh).HasColumnType("decimal(18, 3)");
        builder.Property(er => er.PowerInW).HasColumnType("decimal(18, 3)");
        builder.Property(er => er.RawData).HasColumnType("nvarchar(max)");
        
        builder.HasIndex(er => new { er.DeviceId, er.Timestamp }).HasDatabaseName("IX_EnergyReading_DeviceId_Timestamp");
        builder.Property(er => er.EnergyUsageInkWh).HasColumnType("decimal(18, 3)");
        builder.Property(er => er.PowerInW).HasColumnType("decimal(18, 3)");

        builder.ToTable(b =>
        {
            b.HasCheckConstraint("CK_EnergyReading_NonNegative", "[EnergyUsageInkWh] >= 0");
            b.HasCheckConstraint("CK_EnergyReading_NonNegativePower", "[PowerInW] >= 0");
        });
    }
}