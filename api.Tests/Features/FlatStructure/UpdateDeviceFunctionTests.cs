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

public class UpdateDeviceFunctionTests
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

    private static HttpRequest MakeRequest(object body)
    {
        var json = JsonSerializer.Serialize(body);
        var ctx = new DefaultHttpContext();
        ctx.Request.ContentType = "application/json";
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return ctx.Request;
    }

    private static async Task<(Flat flat, PowerPoint ppOld, PowerPoint ppNew, Device device, AppDbContext db)>
        SeedFlatWithDeviceAsync(string userId = "user-test-123")
    {
        var db = MakeDb();
        db.Users.Add(new User { UserId = userId });
        var flat = new Flat
        {
            FlatId = Guid.NewGuid(), UserId = userId, Name = "Test Flat", AnnualKwhBaseline = 3500m, SpikeThreshold = 2.0m
        };
        var room = new Room { RoomId = Guid.NewGuid(), FlatId = flat.FlatId, Name = "Room", SortOrder = 0 };
        var ppOld = new PowerPoint { PowerPointId = Guid.NewGuid(), RoomId = room.RoomId, FlatId = flat.FlatId, Name = "Socket A" };
        var ppNew = new PowerPoint { PowerPointId = Guid.NewGuid(), RoomId = room.RoomId, FlatId = flat.FlatId, Name = "Socket B" };
        var device = new Device
        {
            DeviceId = Guid.NewGuid(), PowerPointId = ppOld.PowerPointId, Name = "Old Device",
            ConsumptionApproach = ConsumptionApproach.None, RowVersion = TestRowVersion
        };
        db.Flats.Add(flat);
        db.Rooms.Add(room);
        db.PowerPoints.AddRange(ppOld, ppNew);
        db.Devices.Add(device);
        db.DeviceAssignmentPeriods.Add(new DeviceAssignmentPeriod
        {
            Id = Guid.NewGuid(),
            DeviceId = device.DeviceId,
            PowerPointId = ppOld.PowerPointId,
            FlatId = flat.FlatId,
            From = DateTimeOffset.UtcNow.AddDays(-10),
            To = null
        });
        await db.SaveChangesAsync();
        return (flat, ppOld, ppNew, device, db);
    }

    private static UpdateDeviceFunction MakeFunction(AppDbContext db) =>
        new(db, new DeviceValidator());

    private static object ValidBody(byte[] rowVersion) => new
    {
        name = "Renamed Device",
        consumptionApproach = "None",
        rowVersion = Convert.ToBase64String(rowVersion)
    };

    [Fact]
    public async Task RunAsync_ValidRequestSamePowerPoint_UpdatesFieldsAndReturns200()
    {
        var (flat, ppOld, _, device, db) = await SeedFlatWithDeviceAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(ValidBody(device.RowVersion));
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), ppOld.PowerPointId.ToString(), device.DeviceId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<DeviceResponse>();
        response.Name.ShouldBe("Renamed Device");

        var dbDevice = await db.Devices.SingleAsync();
        dbDevice.Name.ShouldBe("Renamed Device");
        dbDevice.PowerPointId.ShouldBe(ppOld.PowerPointId);
        (await db.DeviceAssignmentPeriods.CountAsync(p => p.DeviceId == device.DeviceId)).ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_ReassignedToDifferentPowerPoint_ClosesOldPeriodAndOpensNewOnePreservingDevicePk()
    {
        var (flat, ppOld, ppNew, device, db) = await SeedFlatWithDeviceAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(ValidBody(device.RowVersion));
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), ppNew.PowerPointId.ToString(), device.DeviceId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
        var dbDevice = await db.Devices.SingleAsync();
        dbDevice.DeviceId.ShouldBe(device.DeviceId);
        dbDevice.PowerPointId.ShouldBe(ppNew.PowerPointId);

        var periods = await db.DeviceAssignmentPeriods.Where(p => p.DeviceId == device.DeviceId).ToListAsync();
        periods.Count.ShouldBe(2);
        var closed = periods.Single(p => p.PowerPointId == ppOld.PowerPointId);
        closed.To.ShouldNotBeNull();
        var open = periods.Single(p => p.PowerPointId == ppNew.PowerPointId);
        open.To.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_StaleRowVersion_Returns409ConflictAndPersistsNothing()
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
        var powerPoint = new PowerPoint { PowerPointId = powerPointId, RoomId = roomId, FlatId = flatId, Name = "Socket A" };
        var device = new Device
        {
            DeviceId = deviceId, PowerPointId = powerPointId, Name = "Old Device",
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
        var req = MakeRequest(ValidBody(TestRowVersion));
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flatId.ToString(), powerPointId.ToString(), deviceId.ToString(), ctx, CancellationToken.None);

        var conflict = result.ShouldBeOfType<ObjectResult>();
        conflict.StatusCode.ShouldBe(409);
        using var verifyCtx = new AppDbContext(dbOptions);
        (await verifyCtx.Devices.SingleAsync(d => d.DeviceId == deviceId)).Name.ShouldBe("Old Device");
    }

    [Fact]
    public async Task RunAsync_FlatNotOwnedByUser_Returns403()
    {
        var (flat, ppOld, _, device, db) = await SeedFlatWithDeviceAsync(userId: "owner");
        var fn = MakeFunction(db);
        var req = MakeRequest(ValidBody(device.RowVersion));
        var ctx = MakeFunctionContext(userId: "intruder");

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), ppOld.PowerPointId.ToString(), device.DeviceId.ToString(), ctx, CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task RunAsync_DeviceNotBelongingToFlat_Returns404()
    {
        var (flat, ppOld, _, _, db) = await SeedFlatWithDeviceAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(ValidBody([1, 2, 3]));
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), ppOld.PowerPointId.ToString(), Guid.NewGuid().ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RunAsync_MissingName_Returns400AndPersistsNothing()
    {
        var (flat, ppOld, _, device, db) = await SeedFlatWithDeviceAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new { name = "", consumptionApproach = "None", rowVersion = Convert.ToBase64String(device.RowVersion) });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), ppOld.PowerPointId.ToString(), device.DeviceId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        (await db.Devices.SingleAsync()).Name.ShouldBe("Old Device");
    }

    [Fact]
    public async Task RunAsync_MissingRowVersion_Returns400AndPersistsNothing()
    {
        var (flat, ppOld, _, device, db) = await SeedFlatWithDeviceAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new { name = "Renamed Device", consumptionApproach = "None" });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), ppOld.PowerPointId.ToString(), device.DeviceId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        (await db.Devices.SingleAsync()).Name.ShouldBe("Old Device");
    }

    [Fact]
    public async Task RunAsync_EuAnnualKwhExceedsFourDecimalPlaces_Returns400()
    {
        var (flat, ppOld, _, device, db) = await SeedFlatWithDeviceAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new
        {
            name = "Fridge", consumptionApproach = "EuLabel", euLabelClass = "A", euAnnualKwh = 123.56789m,
            rowVersion = Convert.ToBase64String(device.RowVersion)
        });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), ppOld.PowerPointId.ToString(), device.DeviceId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_SelfMeasuredKwhExceedsFourDecimalPlaces_Returns400()
    {
        var (flat, ppOld, _, device, db) = await SeedFlatWithDeviceAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new
        {
            name = "Fridge", consumptionApproach = "SelfMeasured", selfMeasuredKwh = 1.56789m, selfMeasuredPeriod = "Daily",
            rowVersion = Convert.ToBase64String(device.RowVersion)
        });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), ppOld.PowerPointId.ToString(), device.DeviceId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_EuAnnualKwhWithTrailingZerosBeyondFourDecimals_Succeeds()
    {
        var (flat, ppOld, _, device, db) = await SeedFlatWithDeviceAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new
        {
            name = "Fridge", consumptionApproach = "EuLabel", euLabelClass = "A", euAnnualKwh = 123.500000m,
            rowVersion = Convert.ToBase64String(device.RowVersion)
        });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), ppOld.PowerPointId.ToString(), device.DeviceId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RunAsync_SelfMeasuredKwhWithTrailingZerosBeyondFourDecimals_Succeeds()
    {
        var (flat, ppOld, _, device, db) = await SeedFlatWithDeviceAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new
        {
            name = "Fridge", consumptionApproach = "SelfMeasured", selfMeasuredKwh = 1.500000m, selfMeasuredPeriod = "Daily",
            rowVersion = Convert.ToBase64String(device.RowVersion)
        });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), ppOld.PowerPointId.ToString(), device.DeviceId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RunAsync_ConsumptionApproachOutOfEnumRange_Returns400()
    {
        var (flat, ppOld, _, device, db) = await SeedFlatWithDeviceAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new
        {
            name = "Device", consumptionApproach = 999, rowVersion = Convert.ToBase64String(device.RowVersion)
        });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), ppOld.PowerPointId.ToString(), device.DeviceId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_EuLabelApproachWithKwhButNoClass_Returns200()
    {
        var (flat, ppOld, _, device, db) = await SeedFlatWithDeviceAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new
        {
            name = "Device", consumptionApproach = "EuLabel", euAnnualKwh = 150m,
            rowVersion = Convert.ToBase64String(device.RowVersion)
        });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), ppOld.PowerPointId.ToString(), device.DeviceId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<DeviceResponse>();
        response.EuLabelClass.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_EuLabelApproachWithClassButNoKwh_Returns400()
    {
        var (flat, ppOld, _, device, db) = await SeedFlatWithDeviceAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new
        {
            name = "Device", consumptionApproach = "EuLabel", euLabelClass = "A+++",
            rowVersion = Convert.ToBase64String(device.RowVersion)
        });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), ppOld.PowerPointId.ToString(), device.DeviceId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_NegativeEuAnnualKwh_Returns400()
    {
        var (flat, ppOld, _, device, db) = await SeedFlatWithDeviceAsync();
        var fn = MakeFunction(db);
        var req = MakeRequest(new
        {
            name = "Device", consumptionApproach = "EuLabel", euLabelClass = "A", euAnnualKwh = -5m,
            rowVersion = Convert.ToBase64String(device.RowVersion)
        });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(
            req, flat.FlatId.ToString(), ppOld.PowerPointId.ToString(), device.DeviceId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }
}
