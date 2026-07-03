using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Tariffs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace api.Tests.Features.Tariffs;

public class GetTariffsFunctionTests
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
        decimal pricePerKwh = 0.30m, decimal monthlyBaseFee = 10m,
        string? providerName = null, int? contractDurationMonths = null)
    {
        var tariff = new Tariff
        {
            TariffId = Guid.NewGuid(),
            FlatId = flatId,
            ContractStartDate = contractStartDate,
            PricePerKwh = pricePerKwh,
            MonthlyBaseFee = monthlyBaseFee,
            ProviderName = providerName,
            ContractDurationMonths = contractDurationMonths
        };
        db.Tariffs.Add(tariff);
        await db.SaveChangesAsync();
        return tariff;
    }

    private static HttpRequest MakeRequest()
    {
        var ctx = new DefaultHttpContext();
        return ctx.Request;
    }

    [Fact]
    public async Task RunAsync_NoTariffs_Returns200WithEmptyArray()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new GetTariffsFunction(db);
        var req = MakeRequest();
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<List<TariffResponse>>();
        response.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_MultipleTariffs_ReturnsDescendingByContractStartDate()
    {
        var (flat, db) = await SeedFlatAsync();
        var oldest = await SeedTariffAsync(db, flat.FlatId, DateTimeOffset.UtcNow.AddYears(-2));
        var newest = await SeedTariffAsync(db, flat.FlatId, DateTimeOffset.UtcNow.AddYears(1));
        var middle = await SeedTariffAsync(db, flat.FlatId, DateTimeOffset.UtcNow.AddMonths(-6));
        var fn = new GetTariffsFunction(db);
        var req = MakeRequest();
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<List<TariffResponse>>();
        response.Count.ShouldBe(3);
        response[0].TariffId.ShouldBe(newest.TariffId);
        response[1].TariffId.ShouldBe(middle.TariffId);
        response[2].TariffId.ShouldBe(oldest.TariffId);
    }

    [Fact]
    public async Task RunAsync_PastContractStartWithDuration_IsLockedTrue()
    {
        var (flat, db) = await SeedFlatAsync();
        await SeedTariffAsync(db, flat.FlatId, DateTimeOffset.UtcNow.AddMonths(-1), contractDurationMonths: 12);
        var fn = new GetTariffsFunction(db);
        var req = MakeRequest();
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<List<TariffResponse>>();
        response.Single().IsLocked.ShouldBeTrue();
    }

    [Fact]
    public async Task RunAsync_FutureContractStart_IsLockedFalse()
    {
        var (flat, db) = await SeedFlatAsync();
        await SeedTariffAsync(db, flat.FlatId, DateTimeOffset.UtcNow.AddMonths(1), contractDurationMonths: 12);
        var fn = new GetTariffsFunction(db);
        var req = MakeRequest();
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<List<TariffResponse>>();
        response.Single().IsLocked.ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_PastContractStartNoDuration_IsLockedTrue()
    {
        var (flat, db) = await SeedFlatAsync();
        await SeedTariffAsync(db, flat.FlatId, DateTimeOffset.UtcNow.AddMonths(-1), contractDurationMonths: null);
        var fn = new GetTariffsFunction(db);
        var req = MakeRequest();
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<List<TariffResponse>>();
        response.Single().IsLocked.ShouldBeTrue();
    }

    [Fact]
    public async Task RunAsync_FlatNotOwnedByUser_Returns403()
    {
        var (flat, db) = await SeedFlatAsync(userId: "owner");
        var fn = new GetTariffsFunction(db);
        var req = MakeRequest();
        var ctx = MakeFunctionContext(userId: "intruder");

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task RunAsync_InvalidFlatIdGuid_Returns400()
    {
        var (_, db) = await SeedFlatAsync();
        var fn = new GetTariffsFunction(db);
        var req = MakeRequest();
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, "not-a-guid", ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }
}
