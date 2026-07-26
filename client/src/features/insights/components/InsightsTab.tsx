import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useDashboard } from '@/features/dashboard/hooks/useDashboard'
import { TrendChart } from '@/features/dashboard/components/TrendChart'
import { useInsights } from '@/features/insights/hooks/useInsights'
import { useTriggerInsights } from '@/features/insights/hooks/useTriggerInsights'
import { InsightsPeriodSelector, type InsightsPeriod } from '@/features/insights/components/InsightsPeriodSelector'
import { InsightCard } from '@/features/insights/components/InsightCard'
import { InsightDiscoveryProgress } from '@/features/insights/components/InsightDiscoveryProgress'

type Props = { flatId: string | undefined }

export function InsightsTab({ flatId }: Props) {
  const { t } = useTranslation('insights')
  const [days, setDays] = useState<InsightsPeriod>(30)

  const {
    data: dashboard,
    isPending: isDashboardPending,
    isError: isDashboardError,
    refetch: refetchDashboard,
  } = useDashboard(flatId, days)
  const {
    data: historyDashboard,
    isPending: isHistoryPending,
    isError: isHistoryError,
    refetch: refetchHistory,
  } = useDashboard(flatId, 30)
  const {
    data: insightsData,
    isPending: isInsightsPending,
    isError: isInsightsError,
    refetch: refetchInsights,
  } = useInsights(flatId)
  const triggerInsights = useTriggerInsights(flatId)

  const isPending = isHistoryPending || isInsightsPending
  const isError = isDashboardError || isHistoryError || isInsightsError

  const runStatus = insightsData?.runStatus ?? null
  const insights = insightsData?.insights ?? []
  const isDiscovering = runStatus?.status === 'Pending' || runStatus?.status === 'Processing'
  const readingHistoryDays = historyDashboard?.readingHistoryDays ?? 0

  return (
    <div className="flex flex-col gap-3">
      <TrendChart
        dashboard={isDashboardPending ? undefined : dashboard}
        flatId={flatId}
        days={days}
        headerExtra={<InsightsPeriodSelector value={days} onChange={setDays} />}
      />

      <button
        type="button"
        onClick={() => triggerInsights.mutate()}
        disabled={isDiscovering || triggerInsights.isPending || !flatId}
        className="mx-4 min-h-11 rounded-input border border-white/[0.12] bg-white/[0.07] px-4 py-2.5 text-sm font-medium text-white/85 disabled:opacity-40"
      >
        {t('refreshButton')}
      </button>

      <div className="mx-4 flex flex-col gap-3">
        {isPending && (
          <div className="flex flex-col gap-3">
            <div className="h-[72px] animate-pulse rounded-card border border-glass-border bg-white/10" />
            <div className="h-[72px] animate-pulse rounded-card border border-glass-border bg-white/10" />
          </div>
        )}

        {!isPending && isError && (
          <div>
            <p role="alert" className="text-body-sm text-accent-error">
              {t('loadError')}
            </p>
            <button
              type="button"
              onClick={() => {
                refetchDashboard()
                refetchHistory()
                refetchInsights()
              }}
              className="mt-2 min-h-11 min-w-11 text-body-sm text-text-secondary underline"
            >
              {t('retry')}
            </button>
          </div>
        )}

        {!isPending && !isError && (
          <>
            {isDiscovering && <InsightDiscoveryProgress />}

            {insights.length > 0 ? (
              <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
                {insights.map(insight => (
                  <InsightCard key={insight.insightId} insight={insight} />
                ))}
              </div>
            ) : isDiscovering ? null : runStatus?.status === 'Failed' ? (
              <p className="text-body-sm text-text-secondary">{t('emptyState.runFailed')}</p>
            ) : readingHistoryDays < 30 ? (
              <p className="text-body-sm text-text-secondary">{t('emptyState.insufficientData')}</p>
            ) : (
              <p className="text-body-sm text-text-secondary">{t('emptyState.noFindings')}</p>
            )}
          </>
        )}
      </div>
    </div>
  )
}
