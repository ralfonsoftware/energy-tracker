using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyTracker.Api.Data.Configurations;

public class MeterReadingConfiguration : IEntityTypeConfiguration<MeterReading>
{
    public void Configure(EntityTypeBuilder<MeterReading> builder)
    {
        builder.ToTable("MeterReadings");
        builder.HasKey(r => r.ReadingId);
        builder.Property(r => r.ReadingId).ValueGeneratedOnAdd();
        builder.Property(r => r.FlatId).IsRequired();
        builder.Property(r => r.KwhValue).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(r => r.ReadingDate).IsRequired();
        builder.Property(r => r.IsCorrected).IsRequired().HasDefaultValue(false);
        builder.Property(r => r.OriginalKwhValue).HasColumnType("decimal(18,4)").IsRequired(false);
        builder.Property(r => r.RowVersion).IsRowVersion();
        builder.HasOne(r => r.Flat)
            .WithMany()
            .HasForeignKey(r => r.FlatId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => new { r.FlatId, r.ReadingDate })
            .IsUnique()
            .HasDatabaseName("IX_MeterReadings_FlatId_ReadingDate");
    }
}
