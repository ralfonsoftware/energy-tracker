using System.Text;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.FlatStructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace api.Tests.Features.FlatStructure;

public class CreateRoomFunctionTests
{
    private static AppDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class UniqueConstraintConflictDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        private int _saveCount;

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            _saveCount++;
            if (_saveCount == 1)
                throw new DbUpdateException("Simulated unique-constraint conflict.");
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private static FunctionContext MakeFunctionContext(string userId = "user-test-123")
    {
        var mock = new Mock<FunctionContext>();
        var items = new Dictionary<object, object> { ["UserId"] = userId };
        mock.Setup(c => c.Items).Returns(items);
        return mock.Object;
    }

    private static HttpRequest MakeRequest(string body)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        return ctx.Request;
    }

    private static async Task<(Flat flat, AppDbContext db)> SeedFlatAsync(string userId = "user-test-123")
    {
        var db = MakeDb();
        db.Users.Add(new User { UserId = userId });
        var flat = new Flat
        {
            FlatId = Guid.NewGuid(), UserId = userId, Name = "Test Flat", AnnualKwhBaseline = 3500m, SpikeThreshold = 2.0m
        };
        db.Flats.Add(flat);
        await db.SaveChangesAsync();
        return (flat, db);
    }

    private static CreateRoomFunction MakeFunction(AppDbContext db) =>
        new(db, new CreateRoomRequestValidator());

    private const string ValidPayloadNoPowerPoints = """
        { "name": "Living Room", "sortOrder": 0, "powerPoints": [] }
        """;

    [Fact]
    public async Task RunAsync_ZeroPowerPoints_CreatesRoomAndReturns201()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = MakeFunction(db);

        var result = await fn.RunAsync(
            MakeRequest(ValidPayloadNoPowerPoints), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var created = result.ShouldBeOfType<CreatedResult>();
        var response = created.Value.ShouldBeOfType<RoomResponse>();
        created.Location.ShouldBe($"/api/v1/flats/{flat.FlatId}/rooms/{response.RoomId}");
        response.Name.ShouldBe("Living Room");
        response.PowerPoints.ShouldBeEmpty();

        var dbRoom = await db.Rooms.SingleAsync(r => r.FlatId == flat.FlatId);
        dbRoom.RoomId.ShouldBe(response.RoomId);
    }

    [Fact]
    public async Task RunAsync_WithNestedPowerPointsSeededAtCreation_PersistsRoomAndPowerPointsTogether()
    {
        // Gap found #1: a brand-new room's first save can already carry drafted power points
        // (RoomEditor.tsx's handleAddPowerPoint mutates local draft state before any Save click).
        var (flat, db) = await SeedFlatAsync();
        var fn = MakeFunction(db);
        const string payload = """
            {
                "name": "Living Room", "sortOrder": 0,
                "powerPoints": [
                    { "name": "Wall Socket", "plugId": "plug-1" },
                    { "name": "TV Socket", "plugId": null }
                ]
            }
            """;

        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var created = result.ShouldBeOfType<CreatedResult>();
        var response = created.Value.ShouldBeOfType<RoomResponse>();
        response.PowerPoints.Count.ShouldBe(2);
        response.PowerPoints.ShouldAllBe(pp => pp.Devices.Count == 0);

        var dbPowerPoints = await db.PowerPoints.Where(pp => pp.RoomId == response.RoomId).ToListAsync();
        dbPowerPoints.Count.ShouldBe(2);
        dbPowerPoints.ShouldContain(pp => pp.PlugId == "plug-1");
    }

    [Fact]
    public async Task RunAsync_InvalidFlatIdFormat_Returns400()
    {
        var db = MakeDb();
        var fn = MakeFunction(db);

        var result = await fn.RunAsync(
            MakeRequest(ValidPayloadNoPowerPoints), "not-a-guid", MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_MalformedJsonBody_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = MakeFunction(db);

        var result = await fn.RunAsync(
            MakeRequest("{ not valid json"), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        (await db.Rooms.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_FlatNotOwnedByUser_Returns403AndPersistsNothing()
    {
        var (flat, db) = await SeedFlatAsync(userId: "owner");
        var fn = MakeFunction(db);
        var ctx = MakeFunctionContext(userId: "intruder");

        var result = await fn.RunAsync(MakeRequest(ValidPayloadNoPowerPoints), flat.FlatId.ToString(), ctx, CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
        (await db.Rooms.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_BlankRoomName_Returns400AndPersistsNothing()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = MakeFunction(db);
        const string payload = """{ "name": "", "sortOrder": 0, "powerPoints": [] }""";

        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        (await db.Rooms.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_BlankPowerPointName_Returns400AndPersistsNothing()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = MakeFunction(db);
        const string payload = """
            { "name": "Living Room", "sortOrder": 0, "powerPoints": [ { "name": "", "plugId": null } ] }
            """;

        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        (await db.Rooms.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_PlugIdExceedsMaxLength_Returns400AndPersistsNothing()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = MakeFunction(db);
        var overLong = new string('x', 201);
        var payload = $$"""
            { "name": "Living Room", "sortOrder": 0, "powerPoints": [ { "name": "Socket", "plugId": "{{overLong}}" } ] }
            """;

        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        (await db.Rooms.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_DuplicatePlugIdWithinPayload_Returns422AndPersistsNothing()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = MakeFunction(db);
        const string payload = """
            {
                "name": "Living Room", "sortOrder": 0,
                "powerPoints": [
                    { "name": "Socket 1", "plugId": "plug-dup" },
                    { "name": "Socket 2", "plugId": "plug-dup" }
                ]
            }
            """;

        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(422);
        (await db.Rooms.CountAsync()).ShouldBe(0);
        (await db.PowerPoints.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_PlugIdAlreadyUsedByAnotherRoomInFlat_Returns409ConflictAndPersistsNothing()
    {
        var flat = new Flat
        {
            FlatId = Guid.NewGuid(), UserId = "user-test-123", Name = "Test Flat", AnnualKwhBaseline = 3500m, SpikeThreshold = 2.0m
        };
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using (var seedCtx = new AppDbContext(dbOptions))
        {
            seedCtx.Users.Add(new User { UserId = "user-test-123" });
            seedCtx.Flats.Add(flat);
            await seedCtx.SaveChangesAsync();
        }

        var db = new UniqueConstraintConflictDbContext(dbOptions);
        var fn = MakeFunction(db);
        const string payload = """
            { "name": "Living Room", "sortOrder": 0, "powerPoints": [ { "name": "Socket", "plugId": "plug-1" } ] }
            """;

        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var conflict = result.ShouldBeOfType<ConflictObjectResult>();
        conflict.StatusCode.ShouldBe(409);
        var value = conflict.Value.ShouldNotBeNull();
        value.GetType().GetProperty("detail")!.GetValue(value)
            .ShouldBe("This Smart Plug is already assigned to another Power Point in this flat.");
        using var verifyCtx = new AppDbContext(dbOptions);
        (await verifyCtx.Rooms.CountAsync(r => r.FlatId == flat.FlatId)).ShouldBe(0);
    }
}
