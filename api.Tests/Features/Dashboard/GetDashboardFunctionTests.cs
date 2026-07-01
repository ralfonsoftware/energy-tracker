using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Dashboard;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace api.Tests.Features.Dashboard;

public class GetDashboardFunctionTests
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
            AnnualKwhBaseline = 3650m,
            SpikeThreshold = 2.0m
        };
        db.Flats.Add(flat);
        await db.SaveChangesAsync();
        return (flat, db);
    }

    private static HttpRequest MakeGetRequest()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = "GET";
        return ctx.Request;
    }

    [Fact]
    public async Task RunAsync_ValidFlatNoReadings_Returns200WithZeroSummary()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new GetDashboardFunction(db, new KpiCalculator());
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeGetRequest(), flat.FlatId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        ok.StatusCode.ShouldBe(200);
        var summary = ok.Value.ShouldBeOfType<DashboardSummary>();
        summary.DailyAvgKwh.ShouldBe(0m);
        summary.LastReadingDate.ShouldBeNull();
        summary.Cost.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_InvalidFlatIdGuid_Returns400()
    {
        var db = MakeDb();
        var fn = new GetDashboardFunction(db, new KpiCalculator());
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeGetRequest(), "not-a-guid", ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_FlatNotOwnedByUser_Returns403()
    {
        var (flat, db) = await SeedFlatAsync(userId: "owner");
        var fn = new GetDashboardFunction(db, new KpiCalculator());
        var ctx = MakeFunctionContext(userId: "intruder");

        var result = await fn.RunAsync(MakeGetRequest(), flat.FlatId.ToString(), ctx, CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task RunAsync_ValidFlatWithReadings_Returns200WithComputedSummary()
    {
        var (flat, db) = await SeedFlatAsync();

        var date1 = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var date2 = date1.AddDays(10);
        db.MeterReadings.Add(new MeterReading
        {
            ReadingId = Guid.NewGuid(), FlatId = flat.FlatId,
            KwhValue = 100m, ReadingDate = date1, IsCorrected = false
        });
        db.MeterReadings.Add(new MeterReading
        {
            ReadingId = Guid.NewGuid(), FlatId = flat.FlatId,
            KwhValue = 150m, ReadingDate = date2, IsCorrected = false
        });
        db.Tariffs.Add(new Tariff
        {
            TariffId = Guid.NewGuid(), FlatId = flat.FlatId,
            EffectiveDate = date1.AddDays(-1), PricePerKwh = 0.30m, MonthlyBaseFee = 10m
        });
        await db.SaveChangesAsync();

        var fn = new GetDashboardFunction(db, new KpiCalculator());
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeGetRequest(), flat.FlatId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        ok.StatusCode.ShouldBe(200);
        var summary = ok.Value.ShouldBeOfType<DashboardSummary>();
        summary.DailyAvgKwh.ShouldBeGreaterThan(0m);
        summary.LastReadingDate.ShouldNotBeNull();
        summary.Cost.ShouldNotBeNull();
        summary.Cost!.DailyAvgCost.ShouldBe(1.5m);
        summary.Cost!.HasCostGap.ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_FlatWithPartialTariffCoverage_HasCostGapIsTrue()
    {
        var (flat, db) = await SeedFlatAsync();

        var dateA = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var dateB = dateA.AddDays(10);
        var dateC = dateB.AddDays(10);
        db.MeterReadings.Add(new MeterReading
        {
            ReadingId = Guid.NewGuid(), FlatId = flat.FlatId,
            KwhValue = 100m, ReadingDate = dateA, IsCorrected = false
        });
        db.MeterReadings.Add(new MeterReading
        {
            ReadingId = Guid.NewGuid(), FlatId = flat.FlatId,
            KwhValue = 150m, ReadingDate = dateB, IsCorrected = false
        });
        db.MeterReadings.Add(new MeterReading
        {
            ReadingId = Guid.NewGuid(), FlatId = flat.FlatId,
            KwhValue = 200m, ReadingDate = dateC, IsCorrected = false
        });
        db.Tariffs.Add(new Tariff
        {
            TariffId = Guid.NewGuid(), FlatId = flat.FlatId,
            EffectiveDate = dateB, PricePerKwh = 0.30m, MonthlyBaseFee = 10m
        });
        await db.SaveChangesAsync();

        var fn = new GetDashboardFunction(db, new KpiCalculator());
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeGetRequest(), flat.FlatId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        ok.StatusCode.ShouldBe(200);
        var summary = ok.Value.ShouldBeOfType<DashboardSummary>();
        summary.Cost.ShouldNotBeNull();
        summary.Cost!.HasCostGap.ShouldBeTrue();
        summary.Cost!.CoveredDays.ShouldBeLessThan(summary.Cost!.TotalDays);
    }
}
