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

    private static HttpRequest MakeRequest(string? status = null)
    {
        var ctx = new DefaultHttpContext();
        if (status is not null)
            ctx.Request.QueryString = new QueryString($"?status={status}");
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

    private static Insight MakeInsight(Guid flatId, DateTimeOffset createdAt, InsightType type = InsightType.Standby, Guid? deviceId = null, bool isDismissed = false) => new()
    {
        InsightId = Guid.NewGuid(),
        FlatId = flatId,
        Type = type,
        DeviceId = deviceId,
        Data = """{"deviceName":"Fridge","standbyWatts":12.5}""",
        CreatedAt = createdAt,
        IsDismissed = isDismissed,
        DismissedAt = isDismissed ? createdAt.AddMinutes(1) : null
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

    [Fact]
    public async Task RunAsync_DefaultStatus_ExcludesDismissedIdentityEntirely()
    {
        var (flat, db) = await SeedFlatAsync();
        var deviceId = Guid.NewGuid();
        var dismissed = MakeInsight(flat.FlatId, DateTimeOffset.UtcNow, InsightType.Standby, deviceId, isDismissed: true);
        db.Insights.Add(dismissed);
        await db.SaveChangesAsync();

        var fn = new GetInsightsFunction(db, Mock.Of<ILogger<GetInsightsFunction>>());
        var result = await fn.RunAsync(MakeRequest(), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<InsightsResponse>();
        response.Insights.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_StatusActive_ExcludesDismissedIdentityEntirely()
    {
        var (flat, db) = await SeedFlatAsync();
        var deviceId = Guid.NewGuid();
        var dismissed = MakeInsight(flat.FlatId, DateTimeOffset.UtcNow, InsightType.Standby, deviceId, isDismissed: true);
        db.Insights.Add(dismissed);
        await db.SaveChangesAsync();

        var fn = new GetInsightsFunction(db, Mock.Of<ILogger<GetInsightsFunction>>());
        var result = await fn.RunAsync(MakeRequest("active"), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<InsightsResponse>();
        response.Insights.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_StatusDismissed_ReturnsOnlyDismissedRow()
    {
        var (flat, db) = await SeedFlatAsync();
        var dismissedDeviceId = Guid.NewGuid();
        var activeDeviceId = Guid.NewGuid();
        var dismissed = MakeInsight(flat.FlatId, DateTimeOffset.UtcNow, InsightType.Standby, dismissedDeviceId, isDismissed: true);
        var active = MakeInsight(flat.FlatId, DateTimeOffset.UtcNow, InsightType.Standby, activeDeviceId, isDismissed: false);
        db.Insights.AddRange(dismissed, active);
        await db.SaveChangesAsync();

        var fn = new GetInsightsFunction(db, Mock.Of<ILogger<GetInsightsFunction>>());
        var result = await fn.RunAsync(MakeRequest("dismissed"), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<InsightsResponse>();
        response.Insights.Select(i => i.InsightId).ShouldBe([dismissed.InsightId]);
    }

    [Fact]
    public async Task RunAsync_StatusDismissed_IdentityWithNoDismissedRow_ReturnsNothing()
    {
        var (flat, db) = await SeedFlatAsync();
        var active = MakeInsight(flat.FlatId, DateTimeOffset.UtcNow, InsightType.Standby, Guid.NewGuid(), isDismissed: false);
        db.Insights.Add(active);
        await db.SaveChangesAsync();

        var fn = new GetInsightsFunction(db, Mock.Of<ILogger<GetInsightsFunction>>());
        var result = await fn.RunAsync(MakeRequest("dismissed"), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<InsightsResponse>();
        response.Insights.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_DefaultStatus_ReturnsActiveRowWhileExcludingDismissedIdentity()
    {
        var (flat, db) = await SeedFlatAsync();
        var dismissedDeviceId = Guid.NewGuid();
        var activeDeviceId = Guid.NewGuid();
        var dismissed = MakeInsight(flat.FlatId, DateTimeOffset.UtcNow, InsightType.Standby, dismissedDeviceId, isDismissed: true);
        var active = MakeInsight(flat.FlatId, DateTimeOffset.UtcNow, InsightType.Standby, activeDeviceId, isDismissed: false);
        db.Insights.AddRange(dismissed, active);
        await db.SaveChangesAsync();

        var fn = new GetInsightsFunction(db, Mock.Of<ILogger<GetInsightsFunction>>());
        var result = await fn.RunAsync(MakeRequest(), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<InsightsResponse>();
        response.Insights.Select(i => i.InsightId).ShouldBe([active.InsightId]);
    }

    [Fact]
    public async Task RunAsync_IdentityHasOlderActiveRowAndNewerDismissedRow_ActiveViewDoesNotResurrectOlderRow()
    {
        // Regression test for AD-8c's whole-identity-suppression design: once the identity's
        // current (newest) row is dismissed, the identity must disappear from Active entirely —
        // it must not fall back to an older, still-non-dismissed row for the same identity.
        var (flat, db) = await SeedFlatAsync();
        var deviceId = Guid.NewGuid();
        var olderActiveRow = MakeInsight(flat.FlatId, DateTimeOffset.UtcNow.AddDays(-2), InsightType.Standby, deviceId, isDismissed: false);
        var newerDismissedRow = MakeInsight(flat.FlatId, DateTimeOffset.UtcNow, InsightType.Standby, deviceId, isDismissed: true);
        db.Insights.AddRange(olderActiveRow, newerDismissedRow);
        await db.SaveChangesAsync();

        var fn = new GetInsightsFunction(db, Mock.Of<ILogger<GetInsightsFunction>>());

        var activeResult = await fn.RunAsync(MakeRequest("active"), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);
        var activeResponse = activeResult.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<InsightsResponse>();
        activeResponse.Insights.ShouldBeEmpty();

        var dismissedResult = await fn.RunAsync(MakeRequest("dismissed"), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);
        var dismissedResponse = dismissedResult.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<InsightsResponse>();
        dismissedResponse.Insights.Select(i => i.InsightId).ShouldBe([newerDismissedRow.InsightId]);
    }

    [Fact]
    public async Task RunAsync_InvalidStatusValue_Returns400BadRequest()
    {
        var (flat, db) = await SeedFlatAsync();

        var fn = new GetInsightsFunction(db, Mock.Of<ILogger<GetInsightsFunction>>());
        var result = await fn.RunAsync(MakeRequest("archived"), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        badRequest.StatusCode.ShouldBe(400);
    }
}
