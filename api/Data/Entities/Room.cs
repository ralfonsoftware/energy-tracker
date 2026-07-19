namespace EnergyTracker.Api.Data.Entities;

public class Room
{
    public Guid RoomId { get; set; }
    public Guid FlatId { get; set; }
    public required string Name { get; set; }
    public int SortOrder { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Flat Flat { get; set; } = null!;
    public ICollection<PowerPoint> PowerPoints { get; set; } = new List<PowerPoint>();
}
