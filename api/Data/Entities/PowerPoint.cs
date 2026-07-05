namespace EnergyTracker.Api.Data.Entities;

public class PowerPoint
{
    public Guid PowerPointId { get; set; }
    public Guid RoomId { get; set; }
    public required string Name { get; set; }
    public string? PlugId { get; set; }
    public Room Room { get; set; } = null!;
    public ICollection<Device> Devices { get; set; } = new List<Device>();
}
