namespace EnergyTracker.WebApi.Data;

public class EnergyReading
{
    public int EnergyReadingId { get; set; }
    public DateTime Timestamp { get; set; }
    public decimal? EnergyUsageInkWh { get; set; }
    public decimal? PowerInW { get; set; }
    public string? RawData { get; set; }
    
    public int DeviceId { get; set; }
    public Device Device { get; set; } = null!;
}