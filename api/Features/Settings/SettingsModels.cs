namespace EnergyTracker.Api.Features.Settings;

public record UserSettingsResponse(string? Locale, bool HasFlat);
public record UpdateUserSettingsRequest(string Locale);
