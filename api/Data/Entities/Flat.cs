namespace EnergyTracker.Api.Data.Entities;

public class Flat
{
    public Guid FlatId { get; set; }
    public required string UserId { get; set; }
    public required string Name { get; set; }
    public decimal AnnualKwhBaseline { get; set; }
    public decimal SpikeThreshold { get; set; }
    public decimal? PlannedAnnualSpend { get; set; }
    public User User { get; set; } = null!;
}
