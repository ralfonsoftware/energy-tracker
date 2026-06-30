using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Readings;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using System.Text;
using System.Text.Json;

namespace api.Tests.Features.Readings;

public class SubmitReadingTests
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

    private static HttpRequest MakeRequest(object body)
    {
        var json = JsonSerializer.Serialize(body);
        var ctx = new DefaultHttpContext();
        ctx.Request.ContentType = "application/json";
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return ctx.Request;
    }

    [Fact]
    public async Task RunAsync_ValidReading_Returns201WithReadingResponse()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new SubmitReadingFunction(db, new ReadingValidator());
        var readingDate = new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero);
        var req = MakeRequest(new { kwhValue = 123.45m, readingDate });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var created = result.ShouldBeOfType<CreatedResult>();
        created.StatusCode.ShouldBe(201);
        var response = created.Value.ShouldBeOfType<ReadingResponse>();
        response.KwhValue.ShouldBe(123.45m);
        response.ReadingDate.ShouldBe(readingDate);
        response.IsCorrected.ShouldBeFalse();
        response.OriginalKwhValue.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_ValidReading_PersistsToDatabase()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new SubmitReadingFunction(db, new ReadingValidator());
        var readingDate = new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero);
        var req = MakeRequest(new { kwhValue = 250.0m, readingDate });
        var ctx = MakeFunctionContext();

        await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var readings = await db.MeterReadings.ToListAsync();
        readings.Count.ShouldBe(1);
        readings[0].KwhValue.ShouldBe(250.0m);
        readings[0].FlatId.ShouldBe(flat.FlatId);
        readings[0].IsCorrected.ShouldBeFalse();
        readings[0].OriginalKwhValue.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_ValidReading_SetsLocationHeader()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new SubmitReadingFunction(db, new ReadingValidator());
        var readingDate = new DateTimeOffset(2026, 6, 29, 8, 0, 0, TimeSpan.Zero);
        var req = MakeRequest(new { kwhValue = 99.9m, readingDate });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var created = result.ShouldBeOfType<CreatedResult>();
        var response = created.Value.ShouldBeOfType<ReadingResponse>();
        created.Location.ShouldBe($"/api/v1/flats/{flat.FlatId}/readings/{response.ReadingId}");
    }

    [Fact]
    public async Task RunAsync_KwhValueZero_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new SubmitReadingFunction(db, new ReadingValidator());
        var req = MakeRequest(new { kwhValue = 0m, readingDate = DateTimeOffset.UtcNow });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        var readings = await db.MeterReadings.CountAsync();
        readings.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_KwhValueNegative_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new SubmitReadingFunction(db, new ReadingValidator());
        var req = MakeRequest(new { kwhValue = -1m, readingDate = DateTimeOffset.UtcNow });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        var readings = await db.MeterReadings.CountAsync();
        readings.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_MissingReadingDate_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new SubmitReadingFunction(db, new ReadingValidator());
        var req = MakeRequest(new { kwhValue = 50.3m });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        var readings = await db.MeterReadings.CountAsync();
        readings.ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_FlatNotOwnedByUser_Returns403()
    {
        var (flat, db) = await SeedFlatAsync(userId: "owner");
        var fn = new SubmitReadingFunction(db, new ReadingValidator());
        var req = MakeRequest(new { kwhValue = 100m, readingDate = DateTimeOffset.UtcNow });
        var ctx = MakeFunctionContext(userId: "intruder");

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task RunAsync_InvalidFlatIdGuid_Returns400()
    {
        var db = MakeDb();
        var fn = new SubmitReadingFunction(db, new ReadingValidator());
        var req = MakeRequest(new { kwhValue = 100m, readingDate = DateTimeOffset.UtcNow });
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, "not-a-guid", ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_RetroactiveReading_StoresProvidedDate()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new SubmitReadingFunction(db, new ReadingValidator());
        var pastDate = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.FromHours(1));
        var req = MakeRequest(new { kwhValue = 500m, readingDate = pastDate });
        var ctx = MakeFunctionContext();

        await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var reading = await db.MeterReadings.SingleAsync();
        reading.ReadingDate.ShouldBe(pastDate);
    }
}
