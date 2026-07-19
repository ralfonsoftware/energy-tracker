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
  consumptionApproach: ConsumptionApproach
  euLabelClass: string | null
  euAnnualKwh: number | null
  selfMeasuredKwh: number | null
  selfMeasuredPeriod: SelfMeasuredPeriod
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

export type DeviceInput = {
  name: string
  type?: string
  manufacturer?: string
  model?: string
  purchaseDate?: string
  consumptionApproach: ConsumptionApproach
  euLabelClass?: string
  euAnnualKwh?: number
  selfMeasuredKwh?: number
  selfMeasuredPeriod?: SelfMeasuredPeriod
}

export type PowerPointInput = {
  name: string
  plugId?: string
  devices: DeviceInput[]
}

export type RoomInput = {
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
