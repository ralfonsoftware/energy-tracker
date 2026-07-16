import type {
  ConsumptionApproach,
  RoomInput,
  RoomResponse,
  SelfMeasuredPeriod,
  UpdateFlatStructureRequest,
} from '@/features/flat-structure/api/flatStructureApi'

export type DraftDevice = {
  key: string
  name: string
  type: string
  manufacturer: string
  model: string
  // Preserved verbatim from the server for existing devices — this story's UI
  // never edits these; only newly-added devices default to 'None'/undefined.
  consumptionApproach: ConsumptionApproach
  purchaseDate?: string
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
}

export type DraftRoom = {
  key: string
  name: string
  // Absent = new room, never persisted (always dirty). Present = existing
  // room; dirty only when `name` differs from this last-saved value.
  originalName?: string
  powerPoints: DraftPowerPoint[]
}

export function toDraftRooms(rooms: RoomResponse[]): DraftRoom[] {
  return rooms.map(room => ({
    key: crypto.randomUUID(),
    name: room.name,
    originalName: room.name.trim(),
    powerPoints: room.powerPoints.map(powerPoint => ({
      key: crypto.randomUUID(),
      name: powerPoint.name,
      plugId: powerPoint.plugId ?? '',
      devices: powerPoint.devices.map(device => ({
        key: crypto.randomUUID(),
        name: device.name,
        type: device.type ?? '',
        manufacturer: device.manufacturer ?? '',
        model: device.model ?? '',
        consumptionApproach: device.consumptionApproach,
        purchaseDate: device.purchaseDate ?? undefined,
        euLabelClass: device.euLabelClass ?? undefined,
        euAnnualKwh: device.euAnnualKwh ?? undefined,
        selfMeasuredKwh: device.selfMeasuredKwh ?? undefined,
        selfMeasuredPeriod: device.selfMeasuredPeriod ?? undefined,
      })),
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
    name,
    sortOrder: 0,
    powerPoints: room.powerPoints.map(powerPoint => ({
      name: powerPoint.name,
      plugId: powerPoint.plugId.trim() || undefined,
      devices: powerPoint.devices.map(device => ({
        name: device.name,
        type: device.type.trim() || undefined,
        manufacturer: device.manufacturer.trim() || undefined,
        model: device.model.trim() || undefined,
        purchaseDate: device.purchaseDate,
        consumptionApproach: device.consumptionApproach,
        euLabelClass: device.euLabelClass,
        euAnnualKwh: device.euAnnualKwh,
        selfMeasuredKwh: device.selfMeasuredKwh,
        selfMeasuredPeriod: device.selfMeasuredPeriod,
      })),
    })),
  }
}

export function toUpdateRequest(rooms: DraftRoom[]): UpdateFlatStructureRequest {
  return {
    rooms: rooms.map((room, index) => ({ ...toRoomInput(room, room.name), sortOrder: index })),
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

export function toWireRequest(keyedRooms: KeyedRoomInput[]): UpdateFlatStructureRequest {
  return {
    rooms: keyedRooms.map(({ room }, index) => ({ ...room, sortOrder: index })),
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
