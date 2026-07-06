using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyTracker.Api.Data.Configurations;

public class SmartPlugIntervalDataConfiguration : IEntityTypeConfiguration<SmartPlugIntervalData>
{
    public void Configure(EntityTypeBuilder<SmartPlugIntervalData> builder)
    {
        builder.ToTable("SmartPlugIntervalData");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedOnAdd();
        builder.Property(d => d.PlugId).IsRequired();
        builder.Property(d => d.FlatId).IsRequired();
        builder.Property(d => d.Timestamp).IsRequired();
        builder.Property(d => d.WhValue).HasColumnType("decimal(18,4)").IsRequired();
        builder.HasOne(d => d.Flat)
            .WithMany()
            .HasForeignKey(d => d.FlatId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(d => new { d.FlatId, d.PlugId, d.Timestamp })
            .HasDatabaseName("IX_SmartPlugIntervalData_FlatId_PlugId_Timestamp");
    }
}
