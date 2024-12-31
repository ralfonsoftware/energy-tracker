namespace EnergyTracker.WebApi.Data;

public class Device
{
    public int DeviceId { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string? Manufacturer { get; set; }
    public string? Specifications { get; set; }

    public int ApartmentId { get; set; }
    public Apartment Apartment { get; set; } = null!;
    
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    
    public ICollection<EnergyReading> EnergyReadings { get; set; } = null!;
}