namespace EnergyTracker.Api.Features.Flats;

public record PatchFlatRequest(
    string? Name,
    decimal? AnnualKwhBaseline,
    bool PlannedAnnualSpendProvided,
    decimal? PlannedAnnualSpend
);

public record FlatResponse(Guid FlatId, string Name, decimal AnnualKwhBaseline, decimal? PlannedAnnualSpend);
