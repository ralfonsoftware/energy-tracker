using EnergyTracker.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace api.Tests.Integration;

// MeterReading.KwhValue is decimal(18,4) in production SQL Server. SQLite does not enforce
// column scale natively (Microsoft Learn: SQLite EF Core Provider Limitations), so
// SqliteAppDbContext applies a rounding value converter (see OnModelCreating) to make this
// test meaningful — it exercises the converter, not a SQLite-native constraint.
public class DecimalPrecisionTruncationTests : SqliteIntegrationTestBase
{
    [Fact]
    public async Task MeterReading_KwhValueWithExcessDecimalPlaces_IsRoundedToFourDecimalPlacesOnReadBack()
    {
        var flatId = Guid.NewGuid();
        var readingId = Guid.NewGuid();

        using (var db = CreateContext())
        {
            db.Users.Add(new User { UserId = "user-decimal-test" });
            db.Flats.Add(new Flat
            {
                FlatId = flatId,
                UserId = "user-decimal-test",
                Name = "Decimal Test Flat",
                AnnualKwhBaseline = 3500m,
                SpikeThreshold = 2.0m
            });
            db.MeterReadings.Add(new MeterReading
            {
                ReadingId = readingId,
                FlatId = flatId,
                KwhValue = 123.456789m,
                ReadingDate = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var verifyDb = CreateContext();
        var reading = await verifyDb.MeterReadings.SingleAsync(r => r.ReadingId == readingId);

        reading.KwhValue.ShouldBe(123.4568m);
    }
}
