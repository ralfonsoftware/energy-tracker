namespace EnergyTracker.WebApi.Data;

public class Meter
{
    public int MeterId { get; set; }
    public string Name { get; set; } = null!;
    public string? Location { get; set; }
    public string? Manufacturer { get; set; }
    
    public int ApartmentId { get; set; }
    public Apartment Apartment { get; set; } = null!;

    public ICollection<MeterReading> MeterReadings { get; set; } = null!;
}