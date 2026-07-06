using System.IO.Compression;
using System.Text;
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Features.SmartPlugImport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace api.Tests.Features.SmartPlugImport;

public class EveHomeParserTests
{
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

    private static EveHomeParser MakeParser(AppDbContext db) =>
        new(db, Mock.Of<ILogger<EveHomeParser>>());

    private static MemoryStream BuildEveHomeXlsx(
        string cellA1, string cellA2,
        IReadOnlyList<(string? Timestamp, string? WhValue)> dataRows)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);
            WriteEntry(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            WriteEntry(zip, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                          xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Gesamtverbrauch" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            WriteEntry(zip, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);

            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
            sb.Append($"<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>{cellA1}</t></is></c></row>");
            sb.Append($"<row r=\"2\"><c r=\"A2\" t=\"inlineStr\"><is><t>{cellA2}</t></is></c></row>");
            sb.Append("<row r=\"3\"><c r=\"A3\" t=\"inlineStr\"><is><t>Zuhause: Test</t></is></c></row>");
            sb.Append("<row r=\"4\"><c r=\"A4\" t=\"inlineStr\"><is><t>Datum</t></is></c><c r=\"B4\" t=\"inlineStr\"><is><t>Gesamtverbrauch (Wh)</t></is></c></row>");
            var rowNum = 5;
            foreach (var (ts, val) in dataRows)
            {
                sb.Append($"<row r=\"{rowNum}\">");
                sb.Append(ts is null ? $"<c r=\"A{rowNum}\"/>" : $"<c r=\"A{rowNum}\" t=\"d\"><v>{ts}</v></c>");
                sb.Append(val is null ? $"<c r=\"B{rowNum}\"/>" : $"<c r=\"B{rowNum}\"><v>{val}</v></c>");
                sb.Append("</row>");
                rowNum++;
            }
            sb.Append("</sheetData></worksheet>");
            WriteEntry(zip, "xl/worksheets/sheet1.xml", sb.ToString());
        }
        stream.Position = 0;
        return stream;

        static void WriteEntry(ZipArchive zip, string name, string content)
        {
            var entry = zip.CreateEntry(name);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }
    }

    [Fact]
    public void ParseFile_ExtractsDeviceNameStrippingGeratPrefix()
    {
        using var stream = BuildEveHomeXlsx("Gerät: Steckdose Tür", "Raum: Wohnzimmer",
            [("2026-06-20T12:00:00", "0.5")]);

        var (deviceName, _) = EveHomeParser.ParseFile(stream);

        deviceName.ShouldBe("Steckdose Tür");
    }

    [Fact]
    public void ParseFile_RowsWithNullTimestampOrValue_AreExcluded()
    {
        using var stream = BuildEveHomeXlsx("Gerät: Test", "Raum: Test",
            [
                ("2026-06-20T12:00:00", "0.5"),
                (null, "0.5"),
                ("2026-06-20T12:10:00", null)
            ]);

        var (_, rows) = EveHomeParser.ParseFile(stream);

        rows.Count.ShouldBe(1);
    }

    [Fact]
    public void ParseFile_TimestampIsNotConvertedToUtc()
    {
        using var stream = BuildEveHomeXlsx("Gerät: Test", "Raum: Test",
            [("2026-06-20T12:00:18", "0.5")]);

        var (_, rows) = EveHomeParser.ParseFile(stream);

        var (timestamp, _) = rows.Single();
        timestamp.Offset.ShouldBe(TimeSpan.Zero);
        timestamp.DateTime.Year.ShouldBe(2026);
        timestamp.DateTime.Month.ShouldBe(6);
        timestamp.DateTime.Day.ShouldBe(20);
        timestamp.DateTime.Hour.ShouldBe(12);
    }

    [Fact]
    public void ExtractWallClockTimestamp_DateTimeInput_ReturnsUnchanged()
    {
        var input = new DateTime(2026, 6, 20, 12, 0, 18);

        var result = EveHomeParser.ExtractWallClockTimestamp(input);

        result.ShouldBe(input);
    }

    [Fact]
    public void ExtractWallClockTimestamp_DateTimeOffsetInput_DiscardsOffsetKeepsWallClock()
    {
        // Reproduces the real Eve Home export behavior confirmed against the actual sample files:
        // ExcelDataReader can return a DateTimeOffset (not DateTime) for a t="d" cell, tagged with the
        // reading machine's local system offset. The offset must be discarded, not applied as an
        // adjustment, so the wall-clock value the cell actually encodes is preserved unchanged.
        var input = new DateTimeOffset(2026, 6, 20, 12, 0, 18, TimeSpan.FromHours(2));

        var result = EveHomeParser.ExtractWallClockTimestamp(input);

        result.ShouldBe(new DateTime(2026, 6, 20, 12, 0, 18));
    }

    [Fact]
    public void ExtractWallClockTimestamp_ParseableStringInput_ReturnsParsedValue()
    {
        var result = EveHomeParser.ExtractWallClockTimestamp("2026-06-20T12:00:18");

        result.ShouldBe(new DateTime(2026, 6, 20, 12, 0, 18));
    }

    [Fact]
    public void ExtractWallClockTimestamp_UnparseableValue_ThrowsUnreadableFileException()
    {
        Should.Throw<UnreadableFileException>(() => EveHomeParser.ExtractWallClockTimestamp("not-a-date"));
    }

    [Fact]
    public async Task ParseAndStoreAsync_ValidFile_ProducesCorrectDailyTotal()
    {
        var (flat, db) = await SeedFlatAsync();
        var parser = MakeParser(db);
        using var stream = BuildEveHomeXlsx("Gerät: Test", "Raum: Test",
            [
                ("2026-06-20T00:00:00", "500"),
                ("2026-06-20T00:10:00", "300"),
                ("2026-06-20T00:20:00", "200")
            ]);

        await parser.ParseAndStoreAsync(flat.FlatId, "plug-1", stream, CancellationToken.None);

        var daily = await db.SmartPlugDailyData.SingleAsync(d => d.FlatId == flat.FlatId && d.PlugId == "plug-1");
        daily.KwhValue.ShouldBe(1.0m);
        daily.IsInterpolated.ShouldBeFalse();
        daily.Date.ShouldBe(new DateOnly(2026, 6, 20));
        (await db.SmartPlugIntervalData.CountAsync(d => d.FlatId == flat.FlatId && d.PlugId == "plug-1")).ShouldBe(3);
    }

    [Fact]
    public async Task ParseAndStoreAsync_SameFileImportedTwice_DoesNotDoubleTotalOrDuplicateRows()
    {
        var (flat, db) = await SeedFlatAsync();
        var parser = MakeParser(db);
        (string? Timestamp, string? WhValue)[] rows =
        [
            ("2026-06-20T00:00:00", "500"),
            ("2026-06-20T00:10:00", "300")
        ];

        using (var stream1 = BuildEveHomeXlsx("Gerät: Test", "Raum: Test", rows))
            await parser.ParseAndStoreAsync(flat.FlatId, "plug-1", stream1, CancellationToken.None);
        using (var stream2 = BuildEveHomeXlsx("Gerät: Test", "Raum: Test", rows))
            await parser.ParseAndStoreAsync(flat.FlatId, "plug-1", stream2, CancellationToken.None);

        (await db.SmartPlugIntervalData.CountAsync(d => d.FlatId == flat.FlatId && d.PlugId == "plug-1")).ShouldBe(2);
        var daily = await db.SmartPlugDailyData.SingleAsync(d => d.FlatId == flat.FlatId && d.PlugId == "plug-1");
        daily.KwhValue.ShouldBe(0.8m);
    }

    [Fact]
    public async Task ParseAndStoreAsync_OverlappingSecondImport_RecomputesAffectedDayNotAppends()
    {
        var (flat, db) = await SeedFlatAsync();
        var parser = MakeParser(db);

        using (var stream1 = BuildEveHomeXlsx("Gerät: Test", "Raum: Test",
            [
                ("2026-06-20T00:00:00", "500"),
                ("2026-06-20T00:10:00", "300")
            ]))
            await parser.ParseAndStoreAsync(flat.FlatId, "plug-1", stream1, CancellationToken.None);

        using (var stream2 = BuildEveHomeXlsx("Gerät: Test", "Raum: Test",
            [
                ("2026-06-20T00:10:00", "300"),
                ("2026-06-20T00:20:00", "200")
            ]))
            await parser.ParseAndStoreAsync(flat.FlatId, "plug-1", stream2, CancellationToken.None);

        (await db.SmartPlugIntervalData.CountAsync(d => d.FlatId == flat.FlatId && d.PlugId == "plug-1")).ShouldBe(3);
        var daily = await db.SmartPlugDailyData.SingleAsync(d => d.FlatId == flat.FlatId && d.PlugId == "plug-1");
        daily.KwhValue.ShouldBe(1.0m);
    }
}
