namespace EnergyTracker.Api.Data.Entities;

public class SmartPlugDailyData
{
    public Guid Id { get; set; }
    public required string PlugId { get; set; }
    public Guid FlatId { get; set; }
    public DateOnly Date { get; set; }
    public decimal KwhValue { get; set; }
    public bool IsInterpolated { get; set; }
    public Flat Flat { get; set; } = null!;
}
