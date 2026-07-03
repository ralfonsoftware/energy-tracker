using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Flats;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace api.Tests.Features.Flats;

public class GetFlatsFunctionTests
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

    private static Flat MakeFlat(string userId, string name = "Test Flat", decimal? plannedAnnualSpend = null) => new()
    {
        FlatId = Guid.NewGuid(),
        UserId = userId,
        Name = name,
        AnnualKwhBaseline = 3500m,
        SpikeThreshold = 2.0m,
        PlannedAnnualSpend = plannedAnnualSpend
    };

    [Fact]
    public async Task RunAsync_UserWithNoFlats_ReturnsEmptyList()
    {
        using var db = MakeDb();
        var fn = new GetFlatsFunction(db);
        var req = new DefaultHttpContext().Request;
        var ctx = MakeFunctionContext("user-with-no-flats");

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var flats = ok.Value.ShouldBeOfType<List<FlatSummary>>();
        flats.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_UserWithMultipleFlats_ReturnsAllOfThem()
    {
        using var db = MakeDb();
        var flat1 = MakeFlat("multi-user", name: "Flat One", plannedAnnualSpend: 900m);
        var flat2 = MakeFlat("multi-user", name: "Flat Two");
        db.Flats.AddRange(flat1, flat2);
        await db.SaveChangesAsync();

        var fn = new GetFlatsFunction(db);
        var req = new DefaultHttpContext().Request;
        var ctx = MakeFunctionContext("multi-user");

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var flats = ok.Value.ShouldBeOfType<List<FlatSummary>>();
        flats.Count.ShouldBe(2);
        var summary1 = flats.Single(f => f.FlatId == flat1.FlatId);
        summary1.Name.ShouldBe("Flat One");
        summary1.AnnualKwhBaseline.ShouldBe(3500m);
        summary1.SpikeThreshold.ShouldBe(2.0m);
        summary1.PlannedAnnualSpend.ShouldBe(900m);
        var summary2 = flats.Single(f => f.FlatId == flat2.FlatId);
        summary2.Name.ShouldBe("Flat Two");
        summary2.PlannedAnnualSpend.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_FlatsBelongingToOtherUsers_AreNeverReturned()
    {
        using var db = MakeDb();
        var ownFlat = MakeFlat("owner-user");
        var otherFlat = MakeFlat("other-user");
        db.Flats.AddRange(ownFlat, otherFlat);
        await db.SaveChangesAsync();

        var fn = new GetFlatsFunction(db);
        var req = new DefaultHttpContext().Request;
        var ctx = MakeFunctionContext("owner-user");

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var flats = ok.Value.ShouldBeOfType<List<FlatSummary>>();
        flats.Count.ShouldBe(1);
        flats[0].FlatId.ShouldBe(ownFlat.FlatId);
    }
}
