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

public class DeleteDeviceFunctionTests
{
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

    private static HttpRequest MakeRequest(byte[] rowVersion)
    {
        var json = JsonSerializer.Serialize(new { rowVersion = Convert.ToBase64String(rowVersion) });
        var ctx = new DefaultHttpContext();
        ctx.Request.ContentType = "application/json";
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return ctx.Request;
    }

    private static async Task<(Flat flat, PowerPoint powerPoint, Device device, AppDbContext db)> SeedFlatWithDeviceAsync(
        string userId = "user-test-123")
    {
        var db = MakeDb();
        db.Users.Add(new User { UserId = userId });
        var flat = new Flat
        {
            FlatId = Guid.NewGuid(), UserId = userId, Name = "Test Flat", AnnualKwhBaseline = 3500m, SpikeThreshold = 2.0m
        };
        var room = new Room { RoomId = Guid.NewGuid(), FlatId = flat.FlatId, Name = "Room", SortOrder = 0 };
        var powerPoint = new PowerPoint { PowerPointId = Guid.NewGuid(), RoomId = room.RoomId, FlatId = flat.FlatId, Name = "Socket" };
        var device = new Device
        {
            DeviceId = Guid.NewGuid(), PowerPointId = powerPoint.PowerPointId, Name = "Device",
            ConsumptionApproach = ConsumptionApproach.None, RowVersion = TestRowVersion
        };
        db.Flats.Add(flat);
        db.Rooms.Add(room);
        db.PowerPoints.Add(powerPoint);
        db.Devices.Add(device);
        db.DeviceAssignmentPeriods.Add(new DeviceAssignmentPeriod
        {
            Id = Guid.NewGuid(),
            DeviceId = device.DeviceId,
            PowerPointId = powerPoint.PowerPointId,
            FlatId = flat.FlatId,
            From = DateTimeOffset.UtcNow.AddDays(-5),
            To = null
        });
        await db.SaveChangesAsync();
        return (flat, powerPoint, device, db);
    }

    private static DeleteDeviceFunction MakeFunction(AppDbContext db) => new(db);

    [Fact]
    public async Task RunAsync_ValidRequest_DeletesDeviceAndCascadesAssignmentPeriodsAndReturns204()
    {
        var (flat, powerPoint, device, db) = await SeedFlatWithDeviceAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(device.RowVersion);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), powerPoint.PowerPointId.ToString(), device.DeviceId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<NoContentResult>();
        (await db.Devices.AnyAsync(d => d.DeviceId == device.DeviceId)).ShouldBeFalse();
        (await db.DeviceAssignmentPeriods.AnyAsync(p => p.DeviceId == device.DeviceId)).ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_StaleRowVersion_Returns409ConflictAndPersistsDevice()
    {
        var flatId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var powerPointId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var flat = new Flat
        {
            FlatId = flatId, UserId = "user-test-123", Name = "Test Flat", AnnualKwhBaseline = 3500m, SpikeThreshold = 2.0m
        };
        var room = new Room { RoomId = roomId, FlatId = flatId, Name = "Room", SortOrder = 0 };
        var powerPoint = new PowerPoint { PowerPointId = powerPointId, RoomId = roomId, FlatId = flatId, Name = "Socket" };
        var device = new Device
        {
            DeviceId = deviceId, PowerPointId = powerPointId, Name = "Device",
            ConsumptionApproach = ConsumptionApproach.None, RowVersion = TestRowVersion
        };
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using (var seedCtx = new AppDbContext(dbOptions))
        {
            seedCtx.Users.Add(new User { UserId = "user-test-123" });
            seedCtx.Flats.Add(flat);
            seedCtx.Rooms.Add(room);
            seedCtx.PowerPoints.Add(powerPoint);
            seedCtx.Devices.Add(device);
            await seedCtx.SaveChangesAsync();
        }

        var conflictDb = new ConcurrencyConflictDbContext(dbOptions);
        var fn = MakeFunction(conflictDb);
        var req = MakeRequest(TestRowVersion);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flatId.ToString(), powerPointId.ToString(), deviceId.ToString(), ctx, CancellationToken.None);

        var conflict = result.ShouldBeOfType<ObjectResult>();
        conflict.StatusCode.ShouldBe(409);
        using var verifyCtx = new AppDbContext(dbOptions);
        (await verifyCtx.Devices.AnyAsync(d => d.DeviceId == deviceId)).ShouldBeTrue();
    }

    [Fact]
    public async Task RunAsync_FlatNotOwnedByUser_Returns403()
    {
        var (flat, powerPoint, device, db) = await SeedFlatWithDeviceAsync(userId: "owner");
        var fn = MakeFunction(db);
        var req = MakeRequest(device.RowVersion);
        var ctx = MakeFunctionContext(userId: "intruder");

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), powerPoint.PowerPointId.ToString(), device.DeviceId.ToString(), ctx, CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task RunAsync_UnknownDeviceId_Returns404()
    {
        var (flat, powerPoint, _, db) = await SeedFlatWithDeviceAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest([1, 2, 3]);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), powerPoint.PowerPointId.ToString(), Guid.NewGuid().ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RunAsync_AlreadyDeletedDevice_Returns404()
    {
        var (flat, powerPoint, device, db) = await SeedFlatWithDeviceAsync();
        var fn = MakeFunction(db);
        await fn.RunAsync(
            MakeRequest(device.RowVersion), flat.FlatId.ToString(), powerPoint.PowerPointId.ToString(),
            device.DeviceId.ToString(), MakeFunctionContext(), CancellationToken.None);

        var result = await fn.RunAsync(
            MakeRequest(device.RowVersion), flat.FlatId.ToString(), powerPoint.PowerPointId.ToString(),
            device.DeviceId.ToString(), MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RunAsync_MissingRowVersion_Returns400AndPersistsDevice()
    {
        var (flat, powerPoint, device, db) = await SeedFlatWithDeviceAsync();
        var fn = MakeFunction(db);
        var json = JsonSerializer.Serialize(new { });
        var ctx = new DefaultHttpContext();
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var result = await fn.RunAsync(
            ctx.Request, flat.FlatId.ToString(), powerPoint.PowerPointId.ToString(), device.DeviceId.ToString(),
            MakeFunctionContext(), CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        (await db.Devices.AnyAsync(d => d.DeviceId == device.DeviceId)).ShouldBeTrue();
    }
}
