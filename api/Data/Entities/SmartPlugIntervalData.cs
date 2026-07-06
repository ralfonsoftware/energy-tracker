namespace EnergyTracker.Api.Data.Entities;

public class SmartPlugIntervalData
{
    public Guid Id { get; set; }
    public required string PlugId { get; set; }
    public Guid FlatId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public decimal WhValue { get; set; }
    public Flat Flat { get; set; } = null!;
}
