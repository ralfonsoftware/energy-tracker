using System.Text.Json;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.SmartPlugImport;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
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

    // Regression test for the Story 6.6 production incident: Http.Json.JsonOptions was
    // configured with the enum converter, but Mvc.JsonOptions — the options type that
    // actually governs ObjectResult/OkObjectResult, i.e. every HTTP Function response in
    // this codebase — was not, so GetImportStatusFunction's real response silently
    // serialized `status` as an integer. This test does not call JsonSerializationDefaults
    // directly; it builds a real DI container via ConfigureAspNetCoreJsonOptions (the single
    // method Program.cs calls) and executes a real ObjectResult through the real
    // IActionResultExecutor<ObjectResult> ASP.NET Core pipeline, then inspects the raw
    // response bytes — the same level at which the original bug manifested.
    [Fact]
    public async Task ConfigureAspNetCoreJsonOptions_RealObjectResultOverMvcPipeline_SerializesEnumAsStringNotInteger()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvcCore();
        JsonSerializationDefaults.ConfigureAspNetCoreJsonOptions(services);
        await using var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = provider };
        using var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        var response = new ImportJobStatusResponse(
            Guid.NewGuid(), ImportStatus.Complete, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null);
        var objectResult = new OkObjectResult(response);

        var executor = provider.GetRequiredService<IActionResultExecutor<ObjectResult>>();
        await executor.ExecuteAsync(actionContext, objectResult);

        responseBody.Position = 0;
        var json = await new StreamReader(responseBody).ReadToEndAsync();

        json.ShouldContain("\"status\":\"Complete\"");
        json.ShouldNotContain("\"status\":2");
    }
}
