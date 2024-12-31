namespace EnergyTracker.WebApi.Data;

public class MeterReading
{
    public int MeterReadingId { get; set; }
    public DateTime Timestamp { get; set; }
    public decimal ValueInKWh { get; set; }
    
    public int MeterId { get; set; }
    public Meter Meter { get; set; } = null!;
}