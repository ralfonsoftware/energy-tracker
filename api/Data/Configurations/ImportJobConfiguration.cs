using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyTracker.Api.Data.Configurations;

public class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> builder)
    {
        builder.ToTable("ImportJobs");
        builder.HasKey(j => j.ImportJobId);
        builder.Property(j => j.ImportJobId).ValueGeneratedOnAdd();
        builder.Property(j => j.FlatId).IsRequired();
        builder.Property(j => j.PlugId).IsRequired();
        builder.Property(j => j.OriginalFileName).IsRequired();
        builder.Property(j => j.Status).IsRequired();
        builder.Property(j => j.CreatedAt).IsRequired();
        builder.Property(j => j.CompletedAt).IsRequired(false);
        builder.Property(j => j.ErrorCategory).IsRequired(false);
        builder.Property(j => j.RowVersion).IsRowVersion();
        builder.Property(j => j.GapNotifications).IsRequired(false);
        builder.HasOne(j => j.Flat)
            .WithMany()
            .HasForeignKey(j => j.FlatId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
