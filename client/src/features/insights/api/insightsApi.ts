import { apiClient } from '@/lib/apiClient'

export type RunStatus = 'Pending' | 'Processing' | 'Complete' | 'Failed'

export type RunStatusDto = {
  status: RunStatus
  startedAt: string
  completedAt: string | null
}

export type StandbyInsightData = {
  deviceName: string
  meanStandbyWatts: number
  estimatedMonthlyKwh: number
  estimatedMonthlyCost: number
}

export type ReplacementInsightData = {
  deviceName: string
  estimatedAnnualKwh: number
  estimatedAnnualCost: number
  suggestedClass: string
  estimatedSavingsEur: number
}

export type BudgetInsightData = {
  projectedAnnualCost: number
  plannedAnnualSpend: number
  overspendEur: number
}

export type InvoiceDeviationInsightData = {
  projectedAnnualKwh: number
  baselineKwh: number
  deviationPct: number
  impliedDeltaEur: number
  direction: 'above' | 'below'
}

type InsightBase = {
  insightId: string
  deviceId: string | null
  createdAt: string
}

export type InsightDto =
  | (InsightBase & { type: 'Standby'; data: StandbyInsightData })
  | (InsightBase & { type: 'Replacement'; data: ReplacementInsightData })
  | (InsightBase & { type: 'Budget'; data: BudgetInsightData })
  | (InsightBase & { type: 'InvoiceDeviation'; data: InvoiceDeviationInsightData })

export type InsightsResponse = {
  runStatus: RunStatusDto | null
  insights: InsightDto[]
}

export type TriggerInsightsResponse = { runId: string }

export const getInsights = (flatId: string) =>
  apiClient.get<InsightsResponse>(`/flats/${flatId}/insights`)

export const triggerInsights = (flatId: string) =>
  apiClient.post<TriggerInsightsResponse>(`/flats/${flatId}/insights/trigger`)
