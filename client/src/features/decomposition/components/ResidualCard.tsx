import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'

type Props = { kwh: number; cost: number; totalKwh: number }

const formatNumber = (value: number) =>
  new Intl.NumberFormat(i18n.language, { maximumFractionDigits: 1 }).format(value)

const formatKwh = (value: number) => `${formatNumber(value)} kWh`

const formatCurrency = (value: number) =>
  new Intl.NumberFormat(i18n.language, { style: 'currency', currency: 'EUR' }).format(value)

export function ResidualCard({ kwh, cost, totalKwh }: Props) {
  const { t } = useTranslation('decomposition')
  const percentOfTotal = totalKwh === 0 ? null : Math.round((kwh / totalKwh) * 100)

  return (
    <div
      className="rounded-card border p-4"
      style={{ background: 'var(--color-residual-tint)', border: '1px solid rgba(251,191,36,0.2)' }}
    >
      <div className="text-body-sm text-white">{t('residual.title')}</div>
      <div className="mt-0.5 text-display-kpi text-white">{formatKwh(kwh)}</div>
      <div className="mt-0.5 text-body-sm text-white/55">
        {formatCurrency(cost)}
        {percentOfTotal !== null && ` · ${percentOfTotal}%`}
      </div>
    </div>
  )
}
