import { apiClient } from '@/lib/apiClient'

export type TariffResponse = {
  tariffId: string
  effectiveDate: string
  pricePerKwh: number
  monthlyBaseFee: number
  providerName: string | null
  contractStartDate: string | null
  contractDurationMonths: number | null
  isLocked: boolean
}

export type CreateTariffRequest = {
  effectiveDate: string
  pricePerKwh: number
  monthlyBaseFee: number
  providerName?: string
  contractStartDate?: string
  contractDurationMonths?: number
}

export const getTariffs = (flatId: string) =>
  apiClient.get<TariffResponse[]>(`/flats/${flatId}/tariffs`)

export const createTariff = (flatId: string, body: CreateTariffRequest) =>
  apiClient.post<TariffResponse>(`/flats/${flatId}/tariffs`, body)
