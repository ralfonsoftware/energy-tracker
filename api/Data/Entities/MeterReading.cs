namespace EnergyTracker.Api.Data.Entities;

public class MeterReading
{
    public Guid ReadingId { get; set; }
    public Guid FlatId { get; set; }
    public decimal KwhValue { get; set; }
    public DateTimeOffset ReadingDate { get; set; }
    public bool IsCorrected { get; set; }
    public decimal? OriginalKwhValue { get; set; }
    public Flat Flat { get; set; } = null!;
}
