using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Flats;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace api.Tests.Features.Flats;

public class DeleteFlatFunctionTests
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

    private static Flat MakeFlat(string userId, string name = "Test Flat") => new()
    {
        FlatId = Guid.NewGuid(),
        UserId = userId,
        Name = name,
        AnnualKwhBaseline = 3500m,
        SpikeThreshold = 2.0m
    };

    private static MeterReading MakeReading(Guid flatId, DateTimeOffset readingDate) => new()
    {
        ReadingId = Guid.NewGuid(),
        FlatId = flatId,
        KwhValue = 100m,
        ReadingDate = readingDate
    };

    private static Tariff MakeTariff(Guid flatId, DateTimeOffset contractStartDate) => new()
    {
        TariffId = Guid.NewGuid(),
        FlatId = flatId,
        ContractStartDate = contractStartDate,
        PricePerKwh = 0.30m,
        MonthlyBaseFee = 10m
    };

    private static async Task<(Room room, PowerPoint powerPoint, Device device)> SeedStructureAsync(AppDbContext db, Guid flatId)
    {
        var room = new Room { RoomId = Guid.NewGuid(), FlatId = flatId, Name = "Room", SortOrder = 0 };
        var powerPoint = new PowerPoint { PowerPointId = Guid.NewGuid(), RoomId = room.RoomId, Name = "Socket" };
        var device = new Device { DeviceId = Guid.NewGuid(), PowerPointId = powerPoint.PowerPointId, Name = "Device", ConsumptionApproach = ConsumptionApproach.None };
        db.Rooms.Add(room);
        db.PowerPoints.Add(powerPoint);
        db.Devices.Add(device);
        await db.SaveChangesAsync();
        return (room, powerPoint, device);
    }

    [Fact]
    public async Task RunAsync_InvalidFlatIdFormat_Returns400()
    {
        using var db = MakeDb();
        var fn = new DeleteFlatFunction(db);
        var req = MakeRequest();
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, "not-a-guid", ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_FlatDoesNotExist_Returns403()
    {
        using var db = MakeDb();
        var fn = new DeleteFlatFunction(db);
        var req = MakeRequest();
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, Guid.NewGuid().ToString(), ctx, CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task RunAsync_FlatNotOwnedByUser_Returns403AndPerformsNoDeletion()
    {
        using var db = MakeDb();
        var flat = MakeFlat("owner-user");
        db.Flats.Add(flat);
        var reading = MakeReading(flat.FlatId, DateTimeOffset.UtcNow);
        db.MeterReadings.Add(reading);
        var tariff = MakeTariff(flat.FlatId, DateTimeOffset.UtcNow);
        db.Tariffs.Add(tariff);
        await db.SaveChangesAsync();
        var (room, powerPoint, device) = await SeedStructureAsync(db, flat.FlatId);

        var fn = new DeleteFlatFunction(db);
        var req = MakeRequest();
        var ctx = MakeFunctionContext("attacker-user");

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);

        (await db.Flats.CountAsync(f => f.FlatId == flat.FlatId)).ShouldBe(1);
        (await db.MeterReadings.CountAsync(r => r.FlatId == flat.FlatId)).ShouldBe(1);
        (await db.Tariffs.CountAsync(t => t.FlatId == flat.FlatId)).ShouldBe(1);
        (await db.Rooms.CountAsync(r => r.RoomId == room.RoomId)).ShouldBe(1);
        (await db.PowerPoints.CountAsync(pp => pp.PowerPointId == powerPoint.PowerPointId)).ShouldBe(1);
        (await db.Devices.CountAsync(d => d.DeviceId == device.DeviceId)).ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_ValidDelete_Returns204AndCascadeDeletesAllMeterReadingsAndTariffs()
    {
        using var db = MakeDb();
        var flat = MakeFlat("owner-user");
        db.Flats.Add(flat);
        db.MeterReadings.AddRange(
            MakeReading(flat.FlatId, DateTimeOffset.UtcNow.AddDays(-2)),
            MakeReading(flat.FlatId, DateTimeOffset.UtcNow.AddDays(-1)));
        db.Tariffs.AddRange(
            MakeTariff(flat.FlatId, DateTimeOffset.UtcNow.AddMonths(-6)),
            MakeTariff(flat.FlatId, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var fn = new DeleteFlatFunction(db);
        var req = MakeRequest();
        var ctx = MakeFunctionContext("owner-user");

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<NoContentResult>();
        (await db.MeterReadings.CountAsync(r => r.FlatId == flat.FlatId)).ShouldBe(0);
        (await db.Tariffs.CountAsync(t => t.FlatId == flat.FlatId)).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_ValidDelete_CascadeDeletesAllRoomsPowerPointsAndDevices()
    {
        using var db = MakeDb();
        var flat = MakeFlat("owner-user");
        db.Flats.Add(flat);
        db.MeterReadings.Add(MakeReading(flat.FlatId, DateTimeOffset.UtcNow));
        db.Tariffs.Add(MakeTariff(flat.FlatId, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        var (room, powerPoint, device) = await SeedStructureAsync(db, flat.FlatId);

        var fn = new DeleteFlatFunction(db);
        var req = MakeRequest();
        var ctx = MakeFunctionContext("owner-user");

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<NoContentResult>();
        (await db.Rooms.CountAsync(r => r.FlatId == flat.FlatId)).ShouldBe(0);
        (await db.PowerPoints.CountAsync(pp => pp.PowerPointId == powerPoint.PowerPointId)).ShouldBe(0);
        (await db.Devices.CountAsync(d => d.DeviceId == device.DeviceId)).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_ValidDelete_RemovesFlatItself()
    {
        using var db = MakeDb();
        var flat = MakeFlat("owner-user");
        db.Flats.Add(flat);
        await db.SaveChangesAsync();

        var fn = new DeleteFlatFunction(db);
        var req = MakeRequest();
        var ctx = MakeFunctionContext("owner-user");

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<NoContentResult>();
        (await db.Flats.CountAsync(f => f.FlatId == flat.FlatId)).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_DeletingOneFlat_LeavesSiblingFlatDataUntouched()
    {
        using var db = MakeDb();
        var flatToDelete = MakeFlat("user-a", name: "Flat To Delete");
        var siblingFlat = MakeFlat("user-b", name: "Sibling Flat");
        db.Flats.AddRange(flatToDelete, siblingFlat);

        var deleteReading = MakeReading(flatToDelete.FlatId, DateTimeOffset.UtcNow);
        var siblingReading = MakeReading(siblingFlat.FlatId, DateTimeOffset.UtcNow);
        db.MeterReadings.AddRange(deleteReading, siblingReading);

        var deleteTariff = MakeTariff(flatToDelete.FlatId, DateTimeOffset.UtcNow);
        var siblingTariff = MakeTariff(siblingFlat.FlatId, DateTimeOffset.UtcNow);
        db.Tariffs.AddRange(deleteTariff, siblingTariff);
        await db.SaveChangesAsync();
        var (_, _, _) = await SeedStructureAsync(db, flatToDelete.FlatId);
        var (siblingRoom, siblingPowerPoint, siblingDevice) = await SeedStructureAsync(db, siblingFlat.FlatId);

        var fn = new DeleteFlatFunction(db);
        var req = MakeRequest();
        var ctx = MakeFunctionContext("user-a");

        var result = await fn.RunAsync(req, flatToDelete.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<NoContentResult>();

        var remainingSiblingFlat = await db.Flats.SingleAsync(f => f.FlatId == siblingFlat.FlatId);
        remainingSiblingFlat.Name.ShouldBe("Sibling Flat");

        var remainingSiblingReading = await db.MeterReadings.SingleAsync(r => r.FlatId == siblingFlat.FlatId);
        remainingSiblingReading.ReadingId.ShouldBe(siblingReading.ReadingId);

        var remainingSiblingTariff = await db.Tariffs.SingleAsync(t => t.FlatId == siblingFlat.FlatId);
        remainingSiblingTariff.TariffId.ShouldBe(siblingTariff.TariffId);

        (await db.Rooms.CountAsync(r => r.RoomId == siblingRoom.RoomId)).ShouldBe(1);
        (await db.PowerPoints.CountAsync(pp => pp.PowerPointId == siblingPowerPoint.PowerPointId)).ShouldBe(1);
        (await db.Devices.CountAsync(d => d.DeviceId == siblingDevice.DeviceId)).ShouldBe(1);

        (await db.Flats.CountAsync(f => f.FlatId == flatToDelete.FlatId)).ShouldBe(0);
        (await db.MeterReadings.CountAsync(r => r.FlatId == flatToDelete.FlatId)).ShouldBe(0);
        (await db.Tariffs.CountAsync(t => t.FlatId == flatToDelete.FlatId)).ShouldBe(0);
        (await db.Rooms.CountAsync(r => r.FlatId == flatToDelete.FlatId)).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_DeletingOneFlat_LeavesSameOwnerSiblingFlatDataUntouched()
    {
        using var db = MakeDb();
        var flatToDelete = MakeFlat("user-a", name: "Flat To Delete");
        var siblingFlat = MakeFlat("user-a", name: "Sibling Flat");
        db.Flats.AddRange(flatToDelete, siblingFlat);

        var deleteReading = MakeReading(flatToDelete.FlatId, DateTimeOffset.UtcNow);
        var siblingReading = MakeReading(siblingFlat.FlatId, DateTimeOffset.UtcNow);
        db.MeterReadings.AddRange(deleteReading, siblingReading);

        var deleteTariff = MakeTariff(flatToDelete.FlatId, DateTimeOffset.UtcNow);
        var siblingTariff = MakeTariff(siblingFlat.FlatId, DateTimeOffset.UtcNow);
        db.Tariffs.AddRange(deleteTariff, siblingTariff);
        await db.SaveChangesAsync();
        var (_, _, _) = await SeedStructureAsync(db, flatToDelete.FlatId);
        var (siblingRoom, siblingPowerPoint, siblingDevice) = await SeedStructureAsync(db, siblingFlat.FlatId);

        var fn = new DeleteFlatFunction(db);
        var req = MakeRequest();
        var ctx = MakeFunctionContext("user-a");

        var result = await fn.RunAsync(req, flatToDelete.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<NoContentResult>();

        var remainingSiblingFlat = await db.Flats.SingleAsync(f => f.FlatId == siblingFlat.FlatId);
        remainingSiblingFlat.Name.ShouldBe("Sibling Flat");

        var remainingSiblingReading = await db.MeterReadings.SingleAsync(r => r.FlatId == siblingFlat.FlatId);
        remainingSiblingReading.ReadingId.ShouldBe(siblingReading.ReadingId);

        var remainingSiblingTariff = await db.Tariffs.SingleAsync(t => t.FlatId == siblingFlat.FlatId);
        remainingSiblingTariff.TariffId.ShouldBe(siblingTariff.TariffId);

        (await db.Rooms.CountAsync(r => r.RoomId == siblingRoom.RoomId)).ShouldBe(1);
        (await db.PowerPoints.CountAsync(pp => pp.PowerPointId == siblingPowerPoint.PowerPointId)).ShouldBe(1);
        (await db.Devices.CountAsync(d => d.DeviceId == siblingDevice.DeviceId)).ShouldBe(1);

        (await db.Flats.CountAsync(f => f.FlatId == flatToDelete.FlatId)).ShouldBe(0);
        (await db.MeterReadings.CountAsync(r => r.FlatId == flatToDelete.FlatId)).ShouldBe(0);
        (await db.Tariffs.CountAsync(t => t.FlatId == flatToDelete.FlatId)).ShouldBe(0);
        (await db.Rooms.CountAsync(r => r.FlatId == flatToDelete.FlatId)).ShouldBe(0);
    }
}
