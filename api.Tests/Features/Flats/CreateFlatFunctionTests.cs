using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Flats;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using System.Text;
using System.Text.Json;

namespace api.Tests.Features.Flats;

public class CreateFlatFunctionTests
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

    private static HttpRequest MakeRequest(object body)
    {
        var json = JsonSerializer.Serialize(body);
        var ctx = new DefaultHttpContext();
        ctx.Request.ContentType = "application/json";
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return ctx.Request;
    }

    private static HttpRequest MakeRawRequest(string body)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.ContentType = "application/json";
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return ctx.Request;
    }

    private static CreateFlatFunction MakeFn(AppDbContext db) => new(db, new CreateFlatValidator());

    [Fact]
    public async Task RunAsync_ValidRequest_Returns201WithLocationAndSpikeThresholdDefaultsTo2()
    {
        using var db = MakeDb();
        var fn = MakeFn(db);
        var req = MakeRequest(new { name = "New Flat", annualKwhBaseline = 3500m, plannedAnnualSpend = 900m });
        var ctx = MakeFunctionContext("creator-user");

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        var created = result.ShouldBeOfType<CreatedResult>();
        var response = created.Value.ShouldBeOfType<FlatSummary>();
        created.Location.ShouldBe($"/api/v1/flats/{response.FlatId}");
        response.SpikeThreshold.ShouldBe(2.0m);

        var persisted = await db.Flats.SingleAsync(f => f.FlatId == response.FlatId);
        persisted.SpikeThreshold.ShouldBe(2.0m);
    }

    [Fact]
    public async Task RunAsync_ValidRequest_PersistsFlatScopedToResolvedUserId()
    {
        using var db = MakeDb();
        var fn = MakeFn(db);
        var req = MakeRequest(new { name = "New Flat", annualKwhBaseline = 3500m, plannedAnnualSpend = (decimal?)null });
        var ctx = MakeFunctionContext("creator-user");

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        var created = result.ShouldBeOfType<CreatedResult>();
        var response = created.Value.ShouldBeOfType<FlatSummary>();

        var persisted = await db.Flats.SingleAsync(f => f.FlatId == response.FlatId);
        persisted.UserId.ShouldBe("creator-user");
        persisted.Name.ShouldBe("New Flat");
        persisted.AnnualKwhBaseline.ShouldBe(3500m);
    }

    [Fact]
    public async Task RunAsync_MissingOrEmptyName_Returns400()
    {
        using var db = MakeDb();
        var fn = MakeFn(db);
        var req = MakeRequest(new { name = "", annualKwhBaseline = 3500m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_NameKeyAbsentFromBody_Returns400()
    {
        using var db = MakeDb();
        var fn = MakeFn(db);
        var req = MakeRawRequest("""{"annualKwhBaseline": 3500}""");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(20000)]
    [InlineData(20001)]
    public async Task RunAsync_AnnualKwhBaselineOutOfBounds_Returns400(decimal annualKwhBaseline)
    {
        using var db = MakeDb();
        var fn = MakeFn(db);
        var req = MakeRequest(new { name = "New Flat", annualKwhBaseline });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_PlannedAnnualSpendOmitted_CreatesFlatWithNullPlannedAnnualSpend()
    {
        using var db = MakeDb();
        var fn = MakeFn(db);
        var req = MakeRawRequest("""{"name":"New Flat","annualKwhBaseline":3500}""");
        var ctx = MakeFunctionContext("creator-user");

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        var created = result.ShouldBeOfType<CreatedResult>();
        var response = created.Value.ShouldBeOfType<FlatSummary>();
        response.PlannedAnnualSpend.ShouldBeNull();

        var persisted = await db.Flats.SingleAsync(f => f.FlatId == response.FlatId);
        persisted.PlannedAnnualSpend.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(50000)]
    [InlineData(50001)]
    public async Task RunAsync_PlannedAnnualSpendOutOfBounds_Returns400(decimal plannedAnnualSpend)
    {
        using var db = MakeDb();
        var fn = MakeFn(db);
        var req = MakeRequest(new { name = "New Flat", annualKwhBaseline = 3500m, plannedAnnualSpend });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_MalformedJsonBody_Returns400()
    {
        using var db = MakeDb();
        var fn = MakeFn(db);
        var req = MakeRawRequest("{not valid json");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_UserAlreadyHasAFlat_StillSucceeds()
    {
        using var db = MakeDb();
        db.Flats.Add(new Flat
        {
            FlatId = Guid.NewGuid(),
            UserId = "existing-user",
            Name = "Existing Flat",
            AnnualKwhBaseline = 3000m,
            SpikeThreshold = 2.0m
        });
        await db.SaveChangesAsync();

        var fn = MakeFn(db);
        var req = MakeRequest(new { name = "Second Flat", annualKwhBaseline = 4000m });
        var ctx = MakeFunctionContext("existing-user");

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        result.ShouldBeOfType<CreatedResult>();
        (await db.Flats.CountAsync(f => f.UserId == "existing-user")).ShouldBe(2);
    }
}
