using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyTracker.Api.Data.Configurations;

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("Rooms");
        builder.HasKey(r => r.RoomId);
        builder.Property(r => r.RoomId).ValueGeneratedOnAdd();
        builder.Property(r => r.FlatId).IsRequired();
        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.SortOrder).IsRequired();
        builder.Property(r => r.RowVersion).IsRowVersion();
        builder.HasOne(r => r.Flat)
            .WithMany()
            .HasForeignKey(r => r.FlatId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
