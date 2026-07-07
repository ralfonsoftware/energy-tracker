using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

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
}
