namespace EnergyTracker.Api.Data.Entities;

public class Tariff
{
    public Guid TariffId { get; set; }
    public Guid FlatId { get; set; }
    public decimal PricePerKwh { get; set; }
    public decimal MonthlyBaseFee { get; set; }
    public string? ProviderName { get; set; }
    public DateTimeOffset ContractStartDate { get; set; }
    public int? ContractDurationMonths { get; set; }
    public Flat Flat { get; set; } = null!;
}
