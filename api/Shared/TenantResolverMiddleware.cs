using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EnergyTracker.Api.Shared;

public sealed class TenantResolverMiddleware : IFunctionsWorkerMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();

        if (httpContext is null)
        {
            // Non-HTTP trigger (timer, blob, queue) — skip auth check
            await next(context);
            return;
        }

        if (!TryResolveUserId(httpContext, out var userId))
        {
            httpContext.Response.StatusCode = 403;
            httpContext.Response.ContentType = "application/problem+json";
            var body = JsonSerializer.Serialize(new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                title = "Forbidden",
                status = 403,
                detail = "Missing or invalid authentication context."
            });
            await httpContext.Response.WriteAsync(body);
            return;
        }

        context.Items["UserId"] = userId;
        await next(context);
    }

    internal static bool TryResolveUserId(Microsoft.AspNetCore.Http.HttpContext httpContext, out string userId)
    {
        userId = string.Empty;

        var headerValue = httpContext.Request.Headers["X-MS-CLIENT-PRINCIPAL"].FirstOrDefault();
        if (string.IsNullOrEmpty(headerValue))
            return false;

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(headerValue));
            var principal = JsonSerializer.Deserialize<ClientPrincipal>(decoded, JsonOptions);

            if (principal is null || string.IsNullOrWhiteSpace(principal.UserId))
                return false;

            userId = principal.UserId;
            return true;
        }
        catch
        {
            return false;
        }
    }
}

internal sealed record ClientPrincipal(
    [property: JsonPropertyName("identityProvider")] string IdentityProvider,
    [property: JsonPropertyName("userId")] string UserId,
    [property: JsonPropertyName("userDetails")] string UserDetails,
    [property: JsonPropertyName("userRoles")] IReadOnlyList<string> UserRoles,
    [property: JsonPropertyName("claims")] IReadOnlyList<UserClaim>? Claims = null
);

internal sealed record UserClaim(
    [property: JsonPropertyName("typ")] string Typ,
    [property: JsonPropertyName("val")] string Val
);
