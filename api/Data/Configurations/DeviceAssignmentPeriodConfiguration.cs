using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyTracker.Api.Data.Configurations;

public class DeviceAssignmentPeriodConfiguration : IEntityTypeConfiguration<DeviceAssignmentPeriod>
{
    public void Configure(EntityTypeBuilder<DeviceAssignmentPeriod> builder)
    {
        builder.ToTable("DeviceAssignmentPeriods");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedOnAdd();
        builder.Property(p => p.DeviceId).IsRequired();
        builder.Property(p => p.PowerPointId).IsRequired();
        builder.Property(p => p.FlatId).IsRequired();
        builder.Property(p => p.From).IsRequired();
        builder.Property(p => p.To).IsRequired(false);
        builder.HasOne(p => p.Device)
            .WithMany()
            .HasForeignKey(p => p.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(p => new { p.DeviceId, p.From })
            .HasDatabaseName("IX_DeviceAssignmentPeriods_DeviceId_From");
        // Defense-in-depth for the "at most one open period per Device" invariant that
        // UpdateFlatStructureFunction's close-then-open logic maintains at the application level.
        builder.HasIndex(p => p.DeviceId)
            .IsUnique()
            .HasDatabaseName("IX_DeviceAssignmentPeriods_DeviceId_OneOpenPeriod")
            .HasFilter("[To] IS NULL");
    }
}
