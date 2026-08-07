using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

public class UpdateRoomFunctionTests
{
    private const string TestRowVersionBase64 = "AQID";
    private static readonly byte[] TestRowVersion = [1, 2, 3];

    private static AppDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

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

    private static FunctionContext MakeFunctionContext(string userId = "user-test-123")
    {
        var mock = new Mock<FunctionContext>();
        var items = new Dictionary<object, object> { ["UserId"] = userId };
        mock.Setup(c => c.Items).Returns(items);
        return mock.Object;
    }

    private static HttpRequest MakeRequest(string body, string? rowVersion = TestRowVersionBase64)
    {
        var toSend = body;
        if (rowVersion is not null)
        {
            try
            {
                if (JsonNode.Parse(body) is JsonObject obj)
                {
                    obj["rowVersion"] = rowVersion;
                    toSend = obj.ToJsonString();
                }
            }
            catch (JsonException) { /* malformed-JSON test cases: pass the raw body through unchanged */ }
        }
        var ctx = new DefaultHttpContext();
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(toSend));
        return ctx.Request;
    }

    private static async Task<(Flat flat, Room room, PowerPoint powerPoint, AppDbContext db)> SeedFlatWithRoomAsync(
        string userId = "user-test-123")
    {
        var db = MakeDb();
        db.Users.Add(new User { UserId = userId });
        var flat = new Flat
        {
            FlatId = Guid.NewGuid(), UserId = userId, Name = "Test Flat", AnnualKwhBaseline = 3500m, SpikeThreshold = 2.0m
        };
        var room = new Room { RoomId = Guid.NewGuid(), FlatId = flat.FlatId, Name = "Old Name", SortOrder = 0, RowVersion = TestRowVersion };
        var powerPoint = new PowerPoint { PowerPointId = Guid.NewGuid(), RoomId = room.RoomId, FlatId = flat.FlatId, Name = "Old Socket" };
        db.Flats.Add(flat);
        db.Rooms.Add(room);
        db.PowerPoints.Add(powerPoint);
        await db.SaveChangesAsync();
        return (flat, room, powerPoint, db);
    }

    private static UpdateRoomFunction MakeFunction(AppDbContext db) =>
        new(db, new UpdateRoomRequestValidator());

    [Fact]
    public async Task RunAsync_ValidRequest_UpdatesNameAndMatchedPowerPointInPlacePreservingPrimaryKeys()
    {
        var (flat, room, powerPoint, db) = await SeedFlatWithRoomAsync();
        var fn = MakeFunction(db);
        var payload = $$"""
            {
                "name": "New Name", "sortOrder": 1,
                "powerPoints": [
                    { "powerPointId": "{{powerPoint.PowerPointId}}", "name": "New Socket", "plugId": "plug-1" }
                ]
            }
            """;

        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), room.RoomId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<RoomResponse>();
        response.Name.ShouldBe("New Name");
        response.SortOrder.ShouldBe(1);
        var ppResponse = response.PowerPoints.Single();
        ppResponse.PowerPointId.ShouldBe(powerPoint.PowerPointId);
        ppResponse.Name.ShouldBe("New Socket");
        ppResponse.PlugId.ShouldBe("plug-1");

        var dbRoom = await db.Rooms.SingleAsync(r => r.RoomId == room.RoomId);
        dbRoom.Name.ShouldBe("New Name");
        var dbPp = await db.PowerPoints.SingleAsync(pp => pp.RoomId == room.RoomId);
        dbPp.PowerPointId.ShouldBe(powerPoint.PowerPointId);
        dbPp.Name.ShouldBe("New Socket");
    }

    [Fact]
    public async Task RunAsync_PowerPointAbsentFromPayload_IsDeletedCascadingItsDevices()
    {
        var (flat, room, powerPoint, db) = await SeedFlatWithRoomAsync();
        var device = new Device { DeviceId = Guid.NewGuid(), PowerPointId = powerPoint.PowerPointId, Name = "Lamp", ConsumptionApproach = ConsumptionApproach.None };
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        var fn = MakeFunction(db);
        const string payload = """{ "name": "Old Name", "sortOrder": 0, "powerPoints": [] }""";

        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), room.RoomId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<RoomResponse>();
        response.PowerPoints.ShouldBeEmpty();

        (await db.PowerPoints.AnyAsync(pp => pp.PowerPointId == powerPoint.PowerPointId)).ShouldBeFalse();
        (await db.Devices.AnyAsync(d => d.DeviceId == device.DeviceId)).ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_NewPowerPointWithoutPowerPointId_IsInserted()
    {
        var (flat, room, powerPoint, db) = await SeedFlatWithRoomAsync();
        var fn = MakeFunction(db);
        var payload = $$"""
            {
                "name": "Old Name", "sortOrder": 0,
                "powerPoints": [
                    { "powerPointId": "{{powerPoint.PowerPointId}}", "name": "Old Socket", "plugId": null },
                    { "name": "New Socket", "plugId": "plug-new" }
                ]
            }
            """;

        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), room.RoomId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<RoomResponse>();
        response.PowerPoints.Count.ShouldBe(2);

        (await db.PowerPoints.CountAsync(pp => pp.RoomId == room.RoomId)).ShouldBe(2);
        (await db.PowerPoints.AnyAsync(pp => pp.PlugId == "plug-new")).ShouldBeTrue();
    }

    [Fact]
    public async Task RunAsync_StaleRowVersion_Returns409ConflictAndPersistsNothing()
    {
        var flat = new Flat
        {
            FlatId = Guid.NewGuid(), UserId = "user-test-123", Name = "Test Flat", AnnualKwhBaseline = 3500m, SpikeThreshold = 2.0m
        };
        var room = new Room { RoomId = Guid.NewGuid(), FlatId = flat.FlatId, Name = "Old Name", SortOrder = 0, RowVersion = TestRowVersion };
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using (var seedCtx = new AppDbContext(dbOptions))
        {
            seedCtx.Users.Add(new User { UserId = "user-test-123" });
            seedCtx.Flats.Add(flat);
            seedCtx.Rooms.Add(room);
            await seedCtx.SaveChangesAsync();
        }

        var db = new ConcurrencyConflictDbContext(dbOptions);
        var fn = MakeFunction(db);
        const string payload = """{ "name": "New Name", "sortOrder": 0, "powerPoints": [] }""";

        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), room.RoomId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var conflict = result.ShouldBeOfType<ObjectResult>();
        conflict.StatusCode.ShouldBe(409);
        using var verifyCtx = new AppDbContext(dbOptions);
        var dbRoom = await verifyCtx.Rooms.SingleAsync(r => r.RoomId == room.RoomId);
        dbRoom.Name.ShouldBe("Old Name");
    }

    [Fact]
    public async Task RunAsync_InvalidFlatIdFormat_Returns400()
    {
        var (_, room, _, db) = await SeedFlatWithRoomAsync();
        var fn = MakeFunction(db);
        const string payload = """{ "name": "New Name", "sortOrder": 0, "powerPoints": [] }""";

        var result = await fn.RunAsync(MakeRequest(payload), "not-a-guid", room.RoomId.ToString(), MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_InvalidRoomIdFormat_Returns400()
    {
        var (flat, _, _, db) = await SeedFlatWithRoomAsync();
        var fn = MakeFunction(db);
        const string payload = """{ "name": "New Name", "sortOrder": 0, "powerPoints": [] }""";

        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), "not-a-guid", MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_MalformedJsonBody_Returns400()
    {
        var (flat, room, _, db) = await SeedFlatWithRoomAsync();
        var fn = MakeFunction(db);

        var result = await fn.RunAsync(MakeRequest("{ not valid json"), flat.FlatId.ToString(), room.RoomId.ToString(), MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        var dbRoom = await db.Rooms.SingleAsync(r => r.RoomId == room.RoomId);
        dbRoom.Name.ShouldBe("Old Name");
    }

    [Fact]
    public async Task RunAsync_DuplicatePowerPointIdWithinPayload_Returns422AndPersistsNothing()
    {
        var (flat, room, powerPoint, db) = await SeedFlatWithRoomAsync();
        var fn = MakeFunction(db);
        var payload = $$"""
            {
                "name": "Old Name", "sortOrder": 0,
                "powerPoints": [
                    { "powerPointId": "{{powerPoint.PowerPointId}}", "name": "Socket A", "plugId": "plug-a" },
                    { "powerPointId": "{{powerPoint.PowerPointId}}", "name": "Socket B", "plugId": "plug-b" }
                ]
            }
            """;

        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), room.RoomId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(422);
        var dbPp = await db.PowerPoints.SingleAsync(pp => pp.RoomId == room.RoomId);
        dbPp.Name.ShouldBe("Old Socket");
    }

    [Fact]
    public async Task RunAsync_RoomIdNotBelongingToFlat_Returns404()
    {
        var (flat, _, _, db) = await SeedFlatWithRoomAsync();
        var fn = MakeFunction(db);
        const string payload = """{ "name": "New Name", "sortOrder": 0, "powerPoints": [] }""";

        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), Guid.NewGuid().ToString(), MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RunAsync_RoomBelongsToAnotherFlat_Returns404()
    {
        var (flatA, roomA, _, db) = await SeedFlatWithRoomAsync(userId: "user-a");
        db.Users.Add(new User { UserId = "user-b" });
        var flatB = new Flat
        {
            FlatId = Guid.NewGuid(), UserId = "user-b", Name = "Flat B", AnnualKwhBaseline = 3500m, SpikeThreshold = 2.0m
        };
        db.Flats.Add(flatB);
        await db.SaveChangesAsync();

        var fn = MakeFunction(db);
        const string payload = """{ "name": "New Name", "sortOrder": 0, "powerPoints": [] }""";

        var result = await fn.RunAsync(MakeRequest(payload), flatB.FlatId.ToString(), roomA.RoomId.ToString(), MakeFunctionContext(userId: "user-b"), CancellationToken.None);

        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RunAsync_FlatNotOwnedByUser_Returns403()
    {
        var (flat, room, _, db) = await SeedFlatWithRoomAsync(userId: "owner");
        var fn = MakeFunction(db);
        const string payload = """{ "name": "New Name", "sortOrder": 0, "powerPoints": [] }""";

        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), room.RoomId.ToString(), MakeFunctionContext(userId: "intruder"), CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task RunAsync_BlankRoomName_Returns400()
    {
        var (flat, room, _, db) = await SeedFlatWithRoomAsync();
        var fn = MakeFunction(db);
        const string payload = """{ "name": "", "sortOrder": 0, "powerPoints": [] }""";

        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), room.RoomId.ToString(), MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_UnchangedPowerPoint_StillHasItsExistingDevicesInResponse()
    {
        var (flat, room, powerPoint, db) = await SeedFlatWithRoomAsync();
        var device = new Device { DeviceId = Guid.NewGuid(), PowerPointId = powerPoint.PowerPointId, Name = "Lamp", ConsumptionApproach = ConsumptionApproach.None };
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        var fn = MakeFunction(db);
        var payload = $$"""
            {
                "name": "New Name", "sortOrder": 0,
                "powerPoints": [
                    { "powerPointId": "{{powerPoint.PowerPointId}}", "name": "Old Socket", "plugId": null }
                ]
            }
            """;

        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), room.RoomId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<RoomResponse>();
        var ppResponse = response.PowerPoints.Single();
        ppResponse.Devices.ShouldHaveSingleItem();
        ppResponse.Devices.Single().DeviceId.ShouldBe(device.DeviceId);
    }

    [Fact]
    public async Task RunAsync_MissingRowVersion_Returns400AndPersistsNothing()
    {
        var (flat, room, _, db) = await SeedFlatWithRoomAsync();
        var fn = MakeFunction(db);
        const string payload = """{ "name": "New Name", "sortOrder": 0, "powerPoints": [] }""";

        var result = await fn.RunAsync(MakeRequest(payload, rowVersion: null), flat.FlatId.ToString(), room.RoomId.ToString(), MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        var dbRoom = await db.Rooms.SingleAsync(r => r.RoomId == room.RoomId);
        dbRoom.Name.ShouldBe("Old Name");
    }

    [Fact]
    public async Task RunAsync_DuplicatePlugIdWithinPayload_Returns422AndPersistsNothing()
    {
        var (flat, room, powerPoint, db) = await SeedFlatWithRoomAsync();
        var fn = MakeFunction(db);
        var payload = $$"""
            {
                "name": "Old Name", "sortOrder": 0,
                "powerPoints": [
                    { "powerPointId": "{{powerPoint.PowerPointId}}", "name": "Socket 1", "plugId": "plug-dup" },
                    { "name": "Socket 2", "plugId": "plug-dup" }
                ]
            }
            """;

        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), room.RoomId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(422);
        var dbPp = await db.PowerPoints.SingleAsync(pp => pp.RoomId == room.RoomId);
        dbPp.PlugId.ShouldBeNull();
    }
}
