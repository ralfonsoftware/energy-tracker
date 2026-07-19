using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Readings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace api.Tests.Features.Readings;

public class PatchReadingFunctionTests
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

    private static async Task<MeterReading> SeedReadingAsync(
        AppDbContext db, Guid flatId, decimal kwhValue, bool isCorrected = false, decimal? originalKwhValue = null)
    {
        var reading = new MeterReading
        {
            ReadingId = Guid.NewGuid(),
            FlatId = flatId,
            KwhValue = kwhValue,
            ReadingDate = DateTimeOffset.UtcNow,
            IsCorrected = isCorrected,
            OriginalKwhValue = originalKwhValue,
            RowVersion = TestRowVersion
        };
        db.MeterReadings.Add(reading);
        await db.SaveChangesAsync();
        return reading;
    }

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
    public async Task RunAsync_ValidCorrection_UpdatesKwhValueSetsIsCorrectedAndOriginalKwhValue()
    {
        var (flat, db) = await SeedFlatAsync();
        var reading = await SeedReadingAsync(db, flat.FlatId, 100m);
        var fn = new PatchReadingFunction(db, new PatchReadingValidator());
        var req = MakeRequest(new { kwhValue = 120m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), reading.ReadingId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<ReadingResponse>();
        response.KwhValue.ShouldBe(120m);
        response.IsCorrected.ShouldBeTrue();
        response.OriginalKwhValue.ShouldBe(100m);
    }

    [Fact]
    public async Task RunAsync_UnchangedKwhValue_DoesNotMutateOrMarkAsCorrected()
    {
        var (flat, db) = await SeedFlatAsync();
        var reading = await SeedReadingAsync(db, flat.FlatId, 100m);
        var fn = new PatchReadingFunction(db, new PatchReadingValidator());
        var req = MakeRequest(new { kwhValue = 100m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), reading.ReadingId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<ReadingResponse>();
        response.KwhValue.ShouldBe(100m);
        response.IsCorrected.ShouldBeFalse();
        response.OriginalKwhValue.ShouldBeNull();
        var persisted = await db.MeterReadings.SingleAsync(r => r.ReadingId == reading.ReadingId);
        persisted.IsCorrected.ShouldBeFalse();
        persisted.OriginalKwhValue.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_SecondCorrection_PreservesFirstOriginalKwhValue()
    {
        var (flat, db) = await SeedFlatAsync();
        var reading = await SeedReadingAsync(db, flat.FlatId, 120m, isCorrected: true, originalKwhValue: 100m);
        var fn = new PatchReadingFunction(db, new PatchReadingValidator());
        var req = MakeRequest(new { kwhValue = 125m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), reading.ReadingId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<ReadingResponse>();
        response.KwhValue.ShouldBe(125m);
        response.OriginalKwhValue.ShouldBe(100m);
    }

    [Fact]
    public async Task RunAsync_ValidCorrection_PersistsToDatabase()
    {
        var (flat, db) = await SeedFlatAsync();
        var reading = await SeedReadingAsync(db, flat.FlatId, 100m);
        var fn = new PatchReadingFunction(db, new PatchReadingValidator());
        var req = MakeRequest(new { kwhValue = 120m });
        var ctx = MakeFunctionContext();

        await fn.RunAsync(req, flat.FlatId.ToString(), reading.ReadingId.ToString(), ctx, CancellationToken.None);

        var persisted = await db.MeterReadings.SingleAsync(r => r.ReadingId == reading.ReadingId);
        persisted.KwhValue.ShouldBe(120m);
        persisted.IsCorrected.ShouldBeTrue();
        persisted.OriginalKwhValue.ShouldBe(100m);
    }

    [Fact]
    public async Task RunAsync_KwhValueZero_Returns400AndDoesNotMutate()
    {
        var (flat, db) = await SeedFlatAsync();
        var reading = await SeedReadingAsync(db, flat.FlatId, 100m);
        var fn = new PatchReadingFunction(db, new PatchReadingValidator());
        var req = MakeRequest(new { kwhValue = 0m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), reading.ReadingId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        var persisted = await db.MeterReadings.SingleAsync(r => r.ReadingId == reading.ReadingId);
        persisted.KwhValue.ShouldBe(100m);
        persisted.IsCorrected.ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_KwhValueExceedsFourDecimalPlaces_Returns400AndDoesNotMutate()
    {
        var (flat, db) = await SeedFlatAsync();
        var reading = await SeedReadingAsync(db, flat.FlatId, 100m);
        var fn = new PatchReadingFunction(db, new PatchReadingValidator());
        var req = MakeRequest(new { kwhValue = 120.56789m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), reading.ReadingId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        var persisted = await db.MeterReadings.SingleAsync(r => r.ReadingId == reading.ReadingId);
        persisted.KwhValue.ShouldBe(100m);
        persisted.IsCorrected.ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_KwhValueWithTrailingZerosBeyondFourDecimals_Succeeds()
    {
        var (flat, db) = await SeedFlatAsync();
        var reading = await SeedReadingAsync(db, flat.FlatId, 100m);
        var fn = new PatchReadingFunction(db, new PatchReadingValidator());
        var req = MakeRequest(new { kwhValue = 120.500000m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), reading.ReadingId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RunAsync_ReadingNotFound_Returns404()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new PatchReadingFunction(db, new PatchReadingValidator());
        var req = MakeRequest(new { kwhValue = 120m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), Guid.NewGuid().ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RunAsync_ReadingBelongsToDifferentFlat_Returns404()
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
        var reading = await SeedReadingAsync(db, flatA.FlatId, 100m);
        var fn = new PatchReadingFunction(db, new PatchReadingValidator());
        var req = MakeRequest(new { kwhValue = 120m });
        var ctx = MakeFunctionContext(userId: flatA.UserId);

        var result = await fn.RunAsync(req, flatB.FlatId.ToString(), reading.ReadingId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RunAsync_FlatNotOwnedByUser_Returns403()
    {
        var (flat, db) = await SeedFlatAsync(userId: "owner");
        var reading = await SeedReadingAsync(db, flat.FlatId, 100m);
        var fn = new PatchReadingFunction(db, new PatchReadingValidator());
        var req = MakeRequest(new { kwhValue = 120m });
        var ctx = MakeFunctionContext(userId: "intruder");

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), reading.ReadingId.ToString(), ctx, CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task RunAsync_InvalidReadingIdGuid_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new PatchReadingFunction(db, new PatchReadingValidator());
        var req = MakeRequest(new { kwhValue = 120m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), "not-a-guid", ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_MissingRowVersion_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var reading = await SeedReadingAsync(db, flat.FlatId, 100m);
        var fn = new PatchReadingFunction(db, new PatchReadingValidator());
        var req = MakeRequest(new { kwhValue = 120m }, rowVersion: null);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), reading.ReadingId.ToString(), ctx, CancellationToken.None);

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        badRequest.StatusCode.ShouldBe(400);
        var persisted = await db.MeterReadings.SingleAsync(r => r.ReadingId == reading.ReadingId);
        persisted.KwhValue.ShouldBe(100m);
    }

    [Fact]
    public async Task RunAsync_MalformedRowVersion_Returns400()
    {
        // byte[]-typed properties are base64-decoded by System.Text.Json during deserialization
        // itself, so this surfaces as a JsonException ("Invalid JSON in request body") rather
        // than this Function's own "rowVersion is required" check — still a 400.
        var (flat, db) = await SeedFlatAsync();
        var reading = await SeedReadingAsync(db, flat.FlatId, 100m);
        var fn = new PatchReadingFunction(db, new PatchReadingValidator());
        var req = MakeRequest(new { kwhValue = 120m }, rowVersion: "not-valid-base64!!");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), reading.ReadingId.ToString(), ctx, CancellationToken.None);

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        badRequest.StatusCode.ShouldBe(400);
        var persisted = await db.MeterReadings.SingleAsync(r => r.ReadingId == reading.ReadingId);
        persisted.KwhValue.ShouldBe(100m);
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
        var reading = new MeterReading
        {
            ReadingId = Guid.NewGuid(),
            FlatId = flat.FlatId,
            KwhValue = 100m,
            ReadingDate = DateTimeOffset.UtcNow,
            RowVersion = TestRowVersion
        };
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        using (var seedCtx = new AppDbContext(dbOptions))
        {
            seedCtx.Users.Add(new User { UserId = "user-test-123" });
            seedCtx.Flats.Add(flat);
            seedCtx.MeterReadings.Add(reading);
            await seedCtx.SaveChangesAsync();
        }

        var db = new ConcurrencyConflictDbContext(dbOptions);
        var fn = new PatchReadingFunction(db, new PatchReadingValidator());
        var req = MakeRequest(new { kwhValue = 120m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), reading.ReadingId.ToString(), ctx, CancellationToken.None);

        var conflict = result.ShouldBeOfType<ObjectResult>();
        conflict.StatusCode.ShouldBe(409);
        using var verifyCtx = new AppDbContext(dbOptions);
        var persisted = await verifyCtx.MeterReadings.SingleAsync(r => r.ReadingId == reading.ReadingId);
        persisted.KwhValue.ShouldBe(100m);
    }
}
