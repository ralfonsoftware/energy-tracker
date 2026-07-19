namespace EnergyTracker.Api.Features.Settings;

public record UserSettingsResponse(
    string? Locale,
    bool HasFlat,
    Guid? FlatId,
    string? FlatName,
    decimal? AnnualKwhBaseline,
    decimal? PlannedAnnualSpend,
    Guid? ActiveFlatId,
    byte[]? FlatRowVersion
);
