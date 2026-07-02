using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Readings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace api.Tests.Features.Readings;

public class GetReadingHistoryFunctionTests
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
            AnnualKwhBaseline = 3500m,
            SpikeThreshold = 2.0m
        };
        db.Flats.Add(flat);
        await db.SaveChangesAsync();
        return (flat, db);
    }

    private static HttpRequest MakeGetRequest()
    {
        var ctx = new DefaultHttpContext();
        return ctx.Request;
    }

    [Fact]
    public async Task RunAsync_MultipleReadings_ReturnsReverseChronologicalOrder()
    {
        var (flat, db) = await SeedFlatAsync();
        db.MeterReadings.AddRange(
            new MeterReading { ReadingId = Guid.NewGuid(), FlatId = flat.FlatId, KwhValue = 100m, ReadingDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) },
            new MeterReading { ReadingId = Guid.NewGuid(), FlatId = flat.FlatId, KwhValue = 200m, ReadingDate = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero) },
            new MeterReading { ReadingId = Guid.NewGuid(), FlatId = flat.FlatId, KwhValue = 150m, ReadingDate = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero) });
        await db.SaveChangesAsync();
        var fn = new GetReadingHistoryFunction(db);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeGetRequest(), flat.FlatId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var readings = ok.Value.ShouldBeAssignableTo<List<ReadingResponse>>()!;
        readings.Count.ShouldBe(3);
        readings[0].ReadingDate.ShouldBe(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
        readings[1].ReadingDate.ShouldBe(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
        readings[2].ReadingDate.ShouldBe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task RunAsync_ReadingWithCorrection_IncludesIsCorrectedAndOriginalKwhValue()
    {
        var (flat, db) = await SeedFlatAsync();
        db.MeterReadings.Add(new MeterReading
        {
            ReadingId = Guid.NewGuid(),
            FlatId = flat.FlatId,
            KwhValue = 120m,
            ReadingDate = DateTimeOffset.UtcNow,
            IsCorrected = true,
            OriginalKwhValue = 100m
        });
        await db.SaveChangesAsync();
        var fn = new GetReadingHistoryFunction(db);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeGetRequest(), flat.FlatId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var readings = ok.Value.ShouldBeAssignableTo<List<ReadingResponse>>()!;
        readings.Single().IsCorrected.ShouldBeTrue();
        readings.Single().OriginalKwhValue.ShouldBe(100m);
    }

    [Fact]
    public async Task RunAsync_NoReadings_ReturnsEmptyArray()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new GetReadingHistoryFunction(db);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeGetRequest(), flat.FlatId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var readings = ok.Value.ShouldBeAssignableTo<List<ReadingResponse>>()!;
        readings.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_FlatNotOwnedByUser_Returns403()
    {
        var (flat, db) = await SeedFlatAsync(userId: "owner");
        var fn = new GetReadingHistoryFunction(db);
        var ctx = MakeFunctionContext(userId: "intruder");

        var result = await fn.RunAsync(MakeGetRequest(), flat.FlatId.ToString(), ctx, CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task RunAsync_InvalidFlatIdGuid_Returns400()
    {
        var db = MakeDb();
        var fn = new GetReadingHistoryFunction(db);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeGetRequest(), "not-a-guid", ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }
}
