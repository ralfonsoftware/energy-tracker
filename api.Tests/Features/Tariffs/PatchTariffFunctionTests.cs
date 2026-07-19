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
using System.Text.Json.Nodes;

namespace api.Tests.Features.Tariffs;

public class PatchTariffFunctionTests
{
    private const string TestRowVersionBase64 = "AQID";
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
            ContractStartDate = contractStartDate ?? DateTimeOffset.UtcNow,
            PricePerKwh = pricePerKwh,
            MonthlyBaseFee = monthlyBaseFee,
            ProviderName = providerName,
            ContractDurationMonths = contractDurationMonths,
            RowVersion = TestRowVersion
        };
        db.Tariffs.Add(tariff);
        await db.SaveChangesAsync();
        return tariff;
    }

    private static Tariff LockedTariff(Guid flatId) => new()
    {
        TariffId = Guid.NewGuid(),
        FlatId = flatId,
        PricePerKwh = 0.30m,
        MonthlyBaseFee = 10m,
        ContractStartDate = DateTimeOffset.UtcNow.AddMonths(-3),
        ContractDurationMonths = 12,
        RowVersion = TestRowVersion
    };

    private static HttpRequest MakeRequest(object body, string? rowVersion = TestRowVersionBase64)
    {
        var node = JsonSerializer.SerializeToNode(body)!.AsObject();
        if (rowVersion is not null) node["rowVersion"] = rowVersion;
        var json = node.ToJsonString();
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
    public async Task RunAsync_LockedTariff_MixedPatchNoOverride_Returns422AndPersistsNothing()
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
        persisted.ProviderName.ShouldBeNull();
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

    [Fact]
    public async Task RunAsync_NonLockedTariff_PricePatchNoOverride_Returns200()
    {
        var (flat, db) = await SeedFlatAsync();
        var tariff = await SeedTariffAsync(db, flat.FlatId,
            contractStartDate: DateTimeOffset.UtcNow.AddMonths(1), contractDurationMonths: 12);
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { pricePerKwh = 0.5m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), tariff.TariffId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<TariffResponse>();
        response.PricePerKwh.ShouldBe(0.5m);
    }

    [Fact]
    public async Task RunAsync_PastContractStartNoDuration_PricePatchNoOverride_Returns422()
    {
        // Regression case for this story: a past ContractStartDate with no ContractDurationMonths
        // is now locked (TariffLockPolicy.IsLocked no longer depends on ContractDurationMonths).
        var (flat, db) = await SeedFlatAsync();
        var tariff = await SeedTariffAsync(db, flat.FlatId,
            contractStartDate: DateTimeOffset.UtcNow.AddMonths(-1), contractDurationMonths: null);
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
    public async Task RunAsync_PricePerKwhExceedsSixDecimalPlaces_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var tariff = await SeedTariffAsync(db, flat.FlatId);
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { pricePerKwh = 0.1234567m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), tariff.TariffId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        var persisted = await db.Tariffs.SingleAsync(t => t.TariffId == tariff.TariffId);
        persisted.PricePerKwh.ShouldBe(tariff.PricePerKwh);
    }

    [Fact]
    public async Task RunAsync_PricePerKwhWithTrailingZerosBeyondSixDecimals_Succeeds()
    {
        var (flat, db) = await SeedFlatAsync();
        var tariff = await SeedTariffAsync(db, flat.FlatId);
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { pricePerKwh = 0.350000m, lockOverride = true });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), tariff.TariffId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RunAsync_MonthlyBaseFeeExceedsFourDecimalPlaces_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var tariff = await SeedTariffAsync(db, flat.FlatId);
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { monthlyBaseFee = 10.56789m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), tariff.TariffId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        var persisted = await db.Tariffs.SingleAsync(t => t.TariffId == tariff.TariffId);
        persisted.MonthlyBaseFee.ShouldBe(tariff.MonthlyBaseFee);
    }

    [Fact]
    public async Task RunAsync_MonthlyBaseFeeWithTrailingZerosBeyondFourDecimals_Succeeds()
    {
        var (flat, db) = await SeedFlatAsync();
        var tariff = await SeedTariffAsync(db, flat.FlatId);
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { monthlyBaseFee = 10.500000m, lockOverride = true });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), tariff.TariffId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
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
    public async Task RunAsync_ExplicitNullContractDurationMonths_ClearsContractDurationMonths()
    {
        var (flat, db) = await SeedFlatAsync();
        var tariff = await SeedTariffAsync(db, flat.FlatId, contractDurationMonths: 12);
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { contractDurationMonths = (int?)null });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), tariff.TariffId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
        var persisted = await db.Tariffs.SingleAsync(t => t.TariffId == tariff.TariffId);
        persisted.ContractDurationMonths.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_ProviderNameNotInBody_LeavesExistingProviderNameUnchanged()
    {
        var (flat, db) = await SeedFlatAsync();
        var tariff = await SeedTariffAsync(db, flat.FlatId, providerName: "OldCo",
            contractStartDate: DateTimeOffset.UtcNow.AddMonths(1));
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

    [Fact]
    public async Task RunAsync_MissingRowVersion_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var tariff = await SeedTariffAsync(db, flat.FlatId);
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { pricePerKwh = 0.5m }, rowVersion: null);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), tariff.TariffId.ToString(), ctx, CancellationToken.None);

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        badRequest.StatusCode.ShouldBe(400);
        var persisted = await db.Tariffs.SingleAsync(t => t.TariffId == tariff.TariffId);
        persisted.PricePerKwh.ShouldBe(0.30m);
    }

    [Fact]
    public async Task RunAsync_MalformedRowVersion_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var tariff = await SeedTariffAsync(db, flat.FlatId);
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { pricePerKwh = 0.5m }, rowVersion: "not-valid-base64!!");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), tariff.TariffId.ToString(), ctx, CancellationToken.None);

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        badRequest.StatusCode.ShouldBe(400);
        var persisted = await db.Tariffs.SingleAsync(t => t.TariffId == tariff.TariffId);
        persisted.PricePerKwh.ShouldBe(0.30m);
    }

    [Fact]
    public async Task RunAsync_ConcurrentModification_Returns409Conflict()
    {
        var flat = new Flat
        {
            FlatId = Guid.NewGuid(),
            UserId = "user-test-123",
            Name = "Test Flat",
            AnnualKwhBaseline = 3500m,
            SpikeThreshold = 2.0m
        };
        var tariff = new Tariff
        {
            TariffId = Guid.NewGuid(),
            FlatId = flat.FlatId,
            ContractStartDate = DateTimeOffset.UtcNow.AddMonths(1),
            PricePerKwh = 0.30m,
            MonthlyBaseFee = 10m,
            RowVersion = TestRowVersion
        };
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using (var seedCtx = new AppDbContext(dbOptions))
        {
            seedCtx.Users.Add(new User { UserId = "user-test-123" });
            seedCtx.Flats.Add(flat);
            seedCtx.Tariffs.Add(tariff);
            await seedCtx.SaveChangesAsync();
        }

        var db = new ConcurrencyConflictDbContext(dbOptions);
        var fn = new PatchTariffFunction(db, new PatchTariffValidator());
        var req = MakeRequest(new { pricePerKwh = 0.5m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), tariff.TariffId.ToString(), ctx, CancellationToken.None);

        var conflict = result.ShouldBeOfType<ObjectResult>();
        conflict.StatusCode.ShouldBe(409);
        using var verifyCtx = new AppDbContext(dbOptions);
        var persisted = await verifyCtx.Tariffs.SingleAsync(t => t.TariffId == tariff.TariffId);
        persisted.PricePerKwh.ShouldBe(0.30m);
    }
}
