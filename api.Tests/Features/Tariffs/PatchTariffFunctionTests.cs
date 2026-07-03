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

public class PatchTariffFunctionTests
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
        AppDbContext db, Guid flatId,
        decimal pricePerKwh = 0.30m, decimal monthlyBaseFee = 10m,
        string? providerName = null, DateTimeOffset? contractStartDate = null,
        int? contractDurationMonths = null)
    {
        var tariff = new Tariff
        {
            TariffId = Guid.NewGuid(),
            FlatId = flatId,
            EffectiveDate = DateTimeOffset.UtcNow,
            PricePerKwh = pricePerKwh,
            MonthlyBaseFee = monthlyBaseFee,
            ProviderName = providerName,
            ContractStartDate = contractStartDate,
            ContractDurationMonths = contractDurationMonths
        };
        db.Tariffs.Add(tariff);
        await db.SaveChangesAsync();
        return tariff;
    }

    private static Tariff LockedTariff(Guid flatId) => new()
    {
        TariffId = Guid.NewGuid(),
        FlatId = flatId,
        EffectiveDate = DateTimeOffset.UtcNow,
        PricePerKwh = 0.30m,
        MonthlyBaseFee = 10m,
        ContractStartDate = DateTimeOffset.UtcNow.AddMonths(-3),
        ContractDurationMonths = 12
    };

    private static HttpRequest MakeRequest(object body)
    {
        var json = JsonSerializer.Serialize(body);
        var ctx = new DefaultHttpContext();
        ctx.Request.ContentType = "application/json";
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return ctx.Request;
    }

    [Fact]
    public async Task RunAsync_LockedTariff_PriceOnlyPatchNoOverride_Returns422AndPriceUnchanged()
    {
        var (flat, db) = await SeedFlatAsync();
        var tariff = LockedTariff(flat.FlatId);
        db.Tariffs.Add(tariff);
        await db.SaveChangesAsync();
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { pricePerKwh = 0.5m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), tariff.TariffId.ToString(), ctx, CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(422);
        var persisted = await db.Tariffs.SingleAsync(t => t.TariffId == tariff.TariffId);
        persisted.PricePerKwh.ShouldBe(0.30m);
    }

    [Fact]
    public async Task RunAsync_LockedTariff_NonPriceOnlyPatch_Returns200AndApplied()
    {
        var (flat, db) = await SeedFlatAsync();
        var tariff = LockedTariff(flat.FlatId);
        db.Tariffs.Add(tariff);
        await db.SaveChangesAsync();
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { providerName = "NewCo" });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), tariff.TariffId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<TariffResponse>();
        response.ProviderName.ShouldBe("NewCo");
        var persisted = await db.Tariffs.SingleAsync(t => t.TariffId == tariff.TariffId);
        persisted.ProviderName.ShouldBe("NewCo");
    }

    [Fact]
    public async Task RunAsync_LockedTariff_MixedPatchNoOverride_Returns422ButPersistsNonPriceField()
    {
        var (flat, db) = await SeedFlatAsync();
        var tariff = LockedTariff(flat.FlatId);
        db.Tariffs.Add(tariff);
        await db.SaveChangesAsync();
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { pricePerKwh = 0.5m, providerName = "NewCo" });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), tariff.TariffId.ToString(), ctx, CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(422);
        var persisted = await db.Tariffs.SingleAsync(t => t.TariffId == tariff.TariffId);
        persisted.PricePerKwh.ShouldBe(0.30m);
        persisted.ProviderName.ShouldBe("NewCo");
    }

    [Fact]
    public async Task RunAsync_LockedTariff_PricePatchWithLockOverride_Returns200AndPriceUpdated()
    {
        var (flat, db) = await SeedFlatAsync();
        var tariff = LockedTariff(flat.FlatId);
        db.Tariffs.Add(tariff);
        await db.SaveChangesAsync();
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { pricePerKwh = 0.5m, lockOverride = true });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), tariff.TariffId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<TariffResponse>();
        response.PricePerKwh.ShouldBe(0.5m);
        var persisted = await db.Tariffs.SingleAsync(t => t.TariffId == tariff.TariffId);
        persisted.PricePerKwh.ShouldBe(0.5m);
    }

    [Theory]
    [MemberData(nameof(PriceAndContractTermCombinations))]
    public async Task RunAsync_PriceFieldAndContractTermFieldInSameRequest_Returns400(
        decimal? pricePerKwh, decimal? monthlyBaseFee,
        DateTimeOffset? contractStartDate, int? contractDurationMonths)
    {
        var (flat, db) = await SeedFlatAsync();
        var tariff = await SeedTariffAsync(db, flat.FlatId);
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new
        {
            pricePerKwh,
            monthlyBaseFee,
            contractStartDate = contractStartDate ?? DateTimeOffset.UtcNow.AddMonths(1),
            contractDurationMonths
        });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), tariff.TariffId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        var persisted = await db.Tariffs.SingleAsync(t => t.TariffId == tariff.TariffId);
        persisted.PricePerKwh.ShouldBe(0.30m);
        persisted.ContractStartDate.ShouldBeNull();
    }

    public static IEnumerable<object?[]> PriceAndContractTermCombinations()
    {
        yield return [0.5m, null, null, null];
        yield return [null, 15.0m, null, null];
        yield return [0.5m, null, null, 12];
    }

    [Theory]
    [MemberData(nameof(NonLockedTariffCases))]
    public async Task RunAsync_NonLockedTariff_PricePatchNoOverride_Returns200(
        DateTimeOffset? contractStartDate, int? contractDurationMonths)
    {
        var (flat, db) = await SeedFlatAsync();
        var tariff = await SeedTariffAsync(db, flat.FlatId,
            contractStartDate: contractStartDate, contractDurationMonths: contractDurationMonths);
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { pricePerKwh = 0.5m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), tariff.TariffId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<TariffResponse>();
        response.PricePerKwh.ShouldBe(0.5m);
    }

    public static IEnumerable<object?[]> NonLockedTariffCases()
    {
        yield return [null, null];
        yield return [DateTimeOffset.UtcNow.AddMonths(1), 12];
        yield return [DateTimeOffset.UtcNow.AddMonths(-1), null];
    }

    [Fact]
    public async Task RunAsync_PascalCasePropertyNames_AreMatchedCaseInsensitively()
    {
        var (flat, db) = await SeedFlatAsync();
        var tariff = await SeedTariffAsync(db, flat.FlatId);
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { ProviderName = "PascalCo" });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), tariff.TariffId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<TariffResponse>();
        response.ProviderName.ShouldBe("PascalCo");
    }

    [Fact]
    public async Task RunAsync_LockOverrideWrongType_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var tariff = await SeedTariffAsync(db, flat.FlatId);
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { pricePerKwh = 0.5m, lockOverride = "true" });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), tariff.TariffId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_TariffNotFound_Returns404()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { pricePerKwh = 0.5m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), Guid.NewGuid().ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RunAsync_TariffBelongsToDifferentFlat_Returns404()
    {
        var (flatA, db) = await SeedFlatAsync();
        var flatB = new Flat
        {
            FlatId = Guid.NewGuid(),
            UserId = flatA.UserId,
            Name = "Second Flat",
            AnnualKwhBaseline = 3000m,
            SpikeThreshold = 2.0m
        };
        db.Flats.Add(flatB);
        await db.SaveChangesAsync();
        var tariff = await SeedTariffAsync(db, flatA.FlatId);
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { pricePerKwh = 0.5m });
        var ctx = MakeFunctionContext(userId: flatA.UserId);

        var result = await fn.RunAsync(req, flatB.FlatId.ToString(), tariff.TariffId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RunAsync_FlatNotOwnedByUser_Returns403()
    {
        var (flat, db) = await SeedFlatAsync(userId: "owner");
        var tariff = await SeedTariffAsync(db, flat.FlatId);
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { pricePerKwh = 0.5m });
        var ctx = MakeFunctionContext(userId: "intruder");

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), tariff.TariffId.ToString(), ctx, CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public async Task RunAsync_PricePerKwhOutOfBounds_Returns400(decimal price)
    {
        var (flat, db) = await SeedFlatAsync();
        var tariff = await SeedTariffAsync(db, flat.FlatId);
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { pricePerKwh = price });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), tariff.TariffId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_ExplicitNullProviderName_ClearsProviderName()
    {
        var (flat, db) = await SeedFlatAsync();
        var tariff = await SeedTariffAsync(db, flat.FlatId, providerName: "OldCo");
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { providerName = (string?)null });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), tariff.TariffId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
        var persisted = await db.Tariffs.SingleAsync(t => t.TariffId == tariff.TariffId);
        persisted.ProviderName.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_ExplicitNullContractStartDateAndDuration_ClearsContractTerms()
    {
        var (flat, db) = await SeedFlatAsync();
        var tariff = await SeedTariffAsync(db, flat.FlatId,
            contractStartDate: DateTimeOffset.UtcNow.AddMonths(-1), contractDurationMonths: 12);
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { contractStartDate = (DateTimeOffset?)null, contractDurationMonths = (int?)null });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), tariff.TariffId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
        var persisted = await db.Tariffs.SingleAsync(t => t.TariffId == tariff.TariffId);
        persisted.ContractStartDate.ShouldBeNull();
        persisted.ContractDurationMonths.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_ProviderNameNotInBody_LeavesExistingProviderNameUnchanged()
    {
        var (flat, db) = await SeedFlatAsync();
        var tariff = await SeedTariffAsync(db, flat.FlatId, providerName: "OldCo");
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { pricePerKwh = 0.5m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), tariff.TariffId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
        var persisted = await db.Tariffs.SingleAsync(t => t.TariffId == tariff.TariffId);
        persisted.ProviderName.ShouldBe("OldCo");
    }

    [Fact]
    public async Task RunAsync_InvalidTariffIdGuid_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { pricePerKwh = 0.5m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), "not-a-guid", ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }
}
