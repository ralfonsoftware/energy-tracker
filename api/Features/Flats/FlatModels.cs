namespace EnergyTracker.Api.Features.Flats;

public record PatchFlatRequest(
    string? Name,
    decimal? AnnualKwhBaseline,
    bool PlannedAnnualSpendProvided,
    decimal? PlannedAnnualSpend,
    byte[] RowVersion
);

public record FlatResponse(Guid FlatId, string Name, decimal AnnualKwhBaseline, decimal? PlannedAnnualSpend, byte[] RowVersion);

public record FlatSummary(Guid FlatId, string Name, decimal AnnualKwhBaseline, decimal SpikeThreshold, decimal? PlannedAnnualSpend);

public record CreateFlatRequest(string? Name, decimal AnnualKwhBaseline, decimal? PlannedAnnualSpend);
