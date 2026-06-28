using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;

namespace api.Tests.Features.Settings;

public class UpdateUserSettingsFunctionTests
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

    private static HttpRequest MakeRequest(string? body)
    {
        var ctx = new DefaultHttpContext();
        if (body is not null)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(body);
            ctx.Request.Body = new System.IO.MemoryStream(bytes);
            ctx.Request.ContentLength = bytes.Length;
            ctx.Request.ContentType = "application/json";
        }
        return ctx.Request;
    }

    private static UpdateUserSettingsFunction MakeFn(AppDbContext db) =>
        new(db, NullLogger<UpdateUserSettingsFunction>.Instance);

    [Fact]
    public async Task RunAsync_ValidLocaleDeDe_Returns200WithUpdatedLocale()
    {
        using var db = MakeDb();
        db.Users.Add(new User { UserId = "user-1" });
        await db.SaveChangesAsync();

        var fn = MakeFn(db);
        var req = MakeRequest("""{"locale":"de-DE"}""");
        var ctx = MakeFunctionContext("user-1");

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<UserSettingsResponse>();
        response.Locale.ShouldBe("de-DE");
    }

    [Fact]
    public async Task RunAsync_InvalidLocaleFrFr_Returns400WithDetail()
    {
        using var db = MakeDb();
        var fn = MakeFn(db);
        var req = MakeRequest("""{"locale":"fr-FR"}""");
        var ctx = MakeFunctionContext("user-2");

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_NullBody_Returns400()
    {
        using var db = MakeDb();
        var fn = MakeFn(db);
        var req = MakeRequest(null);
        var ctx = MakeFunctionContext("user-3");

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_NewUserWithValidLocale_Returns200AndCreatesUserRow()
    {
        using var db = MakeDb();
        var fn = MakeFn(db);
        var req = MakeRequest("""{"locale":"en-US"}""");
        var ctx = MakeFunctionContext("brand-new-user");

        var result = await fn.RunAsync(req, ctx, CancellationToken.None);

        var ok = result.ShouldBeOfType<OkObjectResult>();
        var response = ok.Value.ShouldBeOfType<UserSettingsResponse>();
        response.Locale.ShouldBe("en-US");

        var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == "brand-new-user");
        user.ShouldNotBeNull();
        user.LocaleOverride.ShouldBe("en-US");
    }
}
