using System.Text.Json;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Shouldly;

namespace api.Tests.Shared;

public class JsonSerializationDefaultsTests
{
    private record SamplePayload(ImportStatus Status, ImportErrorCategory? ErrorCategory);

    [Fact]
    public void Apply_SerializesEnumAsItsDeclaredName()
    {
        var options = new JsonSerializerOptions();
        JsonSerializationDefaults.Apply(options);

        var json = JsonSerializer.Serialize(new SamplePayload(ImportStatus.Complete, null), options);

        json.ShouldContain("\"status\":\"Complete\"");
    }

    [Fact]
    public void Apply_SerializesPopulatedNullableEnumAsItsDeclaredName()
    {
        var options = new JsonSerializerOptions();
        JsonSerializationDefaults.Apply(options);

        var json = JsonSerializer.Serialize(
            new SamplePayload(ImportStatus.Failed, ImportErrorCategory.DataUnreadable), options);

        json.ShouldContain("\"errorCategory\":\"DataUnreadable\"");
    }

    [Fact]
    public void Apply_SerializesNullNullableEnumAsNull()
    {
        var options = new JsonSerializerOptions();
        JsonSerializationDefaults.Apply(options);

        var json = JsonSerializer.Serialize(new SamplePayload(ImportStatus.Pending, null), options);

        json.ShouldContain("\"errorCategory\":null");
    }

    [Fact]
    public void Apply_IsIdempotentWhenCalledTwiceOnTheSameOptions()
    {
        var options = new JsonSerializerOptions();
        JsonSerializationDefaults.Apply(options);
        JsonSerializationDefaults.Apply(options);

        options.Converters.Count.ShouldBe(1);
    }

    [Fact]
    public void MvcJsonOptions_WiredExactlyLikeProgramCs_SerializesEnumAsString()
    {
        var mvcOptions = new Microsoft.AspNetCore.Mvc.JsonOptions();
        JsonSerializationDefaults.Apply(mvcOptions.JsonSerializerOptions);

        var json = JsonSerializer.Serialize(
            new SamplePayload(ImportStatus.Complete, null), mvcOptions.JsonSerializerOptions);

        json.ShouldContain("\"status\":\"Complete\"");
    }

    [Fact]
    public void HttpJsonOptions_WiredExactlyLikeProgramCs_SerializesEnumAsString()
    {
        var httpJsonOptions = new Microsoft.AspNetCore.Http.Json.JsonOptions();
        JsonSerializationDefaults.Apply(httpJsonOptions.SerializerOptions);

        var json = JsonSerializer.Serialize(
            new SamplePayload(ImportStatus.Complete, null), httpJsonOptions.SerializerOptions);

        json.ShouldContain("\"status\":\"Complete\"");
    }
}
