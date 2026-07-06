using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.SmartPlugImport;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace api.Tests.Features.SmartPlugImport;

public class ProcessImportFunctionTests
{
    private sealed class ThrowingStream(Exception exceptionToThrow) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw exceptionToThrow;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            throw exceptionToThrow;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class ConcurrencyConflictDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        private int _saveCount;

        public void ResetSaveCount() => _saveCount = 0;

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            _saveCount++;
            if (_saveCount == 2)
                throw new DbUpdateConcurrencyException("Simulated concurrency conflict.");
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private static AppDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static FunctionContext MakeFunctionContext() => Mock.Of<FunctionContext>();

    private static async Task<(Flat flat, AppDbContext db)> SeedFlatAsync(AppDbContext? db = null)
    {
        db ??= MakeDb();
        const string userId = "user-test-123";
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

    private static async Task<ImportJob> SeedImportJobAsync(AppDbContext db, Guid flatId)
    {
        var job = new ImportJob
        {
            FlatId = flatId,
            Status = ImportStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.ImportJobs.Add(job);
        await db.SaveChangesAsync();
        return job;
    }

    private static ProcessImportFunction MakeFunction(AppDbContext db) =>
        new(db, Mock.Of<ILogger<ProcessImportFunction>>());

    [Fact]
    public async Task RunAsync_HappyPath_TransitionsPendingToComplete()
    {
        var (flat, db) = await SeedFlatAsync();
        var job = await SeedImportJobAsync(db, flat.FlatId);
        var fn = MakeFunction(db);
        using var blobStream = new MemoryStream(new byte[] { 1, 2, 3 });

        await fn.RunAsync(blobStream, "user-test-123", flat.FlatId.ToString(), job.ImportJobId.ToString(), "csv",
            MakeFunctionContext(), CancellationToken.None);

        var persisted = await db.ImportJobs.SingleAsync(j => j.ImportJobId == job.ImportJobId);
        persisted.Status.ShouldBe(ImportStatus.Complete);
        persisted.CompletedAt.ShouldNotBeNull();
        persisted.ErrorCategory.ShouldBeNull();
    }

    [Fact]
    public async Task RunAsync_UnhandledException_SetsFailedWithProcessingFailed()
    {
        var (flat, db) = await SeedFlatAsync();
        var job = await SeedImportJobAsync(db, flat.FlatId);
        var fn = MakeFunction(db);
        using var blobStream = new ThrowingStream(new InvalidOperationException("boom"));

        await fn.RunAsync(blobStream, "user-test-123", flat.FlatId.ToString(), job.ImportJobId.ToString(), "csv",
            MakeFunctionContext(), CancellationToken.None);

        var persisted = await db.ImportJobs.SingleAsync(j => j.ImportJobId == job.ImportJobId);
        persisted.Status.ShouldBe(ImportStatus.Failed);
        persisted.ErrorCategory.ShouldBe(ImportErrorCategory.ProcessingFailed);
    }

    [Fact]
    public async Task RunAsync_UnreadableFile_SetsFailedWithDataUnreadable()
    {
        var (flat, db) = await SeedFlatAsync();
        var job = await SeedImportJobAsync(db, flat.FlatId);
        var fn = MakeFunction(db);
        using var blobStream = new MemoryStream();

        await fn.RunAsync(blobStream, "user-test-123", flat.FlatId.ToString(), job.ImportJobId.ToString(), "txt",
            MakeFunctionContext(), CancellationToken.None);

        var persisted = await db.ImportJobs.SingleAsync(j => j.ImportJobId == job.ImportJobId);
        persisted.Status.ShouldBe(ImportStatus.Failed);
        persisted.ErrorCategory.ShouldBe(ImportErrorCategory.DataUnreadable);
    }

    [Fact]
    public async Task RunAsync_ServiceUnavailable_SetsFailedWithServiceUnavailable()
    {
        var (flat, db) = await SeedFlatAsync();
        var job = await SeedImportJobAsync(db, flat.FlatId);
        var fn = MakeFunction(db);
        using var blobStream = new ThrowingStream(new ImportServiceUnavailableException("storage outage"));

        await fn.RunAsync(blobStream, "user-test-123", flat.FlatId.ToString(), job.ImportJobId.ToString(), "csv",
            MakeFunctionContext(), CancellationToken.None);

        var persisted = await db.ImportJobs.SingleAsync(j => j.ImportJobId == job.ImportJobId);
        persisted.Status.ShouldBe(ImportStatus.Failed);
        persisted.ErrorCategory.ShouldBe(ImportErrorCategory.ServiceUnavailable);
    }

    [Fact]
    public async Task RunAsync_ConcurrencyConflictOnCompletionSave_ReloadsAndMarksFailed()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ConcurrencyConflictDbContext(options);
        var (flat, _) = await SeedFlatAsync(db);
        db.ResetSaveCount();
        var job = await SeedImportJobAsync(db, flat.FlatId);
        db.ResetSaveCount();
        var fn = MakeFunction(db);
        using var blobStream = new MemoryStream(new byte[] { 1, 2, 3 });

        await Should.NotThrowAsync(() => fn.RunAsync(
            blobStream, "user-test-123", flat.FlatId.ToString(), job.ImportJobId.ToString(), "csv",
            MakeFunctionContext(), CancellationToken.None));

        var persisted = await db.ImportJobs.SingleAsync(j => j.ImportJobId == job.ImportJobId);
        persisted.Status.ShouldBe(ImportStatus.Failed);
        persisted.ErrorCategory.ShouldBe(ImportErrorCategory.ProcessingFailed);
        persisted.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task RunAsync_ConcurrencyConflictOnProcessingSave_MarksFailedWithoutThrowing()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ConcurrencyConflictDbContext(options);
        var (flat, _) = await SeedFlatAsync(db);
        db.ResetSaveCount();
        var job = await SeedImportJobAsync(db, flat.FlatId);
        db.ResetSaveCount();
        var fn = MakeFunction(db);
        using var blobStream = new MemoryStream(new byte[] { 1, 2, 3 });

        await Should.NotThrowAsync(() => fn.RunAsync(
            blobStream, "user-test-123", flat.FlatId.ToString(), job.ImportJobId.ToString(), "csv",
            MakeFunctionContext(), CancellationToken.None));

        var persisted = await db.ImportJobs.SingleAsync(j => j.ImportJobId == job.ImportJobId);
        persisted.Status.ShouldBe(ImportStatus.Failed);
        persisted.ErrorCategory.ShouldBe(ImportErrorCategory.ProcessingFailed);
        persisted.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task RunAsync_EmptyBlobStream_SetsFailedWithDataUnreadable()
    {
        var (flat, db) = await SeedFlatAsync();
        var job = await SeedImportJobAsync(db, flat.FlatId);
        var fn = MakeFunction(db);
        using var blobStream = new MemoryStream();

        await fn.RunAsync(blobStream, "user-test-123", flat.FlatId.ToString(), job.ImportJobId.ToString(), "csv",
            MakeFunctionContext(), CancellationToken.None);

        var persisted = await db.ImportJobs.SingleAsync(j => j.ImportJobId == job.ImportJobId);
        persisted.Status.ShouldBe(ImportStatus.Failed);
        persisted.ErrorCategory.ShouldBe(ImportErrorCategory.DataUnreadable);
    }

    [Fact]
    public async Task RunAsync_MalformedImportJobId_ReturnsWithoutThrowing()
    {
        var (flat, db) = await SeedFlatAsync();
        var fn = MakeFunction(db);
        using var blobStream = new MemoryStream(new byte[] { 1, 2, 3 });

        await Should.NotThrowAsync(() => fn.RunAsync(
            blobStream, "user-test-123", flat.FlatId.ToString(), "not-a-guid", "csv",
            MakeFunctionContext(), CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_JobAlreadyComplete_SkipsReprocessingOnRedeliveredTrigger()
    {
        var (flat, db) = await SeedFlatAsync();
        var job = await SeedImportJobAsync(db, flat.FlatId);
        job.Status = ImportStatus.Complete;
        job.CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();
        var fn = MakeFunction(db);
        using var blobStream = new ThrowingStream(new InvalidOperationException("should not be read"));

        await fn.RunAsync(blobStream, "user-test-123", flat.FlatId.ToString(), job.ImportJobId.ToString(), "csv",
            MakeFunctionContext(), CancellationToken.None);

        var persisted = await db.ImportJobs.SingleAsync(j => j.ImportJobId == job.ImportJobId);
        persisted.Status.ShouldBe(ImportStatus.Complete);
        persisted.CompletedAt.ShouldBe(job.CompletedAt);
    }
}
