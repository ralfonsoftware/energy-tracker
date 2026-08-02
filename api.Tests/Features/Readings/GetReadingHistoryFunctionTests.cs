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

    private static HttpRequest MakeGetRequest(string? skip = null, string? take = null)
    {
        var ctx = new DefaultHttpContext();
        if (skip is not null || take is not null)
            ctx.Request.QueryString = new QueryString($"?skip={skip}&take={take}");
        return ctx.Request;
    }

    private static void SeedReadings(AppDbContext db, Flat flat, int count)
    {
        for (var i = 0; i < count; i++)
        {
            db.MeterReadings.Add(new MeterReading
            {
                ReadingId = Guid.NewGuid(),
                FlatId = flat.FlatId,
                KwhValue = 100m + i,
                ReadingDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(i)
            });
        }
        db.SaveChanges();
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
        var response = ok.Value.ShouldBeOfType<ReadingHistoryResponse>();
        response.Items.Count.ShouldBe(3);
        response.TotalCount.ShouldBe(3);
        response.Items[0].ReadingDate.ShouldBe(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
        response.Items[1].ReadingDate.ShouldBe(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
        response.Items[2].ReadingDate.ShouldBe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
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
        var response = ok.Value.ShouldBeOfType<ReadingHistoryResponse>();
        response.Items.Single().IsCorrected.ShouldBeTrue();
        response.Items.Single().OriginalKwhValue.ShouldBe(100m);
    }

    [Fact]
    public async Task RunAsync_NoReadings_ReturnsEmptyArray()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new GetReadingHistoryFunction(db);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeGetRequest(), flat.FlatId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<ReadingHistoryResponse>();
        response.Items.ShouldBeEmpty();
        response.TotalCount.ShouldBe(0);
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

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        var type = (string)badRequest.Value!.GetType().GetProperty("type")!.GetValue(badRequest.Value)!;
        type.ShouldBe("https://tools.ietf.org/html/rfc7231#section-6.5.1");
    }

    [Fact]
    public async Task RunAsync_DefaultPaging_ReturnsFirstTwentyAndTotalCount()
    {
        var (flat, db) = await SeedFlatAsync();
        SeedReadings(db, flat, 25);
        var fn = new GetReadingHistoryFunction(db);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeGetRequest(), flat.FlatId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<ReadingHistoryResponse>();
        response.Items.Count.ShouldBe(20);
        response.TotalCount.ShouldBe(25);
        response.Items[0].ReadingDate.ShouldBe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(24));
    }

    [Fact]
    public async Task RunAsync_SecondPageViaSkip_ReturnsNextSlice()
    {
        var (flat, db) = await SeedFlatAsync();
        SeedReadings(db, flat, 25);
        var fn = new GetReadingHistoryFunction(db);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeGetRequest(skip: "20"), flat.FlatId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<ReadingHistoryResponse>();
        response.Items.Count.ShouldBe(5);
        response.TotalCount.ShouldBe(25);
        response.Items[0].ReadingDate.ShouldBe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(4));
        response.Items[4].ReadingDate.ShouldBe(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task RunAsync_NegativeSkip_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new GetReadingHistoryFunction(db);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeGetRequest(skip: "-1"), flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_NonNumericSkip_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new GetReadingHistoryFunction(db);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeGetRequest(skip: "abc"), flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_NegativeTake_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new GetReadingHistoryFunction(db);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeGetRequest(take: "-5"), flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_TakeExceedsMax_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new GetReadingHistoryFunction(db);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeGetRequest(take: "101"), flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }
}
