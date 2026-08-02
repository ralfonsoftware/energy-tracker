using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Insights;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using System.Text;

namespace api.Tests.Features.Insights;

public class PatchInsightFunctionTests
{
    private static readonly byte[] TestRowVersion = [1, 2, 3];
    private const string TestRowVersionBase64 = "AQID";

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

    private sealed class ConcurrencyConflictDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        private int _saveCount;

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            _saveCount++;
            if (_saveCount == 1)
                throw new DbUpdateConcurrencyException("Simulated concurrency conflict.");
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private static HttpRequest MakeRequest(string rawJson)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.ContentType = "application/json";
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(rawJson));
        return ctx.Request;
    }

    private static async Task<(Flat flat, Insight insight, AppDbContext db)> SeedFlatAndInsightAsync(
        string userId = "user-test-123", bool isDismissed = false)
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
        var insight = new Insight
        {
            InsightId = Guid.NewGuid(),
            FlatId = flat.FlatId,
            Type = InsightType.Standby,
            Data = """{"deviceName":"Fridge","estimatedMonthlyCost":5.0}""",
            CreatedAt = DateTimeOffset.UtcNow,
            IsDismissed = isDismissed,
            DismissedAt = isDismissed ? DateTimeOffset.UtcNow.AddDays(-1) : null,
            RowVersion = TestRowVersion
        };
        db.Flats.Add(flat);
        db.Insights.Add(insight);
        await db.SaveChangesAsync();
        return (flat, insight, db);
    }

    [Fact]
    public async Task RunAsync_DismissActiveInsight_SetsIsDismissedTrueAndPopulatesDismissedAt()
    {
        var (flat, insight, db) = await SeedFlatAndInsightAsync();
        var fn = new PatchInsightFunction(db);
        var req = MakeRequest($$"""{"isDismissed":true,"rowVersion":"{{TestRowVersionBase64}}"}""");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), insight.InsightId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<PatchInsightResponse>();
        response.IsDismissed.ShouldBeTrue();
        response.DismissedAt.ShouldNotBeNull();
        var persisted = await db.Insights.SingleAsync(i => i.InsightId == insight.InsightId);
        persisted.IsDismissed.ShouldBeTrue();
        persisted.DismissedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task RunAsync_ReactivateDismissedInsight_ClearsIsDismissedAndDismissedAt()
    {
        var (flat, insight, db) = await SeedFlatAndInsightAsync(isDismissed: true);
        var fn = new PatchInsightFunction(db);
        var req = MakeRequest($$"""{"isDismissed":false,"rowVersion":"{{TestRowVersionBase64}}"}""");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), insight.InsightId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<PatchInsightResponse>();
        response.IsDismissed.ShouldBeFalse();
        response.DismissedAt.ShouldBeNull();
        var persisted = await db.Insights.SingleAsync(i => i.InsightId == insight.InsightId);
        persisted.IsDismissed.ShouldBeFalse();
        persisted.DismissedAt.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_FlatBelongsToDifferentUser_Returns403AndPersistsNothing()
    {
        var (flat, insight, db) = await SeedFlatAndInsightAsync(userId: "owner-user");
        var fn = new PatchInsightFunction(db);
        var req = MakeRequest("""{"isDismissed":true}""");
        var ctx = MakeFunctionContext(userId: "attacker-user");

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), insight.InsightId.ToString(), ctx, CancellationToken.None);

        var forbidden = result.ShouldBeOfType<ObjectResult>();
        forbidden.StatusCode.ShouldBe(403);
        var persisted = await db.Insights.SingleAsync(i => i.InsightId == insight.InsightId);
        persisted.IsDismissed.ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_FlatDoesNotExist_Returns403()
    {
        var db = MakeDb();
        var fn = new PatchInsightFunction(db);
        var req = MakeRequest("""{"isDismissed":true}""");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), ctx, CancellationToken.None);

        var forbidden = result.ShouldBeOfType<ObjectResult>();
        forbidden.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task RunAsync_InsightIdDoesNotBelongToFlat_Returns404()
    {
        // Two real insights under two different flats for the same user — the requested
        // insightId genuinely exists, just not under the flatId in the route, exercising the
        // actual cross-flat tenant-isolation path rather than a plain "not found at all" 404.
        var (flatA, _, db) = await SeedFlatAndInsightAsync();
        var flatB = new Flat
        {
            FlatId = Guid.NewGuid(),
            UserId = "user-test-123",
            Name = "Other Flat",
            AnnualKwhBaseline = 2000m,
            SpikeThreshold = 2.0m
        };
        var insightUnderFlatB = new Insight
        {
            InsightId = Guid.NewGuid(),
            FlatId = flatB.FlatId,
            Type = InsightType.Standby,
            Data = """{"deviceName":"Freezer","estimatedMonthlyCost":3.0}""",
            CreatedAt = DateTimeOffset.UtcNow,
            RowVersion = TestRowVersion
        };
        db.Flats.Add(flatB);
        db.Insights.Add(insightUnderFlatB);
        await db.SaveChangesAsync();

        var fn = new PatchInsightFunction(db);
        var req = MakeRequest($$"""{"isDismissed":true,"rowVersion":"{{TestRowVersionBase64}}"}""");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flatA.FlatId.ToString(), insightUnderFlatB.InsightId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<NotFoundObjectResult>();
        var persisted = await db.Insights.SingleAsync(i => i.InsightId == insightUnderFlatB.InsightId);
        persisted.IsDismissed.ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_InvalidFlatIdFormat_Returns400()
    {
        var (_, insight, db) = await SeedFlatAndInsightAsync();
        var fn = new PatchInsightFunction(db);
        var req = MakeRequest("""{"isDismissed":true}""");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, "not-a-guid", insight.InsightId.ToString(), ctx, CancellationToken.None);

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        badRequest.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task RunAsync_InvalidInsightIdFormat_Returns400()
    {
        var (flat, _, db) = await SeedFlatAndInsightAsync();
        var fn = new PatchInsightFunction(db);
        var req = MakeRequest("""{"isDismissed":true}""");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), "not-a-guid", ctx, CancellationToken.None);

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        badRequest.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task RunAsync_IsDismissedMissing_Returns400()
    {
        var (flat, insight, db) = await SeedFlatAndInsightAsync();
        var fn = new PatchInsightFunction(db);
        var req = MakeRequest("{}");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), insight.InsightId.ToString(), ctx, CancellationToken.None);

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        badRequest.StatusCode.ShouldBe(400);
        var detail = (string)badRequest.Value!.GetType().GetProperty("detail")!.GetValue(badRequest.Value)!;
        detail.ShouldBe("isDismissed is required and must be a boolean.");
    }

    [Fact]
    public async Task RunAsync_IsDismissedNull_Returns400()
    {
        var (flat, insight, db) = await SeedFlatAndInsightAsync();
        var fn = new PatchInsightFunction(db);
        var req = MakeRequest("""{"isDismissed":null}""");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), insight.InsightId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_IsDismissedNonBoolean_Returns400()
    {
        var (flat, insight, db) = await SeedFlatAndInsightAsync();
        var fn = new PatchInsightFunction(db);
        var req = MakeRequest("""{"isDismissed":"yes"}""");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), insight.InsightId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_MissingRowVersion_Returns400()
    {
        var (flat, insight, db) = await SeedFlatAndInsightAsync();
        var fn = new PatchInsightFunction(db);
        var req = MakeRequest("""{"isDismissed":true}""");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), insight.InsightId.ToString(), ctx, CancellationToken.None);

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        badRequest.StatusCode.ShouldBe(400);
        var persisted = await db.Insights.SingleAsync(i => i.InsightId == insight.InsightId);
        persisted.IsDismissed.ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_MalformedRowVersion_Returns400()
    {
        var (flat, insight, db) = await SeedFlatAndInsightAsync();
        var fn = new PatchInsightFunction(db);
        var req = MakeRequest("""{"isDismissed":true,"rowVersion":"not-valid-base64!!"}""");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), insight.InsightId.ToString(), ctx, CancellationToken.None);

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        badRequest.StatusCode.ShouldBe(400);
        var persisted = await db.Insights.SingleAsync(i => i.InsightId == insight.InsightId);
        persisted.IsDismissed.ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_ConcurrentModification_Returns409Conflict()
    {
        var flat = new Flat
        {
            FlatId = Guid.NewGuid(),
            UserId = "user-test-123",
            Name = "Test Flat",
            AnnualKwhBaseline = 3500m,
            SpikeThreshold = 2.0m
        };
        var insight = new Insight
        {
            InsightId = Guid.NewGuid(),
            FlatId = flat.FlatId,
            Type = InsightType.Standby,
            Data = """{"deviceName":"Fridge","estimatedMonthlyCost":5.0}""",
            CreatedAt = DateTimeOffset.UtcNow,
            RowVersion = TestRowVersion
        };
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using (var seedCtx = new AppDbContext(dbOptions))
        {
            seedCtx.Flats.Add(flat);
            seedCtx.Insights.Add(insight);
            await seedCtx.SaveChangesAsync();
        }

        var db = new ConcurrencyConflictDbContext(dbOptions);
        var fn = new PatchInsightFunction(db);
        var req = MakeRequest($$"""{"isDismissed":true,"rowVersion":"{{TestRowVersionBase64}}"}""");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), insight.InsightId.ToString(), ctx, CancellationToken.None);

        var conflict = result.ShouldBeOfType<ObjectResult>();
        conflict.StatusCode.ShouldBe(409);
        using var verifyCtx = new AppDbContext(dbOptions);
        var persisted = await verifyCtx.Insights.SingleAsync(i => i.InsightId == insight.InsightId);
        persisted.IsDismissed.ShouldBeFalse();
    }
}
