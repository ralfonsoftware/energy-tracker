import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import type { CostSummary, DashboardSummary } from '@/features/dashboard/api/dashboardApi'
import { KpiTile } from './KpiTile'
import { CostGapBadge } from './CostGapBadge'

type Props = {
  dashboard: DashboardSummary | undefined
  annualKwhBaseline: number | undefined
}

const formatNumber = (value: number, maximumFractionDigits = 1) =>
  new Intl.NumberFormat(i18n.language, { maximumFractionDigits }).format(value)

const formatKwh = (value: number) => `${formatNumber(value)} kWh`

const formatCurrency = (value: number) =>
  new Intl.NumberFormat(i18n.language, { style: 'currency', currency: 'EUR' }).format(value)

const formatLastReadDate = (isoDate: string) => {
  const date = new Date(isoDate)
  if (Number.isNaN(date.getTime())) return '—'
  return new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(date)
}

function resolveCostDisplay(cost: CostSummary | null, amount: number): ReactNode {
  if (cost === null) return '—'
  if (!cost.costDetailAvailable) {
    return (
      <span className="inline-flex items-center gap-1.5">
        <span>—</span>
        <CostGapBadge coveredDays={cost.coveredDays} totalDays={cost.totalDays} />
      </span>
    )
  }
  if (cost.hasCostGap) {
    return (
      <span className="inline-flex items-center gap-1.5">
        <span>{formatCurrency(amount)}</span>
        <CostGapBadge coveredDays={cost.coveredDays} totalDays={cost.totalDays} />
      </span>
    )
  }
  return formatCurrency(amount)
}

export function DashboardGrid({ dashboard, annualKwhBaseline }: Props) {
  const { t } = useTranslation('dashboard')

  const isColdOpen = dashboard !== undefined && dashboard.lastReadingDate === null

  let deltaText: string | undefined
  let deltaVariant: 'under' | 'over' | 'neutral' = 'neutral'
  if (dashboard !== undefined && !isColdOpen) {
    const dailyDiff = dashboard.dailyAvgKwh - dashboard.dailyBudgetKwh
    if (dailyDiff < 0) {
      deltaVariant = 'under'
      deltaText = t('tile.underBudget', { amount: formatNumber(Math.abs(dailyDiff)) })
    } else if (dailyDiff > 0) {
      deltaVariant = 'over'
      deltaText = t('tile.overBudget', { amount: formatNumber(dailyDiff) })
    } else {
      deltaVariant = 'neutral'
      deltaText = t('tile.atBudget')
    }
  }

  const lastReadText =
    dashboard === undefined
      ? undefined
      : dashboard.lastReadingDate === null
        ? t('lastRead.never')
        : formatLastReadDate(dashboard.lastReadingDate)

  return (
    <div className="relative z-10 px-4 pt-6 pb-8">
      <div className="grid grid-cols-2 gap-3 md:grid-cols-4">
        <KpiTile
          label={t('tile.dailyAvg')}
          headline={
            dashboard === undefined ? undefined : isColdOpen ? '—' : formatKwh(dashboard.dailyAvgKwh)
          }
          subline={
            dashboard === undefined || isColdOpen
              ? undefined
              : resolveCostDisplay(dashboard.cost, dashboard.cost?.dailyAvgCost ?? 0)
          }
          delta={deltaText}
          deltaVariant={deltaVariant}
          caption={
            dashboard === undefined || isColdOpen || annualKwhBaseline === undefined
              ? undefined
              : t('tile.budgetCaption', { baseline: formatNumber(annualKwhBaseline) })
          }
        />
        <KpiTile
          label={t('tile.weeklyAvg')}
          headline={
            dashboard === undefined ? undefined : isColdOpen ? '—' : formatKwh(dashboard.weeklyAvgKwh)
          }
          subline={
            dashboard === undefined || isColdOpen
              ? undefined
              : resolveCostDisplay(dashboard.cost, dashboard.cost?.weeklyAvgCost ?? 0)
          }
        />
        <KpiTile
          label={t('tile.projectedMonthly')}
          headline={
            dashboard === undefined
              ? undefined
              : isColdOpen
                ? '—'
                : resolveCostDisplay(dashboard.cost, dashboard.cost?.projectedMonthlyCost ?? 0)
          }
        />
        <KpiTile
          label={t('tile.today')}
          headline={
            dashboard === undefined ? undefined : isColdOpen ? '—' : formatKwh(dashboard.todayKwh)
          }
          subline={
            dashboard === undefined || isColdOpen
              ? undefined
              : t('tile.todayVsBudget', { budget: formatNumber(dashboard.dailyBudgetKwh) })
          }
        />
      </div>
      <p className="mt-4 px-1 text-caption text-text-tertiary">
        {dashboard === undefined ? ' ' : `${t('lastRead.label')} ${lastReadText}`}
      </p>
    </div>
  )
}
