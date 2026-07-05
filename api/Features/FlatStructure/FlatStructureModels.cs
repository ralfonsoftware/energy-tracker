using EnergyTracker.Api.Data.Entities;

namespace EnergyTracker.Api.Features.FlatStructure;

public record DeviceResponse(
    Guid DeviceId,
    string Name,
    string? Type,
    string? Manufacturer,
    string? Model,
    DateTimeOffset? PurchaseDate,
    ConsumptionApproach ConsumptionApproach,
    string? EuLabelClass,
    decimal? EuAnnualKwh,
    decimal? SelfMeasuredKwh,
    SelfMeasuredPeriod? SelfMeasuredPeriod);

public record PowerPointResponse(
    Guid PowerPointId,
    string Name,
    string? PlugId,
    List<DeviceResponse> Devices);

public record RoomResponse(
    Guid RoomId,
    string Name,
    int SortOrder,
    List<PowerPointResponse> PowerPoints);

public record FlatStructureResponse(
    Guid FlatId,
    bool HasDefaultTemplate,
    List<RoomResponse> Rooms);

public record DeviceInput(
    string Name,
    string? Type,
    string? Manufacturer,
    string? Model,
    DateTimeOffset? PurchaseDate,
    ConsumptionApproach ConsumptionApproach,
    string? EuLabelClass,
    decimal? EuAnnualKwh,
    decimal? SelfMeasuredKwh,
    SelfMeasuredPeriod? SelfMeasuredPeriod);

public record PowerPointInput(
    string Name,
    string? PlugId,
    List<DeviceInput> Devices);

public record RoomInput(
    string Name,
    int SortOrder,
    List<PowerPointInput> PowerPoints);

public record UpdateFlatStructureRequest(List<RoomInput> Rooms);
