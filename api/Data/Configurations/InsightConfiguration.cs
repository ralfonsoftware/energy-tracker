using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyTracker.Api.Data.Configurations;

public class InsightConfiguration : IEntityTypeConfiguration<Insight>
{
    public void Configure(EntityTypeBuilder<Insight> builder)
    {
        builder.ToTable("Insights");
        builder.HasKey(i => i.InsightId);
        builder.Property(i => i.InsightId).ValueGeneratedOnAdd();
        builder.Property(i => i.FlatId).IsRequired();
        builder.Property(i => i.RunId).IsRequired(false);
        builder.Property(i => i.Type).IsRequired();
        builder.Property(i => i.DeviceId).IsRequired(false);
        builder.Property(i => i.Data).IsRequired();
        builder.Property(i => i.CreatedAt).IsRequired();

        builder.HasOne(i => i.Flat)
            .WithMany()
            .HasForeignKey(i => i.FlatId)
            .OnDelete(DeleteBehavior.Cascade);

        // ClientSetNull (not SetNull): Insight already has a direct cascade path from Flat
        // via FlatId. Device also cascades from Flat via Room -> PowerPoint -> Device, so
        // Insights is reachable from Flat via a second path (Flat -> Room -> PowerPoint ->
        // Device -> Insights). SQL Server's multiple-cascade-paths check (Error 1785)
        // counts SetNull the same as Cascade when computing conflicting paths — a
        // DB-enforced SetNull here still conflicts with the direct Flat -> Insights cascade
        // above (confirmed empirically: SetNull alone was rejected by SQL Server even after
        // the InsightRuns path below was fixed). ClientSetNull nulls DeviceId in EF's change
        // tracker instead (NO ACTION at the DB level), avoiding the conflict.
        builder.HasOne(i => i.Device)
            .WithMany()
            .HasForeignKey(i => i.DeviceId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        // ClientSetNull (not SetNull): epic-specified — an InsightRun's deletion must not
        // remove the Insight rows it produced. Flat also cascades to InsightRuns directly
        // (InsightRunConfiguration), so Insights is reachable from Flat via a second path
        // (Flat -> InsightRuns -> Insights), which conflicts with the direct Flat ->
        // Insights cascade above for the same Error 1785 reason as the Device path.
        // ClientSetNull nulls RunId in EF's change tracker instead (NO ACTION at the DB
        // level), avoiding the conflict.
        builder.HasOne(i => i.Run)
            .WithMany()
            .HasForeignKey(i => i.RunId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasIndex(i => new { i.FlatId, i.Type, i.CreatedAt })
            .HasDatabaseName("IX_Insights_FlatId_Type_CreatedAt")
            .IsDescending(false, false, true);
    }
}
