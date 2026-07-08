using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace EnergyTracker.Api.Shared;

public static class JsonSerializationDefaults
{
    // PropertyNamingPolicy only affects property names (e.g. "status"); it does not
    // camelCase enum member names — JsonStringEnumConverter serializes those as their
    // declared C# names (e.g. "Complete"), which is what the frontend expects.
    public static void Apply(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

        if (!options.Converters.Any(c => c is JsonStringEnumConverter))
            options.Converters.Add(new JsonStringEnumConverter());
    }

    // Registers Apply() against BOTH ASP.NET Core JSON options types. Http.Json.JsonOptions
    // governs minimal-API/WriteAsJsonAsync serialization; Mvc.JsonOptions governs
    // ObjectResult/OkObjectResult — what every HTTP Function in this codebase actually
    // returns. This codebase shipped a production incident (Story 6.6) where only the
    // former was configured: enum-typed response fields silently serialized as integers
    // over real HTTP despite passing every test that inspected IActionResult.Value
    // directly. Call this single method from Program.cs instead of configuring each
    // options type separately, so the two can no longer drift apart.
    public static void ConfigureAspNetCoreJsonOptions(IServiceCollection services)
    {
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options => Apply(options.SerializerOptions));
        services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options => Apply(options.JsonSerializerOptions));
    }
}
