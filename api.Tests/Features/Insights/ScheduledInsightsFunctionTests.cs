using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.Insights;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace api.Tests.Features.Insights;

public class ScheduledInsightsFunctionTests
{
    private static AppDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static FunctionContext MakeFunctionContext()
    {
        var mock = new Mock<FunctionContext>();
        mock.Setup(c => c.Items).Returns(new Dictionary<object, object>());
        return mock.Object;
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

    private static Flat MakeFlat(string userId) => new()
    {
        FlatId = Guid.NewGuid(),
        UserId = userId,
        Name = "Test Flat",
        AnnualKwhBaseline = 3500m,
        SpikeThreshold = 2.0m
    };

    [Fact]
    public async Task RunAsync_MultipleFlats_EnqueuesOneMessagePerFlatAndCreatesOnePendingRunEach()
    {
        var db = MakeDb();
        var flatA = MakeFlat("user-a");
        var flatB = MakeFlat("user-b");
        var flatC = MakeFlat("user-c");
        db.Flats.AddRange(flatA, flatB, flatC);
        await db.SaveChangesAsync();

        var (queueService, queueClientMock) = MakeMockQueueServiceClient();
        var fn = new ScheduledInsightsFunction(db, queueService, Mock.Of<ILogger<ScheduledInsightsFunction>>());

        await fn.RunAsync(new TimerInfo(), MakeFunctionContext(), CancellationToken.None);

        queueClientMock.Verify(q => q.SendMessageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));

        var runs = await db.InsightRuns.ToListAsync();
        runs.Count.ShouldBe(3);
        runs.ShouldAllBe(r => r.Status == InsightRunStatus.Pending);
        runs.Select(r => r.FlatId).ShouldBe([flatA.FlatId, flatB.FlatId, flatC.FlatId], ignoreOrder: true);
    }

    [Fact]
    public async Task RunAsync_NoFlats_EnqueuesNoMessages()
    {
        var db = MakeDb();
        var (queueService, queueClientMock) = MakeMockQueueServiceClient();
        var fn = new ScheduledInsightsFunction(db, queueService, Mock.Of<ILogger<ScheduledInsightsFunction>>());

        await fn.RunAsync(new TimerInfo(), MakeFunctionContext(), CancellationToken.None);

        queueClientMock.Verify(q => q.SendMessageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        (await db.InsightRuns.CountAsync()).ShouldBe(0);
    }
}
