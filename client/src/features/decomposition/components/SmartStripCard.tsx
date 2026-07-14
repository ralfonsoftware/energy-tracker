import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import type {
  DeviceDecomposition,
  SubDeviceDecomposition,
} from '@/features/decomposition/api/decompositionApi'

type Props = { device: DeviceDecomposition; onConfigure: () => void }

const formatNumber = (value: number) =>
  new Intl.NumberFormat(i18n.language, { maximumFractionDigits: 1 }).format(value)

const formatKwh = (value: number) => `${formatNumber(value)} kWh`

const formatCurrency = (value: number) =>
  new Intl.NumberFormat(i18n.language, { style: 'currency', currency: 'EUR' }).format(value)

function sortSubDevices(subDevices: SubDeviceDecomposition[]): SubDeviceDecomposition[] {
  const configured = subDevices.filter(d => d.isConfigured).sort((a, b) => b.kwh - a.kwh)
  const unconfigured = subDevices.filter(d => d.isUnconfigured).sort((a, b) => b.kwh - a.kwh)
  return [...configured, ...unconfigured]
}

export function SmartStripCard({ device, onConfigure }: Props) {
  const { t } = useTranslation('decomposition')
  const subDevices = sortSubDevices(device.subDevices ?? [])

  return (
    <div className="rounded-card border border-glass-border bg-glass-surface overflow-hidden">
      <div className="flex items-center justify-between px-4 py-3.5">
        <span className="text-body text-white">{device.name}</span>
        <div className="flex items-center gap-2">
          <span className="text-body-sm text-white/55">
            {formatKwh(device.kwh)} · {formatCurrency(device.cost)}
          </span>
          <span className="text-body-sm font-semibold text-accent-success">
            {t('deviceCard.badgeMeasured')}
          </span>
        </div>
      </div>
      {subDevices.length > 0 && (
        <div className="flex flex-col gap-1.5 px-4 pb-3.5">
          {subDevices.map(subDevice => (
            <div
              key={subDevice.deviceId}
              className={`flex items-center justify-between ${subDevice.isUnconfigured ? 'opacity-[0.45]' : 'opacity-100'}`}
            >
              <span className="text-caption text-white">{subDevice.name}</span>
              <div className="flex items-center gap-2">
                <span className="text-caption text-white/55">
                  {formatKwh(subDevice.kwh)} · {formatCurrency(subDevice.cost)}
                </span>
                {subDevice.isUnconfigured && (
                  <button
                    type="button"
                    onClick={onConfigure}
                    className="min-h-11 min-w-11 rounded-pill border border-white/[0.18] bg-white/10 px-2.5 py-0.5 text-caption text-white"
                  >
                    {t('smartStripCard.configureHint')}
                  </button>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
