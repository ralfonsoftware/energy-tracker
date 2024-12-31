using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyTracker.WebApi.Data;

internal sealed class MeterReadingEntityConfiguration : IEntityTypeConfiguration<MeterReading>
{
    public void Configure(EntityTypeBuilder<MeterReading> builder)
    {
        builder.HasKey(mr => mr.MeterReadingId);
        builder.Property(mr => mr.Timestamp).IsRequired();
        builder.Property(mr => mr.ValueInKWh).IsRequired().HasColumnType("decimal(18, 3)");
        
        builder.HasIndex(mr => new { mr.MeterId, mr.Timestamp }).HasDatabaseName("IX_MeterReading_MeterId_Timestamp");
    }
}