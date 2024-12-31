namespace EnergyTracker.WebApi.Data;

public class Apartment
{
    public int ApartmentId { get; set; }
    public string Name { get; set; } = null!;

    public ICollection<Meter> Meters { get; set; } = null!;
    public ICollection<Room> Rooms { get; set; } = null!;
    public ICollection<Device> Devices { get; set; } = null!;
}