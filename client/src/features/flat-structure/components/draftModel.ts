import type {
  ConsumptionApproach,
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
  powerPoints: DraftPowerPoint[]
}

export function toDraftRooms(rooms: RoomResponse[]): DraftRoom[] {
  return rooms.map(room => ({
    key: crypto.randomUUID(),
    name: room.name,
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

export function toUpdateRequest(rooms: DraftRoom[]): UpdateFlatStructureRequest {
  return {
    rooms: rooms.map((room, index) => ({
      name: room.name,
      sortOrder: index,
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
    })),
  }
}

export function findPlugIdConflict(rooms: DraftRoom[]): boolean {
  const plugIds = rooms
    .flatMap(room => room.powerPoints)
    .map(powerPoint => powerPoint.plugId.trim())
    .filter(plugId => plugId !== '')
  return new Set(plugIds).size !== plugIds.length
}

export function hasBlankName(rooms: DraftRoom[]): boolean {
  return rooms.some(
    room => room.name.trim() === '' || room.powerPoints.some(pp => pp.name.trim() === '')
  )
}
