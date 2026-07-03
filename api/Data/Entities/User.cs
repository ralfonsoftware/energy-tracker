namespace EnergyTracker.Api.Data.Entities;

public class User
{
    public required string UserId { get; set; }
    public string? LocaleOverride { get; set; }
    public Guid? ActiveFlatId { get; set; }
}
