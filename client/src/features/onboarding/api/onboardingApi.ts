import { apiClient } from '@/lib/apiClient'

export interface CompleteOnboardingPayload {
  flatName: string
  annualKwhBaseline: number
  plannedAnnualSpend: number | null
  pricePerKwh: number
  monthlyBaseFee: number
  providerName?: string
  contractStartDate?: string
  contractDurationMonths?: number | null
}

export const completeOnboarding = (payload: CompleteOnboardingPayload) =>
  apiClient.post<void>('/onboarding', payload)
