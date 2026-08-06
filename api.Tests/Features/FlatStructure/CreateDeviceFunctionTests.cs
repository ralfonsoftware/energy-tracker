using System.Text;
using System.Text.Json;
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

public class CreateDeviceFunctionTests
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

    private static async Task<(Flat flat, PowerPoint powerPoint, AppDbContext db)> SeedFlatWithPowerPointAsync(
        string userId = "user-test-123")
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
        var room = new Room { RoomId = Guid.NewGuid(), FlatId = flat.FlatId, Name = "Room", SortOrder = 0 };
        var powerPoint = new PowerPoint { PowerPointId = Guid.NewGuid(), RoomId = room.RoomId, FlatId = flat.FlatId, Name = "Socket" };
        db.Flats.Add(flat);
        db.Rooms.Add(room);
        db.PowerPoints.Add(powerPoint);
        await db.SaveChangesAsync();
        return (flat, powerPoint, db);
    }

    private static CreateDeviceFunction MakeFunction(AppDbContext db) =>
        new(db, new DeviceValidator());

    [Fact]
    public async Task RunAsync_ValidRequest_CreatesDeviceWithFreshOpenAssignmentPeriodAndReturns201()
    {
        var (flat, powerPoint, db) = await SeedFlatWithPowerPointAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new { name = "TV", consumptionApproach = "None" });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), powerPoint.PowerPointId.ToString(), ctx, CancellationToken.None);

        var created = result.ShouldBeOfType<CreatedResult>();
        var response = created.Value.ShouldBeOfType<DeviceResponse>();
        created.Location.ShouldBe($"/api/v1/flats/{flat.FlatId}/powerpoints/{powerPoint.PowerPointId}/devices/{response.DeviceId}");
        response.Name.ShouldBe("TV");

        var device = await db.Devices.SingleAsync();
        device.DeviceId.ShouldBe(response.DeviceId);
        device.PowerPointId.ShouldBe(powerPoint.PowerPointId);

        var period = (await db.DeviceAssignmentPeriods.Where(p => p.DeviceId == device.DeviceId).ToListAsync())
            .ShouldHaveSingleItem();
        period.To.ShouldBeNull();
        period.PowerPointId.ShouldBe(powerPoint.PowerPointId);
    }

    [Fact]
    public async Task RunAsync_InUseSinceSet_SeedsAssignmentPeriodFromInUseSinceNotNow()
    {
        var (flat, powerPoint, db) = await SeedFlatWithPowerPointAsync();
        var fn = MakeFunction(db);
        var inUseSince = DateTimeOffset.UtcNow.AddDays(-30);
        var req = MakeRequest(new { name = "TV", consumptionApproach = "None", inUseSince });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), powerPoint.PowerPointId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<CreatedResult>();
        var device = await db.Devices.SingleAsync();
        var period = await db.DeviceAssignmentPeriods.SingleAsync(p => p.DeviceId == device.DeviceId);
        period.From.ShouldBe(inUseSince);
    }

    [Fact]
    public async Task RunAsync_FlatNotOwnedByUser_Returns403AndPersistsNothing()
    {
        var (flat, powerPoint, db) = await SeedFlatWithPowerPointAsync(userId: "owner");
        var fn = MakeFunction(db);
        var req = MakeRequest(new { name = "TV", consumptionApproach = "None" });
        var ctx = MakeFunctionContext(userId: "intruder");

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), powerPoint.PowerPointId.ToString(), ctx, CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
        (await db.Devices.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_PowerPointNotBelongingToFlat_Returns404AndPersistsNothing()
    {
        var (flat, _, db) = await SeedFlatWithPowerPointAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new { name = "TV", consumptionApproach = "None" });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), Guid.NewGuid().ToString(), ctx, CancellationToken.None);

        var notFound = result.ShouldBeOfType<NotFoundObjectResult>();
        notFound.StatusCode.ShouldBe(404);
        (await db.Devices.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_PowerPointBelongsToAnotherFlat_Returns404()
    {
        var (flatA, _, db) = await SeedFlatWithPowerPointAsync(userId: "user-a");
        db.Users.Add(new User { UserId = "user-b" });
        var flatB = new Flat
        {
            FlatId = Guid.NewGuid(), UserId = "user-b", Name = "Flat B", AnnualKwhBaseline = 3500m, SpikeThreshold = 2.0m
        };
        var roomB = new Room { RoomId = Guid.NewGuid(), FlatId = flatB.FlatId, Name = "Room B", SortOrder = 0 };
        var ppB = new PowerPoint { PowerPointId = Guid.NewGuid(), RoomId = roomB.RoomId, FlatId = flatB.FlatId, Name = "Socket B" };
        db.Flats.Add(flatB);
        db.Rooms.Add(roomB);
        db.PowerPoints.Add(ppB);
        await db.SaveChangesAsync();

        var fn = MakeFunction(db);
        var req = MakeRequest(new { name = "TV", consumptionApproach = "None" });
        var ctx = MakeFunctionContext(userId: "user-a");

        var result = await fn.RunAsync(req, flatA.FlatId.ToString(), ppB.PowerPointId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RunAsync_InvalidFlatIdFormat_Returns400()
    {
        var (_, powerPoint, db) = await SeedFlatWithPowerPointAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new { name = "TV", consumptionApproach = "None" });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, "not-a-guid", powerPoint.PowerPointId.ToString(), ctx, CancellationToken.None);

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        var type = (string)badRequest.Value!.GetType().GetProperty("type")!.GetValue(badRequest.Value)!;
        type.ShouldBe("https://tools.ietf.org/html/rfc7231#section-6.5.1");
    }

    [Fact]
    public async Task RunAsync_InvalidPowerPointIdFormat_Returns400()
    {
        var (flat, _, db) = await SeedFlatWithPowerPointAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new { name = "TV", consumptionApproach = "None" });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), "not-a-guid", ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_MissingName_Returns400AndPersistsNothing()
    {
        var (flat, powerPoint, db) = await SeedFlatWithPowerPointAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new { name = "", consumptionApproach = "None" });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), powerPoint.PowerPointId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        (await db.Devices.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_DecommissionedDateBeforeInUseSince_Returns400()
    {
        var (flat, powerPoint, db) = await SeedFlatWithPowerPointAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new
        {
            name = "TV",
            consumptionApproach = "None",
            inUseSince = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            decommissionedDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), powerPoint.PowerPointId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_EuLabelApproachMissingKwh_Returns400()
    {
        var (flat, powerPoint, db) = await SeedFlatWithPowerPointAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new { name = "Fridge", consumptionApproach = "EuLabel" });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), powerPoint.PowerPointId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_SelfMeasuredApproachMissingKwhAndPeriod_Returns400()
    {
        var (flat, powerPoint, db) = await SeedFlatWithPowerPointAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new { name = "Fridge", consumptionApproach = "SelfMeasured" });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), powerPoint.PowerPointId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_EuAnnualKwhExceedsFourDecimalPlaces_Returns400()
    {
        var (flat, powerPoint, db) = await SeedFlatWithPowerPointAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new
        {
            name = "Fridge", consumptionApproach = "EuLabel", euLabelClass = "A", euAnnualKwh = 123.56789m
        });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), powerPoint.PowerPointId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_SelfMeasuredKwhExceedsFourDecimalPlaces_Returns400()
    {
        var (flat, powerPoint, db) = await SeedFlatWithPowerPointAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new
        {
            name = "Fridge", consumptionApproach = "SelfMeasured", selfMeasuredKwh = 1.56789m, selfMeasuredPeriod = "Daily"
        });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), powerPoint.PowerPointId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_EuAnnualKwhWithTrailingZerosBeyondFourDecimals_Succeeds()
    {
        var (flat, powerPoint, db) = await SeedFlatWithPowerPointAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new
        {
            name = "Fridge", consumptionApproach = "EuLabel", euLabelClass = "A", euAnnualKwh = 123.500000m
        });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), powerPoint.PowerPointId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<CreatedResult>();
    }

    [Fact]
    public async Task RunAsync_SelfMeasuredKwhWithTrailingZerosBeyondFourDecimals_Succeeds()
    {
        var (flat, powerPoint, db) = await SeedFlatWithPowerPointAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new
        {
            name = "Fridge", consumptionApproach = "SelfMeasured", selfMeasuredKwh = 1.500000m, selfMeasuredPeriod = "Daily"
        });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), powerPoint.PowerPointId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<CreatedResult>();
    }

    [Fact]
    public async Task RunAsync_ConsumptionApproachOutOfEnumRange_Returns400()
    {
        var (flat, powerPoint, db) = await SeedFlatWithPowerPointAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new { name = "Device", consumptionApproach = 999 });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), powerPoint.PowerPointId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_EuLabelApproachWithKwhButNoClass_Returns201()
    {
        var (flat, powerPoint, db) = await SeedFlatWithPowerPointAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new { name = "Device", consumptionApproach = "EuLabel", euAnnualKwh = 150m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), powerPoint.PowerPointId.ToString(), ctx, CancellationToken.None);

        var created = result.ShouldBeOfType<CreatedResult>();
        var response = created.Value.ShouldBeOfType<DeviceResponse>();
        response.EuLabelClass.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_EuLabelApproachWithClassButNoKwh_Returns400()
    {
        var (flat, powerPoint, db) = await SeedFlatWithPowerPointAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new { name = "Device", consumptionApproach = "EuLabel", euLabelClass = "A+++" });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), powerPoint.PowerPointId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_NegativeEuAnnualKwh_Returns400()
    {
        var (flat, powerPoint, db) = await SeedFlatWithPowerPointAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new
        {
            name = "Device", consumptionApproach = "EuLabel", euLabelClass = "A", euAnnualKwh = -5m
        });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), powerPoint.PowerPointId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }
}
