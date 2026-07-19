using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnergyTracker.Api.Data.Configurations;

public class TariffConfiguration : IEntityTypeConfiguration<Tariff>
{
    public void Configure(EntityTypeBuilder<Tariff> builder)
    {
        builder.ToTable("Tariffs");
        builder.HasKey(t => t.TariffId);
        builder.Property(t => t.TariffId).ValueGeneratedOnAdd();
        builder.Property(t => t.FlatId).IsRequired();
        builder.Property(t => t.PricePerKwh).HasColumnType("decimal(18,6)").IsRequired();
        builder.Property(t => t.MonthlyBaseFee).HasColumnType("decimal(18,4)").IsRequired();
        builder.Property(t => t.ProviderName).HasMaxLength(200).IsRequired(false);
        builder.Property(t => t.ContractStartDate).IsRequired();
        builder.Property(t => t.ContractDurationMonths).IsRequired(false);
        builder.Property(t => t.RowVersion).IsRowVersion();
        builder.HasOne(t => t.Flat)
            .WithMany()
            .HasForeignKey(t => t.FlatId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(t => new { t.FlatId, t.ContractStartDate })
            .IsUnique()
            .HasDatabaseName("IX_Tariffs_FlatId_ContractStartDate");
    }
}
