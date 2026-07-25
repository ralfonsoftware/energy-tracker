namespace EnergyTracker.Api.Data.Entities;

public enum InsightRunStatus
{
    Pending,
    Processing,
    Complete,
    Failed
}

public class InsightRun
{
    public Guid RunId { get; set; }
    public Guid FlatId { get; set; }
    public InsightRunStatus Status { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Flat Flat { get; set; } = null!;
}
