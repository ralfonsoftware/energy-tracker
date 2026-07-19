namespace EnergyTracker.Api.Features.Readings;

public record SubmitReadingRequest(decimal KwhValue, DateTimeOffset? ReadingDate);

public record PatchReadingRequest(decimal KwhValue, byte[] RowVersion);

public record ReadingResponse(
    Guid ReadingId,
    decimal KwhValue,
    DateTimeOffset ReadingDate,
    bool IsCorrected,
    decimal? OriginalKwhValue,
    byte[] RowVersion);
