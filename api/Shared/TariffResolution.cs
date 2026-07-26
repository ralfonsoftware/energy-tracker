using EnergyTracker.Api.Data.Entities;

namespace EnergyTracker.Api.Shared;

public static class TariffResolution
{
    /// <summary>
    /// Returns the tariff active on <paramref name="date"/> — the one with the latest
    /// <see cref="Tariff.ContractStartDate"/> on or before <paramref name="date"/> (inclusive), or
    /// <see langword="null"/> if none is active yet. When two tariffs share the same
    /// <see cref="Tariff.ContractStartDate"/>, the one with the higher <see cref="Tariff.TariffId"/>
    /// wins, so the result is deterministic regardless of the input list's enumeration order.
    /// </summary>
    public static Tariff? Resolve(IReadOnlyList<Tariff> tariffs, DateTimeOffset date)
    {
        ArgumentNullException.ThrowIfNull(tariffs);

        Tariff? best = null;
        foreach (var t in tariffs)
        {
            if (t.ContractStartDate > date) continue;
            if (best is null
                || t.ContractStartDate > best.ContractStartDate
                || (t.ContractStartDate == best.ContractStartDate && t.TariffId.CompareTo(best.TariffId) > 0))
                best = t;
        }
        return best;
    }
}
