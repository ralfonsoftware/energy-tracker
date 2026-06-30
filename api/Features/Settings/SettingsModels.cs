namespace EnergyTracker.Api.Features.Settings;

public record UserSettingsResponse(
    string? Locale,
    bool HasFlat,
    Guid? FlatId,
    string? FlatName,
    decimal? AnnualKwhBaseline,
    decimal? PlannedAnnualSpend
);

public record UpdateUserSettingsRequest(string Locale);
