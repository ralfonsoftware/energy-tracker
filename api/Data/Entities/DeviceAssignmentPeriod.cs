namespace EnergyTracker.Api.Data.Entities;

public class DeviceAssignmentPeriod
{
    public Guid Id { get; set; }
    public Guid DeviceId { get; set; }
    public Guid PowerPointId { get; set; }
    public Guid FlatId { get; set; }
    public DateTimeOffset From { get; set; }
    public DateTimeOffset? To { get; set; }
    public Device Device { get; set; } = null!;
}
