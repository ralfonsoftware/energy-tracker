namespace EnergyTracker.Api.Data.Entities;

public enum InsightType
{
    Standby,
    Replacement,
    Budget,
    InvoiceDeviation
}

public class Insight
{
    public Guid InsightId { get; set; }
    public Guid FlatId { get; set; }
    public Guid? RunId { get; set; }
    public InsightType Type { get; set; }
    public Guid? DeviceId { get; set; }
    public required string Data { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsDismissed { get; set; }
    public DateTimeOffset? DismissedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Flat Flat { get; set; } = null!;
    public InsightRun? Run { get; set; }
    public Device? Device { get; set; }
}
