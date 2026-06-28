using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyTracker.Api.Data.Configurations;

public class FlatConfiguration : IEntityTypeConfiguration<Flat>
{
    public void Configure(EntityTypeBuilder<Flat> builder)
    {
        builder.ToTable("Flats");
        builder.HasKey(f => f.FlatId);
        builder.Property(f => f.FlatId).ValueGeneratedOnAdd();
        builder.Property(f => f.UserId).HasMaxLength(450).IsRequired();
        builder.Property(f => f.Name).HasMaxLength(200).IsRequired();
        builder.Property(f => f.AnnualKwhBaseline).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(f => f.SpikeThreshold).HasColumnType("decimal(18,4)").HasDefaultValue(2.0m).HasSentinel(-1m).IsRequired();
        builder.Property(f => f.PlannedAnnualSpend).HasColumnType("decimal(18,4)").IsRequired(false);
        builder.HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
