import { apiClient } from '@/lib/apiClient'

export type ConsumptionApproach = 'None' | 'EuLabel' | 'SelfMeasured'
export type SelfMeasuredPeriod = 'Daily' | 'Weekly' | null

export type DeviceResponse = {
  deviceId: string
  name: string
  type: string | null
  manufacturer: string | null
  model: string | null
  purchaseDate: string | null
  inUseSince: string | null
  decommissionedDate: string | null
  consumptionApproach: ConsumptionApproach
  euLabelClass: string | null
  euAnnualKwh: number | null
  selfMeasuredKwh: number | null
  selfMeasuredPeriod: SelfMeasuredPeriod
  rowVersion: string
}

export type PowerPointResponse = {
  powerPointId: string
  name: string
  plugId: string | null
  devices: DeviceResponse[]
}

export type RoomResponse = {
  roomId: string
  name: string
  sortOrder: number
  powerPoints: PowerPointResponse[]
}

export type FlatStructureResponse = {
  flatId: string
  hasDefaultTemplate: boolean
  rooms: RoomResponse[]
  rowVersion: string
}

export type PowerPointInput = {
  powerPointId?: string
  name: string
  plugId?: string
}

export type RoomInput = {
  roomId?: string
  name: string
  sortOrder: number
  powerPoints: PowerPointInput[]
}

export type UpdateFlatStructureRequest = {
  rooms: RoomInput[]
  rowVersion: string
}

export const getFlatStructure = (flatId: string) =>
  apiClient.get<FlatStructureResponse>(`/flats/${flatId}/structure`)

export const updateFlatStructure = (flatId: string, body: UpdateFlatStructureRequest) =>
  apiClient.put<FlatStructureResponse>(`/flats/${flatId}/structure`, body)

export type DeviceWriteInput = {
  name: string
  type?: string
  manufacturer?: string
  model?: string
  purchaseDate?: string
  inUseSince?: string
  decommissionedDate?: string
  consumptionApproach: ConsumptionApproach
  euLabelClass?: string
  euAnnualKwh?: number
  selfMeasuredKwh?: number
  selfMeasuredPeriod?: SelfMeasuredPeriod
}

export type UpdateDeviceInput = DeviceWriteInput & { rowVersion: string }

export const createDevice = (flatId: string, powerPointId: string, body: DeviceWriteInput) =>
  apiClient.post<DeviceResponse>(`/flats/${flatId}/powerpoints/${powerPointId}/devices`, body)

export const updateDevice = (flatId: string, powerPointId: string, deviceId: string, body: UpdateDeviceInput) =>
  apiClient.put<DeviceResponse>(`/flats/${flatId}/powerpoints/${powerPointId}/devices/${deviceId}`, body)

export const deleteDevice = (flatId: string, powerPointId: string, deviceId: string, rowVersion: string) =>
  apiClient.delete<void>(`/flats/${flatId}/powerpoints/${powerPointId}/devices/${deviceId}`, { rowVersion })
