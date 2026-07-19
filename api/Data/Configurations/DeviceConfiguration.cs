using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyTracker.Api.Data.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("Devices");
        builder.HasKey(d => d.DeviceId);
        builder.Property(d => d.DeviceId).ValueGeneratedOnAdd();
        builder.Property(d => d.PowerPointId).IsRequired();
        builder.Property(d => d.Name).HasMaxLength(200).IsRequired();
        builder.Property(d => d.Type).HasMaxLength(200).IsRequired(false);
        builder.Property(d => d.Manufacturer).HasMaxLength(200).IsRequired(false);
        builder.Property(d => d.Model).HasMaxLength(200).IsRequired(false);
        builder.Property(d => d.PurchaseDate).IsRequired(false);
        builder.Property(d => d.ConsumptionApproach).IsRequired();
        builder.Property(d => d.EuLabelClass).HasMaxLength(200).IsRequired(false);
        builder.Property(d => d.EuAnnualKwh).HasColumnType("decimal(18,4)").IsRequired(false);
        builder.Property(d => d.SelfMeasuredKwh).HasColumnType("decimal(18,4)").IsRequired(false);
        builder.Property(d => d.SelfMeasuredPeriod).IsRequired(false);
        builder.Property(d => d.RowVersion).IsRowVersion();
        builder.HasOne(d => d.PowerPoint)
            .WithMany(pp => pp.Devices)
            .HasForeignKey(d => d.PowerPointId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
