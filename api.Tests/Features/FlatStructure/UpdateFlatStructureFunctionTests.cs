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

public class UpdateFlatStructureFunctionTests
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
                            "plugId": "plug-1",
                            "devices": [
                                {
                                    "name": "TV",
                                    "type": "Entertainment",
                                    "manufacturer": "Acme",
                                    "model": "X100",
                                    "purchaseDate": "2024-01-15T00:00:00+00:00",
                                    "consumptionApproach": "EuLabel",
                                    "euLabelClass": "A+++",
                                    "euAnnualKwh": 120.5,
                                    "selfMeasuredKwh": null,
                                    "selfMeasuredPeriod": null
                                }
                            ]
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

        result.ShouldBeOfType<BadRequestObjectResult>();
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
        var deviceResponse = ppResponse.Devices.Single();
        deviceResponse.ConsumptionApproach.ShouldBe(ConsumptionApproach.EuLabel);

        var dbRoom = await db.Rooms.SingleAsync(r => r.FlatId == flat.FlatId);
        dbRoom.RoomId.ShouldBe(roomResponse.RoomId);
        var dbPowerPoint = await db.PowerPoints.SingleAsync(pp => pp.RoomId == dbRoom.RoomId);
        dbPowerPoint.PlugId.ShouldBe("plug-1");
        var dbDevice = await db.Devices.SingleAsync(d => d.PowerPointId == dbPowerPoint.PowerPointId);
        dbDevice.Name.ShouldBe("TV");
    }

    [Fact]
    public async Task RunAsync_ReplacingExistingStructure_RemovesOldRoomsPowerPointsAndDevices()
    {
        var (flat, db) = await SeedFlatAsync();
        var oldRoom = new Room { RoomId = Guid.NewGuid(), FlatId = flat.FlatId, Name = "Old Room", SortOrder = 0 };
        var oldPowerPoint = new PowerPoint { PowerPointId = Guid.NewGuid(), RoomId = oldRoom.RoomId, Name = "Old Socket" };
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
        (await db.Devices.CountAsync()).ShouldBe(1);
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
            SpikeThreshold = 2.0m
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
    public async Task RunAsync_MissingDeviceName_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        const string payload = """
            {
                "rooms": [
                    {
                        "name": "Room A", "sortOrder": 0,
                        "powerPoints": [
                            {
                                "name": "Socket", "plugId": null,
                                "devices": [ { "name": "", "consumptionApproach": "None" } ]
                            }
                        ]
                    }
                ]
            }
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
    public async Task RunAsync_NullDevicesInPowerPoint_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        const string payload = """
            { "rooms": [ { "name": "Room A", "sortOrder": 0, "powerPoints": [ { "name": "Socket", "plugId": null, "devices": null } ] } ] }
            """;

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
    public async Task RunAsync_ConsumptionApproachOutOfEnumRange_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        const string payload = """
            {
                "rooms": [
                    {
                        "name": "Room A", "sortOrder": 0,
                        "powerPoints": [
                            {
                                "name": "Socket", "plugId": null,
                                "devices": [ { "name": "Device", "consumptionApproach": 999 } ]
                            }
                        ]
                    }
                ]
            }
            """;

        var fn = MakeFunction(db);
        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_EuLabelApproachMissingEuLabelClassAndKwh_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        const string payload = """
            {
                "rooms": [
                    {
                        "name": "Room A", "sortOrder": 0,
                        "powerPoints": [
                            {
                                "name": "Socket", "plugId": null,
                                "devices": [ { "name": "Device", "consumptionApproach": "EuLabel" } ]
                            }
                        ]
                    }
                ]
            }
            """;

        var fn = MakeFunction(db);
        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_SelfMeasuredApproachMissingKwhAndPeriod_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        const string payload = """
            {
                "rooms": [
                    {
                        "name": "Room A", "sortOrder": 0,
                        "powerPoints": [
                            {
                                "name": "Socket", "plugId": null,
                                "devices": [ { "name": "Device", "consumptionApproach": "SelfMeasured" } ]
                            }
                        ]
                    }
                ]
            }
            """;

        var fn = MakeFunction(db);
        var result = await fn.RunAsync(MakeRequest(payload), flat.FlatId.ToString(), MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_NegativeEuAnnualKwh_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        const string payload = """
            {
                "rooms": [
                    {
                        "name": "Room A", "sortOrder": 0,
                        "powerPoints": [
                            {
                                "name": "Socket", "plugId": null,
                                "devices": [ { "name": "Device", "consumptionApproach": "EuLabel", "euLabelClass": "A", "euAnnualKwh": -5 } ]
                            }
                        ]
                    }
                ]
            }
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
}
