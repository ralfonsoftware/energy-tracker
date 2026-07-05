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

public class GetFlatStructureFunctionTests
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

    [Fact]
    public async Task RunAsync_InvalidFlatIdFormat_Returns400()
    {
        using var db = MakeDb();
        var fn = new GetFlatStructureFunction(db);
        var req = MakeRequest();
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, "not-a-guid", ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_FlatNotOwnedByUser_Returns403()
    {
        var (flat, db) = await SeedFlatAsync(userId: "owner");
        var fn = new GetFlatStructureFunction(db);
        var req = MakeRequest();
        var ctx = MakeFunctionContext(userId: "intruder");

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task RunAsync_FlatWithNoRooms_ReturnsHasDefaultTemplateTrueAndEmptyRooms()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new GetFlatStructureFunction(db);
        var req = MakeRequest();
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<FlatStructureResponse>();
        response.HasDefaultTemplate.ShouldBeTrue();
        response.Rooms.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_FlatWithRooms_ReturnsHasDefaultTemplateFalse()
    {
        var (flat, db) = await SeedFlatAsync();
        db.Rooms.Add(new Room { RoomId = Guid.NewGuid(), FlatId = flat.FlatId, Name = "Kitchen", SortOrder = 0 });
        await db.SaveChangesAsync();

        var fn = new GetFlatStructureFunction(db);
        var req = MakeRequest();
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<FlatStructureResponse>();
        response.HasDefaultTemplate.ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_FullNestedHierarchy_ReturnsCorrectlyShapedResponse()
    {
        var (flat, db) = await SeedFlatAsync();
        var room = new Room { RoomId = Guid.NewGuid(), FlatId = flat.FlatId, Name = "Living Room", SortOrder = 1 };
        var powerPoint = new PowerPoint { PowerPointId = Guid.NewGuid(), RoomId = room.RoomId, Name = "Wall Socket", PlugId = "plug-1", Room = room };
        var device = new Device
        {
            DeviceId = Guid.NewGuid(),
            PowerPointId = powerPoint.PowerPointId,
            Name = "TV",
            Type = "Entertainment",
            Manufacturer = "Acme",
            Model = "X100",
            PurchaseDate = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero),
            ConsumptionApproach = ConsumptionApproach.EuLabel,
            EuLabelClass = "A+++",
            EuAnnualKwh = 120.5m,
            SelfMeasuredKwh = null,
            SelfMeasuredPeriod = null,
            PowerPoint = powerPoint
        };
        db.Rooms.Add(room);
        db.PowerPoints.Add(powerPoint);
        db.Devices.Add(device);
        await db.SaveChangesAsync();

        var fn = new GetFlatStructureFunction(db);
        var req = MakeRequest();
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<FlatStructureResponse>();
        response.Rooms.Count.ShouldBe(1);
        var roomResponse = response.Rooms.Single();
        roomResponse.RoomId.ShouldBe(room.RoomId);
        roomResponse.Name.ShouldBe("Living Room");
        roomResponse.SortOrder.ShouldBe(1);
        roomResponse.PowerPoints.Count.ShouldBe(1);
        var ppResponse = roomResponse.PowerPoints.Single();
        ppResponse.PowerPointId.ShouldBe(powerPoint.PowerPointId);
        ppResponse.PlugId.ShouldBe("plug-1");
        ppResponse.Devices.Count.ShouldBe(1);
        var deviceResponse = ppResponse.Devices.Single();
        deviceResponse.DeviceId.ShouldBe(device.DeviceId);
        deviceResponse.Name.ShouldBe("TV");
        deviceResponse.Type.ShouldBe("Entertainment");
        deviceResponse.Manufacturer.ShouldBe("Acme");
        deviceResponse.Model.ShouldBe("X100");
        deviceResponse.PurchaseDate.ShouldBe(device.PurchaseDate);
        deviceResponse.ConsumptionApproach.ShouldBe(ConsumptionApproach.EuLabel);
        deviceResponse.EuLabelClass.ShouldBe("A+++");
        deviceResponse.EuAnnualKwh.ShouldBe(120.5m);
        deviceResponse.SelfMeasuredKwh.ShouldBeNull();
        deviceResponse.SelfMeasuredPeriod.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_RoomsBelongingToOtherFlats_AreNeverReturned()
    {
        var (flat, db) = await SeedFlatAsync(userId: "user-a");
        db.Users.Add(new User { UserId = "user-b" });
        var otherFlat = new Flat
        {
            FlatId = Guid.NewGuid(),
            UserId = "user-b",
            Name = "Other Flat",
            AnnualKwhBaseline = 3500m,
            SpikeThreshold = 2.0m
        };
        db.Flats.Add(otherFlat);
        db.Rooms.Add(new Room { RoomId = Guid.NewGuid(), FlatId = flat.FlatId, Name = "Own Room", SortOrder = 0 });
        db.Rooms.Add(new Room { RoomId = Guid.NewGuid(), FlatId = otherFlat.FlatId, Name = "Other Room", SortOrder = 0 });
        await db.SaveChangesAsync();

        var fn = new GetFlatStructureFunction(db);
        var req = MakeRequest();
        var ctx = MakeFunctionContext(userId: "user-a");

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<FlatStructureResponse>();
        response.Rooms.Count.ShouldBe(1);
        response.Rooms.Single().Name.ShouldBe("Own Room");
    }
}
