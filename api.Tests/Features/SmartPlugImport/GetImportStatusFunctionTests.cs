using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.SmartPlugImport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace api.Tests.Features.SmartPlugImport;

public class GetImportStatusFunctionTests
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

    private static HttpRequest MakeRequest()
    {
        var ctx = new DefaultHttpContext();
        return ctx.Request;
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

    private static async Task<ImportJob> SeedImportJobAsync(AppDbContext db, Guid flatId, ImportStatus status = ImportStatus.Pending)
    {
        var job = new ImportJob
        {
            FlatId = flatId,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.ImportJobs.Add(job);
        await db.SaveChangesAsync();
        return job;
    }

    [Fact]
    public async Task RunAsync_ExistingJob_ReturnsCorrectFields()
    {
        var (flat, db) = await SeedFlatAsync();
        var job = await SeedImportJobAsync(db, flat.FlatId, ImportStatus.Complete);
        var fn = new GetImportStatusFunction(db);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeRequest(), flat.FlatId.ToString(), job.ImportJobId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<ImportJobStatusResponse>();
        response.ImportJobId.ShouldBe(job.ImportJobId);
        response.Status.ShouldBe(ImportStatus.Complete);
        response.CreatedAt.ShouldBe(job.CreatedAt);
    }

    [Fact]
    public async Task RunAsync_UnknownJobIdUnderOwnedFlat_Returns404()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new GetImportStatusFunction(db);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeRequest(), flat.FlatId.ToString(), Guid.NewGuid().ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RunAsync_ForeignFlat_Returns403()
    {
        var (flat, db) = await SeedFlatAsync(userId: "owner");
        var job = await SeedImportJobAsync(db, flat.FlatId);
        var fn = new GetImportStatusFunction(db);
        var ctx = MakeFunctionContext(userId: "intruder");

        var result = await fn.RunAsync(MakeRequest(), flat.FlatId.ToString(), job.ImportJobId.ToString(), ctx, CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task RunAsync_MalformedJobId_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new GetImportStatusFunction(db);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeRequest(), flat.FlatId.ToString(), "not-a-guid", ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_InvalidFlatIdGuid_Returns400()
    {
        var (_, db) = await SeedFlatAsync();
        var fn = new GetImportStatusFunction(db);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(MakeRequest(), "not-a-guid", Guid.NewGuid().ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }
}
