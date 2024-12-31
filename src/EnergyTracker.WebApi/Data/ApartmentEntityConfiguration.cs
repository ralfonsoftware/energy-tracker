using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyTracker.WebApi.Data;

internal sealed class ApartmentEntityConfiguration : IEntityTypeConfiguration<Apartment>
{
    public void Configure(EntityTypeBuilder<Apartment> builder)
    {
        builder.HasKey(a => a.ApartmentId);
        builder.Property(a => a.Name).IsRequired().HasMaxLength(100);

        builder.HasMany(a => a.Meters)
            .WithOne(m => m.Apartment)
            .HasForeignKey(m => m.ApartmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Rooms)
            .WithOne(r => r.Apartment)
            .HasForeignKey(r => r.ApartmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Devices)
            .WithOne(d => d.Apartment)
            .HasForeignKey(d => d.ApartmentId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasIndex(a => a.Name).HasDatabaseName("IX_Apartment_Name");
    }
}