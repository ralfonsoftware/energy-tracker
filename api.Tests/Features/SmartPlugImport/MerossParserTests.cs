using System.Text;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.SmartPlugImport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace api.Tests.Features.SmartPlugImport;

public class MerossParserTests
{
    private const string ValidFileName = "Power Monitor Day Data - Schreibtisch - 20260620.csv";

    private static AppDbContext MakeDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<(Flat flat, AppDbContext db)> SeedFlatAsync()
    {
        var db = MakeDb();
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

    private static MerossParser MakeParser(AppDbContext db) =>
        new(db, Mock.Of<ILogger<MerossParser>>());

    private static byte[] BuildCsvBytes(IReadOnlyList<(string Date, string Value)> rows, bool withBom = false, string? extraBlankLineAt = null)
    {
        var sb = new StringBuilder();
        sb.Append("Date\t,Power Consumption-(kWh)\t\n");
        foreach (var (date, value) in rows)
        {
            if (extraBlankLineAt == date)
                sb.Append('\n');
            sb.Append($"{date}\t,{value}\t\n");
        }
        var bodyBytes = Encoding.UTF8.GetBytes(sb.ToString());
        if (!withBom)
            return bodyBytes;

        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var combined = new byte[bom.Length + bodyBytes.Length];
        Buffer.BlockCopy(bom, 0, combined, 0, bom.Length);
        Buffer.BlockCopy(bodyBytes, 0, combined, bom.Length, bodyBytes.Length);
        return combined;
    }

    [Fact]
    public async Task ParseFileAsync_ValidMultiRowFile_ProducesCorrectTuplesInOrder()
    {
        var bytes = BuildCsvBytes([("2026-01-01", "1.492"), ("2026-01-02", "2.310"), ("2026-01-03", "0.884")]);
        using var stream = new MemoryStream(bytes);

        var (_, rows) = await MerossParser.ParseFileAsync(ValidFileName, stream);

        rows.Count.ShouldBe(3);
        rows[0].Date.ShouldBe(new DateOnly(2026, 1, 1));
        rows[0].KwhValue.ShouldBe(1.492m);
        rows[1].Date.ShouldBe(new DateOnly(2026, 1, 2));
        rows[1].KwhValue.ShouldBe(2.310m);
        rows[2].Date.ShouldBe(new DateOnly(2026, 1, 3));
        rows[2].KwhValue.ShouldBe(0.884m);
    }

    [Fact]
    public async Task ParseFileAsync_BomPrefixedFile_ProducesIdenticalRowsToNonBomVersion()
    {
        var rowsInput = new (string, string)[] { ("2026-01-01", "1.492"), ("2026-01-02", "2.310") };
        using var bomStream = new MemoryStream(BuildCsvBytes(rowsInput, withBom: true));
        using var plainStream = new MemoryStream(BuildCsvBytes(rowsInput));

        var (_, bomRows) = await MerossParser.ParseFileAsync(ValidFileName, bomStream);
        var (_, plainRows) = await MerossParser.ParseFileAsync(ValidFileName, plainStream);

        bomRows.ShouldBe(plainRows);
    }

    [Fact]
    public async Task ParseFileAsync_ZeroValueRow_ProducesTupleWithZeroKwhValue()
    {
        var bytes = BuildCsvBytes([("2026-01-01", "1.492"), ("2026-01-02", "0.000")]);
        using var stream = new MemoryStream(bytes);

        var (_, rows) = await MerossParser.ParseFileAsync(ValidFileName, stream);

        rows.Count.ShouldBe(2);
        rows[1].Date.ShouldBe(new DateOnly(2026, 1, 2));
        rows[1].KwhValue.ShouldBe(0m);
    }

    [Fact]
    public async Task ParseFileAsync_ValidFileName_ExtractsDeviceName()
    {
        var bytes = BuildCsvBytes([("2026-01-01", "1.492")]);
        using var stream = new MemoryStream(bytes);

        var (deviceName, _) = await MerossParser.ParseFileAsync("Power Monitor Day Data - Schreibtisch - 20260620.csv", stream);

        deviceName.ShouldBe("Schreibtisch");
    }

    [Fact]
    public async Task ParseFileAsync_TrailingAndMidFileBlankLines_DoNotProduceExtraRows()
    {
        var bytes = BuildCsvBytes(
            [("2026-01-01", "1.492"), ("2026-01-02", "2.310")],
            extraBlankLineAt: "2026-01-02");
        using var stream = new MemoryStream(bytes);

        var (_, rows) = await MerossParser.ParseFileAsync(ValidFileName, stream);

        rows.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ParseFileAsync_MalformedRowShape_SkipsRowAndKeepsOthers()
    {
        var csv = "Date\t,Power Consumption-(kWh)\t\n" +
                  "2026-01-01\t,1.492\t\n" +
                  "not-a-valid-row\n" +
                  "2026-01-03\t,0.884\t\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var (_, rows) = await MerossParser.ParseFileAsync(ValidFileName, stream);

        rows.Count.ShouldBe(2);
        rows[0].Date.ShouldBe(new DateOnly(2026, 1, 1));
        rows[1].Date.ShouldBe(new DateOnly(2026, 1, 3));
    }

    [Fact]
    public async Task ParseFileAsync_UnparseableDate_SkipsRowAndKeepsOthers()
    {
        var csv = "Date\t,Power Consumption-(kWh)\t\n" +
                  "2026-01-01\t,1.492\t\n" +
                  "not-a-date\t,2.310\t\n" +
                  "2026-01-03\t,0.884\t\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var (_, rows) = await MerossParser.ParseFileAsync(ValidFileName, stream);

        rows.Count.ShouldBe(2);
        rows[0].Date.ShouldBe(new DateOnly(2026, 1, 1));
        rows[1].Date.ShouldBe(new DateOnly(2026, 1, 3));
    }

    [Fact]
    public async Task ParseFileAsync_UnparseableValue_SkipsRowAndKeepsOthers()
    {
        var csv = "Date\t,Power Consumption-(kWh)\t\n" +
                  "2026-01-01\t,1.492\t\n" +
                  "2026-01-02\t,not-a-number\t\n" +
                  "2026-01-03\t,0.884\t\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var (_, rows) = await MerossParser.ParseFileAsync(ValidFileName, stream);

        rows.Count.ShouldBe(2);
        rows[0].Date.ShouldBe(new DateOnly(2026, 1, 1));
        rows[1].Date.ShouldBe(new DateOnly(2026, 1, 3));
    }

    [Fact]
    public async Task ParseFileAsync_NegativeValue_SkipsRowAndKeepsOthers()
    {
        var bytes = BuildCsvBytes([("2026-01-01", "1.492"), ("2026-01-02", "-0.500"), ("2026-01-03", "0.884")]);
        using var stream = new MemoryStream(bytes);

        var (_, rows) = await MerossParser.ParseFileAsync(ValidFileName, stream);

        rows.Count.ShouldBe(2);
        rows[0].Date.ShouldBe(new DateOnly(2026, 1, 1));
        rows[1].Date.ShouldBe(new DateOnly(2026, 1, 3));
    }

    [Fact]
    public async Task ParseFileAsync_FilenameNotMatchingPattern_UsesFullFilenameAsDeviceName()
    {
        var bytes = BuildCsvBytes([("2026-01-01", "1.492")]);
        using var stream = new MemoryStream(bytes);
        const string unexpectedFileName = "some-other-export.csv";

        var (deviceName, _) = await MerossParser.ParseFileAsync(unexpectedFileName, stream);

        deviceName.ShouldBe(unexpectedFileName);
    }

    [Fact]
    public async Task ParseFileAsync_EmptyStream_ThrowsUnreadableFileException()
    {
        using var stream = new MemoryStream([]);

        await Should.ThrowAsync<UnreadableFileException>(
            () => MerossParser.ParseFileAsync(ValidFileName, stream));
    }

    [Fact]
    public async Task ParseAndStoreAsync_ValidFile_ProducesOneDailyRowPerDateAndNoIntervalRows()
    {
        var (flat, db) = await SeedFlatAsync();
        var bytes = BuildCsvBytes([("2026-01-01", "1.492"), ("2026-01-02", "2.310")]);
        using var stream = new MemoryStream(bytes);

        await MakeParser(db).ParseAndStoreAsync(flat.FlatId, "plug-1", ValidFileName, stream, CancellationToken.None);

        var dailyRows = await db.SmartPlugDailyData
            .Where(d => d.FlatId == flat.FlatId && d.PlugId == "plug-1")
            .OrderBy(d => d.Date)
            .ToListAsync();
        dailyRows.Count.ShouldBe(2);
        dailyRows[0].KwhValue.ShouldBe(1.492m);
        dailyRows[0].IsInterpolated.ShouldBeFalse();
        dailyRows[1].KwhValue.ShouldBe(2.310m);
        (await db.SmartPlugIntervalData.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task ParseAndStoreAsync_CalledTwiceWithOverlappingDates_UpdatesExistingRowsInPlace()
    {
        var (flat, db) = await SeedFlatAsync();
        using var firstStream = new MemoryStream(BuildCsvBytes([("2026-01-01", "1.492"), ("2026-01-02", "2.310")]));
        await MakeParser(db).ParseAndStoreAsync(flat.FlatId, "plug-1", ValidFileName, firstStream, CancellationToken.None);

        using var secondStream = new MemoryStream(BuildCsvBytes([("2026-01-02", "9.999"), ("2026-01-03", "0.500")]));
        await MakeParser(db).ParseAndStoreAsync(flat.FlatId, "plug-1", ValidFileName, secondStream, CancellationToken.None);

        var dailyRows = await db.SmartPlugDailyData
            .Where(d => d.FlatId == flat.FlatId && d.PlugId == "plug-1")
            .OrderBy(d => d.Date)
            .ToListAsync();
        dailyRows.Count.ShouldBe(3);
        dailyRows.Single(d => d.Date == new DateOnly(2026, 1, 2)).KwhValue.ShouldBe(9.999m);
        dailyRows.Single(d => d.Date == new DateOnly(2026, 1, 3)).KwhValue.ShouldBe(0.500m);
    }
}
