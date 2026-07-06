using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.SmartPlugImport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using System.Text;

namespace api.Tests.Features.SmartPlugImport;

public class UploadFunctionTests
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

    private static (BlobServiceClient client, Mock<BlobClient> blobClientMock, Mock<BlobContainerClient> containerClientMock)
        MakeMockBlobServiceClient()
    {
        var blobClientMock = new Mock<BlobClient>();
        var contentInfo = BlobsModelFactory.BlobContentInfo(new ETag("etag"), DateTimeOffset.UtcNow, [], null!, 0);
        var response = Response.FromValue(contentInfo, Mock.Of<Response>());
        blobClientMock
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var containerClientMock = new Mock<BlobContainerClient>();
        containerClientMock.Setup(c => c.GetBlobClient(It.IsAny<string>())).Returns(blobClientMock.Object);

        var serviceClientMock = new Mock<BlobServiceClient>();
        serviceClientMock.Setup(s => s.GetBlobContainerClient(It.IsAny<string>())).Returns(containerClientMock.Object);

        return (serviceClientMock.Object, blobClientMock, containerClientMock);
    }

    private static IFormFile MakeFormFile(string fileName, string content = "some,csv,content")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", fileName);
    }

    private static (BlobServiceClient client, Mock<BlobClient> blobClientMock) MakeFailingBlobServiceClient()
    {
        var blobClientMock = new Mock<BlobClient>();
        blobClientMock
            .Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException("simulated storage outage"));

        var containerClientMock = new Mock<BlobContainerClient>();
        containerClientMock.Setup(c => c.GetBlobClient(It.IsAny<string>())).Returns(blobClientMock.Object);

        var serviceClientMock = new Mock<BlobServiceClient>();
        serviceClientMock.Setup(s => s.GetBlobContainerClient(It.IsAny<string>())).Returns(containerClientMock.Object);

        return (serviceClientMock.Object, blobClientMock);
    }

    private static HttpRequest MakeRequestWithFile(IFormFile? file, string? plugId = "plug-test-1")
    {
        var ctx = new DefaultHttpContext();
        var files = file is null
            ? new FormFileCollection()
            : new FormFileCollection { file };
        var formValues = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>();
        if (plugId is not null)
            formValues["plugId"] = plugId;
        ctx.Request.Form = new FormCollection(formValues, files);
        return ctx.Request;
    }

    [Fact]
    public async Task RunAsync_ValidXlsxUpload_CreatesPendingJobAndReturns202()
    {
        var (flat, db) = await SeedFlatAsync();
        var (blobService, blobClientMock, _) = MakeMockBlobServiceClient();
        var fn = new UploadFunction(db, blobService, Mock.Of<ILogger<UploadFunction>>());
        var req = MakeRequestWithFile(MakeFormFile("export.xlsx"));
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var accepted = result.ShouldBeOfType<AcceptedResult>();
        var response = accepted.Value.ShouldBeOfType<UploadImportResponse>();
        response.ImportJobId.ShouldNotBe(Guid.Empty);

        var job = await db.ImportJobs.SingleAsync(j => j.ImportJobId == response.ImportJobId);
        job.Status.ShouldBe(ImportStatus.Pending);
        job.FlatId.ShouldBe(flat.FlatId);

        blobClientMock.Verify(b => b.UploadAsync(It.IsAny<Stream>(), false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunAsync_ValidCsvUpload_CreatesPendingJobAndReturns202()
    {
        var (flat, db) = await SeedFlatAsync();
        var (blobService, _, _) = MakeMockBlobServiceClient();
        var fn = new UploadFunction(db, blobService, Mock.Of<ILogger<UploadFunction>>());
        var req = MakeRequestWithFile(MakeFormFile("readings.csv"));
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<AcceptedResult>();
    }

    [Fact]
    public async Task RunAsync_MissingFile_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var (blobService, _, _) = MakeMockBlobServiceClient();
        var fn = new UploadFunction(db, blobService, Mock.Of<ILogger<UploadFunction>>());
        var req = MakeRequestWithFile(null);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        (await db.ImportJobs.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_MissingPlugId_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var (blobService, _, _) = MakeMockBlobServiceClient();
        var fn = new UploadFunction(db, blobService, Mock.Of<ILogger<UploadFunction>>());
        var req = MakeRequestWithFile(MakeFormFile("export.xlsx"), plugId: null);
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        (await db.ImportJobs.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_WhitespaceOnlyPlugId_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var (blobService, _, _) = MakeMockBlobServiceClient();
        var fn = new UploadFunction(db, blobService, Mock.Of<ILogger<UploadFunction>>());
        var req = MakeRequestWithFile(MakeFormFile("export.xlsx"), plugId: "   ");
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        (await db.ImportJobs.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_DuplicatePlugIdFormField_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var (blobService, _, _) = MakeMockBlobServiceClient();
        var fn = new UploadFunction(db, blobService, Mock.Of<ILogger<UploadFunction>>());
        var ctx = new DefaultHttpContext();
        var formValues = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["plugId"] = new Microsoft.Extensions.Primitives.StringValues(["plug-a", "plug-b"])
        };
        ctx.Request.Form = new FormCollection(formValues, new FormFileCollection { MakeFormFile("export.xlsx") });
        var funcCtx = MakeFunctionContext();

        var result = await fn.RunAsync(ctx.Request, flat.FlatId.ToString(), funcCtx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        (await db.ImportJobs.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_WrongExtension_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var (blobService, _, _) = MakeMockBlobServiceClient();
        var fn = new UploadFunction(db, blobService, Mock.Of<ILogger<UploadFunction>>());
        var req = MakeRequestWithFile(MakeFormFile("notes.txt"));
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        (await db.ImportJobs.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_ForeignFlatId_Returns403()
    {
        var (flat, db) = await SeedFlatAsync(userId: "owner");
        var (blobService, _, _) = MakeMockBlobServiceClient();
        var fn = new UploadFunction(db, blobService, Mock.Of<ILogger<UploadFunction>>());
        var req = MakeRequestWithFile(MakeFormFile("export.xlsx"));
        var ctx = MakeFunctionContext(userId: "intruder");

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(403);
    }

    [Fact]
    public async Task RunAsync_InvalidFlatIdGuid_Returns400()
    {
        var (_, db) = await SeedFlatAsync();
        var (blobService, _, _) = MakeMockBlobServiceClient();
        var fn = new UploadFunction(db, blobService, Mock.Of<ILogger<UploadFunction>>());
        var req = MakeRequestWithFile(MakeFormFile("export.xlsx"));
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, "not-a-guid", ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RunAsync_EmptyFile_Returns400()
    {
        var (flat, db) = await SeedFlatAsync();
        var (blobService, _, _) = MakeMockBlobServiceClient();
        var fn = new UploadFunction(db, blobService, Mock.Of<ILogger<UploadFunction>>());
        var req = MakeRequestWithFile(MakeFormFile("export.xlsx", content: ""));
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        result.ShouldBeOfType<BadRequestObjectResult>();
        (await db.ImportJobs.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RunAsync_BlobWriteFails_MarksJobFailedAndReturns503()
    {
        var (flat, db) = await SeedFlatAsync();
        var (blobService, _) = MakeFailingBlobServiceClient();
        var fn = new UploadFunction(db, blobService, Mock.Of<ILogger<UploadFunction>>());
        var req = MakeRequestWithFile(MakeFormFile("export.xlsx"));
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var objectResult = result.ShouldBeOfType<ObjectResult>();
        objectResult.StatusCode.ShouldBe(503);

        var job = await db.ImportJobs.SingleAsync();
        job.Status.ShouldBe(ImportStatus.Failed);
        job.ErrorCategory.ShouldBe(ImportErrorCategory.ServiceUnavailable);
        job.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task RunAsync_ValidUpload_StoresOriginalFileName()
    {
        var (flat, db) = await SeedFlatAsync();
        var (blobService, _, _) = MakeMockBlobServiceClient();
        var fn = new UploadFunction(db, blobService, Mock.Of<ILogger<UploadFunction>>());
        var req = MakeRequestWithFile(MakeFormFile("export.xlsx"));
        var ctx = MakeFunctionContext();

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var response = result.ShouldBeOfType<AcceptedResult>().Value.ShouldBeOfType<UploadImportResponse>();
        var job = await db.ImportJobs.SingleAsync(j => j.ImportJobId == response.ImportJobId);
        job.OriginalFileName.ShouldBe("export.xlsx");
    }

    [Fact]
    public async Task RunAsync_ValidUpload_BlobPathMatchesUserIdFlatIdImportJobId()
    {
        var (flat, db) = await SeedFlatAsync(userId: "user-test-123");
        var (blobService, _, containerClientMock) = MakeMockBlobServiceClient();
        var fn = new UploadFunction(db, blobService, Mock.Of<ILogger<UploadFunction>>());
        var req = MakeRequestWithFile(MakeFormFile("export.xlsx"));
        var ctx = MakeFunctionContext(userId: "user-test-123");

        var result = await fn.RunAsync(req, flat.FlatId.ToString(), ctx, CancellationToken.None);

        var response = result.ShouldBeOfType<AcceptedResult>().Value.ShouldBeOfType<UploadImportResponse>();
        containerClientMock.Verify(
            c => c.GetBlobClient($"user-test-123/{flat.FlatId}/{response.ImportJobId}.xlsx"),
            Times.Once);
    }
}
