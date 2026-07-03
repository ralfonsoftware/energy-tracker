namespace EnergyTracker.Api.Features.Tariffs;

public record CreateTariffRequest(
    DateTimeOffset? ContractStartDate,
    decimal PricePerKwh,
    decimal MonthlyBaseFee,
    string? ProviderName,
    int? ContractDurationMonths);

public record PatchTariffRequest(
    decimal? PricePerKwh,
    decimal? MonthlyBaseFee,
    bool ProviderNameProvided,
    string? ProviderName,
    bool ContractDurationMonthsProvided,
    int? ContractDurationMonths,
    bool LockOverride);

public record TariffResponse(
    Guid TariffId,
    DateTimeOffset ContractStartDate,
    decimal PricePerKwh,
    decimal MonthlyBaseFee,
    string? ProviderName,
    int? ContractDurationMonths,
    bool IsLocked);

public static class TariffLockPolicy
{
    public static bool IsLocked(DateTimeOffset contractStartDate) =>
        contractStartDate <= DateTimeOffset.UtcNow;
}
