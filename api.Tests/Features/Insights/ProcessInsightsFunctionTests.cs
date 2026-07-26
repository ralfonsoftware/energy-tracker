using System.Text.Json;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Insights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace api.Tests.Features.Insights;

public class ProcessInsightsFunctionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private class ThrowingStandbyDetector(AppDbContext db) : StandbyDetector(db)
    {
        public override Task DetectAsync(Guid flatId, Guid runId, CancellationToken ct) =>
            throw new InvalidOperationException("simulated detector failure");
    }

    // Stands in for a real detector to prove the redelivered run actually writes fresh
    // Insight rows, not just that the stale one from the prior attempt is gone.
    private class WritingStandbyDetector(AppDbContext db) : StandbyDetector(db)
    {
        public override async Task DetectAsync(Guid flatId, Guid runId, CancellationToken ct)
        {
            db.Insights.Add(new Insight
            {
                InsightId = Guid.NewGuid(),
                FlatId = flatId,
                RunId = runId,
                Type = InsightType.Standby,
                Data = "{}",
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }
    }

    // Throws on the first SaveChangesAsync call (persisting the Processing transition),
    // then succeeds — simulating a genuine transient failure distinct from a detector's
    // own isolated exception, so the outer catch can record Status = Failed.
    private class FailingOnFirstSaveDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        private int _saveCount;

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            _saveCount++;
            if (_saveCount == 1)
                throw new InvalidOperationException("simulated transient DB failure");
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private static AppDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static FunctionContext MakeFunctionContext()
    {
        var mock = new Mock<FunctionContext>();
        mock.Setup(c => c.Items).Returns(new Dictionary<object, object>());
        return mock.Object;
    }

    private static async Task<(Flat flat, InsightRun run, AppDbContext db)> SeedFlatAndRunAsync()
    {
        var db = MakeDb();
        var flat = new Flat
        {
            FlatId = Guid.NewGuid(),
            UserId = "user-test-123",
            Name = "Test Flat",
            AnnualKwhBaseline = 3500m,
            SpikeThreshold = 2.0m
        };
        var run = new InsightRun
        {
            RunId = Guid.NewGuid(),
            FlatId = flat.FlatId,
            Status = InsightRunStatus.Pending,
            StartedAt = DateTimeOffset.UtcNow
        };
        db.Flats.Add(flat);
        db.InsightRuns.Add(run);
        await db.SaveChangesAsync();
        return (flat, run, db);
    }

    private static string MakeMessage(Guid flatId, Guid runId) =>
        JsonSerializer.Serialize(new InsightDiscoveryMessage(flatId, runId), JsonOptions);

    [Fact]
    public async Task RunAsync_AllDetectorsSucceed_ReachesCompleteStatus()
    {
        var (flat, run, db) = await SeedFlatAndRunAsync();
        var fn = new ProcessInsightsFunction(
            db,
            new StandbyDetector(db),
            new ReplacementDetector(db),
            new BudgetAlertDetector(db),
            new InvoiceDeviationDetector(db),
            Mock.Of<ILogger<ProcessInsightsFunction>>());

        await fn.RunAsync(MakeMessage(flat.FlatId, run.RunId), MakeFunctionContext(), CancellationToken.None);

        var updated = await db.InsightRuns.SingleAsync(r => r.RunId == run.RunId);
        updated.Status.ShouldBe(InsightRunStatus.Complete);
        updated.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task RunAsync_OneDetectorThrows_OtherThreeStillRunAndRunStillCompletes()
    {
        var (flat, run, db) = await SeedFlatAndRunAsync();
        var replacementDetector = new ReplacementDetector(db);
        var budgetAlertDetector = new BudgetAlertDetector(db);
        var invoiceDeviationDetector = new InvoiceDeviationDetector(db);
        var fn = new ProcessInsightsFunction(
            db,
            new ThrowingStandbyDetector(db),
            replacementDetector,
            budgetAlertDetector,
            invoiceDeviationDetector,
            Mock.Of<ILogger<ProcessInsightsFunction>>());

        await fn.RunAsync(MakeMessage(flat.FlatId, run.RunId), MakeFunctionContext(), CancellationToken.None);

        // The throwing detector must not stop the run from reaching Complete — its failure
        // is caught and logged per-detector, distinct from a genuinely unhandled exception.
        var updated = await db.InsightRuns.SingleAsync(r => r.RunId == run.RunId);
        updated.Status.ShouldBe(InsightRunStatus.Complete);
    }

    [Fact]
    public async Task RunAsync_UnhandledExceptionOutsideDetectors_TransitionsRunToFailed()
    {
        var flat = new Flat
        {
            FlatId = Guid.NewGuid(),
            UserId = "user-test-123",
            Name = "Test Flat",
            AnnualKwhBaseline = 3500m,
            SpikeThreshold = 2.0m
        };
        var run = new InsightRun
        {
            RunId = Guid.NewGuid(),
            FlatId = flat.FlatId,
            Status = InsightRunStatus.Pending,
            StartedAt = DateTimeOffset.UtcNow
        };
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using (var seedDb = new AppDbContext(dbOptions))
        {
            seedDb.Flats.Add(flat);
            seedDb.InsightRuns.Add(run);
            await seedDb.SaveChangesAsync();
        }

        var db = new FailingOnFirstSaveDbContext(dbOptions);
        var fn = new ProcessInsightsFunction(
            db,
            new StandbyDetector(db),
            new ReplacementDetector(db),
            new BudgetAlertDetector(db),
            new InvoiceDeviationDetector(db),
            Mock.Of<ILogger<ProcessInsightsFunction>>());

        await fn.RunAsync(MakeMessage(flat.FlatId, run.RunId), MakeFunctionContext(), CancellationToken.None);

        using var verifyDb = new AppDbContext(dbOptions);
        var updated = await verifyDb.InsightRuns.SingleAsync(r => r.RunId == run.RunId);
        updated.Status.ShouldBe(InsightRunStatus.Failed);
        updated.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task RunAsync_RedeliveredMessage_ClearsStaleInsightsAndKeepsOnlyNewRun()
    {
        var (flat, run, db) = await SeedFlatAndRunAsync();
        // Simulates a partial write left behind by a prior attempt that was killed mid-run
        // before completing — this row must not survive a redelivery of the same message.
        var staleInsight = new Insight
        {
            InsightId = Guid.NewGuid(),
            FlatId = flat.FlatId,
            RunId = run.RunId,
            Type = InsightType.Standby,
            Data = "{}",
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Insights.Add(staleInsight);
        await db.SaveChangesAsync();

        var fn = new ProcessInsightsFunction(
            db,
            new WritingStandbyDetector(db),
            new ReplacementDetector(db),
            new BudgetAlertDetector(db),
            new InvoiceDeviationDetector(db),
            Mock.Of<ILogger<ProcessInsightsFunction>>());

        await fn.RunAsync(MakeMessage(flat.FlatId, run.RunId), MakeFunctionContext(), CancellationToken.None);

        var remaining = await db.Insights.Where(i => i.RunId == run.RunId).ToListAsync();
        remaining.ShouldNotContain(i => i.InsightId == staleInsight.InsightId);
        remaining.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RunAsync_RunNotFound_DoesNotThrow()
    {
        var db = MakeDb();
        var fn = new ProcessInsightsFunction(
            db,
            new StandbyDetector(db),
            new ReplacementDetector(db),
            new BudgetAlertDetector(db),
            new InvoiceDeviationDetector(db),
            Mock.Of<ILogger<ProcessInsightsFunction>>());

        await Should.NotThrowAsync(() =>
            fn.RunAsync(MakeMessage(Guid.NewGuid(), Guid.NewGuid()), MakeFunctionContext(), CancellationToken.None));
    }
}
