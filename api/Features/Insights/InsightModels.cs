using System.Text.Json;
using EnergyTracker.Api.Data.Entities;

namespace EnergyTracker.Api.Features.Insights;

public record TriggerInsightsResponse(Guid RunId);

// Status is the enum type (not string) — the global JsonStringEnumConverter (wired in
// Program.cs) serializes it as its string name, matching ImportJobStatusResponse's convention.
public record RunStatusDto(InsightRunStatus Status, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt);

// Data is a JsonElement (not string) so System.Text.Json embeds the detector's stored
// JSON as-is in the response body, rather than double-encoding it as an escaped string.
public record InsightDto(Guid InsightId, InsightType Type, Guid? DeviceId, JsonElement Data, DateTimeOffset CreatedAt);

public record InsightsResponse(RunStatusDto? RunStatus, IReadOnlyList<InsightDto> Insights);

public record InsightDiscoveryMessage(Guid FlatId, Guid RunId);

internal static class InsightsConstants
{
    public const string QueueName = "insight-discovery";
    public static readonly JsonSerializerOptions MessageJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
}
