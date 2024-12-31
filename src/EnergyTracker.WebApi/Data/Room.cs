namespace EnergyTracker.WebApi.Data;

public class Room
{
    public int RoomId { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = string.Empty;

    public int ApartmentId { get; set; }
    public Apartment Apartment { get; set; } = null!;

    public ICollection<Device> Devices { get; set; } = null!;
}