using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Insights;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace api.Tests.Features.Insights;

public class GetInsightsFunctionTests
{
    private static AppDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static FunctionContext MakeFunctionContext(string userId = "user-test-123")
    {
        var mock = new Mock<FunctionContext>();
        var items = new Dictionary<object, object> { ["UserId"] = userId };
        mock.Setup(c => c.Items).Returns(items);
        return mock.Object;
    }

    private static HttpRequest MakeRequest()
    {
        var ctx = new DefaultHttpContext();
        return ctx.Request;
    }

    private static async Task<(Flat flat, AppDbContext db)> SeedFlatAsync(string userId = "user-test-123")
    {
        var db = MakeDb();
        var flat = new Flat
        {
            FlatId = Guid.NewGuid(),
            UserId = userId,
            Name = "Test Flat",
            AnnualKwhBaseline = 3500m,
            SpikeThreshold = 2.0m
        };
        db.Flats.Add(flat);
        await db.SaveChangesAsync();
        return (flat, db);
    }

    private static Insight MakeInsight(Guid flatId, DateTimeOffset createdAt, InsightType type = InsightType.Standby, Guid? deviceId = null) => new()
    {
        InsightId = Guid.NewGuid(),
        FlatId = flatId,
        Type = type,
        DeviceId = deviceId,
        Data = """{"deviceName":"Fridge","standbyWatts":12.5}""",
        CreatedAt = createdAt
    };

    [Fact]
    public async Task RunAsync_NoInsightRunYet_ReturnsNullRunStatusAndEmptyInsights()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new GetInsightsFunction(db, Mock.Of<ILogger<GetInsightsFunction>>());

        var result = await fn.RunAsync(MakeRequest(), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<InsightsResponse>();
        response.RunStatus.ShouldBeNull();
        response.Insights.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_DistinctIdentities_ReturnsAllSortedByCreatedAtDescending()
    {
        var (flat, db) = await SeedFlatAsync();
        var deviceA = Guid.NewGuid();
        var deviceB = Guid.NewGuid();
        var oldest = MakeInsight(flat.FlatId, DateTimeOffset.UtcNow.AddDays(-2), InsightType.Standby, deviceA);
        var middle = MakeInsight(flat.FlatId, DateTimeOffset.UtcNow.AddDays(-1), InsightType.Standby, deviceB);
        var newest = MakeInsight(flat.FlatId, DateTimeOffset.UtcNow, InsightType.Budget);
        db.Insights.AddRange(oldest, middle, newest);
        await db.SaveChangesAsync();

        var fn = new GetInsightsFunction(db, Mock.Of<ILogger<GetInsightsFunction>>());
        var result = await fn.RunAsync(MakeRequest(), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<InsightsResponse>();
        response.Insights.Select(i => i.InsightId).ShouldBe([newest.InsightId, middle.InsightId, oldest.InsightId]);
        response.Insights[0].Data.GetProperty("deviceName").GetString().ShouldBe("Fridge");
    }

    [Fact]
    public async Task RunAsync_SameIdentityMultipleRows_ReturnsOnlyNewest()
    {
        var (flat, db) = await SeedFlatAsync();
        var oldest = MakeInsight(flat.FlatId, DateTimeOffset.UtcNow.AddDays(-2));
        var middle = MakeInsight(flat.FlatId, DateTimeOffset.UtcNow.AddDays(-1));
        var newest = MakeInsight(flat.FlatId, DateTimeOffset.UtcNow);
        db.Insights.AddRange(oldest, middle, newest);
        await db.SaveChangesAsync();

        var fn = new GetInsightsFunction(db, Mock.Of<ILogger<GetInsightsFunction>>());
        var result = await fn.RunAsync(MakeRequest(), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<InsightsResponse>();
        response.Insights.Select(i => i.InsightId).ShouldBe([newest.InsightId]);
    }

    [Fact]
    public async Task RunAsync_SameIdentitySameCreatedAt_TieBreaksOnInsightIdDescending()
    {
        var (flat, db) = await SeedFlatAsync();
        var createdAt = DateTimeOffset.UtcNow;
        var insightWithLowerId = MakeInsight(flat.FlatId, createdAt);
        var insightWithHigherId = MakeInsight(flat.FlatId, createdAt);
        if (insightWithLowerId.InsightId.CompareTo(insightWithHigherId.InsightId) > 0)
            (insightWithLowerId, insightWithHigherId) = (insightWithHigherId, insightWithLowerId);
        db.Insights.AddRange(insightWithLowerId, insightWithHigherId);
        await db.SaveChangesAsync();

        var fn = new GetInsightsFunction(db, Mock.Of<ILogger<GetInsightsFunction>>());
        var result = await fn.RunAsync(MakeRequest(), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<InsightsResponse>();
        response.Insights.Select(i => i.InsightId).ShouldBe([insightWithHigherId.InsightId]);
    }

    [Fact]
    public async Task RunAsync_MostRecentRunStatus_IncludedRegardlessOfWhetherStillRunning()
    {
        var (flat, db) = await SeedFlatAsync();
        var olderRun = new InsightRun
        {
            RunId = Guid.NewGuid(), FlatId = flat.FlatId, Status = InsightRunStatus.Complete,
            StartedAt = DateTimeOffset.UtcNow.AddDays(-1), CompletedAt = DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(1)
        };
        var latestRun = new InsightRun
        {
            RunId = Guid.NewGuid(), FlatId = flat.FlatId, Status = InsightRunStatus.Processing,
            StartedAt = DateTimeOffset.UtcNow
        };
        db.InsightRuns.AddRange(olderRun, latestRun);
        await db.SaveChangesAsync();

        var fn = new GetInsightsFunction(db, Mock.Of<ILogger<GetInsightsFunction>>());
        var result = await fn.RunAsync(MakeRequest(), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<InsightsResponse>();
        response.RunStatus.ShouldNotBeNull();
        response.RunStatus.Status.ShouldBe(InsightRunStatus.Processing);
        response.RunStatus.CompletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_ForeignFlatId_Returns403()
    {
        var (flat, db) = await SeedFlatAsync(userId: "owner");
        var fn = new GetInsightsFunction(db, Mock.Of<ILogger<GetInsightsFunction>>());

        var result = await fn.RunAsync(MakeRequest(), flat.FlatId.ToString(), MakeFunctionContext(userId: "intruder"), CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task RunAsync_InvalidFlatIdFormat_Returns400()
    {
        var (_, db) = await SeedFlatAsync();
        var fn = new GetInsightsFunction(db, Mock.Of<ILogger<GetInsightsFunction>>());

        var result = await fn.RunAsync(MakeRequest(), "not-a-guid", MakeFunctionContext(), CancellationToken.None);

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        var type = (string)badRequest.Value!.GetType().GetProperty("type")!.GetValue(badRequest.Value)!;
        type.ShouldBe("https://tools.ietf.org/html/rfc7231#section-6.5.1");
    }
}
