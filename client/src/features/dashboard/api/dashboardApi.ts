import { apiClient } from '@/lib/apiClient'

export type CostSummary = {
  dailyAvgCost: number
  weeklyAvgCost: number
  projectedMonthlyCost: number
  hasCostGap: boolean
  coveredDays: number
  totalDays: number
  costDetailAvailable: boolean
}

export type DashboardSummary = {
  dailyAvgKwh: number
  weeklyAvgKwh: number
  todayKwh: number
  dailyBudgetKwh: number
  lastReadingDate: string | null
  spikeDays: string[]
  cost: CostSummary | null
}

export const getDashboard = (flatId: string) =>
  apiClient.get<DashboardSummary>(`/flats/${flatId}/dashboard`)
