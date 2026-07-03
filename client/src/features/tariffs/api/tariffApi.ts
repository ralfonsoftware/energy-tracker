import { apiClient } from '@/lib/apiClient'

export type TariffResponse = {
  tariffId: string
  contractStartDate: string
  pricePerKwh: number
  monthlyBaseFee: number
  providerName: string | null
  contractDurationMonths: number | null
  isLocked: boolean
}

export type CreateTariffRequest = {
  contractStartDate: string
  pricePerKwh: number
  monthlyBaseFee: number
  providerName?: string
  contractDurationMonths?: number
}

export type PatchTariffRequest = {
  pricePerKwh?: number
  monthlyBaseFee?: number
  providerName?: string | null
  contractDurationMonths?: number | null
  lockOverride?: boolean
}

export const getTariffs = (flatId: string) =>
  apiClient.get<TariffResponse[]>(`/flats/${flatId}/tariffs`)

export const createTariff = (flatId: string, body: CreateTariffRequest) =>
  apiClient.post<TariffResponse>(`/flats/${flatId}/tariffs`, body)

export const patchTariff = (flatId: string, tariffId: string, body: PatchTariffRequest) =>
  apiClient.patch<TariffResponse>(`/flats/${flatId}/tariffs/${tariffId}`, body)
