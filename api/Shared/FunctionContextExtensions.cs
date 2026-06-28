using Microsoft.Azure.Functions.Worker;

namespace EnergyTracker.Api.Shared;

public static class FunctionContextExtensions
{
    public static string GetUserId(this FunctionContext context)
    {
        if (context.Items.TryGetValue("UserId", out var userId) && userId is string userIdStr)
            return userIdStr;
        throw new InvalidOperationException(
            "UserId not resolved. Ensure TenantResolverMiddleware is registered in Program.cs.");
    }
}
