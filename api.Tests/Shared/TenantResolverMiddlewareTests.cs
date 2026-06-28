using System.Text;
using System.Text.Json;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Shouldly;

namespace api.Tests.Shared;

public class TenantResolverMiddlewareTests
{
    private static string MakeHeader(string? userId, string identityProvider = "aad")
    {
        var obj = new
        {
            identityProvider,
            userId,
            userDetails = "u@test.com",
            userRoles = new[] { "authenticated", "anonymous" }
        };
        var json = JsonSerializer.Serialize(obj);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    [Fact]
    public void TryResolveUserId_ValidHeader_ReturnsTrueAndExtractsUserId()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-MS-CLIENT-PRINCIPAL"] = MakeHeader("user-abc-123");

        var result = TenantResolverMiddleware.TryResolveUserId(httpContext, out var userId);

        result.ShouldBeTrue();
        userId.ShouldBe("user-abc-123");
    }

    [Fact]
    public void TryResolveUserId_MissingHeader_ReturnsFalse()
    {
        var httpContext = new DefaultHttpContext();

        var result = TenantResolverMiddleware.TryResolveUserId(httpContext, out var userId);

        result.ShouldBeFalse();
        userId.ShouldBe(string.Empty);
    }

    [Fact]
    public void TryResolveUserId_InvalidBase64_ReturnsFalse()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-MS-CLIENT-PRINCIPAL"] = "not-valid-base64!!!";

        var result = TenantResolverMiddleware.TryResolveUserId(httpContext, out var userId);

        result.ShouldBeFalse();
        userId.ShouldBe(string.Empty);
    }

    [Fact]
    public void TryResolveUserId_ValidBase64ButMissingUserIdField_ReturnsFalse()
    {
        var json = """{"identityProvider":"aad","userDetails":"u@test.com","userRoles":["authenticated"]}""";
        var header = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-MS-CLIENT-PRINCIPAL"] = header;

        var result = TenantResolverMiddleware.TryResolveUserId(httpContext, out var userId);

        result.ShouldBeFalse();
        userId.ShouldBe(string.Empty);
    }

    [Fact]
    public void TryResolveUserId_ValidBase64ButEmptyUserId_ReturnsFalse()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-MS-CLIENT-PRINCIPAL"] = MakeHeader(string.Empty);

        var result = TenantResolverMiddleware.TryResolveUserId(httpContext, out var userId);

        result.ShouldBeFalse();
        userId.ShouldBe(string.Empty);
    }

    [Fact]
    public void TryResolveUserId_ValidBase64ButWhitespaceUserId_ReturnsFalse()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-MS-CLIENT-PRINCIPAL"] = MakeHeader("   ");

        var result = TenantResolverMiddleware.TryResolveUserId(httpContext, out var userId);

        result.ShouldBeFalse();
        userId.ShouldBe(string.Empty);
    }
}
