namespace EnergyTracker.Api.Features.Onboarding;

public record CompleteOnboardingRequest(
    string FlatName,
    decimal AnnualKwhBaseline,
    decimal? PlannedAnnualSpend,
    decimal PricePerKwh,
    decimal MonthlyBaseFee,
    string? ProviderName,
    DateTimeOffset? ContractStartDate,
    int? ContractDurationMonths
);
