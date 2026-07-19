namespace EnergyTracker.Api.Data.Entities;

public enum ConsumptionApproach
{
    None,
    EuLabel,
    SelfMeasured
}

public enum SelfMeasuredPeriod
{
    Daily,
    Weekly
}

public class Device
{
    public Guid DeviceId { get; set; }
    public Guid PowerPointId { get; set; }
    public required string Name { get; set; }
    public string? Type { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public DateTimeOffset? PurchaseDate { get; set; }
    public ConsumptionApproach ConsumptionApproach { get; set; }
    public string? EuLabelClass { get; set; }
    public decimal? EuAnnualKwh { get; set; }
    public decimal? SelfMeasuredKwh { get; set; }
    public SelfMeasuredPeriod? SelfMeasuredPeriod { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public PowerPoint PowerPoint { get; set; } = null!;
}
