namespace EnergyTracker.Api.Features.Flats;

public record PatchFlatRequest(
    string? Name,
    decimal? AnnualKwhBaseline,
    bool PlannedAnnualSpendProvided,
    decimal? PlannedAnnualSpend
);

public record FlatResponse(Guid FlatId, string Name, decimal AnnualKwhBaseline, decimal? PlannedAnnualSpend);

public record FlatSummary(Guid FlatId, string Name, decimal AnnualKwhBaseline, decimal SpikeThreshold, decimal? PlannedAnnualSpend);

public record CreateFlatRequest(string? Name, decimal AnnualKwhBaseline, decimal? PlannedAnnualSpend);
