namespace EnergyTracker.Api.Data.Entities;

public enum ImportStatus
{
    Pending,
    Processing,
    Complete,
    Failed
}

public enum ImportErrorCategory
{
    DataUnreadable,
    ProcessingFailed,
    ServiceUnavailable
}

public class ImportJob
{
    public Guid ImportJobId { get; set; }
    public Guid FlatId { get; set; }
    public required string PlugId { get; set; }
    public required string OriginalFileName { get; set; }
    public ImportStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public ImportErrorCategory? ErrorCategory { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public string? GapNotifications { get; set; }
    public Flat Flat { get; set; } = null!;
}
