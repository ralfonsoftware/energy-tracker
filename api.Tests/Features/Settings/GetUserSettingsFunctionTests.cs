using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Settings;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;

namespace api.Tests.Features.Settings;

public class GetUserSettingsFunctionTests
{
    private static AppDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static FunctionContext MakeFunctionContext(string userId = "user-test-123")
    {
        var mockContext = new Mock<FunctionContext>();
        var items = new Dictionary<object, object> { ["UserId"] = userId };
        mockContext.Setup(c => c.Items).Returns(items);
        return mockContext.Object;
    }

    [Fact]
    public async Task RunAsync_NewUser_ReturnsDefaultLocaleAndHasFlatFalse()
    {
        using var db = MakeDb();
        var fn = new GetUserSettingsFunction(db, new LocaleResolver());
        var req = new DefaultHttpContext().Request;
        var ctx = MakeFunctionContext("new-user-1");

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<UserSettingsResponse>();
        response.Locale.ShouldBe("en-US"); // no override, no Accept-Language → default
        response.HasFlat.ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_UserWithStoredLocale_ReturnsStoredLocale()
    {
        using var db = MakeDb();
        db.Users.Add(new User { UserId = "user-with-locale", LocaleOverride = "de-DE" });
        await db.SaveChangesAsync();

        var fn = new GetUserSettingsFunction(db, new LocaleResolver());
        var req = new DefaultHttpContext().Request;
        var ctx = MakeFunctionContext("user-with-locale");

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<UserSettingsResponse>();
        response.Locale.ShouldBe("de-DE");
    }

    [Fact]
    public async Task RunAsync_UserWithOneFlat_ReturnsHasFlatTrue()
    {
        using var db = MakeDb();
        db.Users.Add(new User { UserId = "user-with-flat" });
        db.Flats.Add(new Flat
        {
            FlatId = Guid.NewGuid(),
            UserId = "user-with-flat",
            Name = "Main Flat",
            AnnualKwhBaseline = 3500m,
            SpikeThreshold = 2.0m,
        });
        await db.SaveChangesAsync();

        var fn = new GetUserSettingsFunction(db, new LocaleResolver());
        var req = new DefaultHttpContext().Request;
        var ctx = MakeFunctionContext("user-with-flat");

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<UserSettingsResponse>();
        response.HasFlat.ShouldBeTrue();
    }

    [Fact]
    public async Task RunAsync_UserWithActiveFlatIdSet_ReturnsIt()
    {
        using var db = MakeDb();
        var activeFlatId = Guid.NewGuid();
        db.Users.Add(new User { UserId = "user-with-active-flat", ActiveFlatId = activeFlatId });
        await db.SaveChangesAsync();

        var fn = new GetUserSettingsFunction(db, new LocaleResolver());
        var req = new DefaultHttpContext().Request;
        var ctx = MakeFunctionContext("user-with-active-flat");

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<UserSettingsResponse>();
        response.ActiveFlatId.ShouldBe(activeFlatId);
    }

    [Fact]
    public async Task RunAsync_UserWithNoActiveFlatIdSet_ReturnsNull()
    {
        using var db = MakeDb();
        db.Users.Add(new User { UserId = "user-with-no-active-flat" });
        await db.SaveChangesAsync();

        var fn = new GetUserSettingsFunction(db, new LocaleResolver());
        var req = new DefaultHttpContext().Request;
        var ctx = MakeFunctionContext("user-with-no-active-flat");

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<UserSettingsResponse>();
        response.ActiveFlatId.ShouldBeNull();
    }
}
