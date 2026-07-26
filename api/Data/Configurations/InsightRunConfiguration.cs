using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyTracker.Api.Data.Configurations;

public class InsightRunConfiguration : IEntityTypeConfiguration<InsightRun>
{
    public void Configure(EntityTypeBuilder<InsightRun> builder)
    {
        builder.ToTable("InsightRuns");
        builder.HasKey(r => r.RunId);
        builder.Property(r => r.RunId).ValueGeneratedOnAdd();
        builder.Property(r => r.FlatId).IsRequired();
        builder.Property(r => r.Status).IsRequired();
        builder.Property(r => r.StartedAt).IsRequired();
        builder.Property(r => r.CompletedAt).IsRequired(false);
        builder.Property(r => r.RowVersion).IsRowVersion();
        builder.HasOne(r => r.Flat)
            .WithMany()
            .HasForeignKey(r => r.FlatId)
            .OnDelete(DeleteBehavior.Cascade);

        // Filtered unique index instead of an app-level lock: enforces "at most one active
        // run per flat" at the DB level, closing the TOCTOU window between TriggerInsightsFunction's
        // existence check and its insert under concurrent requests.
        builder.HasIndex(r => r.FlatId)
            .IsUnique()
            .HasDatabaseName("IX_InsightRuns_FlatId_ActiveOnly")
            .HasFilter("[Status] IN (0, 1)");
    }
}
