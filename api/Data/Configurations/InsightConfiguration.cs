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

        // SetNull (not Cascade): Insight already has a direct cascade path from Flat via
        // FlatId. Device also cascades from Flat via Room -> PowerPoint -> Device. If
        // DeviceId cascaded too, SQL Server would reject the model at migration/deploy
        // time for multiple cascade paths reaching Insights from Flat.
        builder.HasOne(i => i.Device)
            .WithMany()
            .HasForeignKey(i => i.DeviceId)
            .OnDelete(DeleteBehavior.SetNull);

        // SetNull: epic-specified — an InsightRun's deletion must not remove the
        // Insight rows it produced.
        builder.HasOne(i => i.Run)
            .WithMany()
            .HasForeignKey(i => i.RunId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(i => new { i.FlatId, i.Type, i.CreatedAt })
            .HasDatabaseName("IX_Insights_FlatId_Type_CreatedAt")
            .IsDescending(false, false, true);
    }
}
