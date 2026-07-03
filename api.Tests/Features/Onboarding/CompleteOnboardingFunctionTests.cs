using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Onboarding;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using System.Text;
using System.Text.Json;

namespace api.Tests.Features.Onboarding;

public class CompleteOnboardingFunctionTests
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

    private static HttpRequest MakeRequest(object? body)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.ContentType = "application/json";
        if (body is string raw)
        {
            ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(raw));
        }
        else
        {
            var json = JsonSerializer.Serialize(body);
            ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        }
        return ctx.Request;
    }

    private static object ValidBody(
        decimal? plannedAnnualSpend = null,
        string? providerName = null,
        DateTimeOffset? contractStartDate = null,
        int? contractDurationMonths = null) => new
    {
        flatName = "My Flat",
        annualKwhBaseline = 3500m,
        plannedAnnualSpend,
        pricePerKwh = 0.30m,
        monthlyBaseFee = 10m,
        providerName,
        contractStartDate,
        contractDurationMonths
    };

    [Fact]
    public async Task RunAsync_ValidRequest_CreatesFlatAndTariffReturns201()
    {
        var db = MakeDb();
        db.Users.Add(new User { UserId = "user-test-123" });
        await db.SaveChangesAsync();
        var fn = new CompleteOnboardingFunction(db, new OnboardingValidator());
        var req = MakeRequest(ValidBody());
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        var flat = await db.Flats.SingleAsync(f => f.UserId == "user-test-123");
        var created = result.ShouldBeOfType<CreatedResult>();
        created.Location.ShouldBe($"/api/v1/flats/{flat.FlatId}");

        flat.Name.ShouldBe("My Flat");
        flat.AnnualKwhBaseline.ShouldBe(3500m);

        var tariff = await db.Tariffs.SingleAsync(t => t.FlatId == flat.FlatId);
        tariff.PricePerKwh.ShouldBe(0.30m);
        tariff.MonthlyBaseFee.ShouldBe(10m);
    }

    [Fact]
    public async Task RunAsync_ProviderNameAndContractDurationProvided_PersistsBothOnTariff()
    {
        var db = MakeDb();
        db.Users.Add(new User { UserId = "user-test-123" });
        await db.SaveChangesAsync();
        var fn = new CompleteOnboardingFunction(db, new OnboardingValidator());
        var req = MakeRequest(ValidBody(providerName: "Vattenfall", contractDurationMonths: 12));
        var ctx = MakeFunctionContext();

        await fn.RunAsync(req, ctx, CancellationToken.None);

        var tariff = await db.Tariffs.SingleAsync();
        tariff.ProviderName.ShouldBe("Vattenfall");
        tariff.ContractDurationMonths.ShouldBe(12);
    }

    [Fact]
    public async Task RunAsync_NoContractStartDateProvided_DefaultsTariffContractStartDateToUtcNow()
    {
        var db = MakeDb();
        db.Users.Add(new User { UserId = "user-test-123" });
        await db.SaveChangesAsync();
        var fn = new CompleteOnboardingFunction(db, new OnboardingValidator());
        var req = MakeRequest(ValidBody());
        var ctx = MakeFunctionContext();
        var before = DateTimeOffset.UtcNow;

        await fn.RunAsync(req, ctx, CancellationToken.None);

        var after = DateTimeOffset.UtcNow;
        var tariff = await db.Tariffs.SingleAsync();
        tariff.ContractStartDate.ShouldBeInRange(before, after);
    }

    [Fact]
    public async Task RunAsync_ContractStartDateProvided_UsesProvidedValueNotUtcNow()
    {
        var db = MakeDb();
        db.Users.Add(new User { UserId = "user-test-123" });
        await db.SaveChangesAsync();
        var fn = new CompleteOnboardingFunction(db, new OnboardingValidator());
        var providedDate = new DateTimeOffset(2024, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var req = MakeRequest(ValidBody(contractStartDate: providedDate));
        var ctx = MakeFunctionContext();

        await fn.RunAsync(req, ctx, CancellationToken.None);

        var tariff = await db.Tariffs.SingleAsync();
        tariff.ContractStartDate.ShouldBe(providedDate);
    }

    [Fact]
    public async Task RunAsync_UserAlreadyHasFlat_Returns409ConflictAndCreatesNothing()
    {
        var db = MakeDb();
        db.Users.Add(new User { UserId = "user-test-123" });
        db.Flats.Add(new Flat { UserId = "user-test-123", Name = "Existing", AnnualKwhBaseline = 1000m, SpikeThreshold = 2.0m });
        await db.SaveChangesAsync();
        var fn = new CompleteOnboardingFunction(db, new OnboardingValidator());
        var req = MakeRequest(ValidBody());
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        var conflict = result.ShouldBeOfType<ConflictObjectResult>();
        conflict.StatusCode.ShouldBe(409);
        (await db.Flats.CountAsync()).ShouldBe(1);
        (await db.Tariffs.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_MalformedJsonBody_Returns400BadRequest()
    {
        var db = MakeDb();
        var fn = new CompleteOnboardingFunction(db, new OnboardingValidator());
        var req = MakeRequest("{ not valid json");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        badRequest.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task RunAsync_EmptyFlatName_Returns400ValidationErrorAndCreatesNothing()
    {
        var db = MakeDb();
        db.Users.Add(new User { UserId = "user-test-123" });
        await db.SaveChangesAsync();
        var fn = new CompleteOnboardingFunction(db, new OnboardingValidator());
        var req = MakeRequest(new
        {
            flatName = "",
            annualKwhBaseline = 3500m,
            plannedAnnualSpend = (decimal?)null,
            pricePerKwh = 0.30m,
            monthlyBaseFee = 10m,
            providerName = (string?)null,
            contractStartDate = (DateTimeOffset?)null,
            contractDurationMonths = (int?)null
        });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        (await db.Flats.CountAsync()).ShouldBe(0);
        (await db.Tariffs.CountAsync()).ShouldBe(0);
    }
}
