import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import type { DeviceDecomposition } from '@/features/decomposition/api/decompositionApi'

type Props = { device: DeviceDecomposition }

const formatNumber = (value: number) =>
  new Intl.NumberFormat(i18n.language, { maximumFractionDigits: 1 }).format(value)

const formatKwh = (value: number) => `${formatNumber(value)} kWh`

const formatCurrency = (value: number) =>
  new Intl.NumberFormat(i18n.language, { style: 'currency', currency: 'EUR' }).format(value)

const disclaimerKeyByApproach: Record<'EuLabel' | 'SelfMeasured', string> = {
  EuLabel: 'deviceCard.disclaimerEuLabel',
  SelfMeasured: 'deviceCard.disclaimerSelfMeasured',
}

export function DeviceCard({ device }: Props) {
  const { t } = useTranslation('decomposition')

  if (device.approach === 'Measured') {
    return (
      <div className="rounded-card border border-glass-border bg-glass-surface px-4 py-3.5">
        <div className="flex items-center justify-between">
          <span className="text-body text-white">{device.name}</span>
          <span className="text-body-sm font-semibold text-accent-success">
            {t('deviceCard.badgeMeasured')}
          </span>
        </div>
        <div className="mt-0.5 text-body-sm text-white/55">
          {formatKwh(device.kwh)} · {formatCurrency(device.cost)}
        </div>
      </div>
    )
  }

  const disclaimerKey =
    device.approach === 'EuLabel' || device.approach === 'SelfMeasured'
      ? disclaimerKeyByApproach[device.approach]
      : undefined

  return (
    <div className="rounded-card border border-glass-border bg-glass-surface px-3.5 py-2.5">
      <div className="flex items-center justify-between">
        <span className="text-body-sm text-white">{device.name}</span>
        <span className="text-caption font-semibold text-accent-neutral">
          {t('deviceCard.badgeEstimated')}
        </span>
      </div>
      <div className="mt-0.5 text-caption text-white/55">
        {formatKwh(device.kwh)} · {formatCurrency(device.cost)}
      </div>
      {disclaimerKey && <div className="mt-0.5 text-caption text-white/45">{t(disclaimerKey)}</div>}
    </div>
  )
}
