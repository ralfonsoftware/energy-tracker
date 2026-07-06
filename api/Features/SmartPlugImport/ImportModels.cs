using EnergyTracker.Api.Data.Entities;

namespace EnergyTracker.Api.Features.SmartPlugImport;

public record UploadImportResponse(Guid ImportJobId);

public record ImportJobStatusResponse(
    Guid ImportJobId,
    ImportStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    ImportErrorCategory? ErrorCategory,
    string? GapNotifications);

public record GapNotification(string PlugId, DateOnly Start, DateOnly End);

public class UnreadableFileException(string message) : Exception(message);

public class ImportServiceUnavailableException(string message) : Exception(message);

public class OverAttributionException(string message, decimal attributedKwh, decimal mainMeterTotal, decimal tolerance)
    : Exception(message)
{
    public decimal AttributedKwh { get; } = attributedKwh;
    public decimal MainMeterTotal { get; } = mainMeterTotal;
    public decimal Tolerance { get; } = tolerance;
}
