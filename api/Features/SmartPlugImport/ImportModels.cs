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

public class UnreadableFileException(string message) : Exception(message);

public class ImportServiceUnavailableException(string message) : Exception(message);
