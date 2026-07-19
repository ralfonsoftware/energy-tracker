import { apiClient } from '@/lib/apiClient'

export type PeriodRange = { startDate: string; endDate: string }

export type ResidualItem = { kwh: number; cost: number }

export type SubDeviceDecomposition = {
  deviceId: string
  name: string
  kwh: number
  cost: number
  isConfigured: boolean
  isUnconfigured: boolean
}

export type DeviceDecomposition = {
  deviceId: string
  powerPointId: string
  name: string
  kwh: number
  cost: number
  approach: 'Measured' | 'EuLabel' | 'SelfMeasured' | 'None'
  isSmartStrip: boolean
  subDevices: SubDeviceDecomposition[] | null
}

export type RoomDecomposition = {
  roomId: string
  roomName: string
  kwh: number
  cost: number
  devices: DeviceDecomposition[]
}

export type DecompositionResponse = {
  period: PeriodRange
  totalKwh: number
  totalCost: number
  isUnavailable: boolean
  hasInterpolatedData: boolean
  residual: ResidualItem
  rooms: RoomDecomposition[]
}

export const getDecomposition = (flatId: string, startDate: string, endDate: string) =>
  apiClient.get<DecompositionResponse>(
    `/flats/${flatId}/decomposition?startDate=${startDate}&endDate=${endDate}`
  )
