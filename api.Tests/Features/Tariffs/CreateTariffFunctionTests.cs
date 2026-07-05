using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Tariffs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using System.Text;
using System.Text.Json;

namespace api.Tests.Features.Tariffs;

public class CreateTariffFunctionTests
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

    private static async Task<Tariff> SeedTariffAsync(
        AppDbContext db, Guid flatId, DateTimeOffset contractStartDate,
        decimal pricePerKwh = 0.30m, decimal monthlyBaseFee = 10m)
    {
        var tariff = new Tariff
        {
            TariffId = Guid.NewGuid(),
            FlatId = flatId,
            ContractStartDate = contractStartDate,
            PricePerKwh = pricePerKwh,
            MonthlyBaseFee = monthlyBaseFee
        };
        db.Tariffs.Add(tariff);
        await db.SaveChangesAsync();
        return tariff;
    }

    private static HttpRequest MakeRequest(object body)
    {
        var json = JsonSerializer.Serialize(body);
        var ctx = new DefaultHttpContext();
        ctx.Request.ContentType = "application/json";
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return ctx.Request;
    }

    [Fact]
    public async Task RunAsync_ValidRequest_Returns201WithLocationAndPersistsDecimalValues()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new CreateTariffFunction(db, new TariffValidator());
        var contractStartDate = DateTimeOffset.UtcNow;
        var req = MakeRequest(new { contractStartDate, pricePerKwh = 0.345679m, monthlyBaseFee = 12.3456m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var created = result.ShouldBeOfType<CreatedResult>();
        var response = created.Value.ShouldBeOfType<TariffResponse>();
        created.Location.ShouldBe($"/api/v1/flats/{flat.FlatId}/tariffs/{response.TariffId}");
        response.PricePerKwh.ShouldBe(0.345679m);
        response.MonthlyBaseFee.ShouldBe(12.3456m);
        var persisted = await db.Tariffs.SingleAsync(t => t.TariffId == response.TariffId);
        persisted.PricePerKwh.ShouldBe(0.345679m);
        persisted.MonthlyBaseFee.ShouldBe(12.3456m);
    }

    [Fact]
    public async Task RunAsync_DuplicateContractStartDate_Returns409AndDoesNotCreate()
    {
        var (flat, db) = await SeedFlatAsync();
        var contractStartDate = DateTimeOffset.UtcNow;
        await SeedTariffAsync(db, flat.FlatId, contractStartDate);
        var fn = new CreateTariffFunction(db, new TariffValidator());
        var req = MakeRequest(new { contractStartDate, pricePerKwh = 0.35m, monthlyBaseFee = 10m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var conflict = result.ShouldBeOfType<ConflictObjectResult>();
        conflict.StatusCode.ShouldBe(409);
        (await db.Tariffs.CountAsync(t => t.FlatId == flat.FlatId)).ShouldBe(1);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(10, 10)]
    [InlineData(11, 10)]
    public async Task RunAsync_PricePerKwhOutOfBounds_Returns400(decimal price, decimal fee)
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new CreateTariffFunction(db, new TariffValidator());
        var req = MakeRequest(new { contractStartDate = DateTimeOffset.UtcNow, pricePerKwh = price, monthlyBaseFee = fee });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1000)]
    [InlineData(1001)]
    public async Task RunAsync_MonthlyBaseFeeOutOfBounds_Returns400(decimal fee)
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new CreateTariffFunction(db, new TariffValidator());
        var req = MakeRequest(new { contractStartDate = DateTimeOffset.UtcNow, pricePerKwh = 0.3m, monthlyBaseFee = fee });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_PricePerKwhExceedsSixDecimalPlaces_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new CreateTariffFunction(db, new TariffValidator());
        var req = MakeRequest(new { contractStartDate = DateTimeOffset.UtcNow, pricePerKwh = 0.1234567m, monthlyBaseFee = 10m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        (await db.Tariffs.CountAsync(t => t.FlatId == flat.FlatId)).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_PricePerKwhWithTrailingZerosBeyondSixDecimals_Succeeds()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new CreateTariffFunction(db, new TariffValidator());
        var req = MakeRequest(new { contractStartDate = DateTimeOffset.UtcNow, pricePerKwh = 0.350000m, monthlyBaseFee = 10m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<CreatedResult>();
    }

    [Fact]
    public async Task RunAsync_MonthlyBaseFeeExceedsFourDecimalPlaces_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new CreateTariffFunction(db, new TariffValidator());
        var req = MakeRequest(new { contractStartDate = DateTimeOffset.UtcNow, pricePerKwh = 0.3m, monthlyBaseFee = 10.56789m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        (await db.Tariffs.CountAsync(t => t.FlatId == flat.FlatId)).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_MonthlyBaseFeeWithTrailingZerosBeyondFourDecimals_Succeeds()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new CreateTariffFunction(db, new TariffValidator());
        var req = MakeRequest(new { contractStartDate = DateTimeOffset.UtcNow, pricePerKwh = 0.3m, monthlyBaseFee = 10.500000m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<CreatedResult>();
    }

    [Fact]
    public async Task RunAsync_MissingContractStartDate_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new CreateTariffFunction(db, new TariffValidator());
        var req = MakeRequest(new { pricePerKwh = 0.3m, monthlyBaseFee = 10m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_FutureContractStartDate_Accepted()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new CreateTariffFunction(db, new TariffValidator());
        var futureDate = DateTimeOffset.UtcNow.AddMonths(6);
        var req = MakeRequest(new { contractStartDate = futureDate, pricePerKwh = 0.3m, monthlyBaseFee = 10m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<CreatedResult>();
    }

    [Fact]
    public async Task RunAsync_FlatNotOwnedByUser_Returns403()
    {
        var (flat, db) = await SeedFlatAsync(userId: "owner");
        var fn = new CreateTariffFunction(db, new TariffValidator());
        var req = MakeRequest(new { contractStartDate = DateTimeOffset.UtcNow, pricePerKwh = 0.3m, monthlyBaseFee = 10m });
        var ctx = MakeFunctionContext(userId: "intruder");

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task RunAsync_InvalidFlatIdGuid_Returns400()
    {
        var (_, db) = await SeedFlatAsync();
        var fn = new CreateTariffFunction(db, new TariffValidator());
        var req = MakeRequest(new { contractStartDate = DateTimeOffset.UtcNow, pricePerKwh = 0.3m, monthlyBaseFee = 10m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, "not-a-guid", ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }
}
