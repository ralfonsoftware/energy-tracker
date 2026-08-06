import type {
  ConsumptionApproach,
  DeviceResponse,
  RoomInput,
  RoomResponse,
  SelfMeasuredPeriod,
  UpdateFlatStructureRequest,
} from '@/features/flat-structure/api/flatStructureApi'

export type DraftDevice = {
  key: string
  // Absent = new device, client-added, never persisted. Present = the
  // server-side Device id, round-tripped back on save so the backend can
  // preserve its identity (and DeviceAssignmentPeriod history) across saves.
  deviceId?: string
  // Concurrency token for the update/delete endpoints; absent for a
  // never-persisted device (create doesn't need one).
  rowVersion?: string
  name: string
  type: string
  manufacturer: string
  model: string
  // Preserved verbatim from the server for existing devices — this story's UI
  // never edits these; only newly-added devices default to 'None'/undefined.
  consumptionApproach: ConsumptionApproach
  purchaseDate?: string
  inUseSince?: string
  decommissionedDate?: string
  euLabelClass?: string
  euAnnualKwh?: number
  selfMeasuredKwh?: number
  selfMeasuredPeriod?: SelfMeasuredPeriod
}

export type DraftPowerPoint = {
  key: string
  name: string
  plugId: string
  devices: DraftDevice[]
  // Absent = new power point, client-added, never persisted. Present = the
  // server-side PowerPoint id, used both for deep-link view targeting and
  // round-tripped back on save so the backend can preserve its identity.
  powerPointId?: string
}

export type DraftRoom = {
  key: string
  // Absent = new room, client-added, never persisted. Present = the
  // server-side Room id, round-tripped back on save so the backend can
  // preserve its identity.
  roomId?: string
  name: string
  // Absent = new room, never persisted (always dirty). Present = existing
  // room; dirty only when `name` differs from this last-saved value.
  originalName?: string
  powerPoints: DraftPowerPoint[]
}

export function toDraftDevice(device: DeviceResponse, key: string): DraftDevice {
  return {
    key,
    deviceId: device.deviceId,
    rowVersion: device.rowVersion,
    name: device.name,
    type: device.type ?? '',
    manufacturer: device.manufacturer ?? '',
    model: device.model ?? '',
    consumptionApproach: device.consumptionApproach,
    purchaseDate: device.purchaseDate ?? undefined,
    inUseSince: device.inUseSince ?? undefined,
    decommissionedDate: device.decommissionedDate ?? undefined,
    euLabelClass: device.euLabelClass ?? undefined,
    euAnnualKwh: device.euAnnualKwh ?? undefined,
    selfMeasuredKwh: device.selfMeasuredKwh ?? undefined,
    selfMeasuredPeriod: device.selfMeasuredPeriod ?? undefined,
  }
}

export function toDraftRooms(rooms: RoomResponse[]): DraftRoom[] {
  return rooms.map(room => ({
    key: crypto.randomUUID(),
    roomId: room.roomId,
    name: room.name,
    originalName: room.name.trim(),
    powerPoints: room.powerPoints.map(powerPoint => ({
      key: crypto.randomUUID(),
      name: powerPoint.name,
      plugId: powerPoint.plugId ?? '',
      powerPointId: powerPoint.powerPointId,
      devices: powerPoint.devices.map(device => toDraftDevice(device, crypto.randomUUID())),
    })),
  }))
}

export function createDefaultDraftRooms(t: (key: string) => string): DraftRoom[] {
  return [
    t('defaultRooms.livingRoom'),
    t('defaultRooms.bedroom'),
    t('defaultRooms.kitchen'),
    t('defaultRooms.bathroom'),
    t('defaultRooms.hallway'),
  ].map(name => ({ key: crypto.randomUUID(), name, powerPoints: [] }))
}

export function toRoomInput(room: DraftRoom, name: string): RoomInput {
  return {
    roomId: room.roomId,
    name,
    sortOrder: 0,
    powerPoints: room.powerPoints.map(powerPoint => ({
      powerPointId: powerPoint.powerPointId,
      name: powerPoint.name,
      plugId: powerPoint.plugId.trim() || undefined,
    })),
  }
}

export function toUpdateRequest(rooms: DraftRoom[], rowVersion: string): UpdateFlatStructureRequest {
  return {
    rooms: rooms.map((room, index) => ({ ...toRoomInput(room, room.name), sortOrder: index })),
    rowVersion,
  }
}

// Tracks each room's own last-saved wire-shape snapshot alongside the
// DraftRoom `key` it corresponds to, so per-room saves/deletes can look a
// room up by identity instead of by array position — positions drift
// whenever never-saved rooms are saved/deleted out of insertion order.
export type KeyedRoomInput = {
  key: string
  room: RoomInput
}

export function toKeyedRooms(rooms: DraftRoom[]): KeyedRoomInput[] {
  return rooms.map(room => ({ key: room.key, room: toRoomInput(room, room.name) }))
}

export function toWireRequest(keyedRooms: KeyedRoomInput[], rowVersion: string): UpdateFlatStructureRequest {
  return {
    rooms: keyedRooms.map(({ room }, index) => ({ ...room, sortOrder: index })),
    rowVersion,
  }
}

export function withRoomAppended(
  base: KeyedRoomInput[],
  key: string,
  room: RoomInput
): KeyedRoomInput[] {
  return [...base, { key, room }]
}

export function withRoomUpdated(
  base: KeyedRoomInput[],
  key: string,
  room: RoomInput
): KeyedRoomInput[] {
  return base.map(entry => (entry.key === key ? { key, room } : entry))
}

export function withRoomRemoved(base: KeyedRoomInput[], key: string): KeyedRoomInput[] {
  return base.filter(entry => entry.key !== key)
}

export function findPlugIdConflict(rooms: DraftRoom[]): boolean {
  const plugIds = rooms
    .flatMap(room => room.powerPoints)
    .map(powerPoint => powerPoint.plugId.trim())
    .filter(plugId => plugId !== '')
  return new Set(plugIds).size !== plugIds.length
}

export function hasBlankNameInRoom(room: DraftRoom): boolean {
  return room.name.trim() === '' || room.powerPoints.some(pp => pp.name.trim() === '')
}

export function hasBlankName(rooms: DraftRoom[]): boolean {
  return rooms.some(hasBlankNameInRoom)
}

export function isRoomDirty(room: DraftRoom, lastSaved: KeyedRoomInput[]): boolean {
  if (room.originalName === undefined) return true
  const savedEntry = lastSaved.find(entry => entry.key === room.key)
  if (!savedEntry) return true
  return (
    JSON.stringify(toRoomInput(room, room.name.trim())) !== JSON.stringify(savedEntry.room)
  )
}

export function hasPlugIdConflictForRoomSave(room: DraftRoom, lastSaved: KeyedRoomInput[]): boolean {
  const ownPlugIds = room.powerPoints
    .map(pp => (pp.plugId ?? '').trim())
    .filter(plugId => plugId !== '')
  if (new Set(ownPlugIds).size !== ownPlugIds.length) return true

  const otherSavedPlugIds = new Set(
    lastSaved
      .filter(entry => entry.key !== room.key)
      .flatMap(entry => entry.room.powerPoints)
      .map(pp => (pp.plugId ?? '').trim())
      .filter(plugId => plugId !== '')
  )
  return ownPlugIds.some(plugId => otherSavedPlugIds.has(plugId))
}
