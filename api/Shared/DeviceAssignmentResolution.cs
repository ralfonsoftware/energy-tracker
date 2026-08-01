using EnergyTracker.Api.Data.Entities;

namespace EnergyTracker.Api.Shared;

public static class DeviceAssignmentResolution
{
    /// <summary>
    /// Returns the <see cref="DeviceAssignmentPeriod.PowerPointId"/> of the period active on
    /// <paramref name="date"/> — the one with the latest <see cref="DeviceAssignmentPeriod.From"/>
    /// on or before <paramref name="date"/> (inclusive) whose <see cref="DeviceAssignmentPeriod.To"/>
    /// is either <see langword="null"/> or on/after <paramref name="date"/>, or <see langword="null"/>
    /// if no period matches. When two periods share the same <see cref="DeviceAssignmentPeriod.From"/>,
    /// the one with the higher <see cref="DeviceAssignmentPeriod.Id"/> wins, so the result is
    /// deterministic regardless of the input list's enumeration order.
    /// </summary>
    public static Guid? Resolve(IReadOnlyList<DeviceAssignmentPeriod> periods, DateTimeOffset date)
    {
        ArgumentNullException.ThrowIfNull(periods);

        DeviceAssignmentPeriod? best = null;
        foreach (var p in periods)
        {
            if (p.From > date) continue;
            if (p.To is not null && p.To < date) continue;
            if (best is null
                || p.From > best.From
                || (p.From == best.From && p.Id.CompareTo(best.Id) > 0))
                best = p;
        }
        return best?.PowerPointId;
    }
}
