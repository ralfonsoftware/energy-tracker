using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyTracker.Api.Data.Configurations;

public class PowerPointConfiguration : IEntityTypeConfiguration<PowerPoint>
{
    public void Configure(EntityTypeBuilder<PowerPoint> builder)
    {
        builder.ToTable("PowerPoints");
        builder.HasKey(pp => pp.PowerPointId);
        builder.Property(pp => pp.PowerPointId).ValueGeneratedOnAdd();
        builder.Property(pp => pp.RoomId).IsRequired();
        builder.Property(pp => pp.Name).HasMaxLength(200).IsRequired();
        builder.Property(pp => pp.PlugId).HasMaxLength(200).IsRequired(false);
        builder.Property(pp => pp.RowVersion).IsRowVersion();
        builder.HasOne(pp => pp.Room)
            .WithMany(room => room.PowerPoints)
            .HasForeignKey(pp => pp.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
