using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyTracker.Api.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.UserId);
        builder.Property(u => u.UserId).HasMaxLength(450).HasColumnType("nvarchar(450)").IsRequired();
        builder.Property(u => u.LocaleOverride).HasMaxLength(10).HasColumnType("nvarchar(10)").IsRequired(false);
        builder.Property(u => u.ActiveFlatId).IsRequired(false);
    }
}
