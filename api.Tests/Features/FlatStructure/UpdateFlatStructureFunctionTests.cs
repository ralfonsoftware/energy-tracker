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

public class UpdateFlatStructureFunctionTests
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

    private static async Task<(Flat flat, AppDbContext db)> SeedFlatAsync(string userId = "user-test-123")
    {
        var db = MakeDb();
        db.Users.Add(new User { UserId = userId });
        var flat = new Flat
        {
            FlatId = Guid.NewGuid(),
            UserId = userId,
            Name = "Test Flat",
            AnnualKwhBaseline = 3500m,
            SpikeThreshold = 2.0m,
            RowVersion = TestRowVersion
        };
        db.Flats.Add(flat);
        await db.SaveChangesAsync();
        return (flat, db);
    }

    private static UpdateFlatStructureFunction MakeFunction(AppDbContext db) =>
        new(db, new UpdateFlatStructureValidator());

    private const string ValidPayload = """
        {
            "rooms": [
                {
                    "name": "Living Room",
                    "sortOrder": 0,
                    "powerPoints": [
                        {
                            "name": "Wall Socket",
                            "plugId": "plug-1"
                        }
                    ]
                }
            ]
        }
        """;

    [Fact]
    public async Task RunAsync_InvalidFlatIdFormat_Returns400()
    {
        using var db = MakeDb();
        var fn = MakeFunction(db);
        var req = MakeRequest(ValidPayload);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, "not-a-guid", ctx, CancellationToken.None);

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        var type = (string)badRequest.Value!.GetType().GetProperty("type")!.GetValue(badRequest.Value)!;
        type.ShouldBe("https://tools.ietf.org/html/rfc7231#section-6.5.1");
    }

    [Fact]
    public async Task RunAsync_FlatNotOwnedByUser_Returns403AndPersistsNothing()
    {
        var (flat, db) = await SeedFlatAsync(userId: "owner");
        var fn = MakeFunction(db);
        var req = MakeRequest(ValidPayload);
        var ctx = MakeFunctionContext(userId: "intruder");

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
        (await db.Rooms.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_ValidPayload_PersistsFullNestedHierarchyAndReturns200()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(ValidPayload);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<FlatStructureResponse>();
        response.HasDefaultTemplate.ShouldBeFalse();
        response.Rooms.Count.ShouldBe(1);
        var roomResponse = response.Rooms.Single();
        roomResponse.Name.ShouldBe("Living Room");
        var ppResponse = roomResponse.PowerPoints.Single();
        ppResponse.PlugId.ShouldBe("plug-1");

        var dbRoom = await db.Rooms.SingleAsync(r => r.FlatId == flat.FlatId);
        dbRoom.RoomId.ShouldBe(roomResponse.RoomId);
        var dbPowerPoint = await db.PowerPoints.SingleAsync(pp => pp.RoomId == dbRoom.RoomId);
        dbPowerPoint.PlugId.ShouldBe("plug-1");
    }

    [Fact]
    public async Task RunAsync_ReplacingExistingStructure_RemovesOldRoomsAndPowerPointsCascadingTheirDevices()
    {
        var (flat, db) = await SeedFlatAsync();
        var oldRoom = new Room { RoomId = Guid.NewGuid(), FlatId = flat.FlatId, Name = "Old Room", SortOrder = 0 };
        var oldPowerPoint = new PowerPoint { PowerPointId = Guid.NewGuid(), RoomId = oldRoom.RoomId, Name = "Old Socket" };
        // A device attached to a removed PowerPoint must still disappear — via EF's PowerPoint->Device
        // cascade delete, not via any device-specific logic in this Function (which no longer has any).
        var oldDevice = new Device { DeviceId = Guid.NewGuid(), PowerPointId = oldPowerPoint.PowerPointId, Name = "Old Device", ConsumptionApproach = ConsumptionApproach.None };
        db.Rooms.Add(oldRoom);
        db.PowerPoints.Add(oldPowerPoint);
        db.Devices.Add(oldDevice);
        await db.SaveChangesAsync();

        var fn = MakeFunction(db);
        var req = MakeRequest(ValidPayload);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();

        (await db.Rooms.AnyAsync(r => r.RoomId == oldRoom.RoomId)).ShouldBeFalse();
        (await db.PowerPoints.AnyAsync(pp => pp.PowerPointId == oldPowerPoint.PowerPointId)).ShouldBeFalse();
        (await db.Devices.AnyAsync(d => d.DeviceId == oldDevice.DeviceId)).ShouldBeFalse();

        (await db.Rooms.CountAsync(r => r.FlatId == flat.FlatId)).ShouldBe(1);
        (await db.PowerPoints.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_DeviceAbsentFromPayload_IsNotDeleted()
    {
        // The core regression test for this story: UpdateFlatStructureFunction used to treat any
        // device absent from the payload as deleted (the root cause closed by this story — see
        // structure-editor-device-not-persisted-investigation.md). A device on a power point that
        // IS present in the payload — but whose own JSON no longer carries any "devices" field at
        // all — must now survive untouched, because this Function never reads or writes Devices.
        var (flat, db) = await SeedFlatAsync();
        var room = new Room { RoomId = Guid.NewGuid(), FlatId = flat.FlatId, Name = "Room", SortOrder = 0 };
        var pp = new PowerPoint { PowerPointId = Guid.NewGuid(), RoomId = room.RoomId, FlatId = flat.FlatId, Name = "Socket" };
        var device = new Device { DeviceId = Guid.NewGuid(), PowerPointId = pp.PowerPointId, Name = "Device", ConsumptionApproach = ConsumptionApproach.None };
        db.Rooms.Add(room);
        db.PowerPoints.Add(pp);
        db.Devices.Add(device);
        db.DeviceAssignmentPeriods.Add(new DeviceAssignmentPeriod
        {
            Id = Guid.NewGuid(),
            DeviceId = device.DeviceId,
            PowerPointId = pp.PowerPointId,
            FlatId = flat.FlatId,
            From = DateTimeOffset.UtcNow.AddDays(-5),
            To = null
        });
        await db.SaveChangesAsync();

        var payload = $$"""
            {
                "rooms": [
                    {
                        "roomId": "{{room.RoomId}}",
                        "name": "Room",
                        "sortOrder": 0,
                        "powerPoints": [
                            { "powerPointId": "{{pp.PowerPointId}}", "name": "Socket", "plugId": null }
                        ]
                    }
                ]
            }
            """;

        var fn = MakeFunction(db);
        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<FlatStructureResponse>();
        var deviceResponse = response.Rooms.Single().PowerPoints.Single().Devices.Single();
        deviceResponse.DeviceId.ShouldBe(device.DeviceId);

        (await db.Devices.AnyAsync(d => d.DeviceId == device.DeviceId)).ShouldBeTrue();
        (await db.DeviceAssignmentPeriods.AnyAsync(p => p.DeviceId == device.DeviceId && p.To == null)).ShouldBeTrue();
    }

    [Fact]
    public async Task RunAsync_EmptyRoomsList_ClearsExistingStructure()
    {
        var (flat, db) = await SeedFlatAsync();
        var oldRoom = new Room { RoomId = Guid.NewGuid(), FlatId = flat.FlatId, Name = "Old Room", SortOrder = 0 };
        db.Rooms.Add(oldRoom);
        await db.SaveChangesAsync();

        var fn = MakeFunction(db);
        var req = MakeRequest("""{ "rooms": [] }""");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<FlatStructureResponse>();
        response.HasDefaultTemplate.ShouldBeTrue();
        response.Rooms.ShouldBeEmpty();
        (await db.Rooms.CountAsync(r => r.FlatId == flat.FlatId)).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_DuplicatePlugIdWithinSameFlatPayload_Returns422AndPersistsNothing()
    {
        var (flat, db) = await SeedFlatAsync();
        const string payload = """
            {
                "rooms": [
                    {
                        "name": "Room A",
                        "sortOrder": 0,
                        "powerPoints": [
                            { "name": "Socket 1", "plugId": "plug-dup", "devices": [] },
                            { "name": "Socket 2", "plugId": "plug-dup", "devices": [] }
                        ]
                    }
                ]
            }
            """;

        var fn = MakeFunction(db);
        var req = MakeRequest(payload);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(422);
        (await db.Rooms.CountAsync()).ShouldBe(0);
        (await db.PowerPoints.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_DuplicateRoomIdWithinSameFlatPayload_Returns422()
    {
        var (flat, db) = await SeedFlatAsync();
        var room = new Room { RoomId = Guid.NewGuid(), FlatId = flat.FlatId, Name = "Room A", SortOrder = 0 };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var payload = $$"""
            {
                "rooms": [
                    { "roomId": "{{room.RoomId}}", "name": "Room A", "sortOrder": 0, "powerPoints": [] },
                    { "roomId": "{{room.RoomId}}", "name": "Room A Renamed", "sortOrder": 1, "powerPoints": [] }
                ]
            }
            """;

        var fn = MakeFunction(db);
        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(422);
    }

    [Fact]
    public async Task RunAsync_DuplicatePowerPointIdWithinSameFlatPayload_Returns422()
    {
        var (flat, db) = await SeedFlatAsync();
        var roomA = new Room { RoomId = Guid.NewGuid(), FlatId = flat.FlatId, Name = "Room A", SortOrder = 0 };
        var roomB = new Room { RoomId = Guid.NewGuid(), FlatId = flat.FlatId, Name = "Room B", SortOrder = 1 };
        var pp = new PowerPoint { PowerPointId = Guid.NewGuid(), RoomId = roomA.RoomId, Name = "Socket" };
        db.Rooms.AddRange(roomA, roomB);
        db.PowerPoints.Add(pp);
        await db.SaveChangesAsync();

        var payload = $$"""
            {
                "rooms": [
                    {
                        "roomId": "{{roomA.RoomId}}", "name": "Room A", "sortOrder": 0,
                        "powerPoints": [ { "powerPointId": "{{pp.PowerPointId}}", "name": "Socket", "plugId": null, "devices": [] } ]
                    },
                    {
                        "roomId": "{{roomB.RoomId}}", "name": "Room B", "sortOrder": 1,
                        "powerPoints": [ { "powerPointId": "{{pp.PowerPointId}}", "name": "Socket", "plugId": null, "devices": [] } ]
                    }
                ]
            }
            """;

        var fn = MakeFunction(db);
        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(422);
    }

    [Fact]
    public async Task RunAsync_SamePlugIdAcrossDifferentFlats_Succeeds()
    {
        var (flatA, db) = await SeedFlatAsync(userId: "user-a");
        db.Users.Add(new User { UserId = "user-b" });
        var flatB = new Flat
        {
            FlatId = Guid.NewGuid(),
            UserId = "user-b",
            Name = "Flat B",
            AnnualKwhBaseline = 3500m,
            SpikeThreshold = 2.0m,
            RowVersion = TestRowVersion
        };
        db.Flats.Add(flatB);
        await db.SaveChangesAsync();

        const string payloadA = """
            { "rooms": [ { "name": "Room A", "sortOrder": 0, "powerPoints": [ { "name": "Socket", "plugId": "plug-1", "devices": [] } ] } ] }
            """;
        const string payloadB = """
            { "rooms": [ { "name": "Room B", "sortOrder": 0, "powerPoints": [ { "name": "Socket", "plugId": "plug-1", "devices": [] } ] } ] }
            """;

        var fn = MakeFunction(db);
        var resultA = await fn.RunAsync(MakeRequest(payloadA), flatA.FlatId.ToString(), MakeFunctionContext("user-a"), CancellationToken.None);
        resultA.ShouldBeOfType<OkObjectResult>();

        var resultB = await fn.RunAsync(MakeRequest(payloadB), flatB.FlatId.ToString(), MakeFunctionContext("user-b"), CancellationToken.None);
        resultB.ShouldBeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RunAsync_MissingRoomName_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        const string payload = """{ "rooms": [ { "name": "", "sortOrder": 0, "powerPoints": [] } ] }""";

        var fn = MakeFunction(db);
        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_MissingPowerPointName_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        const string payload = """
            { "rooms": [ { "name": "Room A", "sortOrder": 0, "powerPoints": [ { "name": "", "plugId": null, "devices": [] } ] } ] }
            """;

        var fn = MakeFunction(db);
        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_MalformedJsonBody_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest("{ not valid json");

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_MissingRoomsKey_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest("{}");

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_NullPowerPointsInRoom_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        const string payload = """{ "rooms": [ { "name": "Room A", "sortOrder": 0, "powerPoints": null } ] }""";

        var fn = MakeFunction(db);
        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_PlugIdExceedsMaxLength_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var overLong = new string('x', 201);
        var payload = $$"""
            { "rooms": [ { "name": "Room A", "sortOrder": 0, "powerPoints": [ { "name": "Socket", "plugId": "{{overLong}}", "devices": [] } ] } ] }
            """;

        var fn = MakeFunction(db);
        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_DuplicateEmptyStringPlugIds_DoesNotTriggerFalsePositive422()
    {
        var (flat, db) = await SeedFlatAsync();
        const string payload = """
            {
                "rooms": [
                    {
                        "name": "Room A", "sortOrder": 0,
                        "powerPoints": [
                            { "name": "Socket 1", "plugId": "", "devices": [] },
                            { "name": "Socket 2", "plugId": "", "devices": [] }
                        ]
                    }
                ]
            }
            """;

        var fn = MakeFunction(db);
        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RunAsync_RoomsSubmittedOutOfSortOrder_ResponseIsSortedBySortOrder()
    {
        var (flat, db) = await SeedFlatAsync();
        const string payload = """
            {
                "rooms": [
                    { "name": "Second Room", "sortOrder": 1, "powerPoints": [] },
                    { "name": "First Room", "sortOrder": 0, "powerPoints": [] }
                ]
            }
            """;

        var fn = MakeFunction(db);
        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<FlatStructureResponse>();
        response.Rooms.Select(r => r.Name).ShouldBe(["First Room", "Second Room"]);
    }

    [Fact]
    public async Task RunAsync_MissingRowVersion_Returns400AndPersistsNothing()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(ValidPayload, rowVersion: null);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        badRequest.StatusCode.ShouldBe(400);
        (await db.Rooms.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_MalformedRowVersion_Returns400AndPersistsNothing()
    {
        // byte[]-typed properties are base64-decoded by System.Text.Json during deserialization
        // itself, so an undecodable rowVersion surfaces as a JsonException ("Invalid JSON in
        // request body") rather than this Function's own "rowVersion is required" check —
        // a different code path from the JsonNode-based Functions, but still a 400.
        var (flat, db) = await SeedFlatAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(
            """{ "rooms": [], "rowVersion": "not-valid-base64!!" }""", rowVersion: null);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        badRequest.StatusCode.ShouldBe(400);
        (await db.Rooms.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_ConcurrentModification_Returns409ConflictAndPersistsNothing()
    {
        var flat = new Flat
        {
            FlatId = Guid.NewGuid(),
            UserId = "user-test-123",
            Name = "Test Flat",
            AnnualKwhBaseline = 3500m,
            SpikeThreshold = 2.0m,
            RowVersion = TestRowVersion
        };
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using (var seedCtx = new AppDbContext(dbOptions))
        {
            seedCtx.Users.Add(new User { UserId = "user-test-123" });
            seedCtx.Flats.Add(flat);
            await seedCtx.SaveChangesAsync();
        }

        var db = new ConcurrencyConflictDbContext(dbOptions);
        var fn = MakeFunction(db);
        var req = MakeRequest(ValidPayload);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var conflict = result.ShouldBeOfType<ObjectResult>();
        conflict.StatusCode.ShouldBe(409);
        using var verifyCtx = new AppDbContext(dbOptions);
        (await verifyCtx.Rooms.CountAsync(r => r.FlatId == flat.FlatId)).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_DuplicatePlugIdAcrossDifferentSavedPowerPoints_Returns409ConflictAndPersistsNothing()
    {
        var flat = new Flat
        {
            FlatId = Guid.NewGuid(),
            UserId = "user-test-123",
            Name = "Test Flat",
            AnnualKwhBaseline = 3500m,
            SpikeThreshold = 2.0m,
            RowVersion = TestRowVersion
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
        var req = MakeRequest(ValidPayload);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var conflict = result.ShouldBeOfType<ConflictObjectResult>();
        conflict.StatusCode.ShouldBe(409);
        var value = conflict.Value.ShouldNotBeNull();
        value.GetType().GetProperty("detail")!.GetValue(value)
            .ShouldBe("This Smart Plug is already assigned to another Power Point in this flat.");
        using var verifyCtx = new AppDbContext(dbOptions);
        (await verifyCtx.Rooms.CountAsync(r => r.FlatId == flat.FlatId)).ShouldBe(0);
        (await verifyCtx.PowerPoints.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_TwoPowerPointsWithNullPlugId_SucceedsWithoutConflict()
    {
        var (flat, db) = await SeedFlatAsync();
        const string payload = """
            {
                "rooms": [
                    {
                        "name": "Room A", "sortOrder": 0,
                        "powerPoints": [
                            { "name": "Socket 1", "plugId": null, "devices": [] },
                            { "name": "Socket 2", "plugId": null, "devices": [] }
                        ]
                    }
                ]
            }
            """;

        var fn = MakeFunction(db);
        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
        (await db.PowerPoints.CountAsync(pp => pp.PlugId == null)).ShouldBe(2);
    }

    [Fact]
    public async Task RunAsync_PayloadWithMatchingIds_UpdatesRowsInPlacePreservingPrimaryKeys()
    {
        var (flat, db) = await SeedFlatAsync();
        var room = new Room { RoomId = Guid.NewGuid(), FlatId = flat.FlatId, Name = "Old Name", SortOrder = 0 };
        var pp = new PowerPoint { PowerPointId = Guid.NewGuid(), RoomId = room.RoomId, FlatId = flat.FlatId, Name = "Old Socket" };
        db.Rooms.Add(room);
        db.PowerPoints.Add(pp);
        await db.SaveChangesAsync();

        var payload = $$"""
            {
                "rooms": [
                    {
                        "roomId": "{{room.RoomId}}",
                        "name": "Renamed Room",
                        "sortOrder": 0,
                        "powerPoints": [
                            {
                                "powerPointId": "{{pp.PowerPointId}}",
                                "name": "Renamed Socket",
                                "plugId": null
                            }
                        ]
                    }
                ]
            }
            """;

        var fn = MakeFunction(db);
        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
        (await db.Rooms.CountAsync(r => r.FlatId == flat.FlatId)).ShouldBe(1);
        var dbRoom = await db.Rooms.SingleAsync(r => r.FlatId == flat.FlatId);
        dbRoom.RoomId.ShouldBe(room.RoomId);
        dbRoom.Name.ShouldBe("Renamed Room");
        var dbPp = await db.PowerPoints.SingleAsync();
        dbPp.PowerPointId.ShouldBe(pp.PowerPointId);
        dbPp.Name.ShouldBe("Renamed Socket");
    }
}
