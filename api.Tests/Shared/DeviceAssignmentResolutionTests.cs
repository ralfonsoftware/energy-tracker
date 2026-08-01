using EnergyTracker.Api.Data.Entities;
using EnergyTracker.Api.Shared;
using Shouldly;

namespace api.Tests.Shared;

public class DeviceAssignmentResolutionTests
{
    private static DeviceAssignmentPeriod MakePeriod(
        DateTimeOffset from,
        DateTimeOffset? to = null,
        Guid? id = null,
        Guid? powerPointId = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            PowerPointId = powerPointId ?? Guid.NewGuid(),
            FlatId = Guid.NewGuid(),
            From = from,
            To = to
        };

    [Fact]
    public void Resolve_NullPeriods_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => DeviceAssignmentResolution.Resolve(null!, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Resolve_NoPeriods_ReturnsNull()
    {
        var result = DeviceAssignmentResolution.Resolve([], DateTimeOffset.UtcNow);

        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_SingleOpenPeriod_CoversAnyDateOnOrAfterFrom()
    {
        var powerPointId = Guid.NewGuid();
        var period = MakePeriod(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), powerPointId: powerPointId);
        var date = new DateTimeOffset(2027, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var result = DeviceAssignmentResolution.Resolve([period], date);

        result.ShouldBe(powerPointId);
    }

    [Fact]
    public void Resolve_TwoPeriods_MidPeriodDateResolvesToCorrectOne()
    {
        var firstPowerPointId = Guid.NewGuid();
        var secondPowerPointId = Guid.NewGuid();
        var first = MakePeriod(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            powerPointId: firstPowerPointId);
        var second = MakePeriod(
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            powerPointId: secondPowerPointId);
        var date = new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero);

        var result = DeviceAssignmentResolution.Resolve([first, second], date);

        result.ShouldBe(firstPowerPointId);
    }

    [Fact]
    public void Resolve_DateBeforeEarliestFrom_ReturnsNull()
    {
        var period = MakePeriod(new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
        var date = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var result = DeviceAssignmentResolution.Resolve([period], date);

        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_DateExactlyOnFromBoundary_ResolvesToThatPeriod_NotThePrevious()
    {
        var earlierPowerPointId = Guid.NewGuid();
        var laterPowerPointId = Guid.NewGuid();
        var earlier = MakePeriod(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            powerPointId: earlierPowerPointId);
        var later = MakePeriod(
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            powerPointId: laterPowerPointId);
        var date = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var result = DeviceAssignmentResolution.Resolve([earlier, later], date);

        result.ShouldBe(laterPowerPointId);
    }

    [Fact]
    public void Resolve_ClosedPeriodWithDateAfterTo_DoesNotMatch()
    {
        var period = MakePeriod(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero));
        var date = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var result = DeviceAssignmentResolution.Resolve([period], date);

        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_TieOnFrom_ResolvesDeterministicallyByHigherId()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var lowerPowerPointId = Guid.NewGuid();
        var higherPowerPointId = Guid.NewGuid();
        var lowerId = MakePeriod(
            start,
            id: Guid.Parse("00000000-0000-0000-0000-000000000001"),
            powerPointId: lowerPowerPointId);
        var higherId = MakePeriod(
            start,
            id: Guid.Parse("00000000-0000-0000-0000-000000000002"),
            powerPointId: higherPowerPointId);
        var date = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var resultAscending = DeviceAssignmentResolution.Resolve([lowerId, higherId], date);
        var resultDescending = DeviceAssignmentResolution.Resolve([higherId, lowerId], date);

        resultAscending.ShouldBe(higherPowerPointId);
        resultDescending.ShouldBe(higherPowerPointId);
    }
}
