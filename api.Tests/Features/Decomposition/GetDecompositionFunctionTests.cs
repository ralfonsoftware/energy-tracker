using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Decomposition;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace api.Tests.Features.Decomposition;

public class GetDecompositionFunctionTests
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

    private static async Task<(Flat flat, AppDbContext db)> SeedFlatAsync(string userId = "user-test-123")
    {
        var db = MakeDb();
        db.Users.Add(new User { UserId = userId });
        var flat = new Flat
        {
            FlatId = Guid.NewGuid(),
            UserId = userId,
            Name = "Test Flat",
            AnnualKwhBaseline = 3650m,
            SpikeThreshold = 2.0m
        };
        db.Flats.Add(flat);
        await db.SaveChangesAsync();
        return (flat, db);
    }

    private static HttpRequest MakeGetRequest(string? startDate = null, string? endDate = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        var query = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>();
        if (startDate is not null)
            query["startDate"] = startDate;
        if (endDate is not null)
            query["endDate"] = endDate;
        ctx.Request.Query = new QueryCollection(query);
        return ctx.Request;
    }

    [Fact]
    public async Task RunAsync_InvalidFlatIdGuid_Returns400()
    {
        var db = MakeDb();
        var fn = new GetDecompositionFunction(db, new DecompositionEngine(db));
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeGetRequest("2026-01-01", "2026-01-05"), "not-a-guid", ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_FlatNotOwnedByUser_Returns403()
    {
        var (flat, db) = await SeedFlatAsync(userId: "owner");
        var fn = new GetDecompositionFunction(db, new DecompositionEngine(db));
        var ctx = MakeFunctionContext(userId: "intruder");

        var result = await fn.RunAsync(MakeGetRequest("2026-01-01", "2026-01-05"), flat.FlatId.ToString(), ctx, CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task RunAsync_MissingStartDate_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new GetDecompositionFunction(db, new DecompositionEngine(db));
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeGetRequest(endDate: "2026-01-05"), flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_UnparsableEndDate_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new GetDecompositionFunction(db, new DecompositionEngine(db));
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeGetRequest("2026-01-01", "not-a-date"), flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_EndDateBeforeStartDate_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new GetDecompositionFunction(db, new DecompositionEngine(db));
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeGetRequest("2026-01-05", "2026-01-01"), flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_ValidRequestNoSmartPlugData_Returns200WithUnavailableResponse()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new GetDecompositionFunction(db, new DecompositionEngine(db));
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeGetRequest("2026-01-01", "2026-01-05"), flat.FlatId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        ok.StatusCode.ShouldBe(200);
        var response = ok.Value.ShouldBeOfType<DecompositionResponse>();
        response.IsUnavailable.ShouldBeTrue();
    }

    [Fact]
    public async Task RunAsync_ValidRequestWithSmartPlugData_Returns200WithComputedResponse()
    {
        var (flat, db) = await SeedFlatAsync();
        var room = new Room { RoomId = Guid.NewGuid(), FlatId = flat.FlatId, Name = "Living Room" };
        db.Rooms.Add(room);
        var pp = new PowerPoint { PowerPointId = Guid.NewGuid(), RoomId = room.RoomId, Name = "Socket", PlugId = "plug-1" };
        db.PowerPoints.Add(pp);
        db.Devices.Add(new Device
        {
            DeviceId = Guid.NewGuid(), PowerPointId = pp.PowerPointId, Name = "TV",
            ConsumptionApproach = ConsumptionApproach.None
        });
        db.SmartPlugDailyData.Add(new SmartPlugDailyData
        {
            FlatId = flat.FlatId, PlugId = "plug-1", Date = new DateOnly(2026, 1, 3), KwhValue = 2m
        });
        await db.SaveChangesAsync();

        var fn = new GetDecompositionFunction(db, new DecompositionEngine(db));
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeGetRequest("2026-01-01", "2026-01-05"), flat.FlatId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        ok.StatusCode.ShouldBe(200);
        var response = ok.Value.ShouldBeOfType<DecompositionResponse>();
        response.IsUnavailable.ShouldBeFalse();
        response.Rooms.Single().Devices.Single().Kwh.ShouldBe(2m);
    }
}
