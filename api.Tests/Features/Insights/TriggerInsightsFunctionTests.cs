using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Insights;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace api.Tests.Features.Insights;

public class TriggerInsightsFunctionTests
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

    private static HttpRequest MakeRequest()
    {
        var ctx = new DefaultHttpContext();
        return ctx.Request;
    }

    private static async Task<(Flat flat, AppDbContext db)> SeedFlatAsync(string userId = "user-test-123")
    {
        var db = MakeDb();
        db.Users.Add(new User { UserId = userId });
        var flat = new Flat
        {
            FlatId = Guid.NewGuid(),
            UserId = userId,
            Name = "Test Flat",
            AnnualKwhBaseline = 3500m,
            SpikeThreshold = 2.0m
        };
        db.Flats.Add(flat);
        await db.SaveChangesAsync();
        return (flat, db);
    }

    private static (QueueServiceClient client, Mock<QueueClient> queueClientMock) MakeMockQueueServiceClient()
    {
        var queueClientMock = new Mock<QueueClient>();
        var receipt = QueuesModelFactory.SendReceipt("msg-1", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(10), "pop-receipt", DateTimeOffset.UtcNow);
        var response = Response.FromValue(receipt, Mock.Of<Response>());
        queueClientMock
            .Setup(q => q.SendMessageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var serviceClientMock = new Mock<QueueServiceClient>();
        serviceClientMock.Setup(s => s.GetQueueClient(It.IsAny<string>())).Returns(queueClientMock.Object);

        return (serviceClientMock.Object, queueClientMock);
    }

    [Fact]
    public async Task RunAsync_FirstTrigger_CreatesPendingRunAndReturns202WithRunId()
    {
        var (flat, db) = await SeedFlatAsync();
        var (queueService, queueClientMock) = MakeMockQueueServiceClient();
        var fn = new TriggerInsightsFunction(db, queueService, Mock.Of<ILogger<TriggerInsightsFunction>>());
        var req = MakeRequest();
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var accepted = result.ShouldBeOfType<AcceptedResult>();
        var response = accepted.Value.ShouldBeOfType<TriggerInsightsResponse>();
        response.RunId.ShouldNotBe(Guid.Empty);

        var run = await db.InsightRuns.SingleAsync(r => r.RunId == response.RunId);
        run.FlatId.ShouldBe(flat.FlatId);
        run.Status.ShouldBe(InsightRunStatus.Pending);

        queueClientMock.Verify(q => q.SendMessageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(InsightRunStatus.Pending)]
    [InlineData(InsightRunStatus.Processing)]
    public async Task RunAsync_DuplicateTriggerWhileRunActive_ReturnsExistingRunIdAndCreatesNoNewRun(InsightRunStatus existingStatus)
    {
        var (flat, db) = await SeedFlatAsync();
        var existingRun = new InsightRun
        {
            RunId = Guid.NewGuid(),
            FlatId = flat.FlatId,
            Status = existingStatus,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        db.InsightRuns.Add(existingRun);
        await db.SaveChangesAsync();

        var (queueService, queueClientMock) = MakeMockQueueServiceClient();
        var fn = new TriggerInsightsFunction(db, queueService, Mock.Of<ILogger<TriggerInsightsFunction>>());
        var req = MakeRequest();
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var accepted = result.ShouldBeOfType<AcceptedResult>();
        var response = accepted.Value.ShouldBeOfType<TriggerInsightsResponse>();
        response.RunId.ShouldBe(existingRun.RunId);

        (await db.InsightRuns.CountAsync(r => r.FlatId == flat.FlatId)).ShouldBe(1);
        queueClientMock.Verify(q => q.SendMessageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_ForeignFlatId_Returns403()
    {
        var (flat, db) = await SeedFlatAsync(userId: "owner");
        var (queueService, _) = MakeMockQueueServiceClient();
        var fn = new TriggerInsightsFunction(db, queueService, Mock.Of<ILogger<TriggerInsightsFunction>>());
        var req = MakeRequest();
        var ctx = MakeFunctionContext(userId: "intruder");

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
        (await db.InsightRuns.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_InvalidFlatIdFormat_Returns400()
    {
        var (_, db) = await SeedFlatAsync();
        var (queueService, _) = MakeMockQueueServiceClient();
        var fn = new TriggerInsightsFunction(db, queueService, Mock.Of<ILogger<TriggerInsightsFunction>>());
        var req = MakeRequest();
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, "not-a-guid", ctx, CancellationToken.None);

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        var type = (string)badRequest.Value!.GetType().GetProperty("type")!.GetValue(badRequest.Value)!;
        type.ShouldBe("https://tools.ietf.org/html/rfc7231#section-6.5.1");
    }
}
