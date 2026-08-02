import { Zap, Recycle, AlertTriangle, Receipt, ArrowUp, ArrowDown, X, RotateCcw } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import type { InsightDto } from '@/features/insights/api/insightsApi'

type Props = {
  insight: InsightDto
  view: 'active' | 'dismissed'
  onDismiss: (insightId: string, rowVersion: string) => void
  onReactivate: (insightId: string, rowVersion: string) => void
  actionDisabled: boolean
}

const formatKwh = (value: number) =>
  new Intl.NumberFormat(i18n.language, { maximumFractionDigits: 1 }).format(value)

const formatCurrency = (value: number) =>
  new Intl.NumberFormat(i18n.language, { style: 'currency', currency: 'EUR' }).format(value)

const ACCENT_CLASS: Record<InsightDto['type'], string> = {
  Standby: 'border-accent-spike',
  Replacement: 'border-accent-under-budget',
  Budget: 'border-accent-error',
  InvoiceDeviation: 'border-accent-info',
}

const ICON_CLASS: Record<InsightDto['type'], string> = {
  Standby: 'text-accent-spike',
  Replacement: 'text-accent-under-budget',
  Budget: 'text-accent-error',
  InvoiceDeviation: 'text-accent-info',
}

function InsightIcon({ type }: { type: InsightDto['type'] }) {
  const className = ICON_CLASS[type]
  switch (type) {
    case 'Standby':
      return <Zap className={className} size={20} aria-hidden="true" />
    case 'Replacement':
      return <Recycle className={className} size={20} aria-hidden="true" />
    case 'Budget':
      return <AlertTriangle className={className} size={20} aria-hidden="true" />
    case 'InvoiceDeviation':
      return <Receipt className={className} size={20} aria-hidden="true" />
  }
}

export function InsightCard({ insight, view, onDismiss, onReactivate, actionDisabled }: Props) {
  const { t } = useTranslation('insights')

  return (
    <div
      className={`rounded-card border border-glass-border bg-glass-surface overflow-hidden border-l-[3px] ${ACCENT_CLASS[insight.type]}`}
    >
      <div className="flex items-start gap-3 px-4 py-3.5">
        <InsightIcon type={insight.type} />
        <div className="min-w-0 flex-1">
          {insight.type === 'Standby' && (
            <>
              <div className="text-body text-white">{insight.data.deviceName}</div>
              <div className="mt-0.5 text-body-sm text-white/70">
                {t('card.standby.watts', { watts: insight.data.meanStandbyWatts })}
              </div>
              <div className="mt-0.5 text-body-sm text-white/55">
                {t('card.standby.cost', { cost: formatCurrency(insight.data.estimatedMonthlyCost) })}
              </div>
            </>
          )}
          {insight.type === 'Replacement' && (
            <>
              <div className="text-body text-white">{insight.data.deviceName}</div>
              <div className="mt-0.5 text-body-sm text-white/70">
                {t('card.replacement.annualCost', { cost: formatCurrency(insight.data.estimatedAnnualCost) })}
              </div>
              <div className="mt-0.5 text-body-sm text-white/55">
                {t('card.replacement.savings', { savings: formatCurrency(insight.data.estimatedSavingsEur) })}
              </div>
              <div className="mt-0.5 text-caption text-white/45">{insight.data.suggestedClass}</div>
            </>
          )}
          {insight.type === 'Budget' && (
            <>
              <div className="text-body-sm text-white/70">
                {t('card.budget.projected', { cost: formatCurrency(insight.data.projectedAnnualCost) })}
              </div>
              <div className="mt-0.5 text-body-sm text-white/70">
                {t('card.budget.planned', { cost: formatCurrency(insight.data.plannedAnnualSpend) })}
              </div>
              <div className="mt-0.5 text-body-sm font-semibold text-accent-error">
                {t('card.budget.overspend', { cost: formatCurrency(insight.data.overspendEur) })}
              </div>
            </>
          )}
          {insight.type === 'InvoiceDeviation' && (
            <>
              <div className="text-body-sm text-white/70">
                {t('card.invoiceDeviation.projected', { kwh: formatKwh(insight.data.projectedAnnualKwh) })}
              </div>
              <div className="mt-0.5 text-body-sm text-white/70">
                {t('card.invoiceDeviation.baseline', { kwh: formatKwh(insight.data.baselineKwh) })}
              </div>
              <div className="mt-0.5 flex items-center gap-1 text-body-sm font-semibold text-white">
                {insight.data.direction === 'above' ? (
                  <ArrowUp className="text-accent-error" size={14} aria-hidden="true" />
                ) : (
                  <ArrowDown className="text-accent-under-budget" size={14} aria-hidden="true" />
                )}
                <span>
                  {t('card.invoiceDeviation.delta', { delta: formatCurrency(Math.abs(insight.data.impliedDeltaEur)) })}
                </span>
                <span>
                  {t(insight.data.direction === 'above' ? 'card.invoiceDeviation.above' : 'card.invoiceDeviation.below')}
                </span>
              </div>
            </>
          )}
        </div>
        {view === 'active' ? (
          <button
            type="button"
            onClick={() => onDismiss(insight.insightId, insight.rowVersion)}
            disabled={actionDisabled}
            aria-label={t('card.dismissLabel')}
            className="min-h-11 min-w-11 flex items-center justify-center rounded-full shrink-0 text-white/50 hover:text-white transition-colors disabled:opacity-40"
          >
            <X className="h-4 w-4" aria-hidden="true" />
          </button>
        ) : (
          <button
            type="button"
            onClick={() => onReactivate(insight.insightId, insight.rowVersion)}
            disabled={actionDisabled}
            aria-label={t('card.reactivateLabel')}
            className="min-h-11 min-w-11 flex items-center justify-center rounded-full shrink-0 text-white/50 hover:text-white transition-colors disabled:opacity-40"
          >
            <RotateCcw className="h-4 w-4" aria-hidden="true" />
          </button>
        )}
      </div>
    </div>
  )
}
