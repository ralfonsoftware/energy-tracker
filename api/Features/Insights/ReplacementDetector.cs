using EnergyTracker.Api.Data;

namespace EnergyTracker.Api.Features.Insights;

// Stub for Story 10.1 — real detection logic lands in Story 10.2 against this exact
// DetectAsync(flatId, runId, ct) signature. Do not change the signature without checking
// ProcessInsightsFunction's call sites.
public class ReplacementDetector(AppDbContext db)
{
    public virtual Task DetectAsync(Guid flatId, Guid runId, CancellationToken ct) => Task.CompletedTask;
}
