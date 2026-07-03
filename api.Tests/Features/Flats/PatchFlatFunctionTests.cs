using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Flats;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using System.Text;

namespace api.Tests.Features.Flats;

public class PatchFlatFunctionTests
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

    private static async Task<(Flat flat, AppDbContext db)> SeedFlatAsync(
        string userId = "user-test-123", decimal? plannedAnnualSpend = null)
    {
        var db = MakeDb();
        db.Users.Add(new User { UserId = userId });
        var flat = new Flat
        {
            FlatId = Guid.NewGuid(),
            UserId = userId,
            Name = "Original Name",
            AnnualKwhBaseline = 3500m,
            SpikeThreshold = 2.0m,
            PlannedAnnualSpend = plannedAnnualSpend
        };
        db.Flats.Add(flat);
        await db.SaveChangesAsync();
        return (flat, db);
    }

    private static HttpRequest MakeRequest(string rawJson)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.ContentType = "application/json";
        ctx.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(rawJson));
        return ctx.Request;
    }

    [Fact]
    public async Task RunAsync_InvalidFlatIdFormat_Returns400BadRequest()
    {
        var db = MakeDb();
        var fn = new PatchFlatFunction(db, new PatchFlatValidator());
        var req = MakeRequest("""{"name":"New Name"}""");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, "not-a-guid", ctx, CancellationToken.None);

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        badRequest.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task RunAsync_FlatDoesNotExist_Returns403Forbidden()
    {
        var db = MakeDb();
        var fn = new PatchFlatFunction(db, new PatchFlatValidator());
        var req = MakeRequest("""{"name":"New Name"}""");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, Guid.NewGuid().ToString(), ctx, CancellationToken.None);

        var forbidden = result.ShouldBeOfType<ObjectResult>();
        forbidden.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task RunAsync_FlatBelongsToDifferentUser_Returns403ForbiddenAndPersistsNothing()
    {
        var (flat, db) = await SeedFlatAsync(userId: "owner-user");
        var fn = new PatchFlatFunction(db, new PatchFlatValidator());
        var req = MakeRequest("""{"name":"New Name"}""");
        var ctx = MakeFunctionContext(userId: "attacker-user");

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var forbidden = result.ShouldBeOfType<ObjectResult>();
        forbidden.StatusCode.ShouldBe(403);
        var persisted = await db.Flats.SingleAsync(f => f.FlatId == flat.FlatId);
        persisted.Name.ShouldBe("Original Name");
    }

    [Fact]
    public async Task RunAsync_ValidNamePatch_Returns200AndUpdatesName()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new PatchFlatFunction(db, new PatchFlatValidator());
        var req = MakeRequest("""{"name":"Renamed Flat"}""");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<FlatResponse>();
        response.Name.ShouldBe("Renamed Flat");
        var persisted = await db.Flats.SingleAsync(f => f.FlatId == flat.FlatId);
        persisted.Name.ShouldBe("Renamed Flat");
    }

    [Fact]
    public async Task RunAsync_ValidAnnualKwhBaselinePatch_Returns200AndUpdatesBaseline()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new PatchFlatFunction(db, new PatchFlatValidator());
        var req = MakeRequest("""{"annualKwhBaseline":4200}""");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
        var persisted = await db.Flats.SingleAsync(f => f.FlatId == flat.FlatId);
        persisted.AnnualKwhBaseline.ShouldBe(4200m);
    }

    [Fact]
    public async Task RunAsync_MultipleFieldsInOnePatch_UpdatesAllThree()
    {
        var (flat, db) = await SeedFlatAsync(plannedAnnualSpend: 1200m);
        var fn = new PatchFlatFunction(db, new PatchFlatValidator());
        var req = MakeRequest("""{"name":"Renamed Flat","annualKwhBaseline":4200,"plannedAnnualSpend":1500}""");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
        var persisted = await db.Flats.SingleAsync(f => f.FlatId == flat.FlatId);
        persisted.Name.ShouldBe("Renamed Flat");
        persisted.AnnualKwhBaseline.ShouldBe(4200m);
        persisted.PlannedAnnualSpend.ShouldBe(1500m);
    }

    [Fact]
    public async Task RunAsync_AnnualKwhBaselineExplicitNull_SilentlyLeavesExistingValueUnchanged()
    {
        // Unlike PlannedAnnualSpend, AnnualKwhBaseline has no *Provided flag distinguishing
        // omitted from explicit null — JSON null parses to a null JsonValue, which the
        // guard treats identically to "not provided." Pinning current behavior; if this
        // asymmetry is ever unintentional, see deferred-work.md.
        var (flat, db) = await SeedFlatAsync();
        var fn = new PatchFlatFunction(db, new PatchFlatValidator());
        var req = MakeRequest("""{"annualKwhBaseline":null}""");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
        var persisted = await db.Flats.SingleAsync(f => f.FlatId == flat.FlatId);
        persisted.AnnualKwhBaseline.ShouldBe(3500m);
    }

    [Fact]
    public async Task RunAsync_EmptyPatchBody_Returns200NoOpAndPersistsNothing()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new PatchFlatFunction(db, new PatchFlatValidator());
        var req = MakeRequest("{}");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
        var persisted = await db.Flats.SingleAsync(f => f.FlatId == flat.FlatId);
        persisted.Name.ShouldBe("Original Name");
        persisted.AnnualKwhBaseline.ShouldBe(3500m);
    }

    [Fact]
    public async Task RunAsync_JsonBodyIsNotAnObject_Returns400BadRequest()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new PatchFlatFunction(db, new PatchFlatValidator());
        var req = MakeRequest("[1,2,3]");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        badRequest.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task RunAsync_PlannedAnnualSpendOmitted_LeavesExistingValueUnchanged()
    {
        var (flat, db) = await SeedFlatAsync(plannedAnnualSpend: 1200m);
        var fn = new PatchFlatFunction(db, new PatchFlatValidator());
        var req = MakeRequest("""{"name":"Renamed Flat"}""");
        var ctx = MakeFunctionContext();

        await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var persisted = await db.Flats.SingleAsync(f => f.FlatId == flat.FlatId);
        persisted.PlannedAnnualSpend.ShouldBe(1200m);
    }

    [Fact]
    public async Task RunAsync_PlannedAnnualSpendExplicitNull_ClearsExistingValue()
    {
        var (flat, db) = await SeedFlatAsync(plannedAnnualSpend: 1200m);
        var fn = new PatchFlatFunction(db, new PatchFlatValidator());
        var req = MakeRequest("""{"plannedAnnualSpend":null}""");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
        var persisted = await db.Flats.SingleAsync(f => f.FlatId == flat.FlatId);
        persisted.PlannedAnnualSpend.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_MalformedJsonBody_Returns400BadRequest()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new PatchFlatFunction(db, new PatchFlatValidator());
        var req = MakeRequest("{ not valid json");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        badRequest.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task RunAsync_AnnualKwhBaselineNotANumber_Returns400BadRequest()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new PatchFlatFunction(db, new PatchFlatValidator());
        var req = MakeRequest("""{"annualKwhBaseline":"not-a-number"}""");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        badRequest.StatusCode.ShouldBe(400);
    }

    [Fact]
    public async Task RunAsync_EmptyNamePatch_Returns400ValidationErrorAndPersistsNothing()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = new PatchFlatFunction(db, new PatchFlatValidator());
        var req = MakeRequest("""{"name":"   "}""");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        var persisted = await db.Flats.SingleAsync(f => f.FlatId == flat.FlatId);
        persisted.Name.ShouldBe("Original Name");
    }
}
