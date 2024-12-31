using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyTracker.WebApi.Data;

internal sealed class RoomEntityConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.HasKey(r => r.RoomId);
        builder.Property(r => r.Name).IsRequired().HasMaxLength(100);
        builder.Property(r => r.Description).HasMaxLength(500);

        builder.HasMany(r => r.Devices)
            .WithOne(d => d.Room)
            .HasForeignKey(d => d.RoomId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasIndex(r => r.ApartmentId).HasDatabaseName("IX_Room_ApartmentId");
        builder.HasIndex(r => new { r.ApartmentId, r.Name }).IsUnique();
    }
}