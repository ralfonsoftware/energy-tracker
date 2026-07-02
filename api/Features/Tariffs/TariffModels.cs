namespace EnergyTracker.Api.Features.Tariffs;

public record CreateTariffRequest(
    DateTimeOffset? EffectiveDate,
    decimal PricePerKwh,
    decimal MonthlyBaseFee,
    string? ProviderName,
    DateTimeOffset? ContractStartDate,
    int? ContractDurationMonths);

public record PatchTariffRequest(
    decimal? PricePerKwh,
    decimal? MonthlyBaseFee,
    string? ProviderName,
    DateTimeOffset? ContractStartDate,
    int? ContractDurationMonths,
    bool LockOverride);

public record TariffResponse(
    Guid TariffId,
    DateTimeOffset EffectiveDate,
    decimal PricePerKwh,
    decimal MonthlyBaseFee,
    string? ProviderName,
    DateTimeOffset? ContractStartDate,
    int? ContractDurationMonths,
    bool IsLocked);

public static class TariffLockPolicy
{
    public static bool IsLocked(DateTimeOffset? contractStartDate, int? contractDurationMonths) =>
        contractStartDate.HasValue && contractStartDate.Value < DateTimeOffset.UtcNow && contractDurationMonths.HasValue;
}
