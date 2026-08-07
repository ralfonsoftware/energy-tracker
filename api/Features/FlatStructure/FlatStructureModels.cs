using EnergyTracker.Api.Data.Entities;

namespace EnergyTracker.Api.Features.FlatStructure;

public record DeviceResponse(
    Guid DeviceId,
    string Name,
    string? Type,
    string? Manufacturer,
    string? Model,
    DateTimeOffset? PurchaseDate,
    DateTimeOffset? InUseSince,
    DateTimeOffset? DecommissionedDate,
    ConsumptionApproach ConsumptionApproach,
    string? EuLabelClass,
    decimal? EuAnnualKwh,
    decimal? SelfMeasuredKwh,
    SelfMeasuredPeriod? SelfMeasuredPeriod,
    byte[] RowVersion);

public record PowerPointResponse(
    Guid PowerPointId,
    string Name,
    string? PlugId,
    List<DeviceResponse> Devices,
    byte[] RowVersion);

public record RoomResponse(
    Guid RoomId,
    string Name,
    int SortOrder,
    List<PowerPointResponse> PowerPoints,
    byte[] RowVersion);

public record FlatStructureResponse(
    Guid FlatId,
    bool HasDefaultTemplate,
    List<RoomResponse> Rooms,
    byte[] RowVersion);

public record DeviceInput(
    Guid? DeviceId,
    string Name,
    string? Type,
    string? Manufacturer,
    string? Model,
    DateTimeOffset? PurchaseDate,
    DateTimeOffset? InUseSince,
    DateTimeOffset? DecommissionedDate,
    ConsumptionApproach ConsumptionApproach,
    string? EuLabelClass,
    decimal? EuAnnualKwh,
    decimal? SelfMeasuredKwh,
    SelfMeasuredPeriod? SelfMeasuredPeriod,
    string? RowVersion = null);

public record PowerPointInput(
    Guid? PowerPointId,
    string Name,
    string? PlugId);

public record CreateRoomRequest(
    string Name,
    int SortOrder,
    List<PowerPointInput> PowerPoints);

public record UpdateRoomRequest(
    string Name,
    int SortOrder,
    List<PowerPointInput> PowerPoints,
    byte[] RowVersion);
