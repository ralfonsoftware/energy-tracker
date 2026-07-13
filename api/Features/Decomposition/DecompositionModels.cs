namespace EnergyTracker.Api.Features.Decomposition;

public enum AttributionApproach { Measured, EuLabel, SelfMeasured, None }

public record PeriodRange(DateOnly StartDate, DateOnly EndDate);

public record ResidualItem(decimal Kwh, decimal Cost);

public record SubDeviceDecomposition(
    Guid DeviceId, string Name, decimal Kwh, decimal Cost, bool IsConfigured, bool IsUnconfigured);

public record DeviceDecomposition(
    Guid DeviceId, string Name, decimal Kwh, decimal Cost,
    AttributionApproach Approach, bool IsSmartStrip,
    IReadOnlyList<SubDeviceDecomposition>? SubDevices);

public record RoomDecomposition(
    Guid RoomId, string RoomName, decimal Kwh, decimal Cost, IReadOnlyList<DeviceDecomposition> Devices);

public record DecompositionResponse(
    PeriodRange Period, decimal TotalKwh, decimal TotalCost,
    bool IsUnavailable, bool HasInterpolatedData,
    ResidualItem Residual, IReadOnlyList<RoomDecomposition> Rooms);
