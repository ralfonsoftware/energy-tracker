using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyTracker.Api.Data.Configurations;

public class SmartPlugDailyDataConfiguration : IEntityTypeConfiguration<SmartPlugDailyData>
{
    public void Configure(EntityTypeBuilder<SmartPlugDailyData> builder)
    {
        builder.ToTable("SmartPlugDailyData");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedOnAdd();
        builder.Property(d => d.PlugId).IsRequired();
        builder.Property(d => d.FlatId).IsRequired();
        builder.Property(d => d.Date).IsRequired();
        builder.Property(d => d.KwhValue).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(d => d.IsInterpolated).IsRequired();
        builder.HasOne(d => d.Flat)
            .WithMany()
            .HasForeignKey(d => d.FlatId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(d => new { d.FlatId, d.PlugId, d.Date })
            .IsUnique()
            .HasDatabaseName("IX_SmartPlugDailyData_FlatId_PlugId_Date");
    }
}
