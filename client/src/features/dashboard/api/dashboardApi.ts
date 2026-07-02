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

export type DailyConsumptionPoint = { date: string; kwhValue: number }

export type DashboardSummary = {
  dailyAvgKwh: number
  weeklyAvgKwh: number
  todayKwh: number
  dailyBudgetKwh: number
  lastReadingDate: string | null
  spikeDays: string[]
  cost: CostSummary | null
  lastKwhValue: number | null
  dailyConsumption: DailyConsumptionPoint[]
}

export const getDashboard = (flatId: string) =>
  apiClient.get<DashboardSummary>(`/flats/${flatId}/dashboard`)
