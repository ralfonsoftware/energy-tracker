import { useTranslation } from 'react-i18next'
import type { DeviceDecomposition } from '@/features/decomposition/api/decompositionApi'

type Props = { device: DeviceDecomposition; onConfigure: () => void }

export function UnmeasuredDeviceCard({ device, onConfigure }: Props) {
  const { t } = useTranslation('decomposition')

  return (
    <div className="rounded-card border border-glass-border bg-glass-surface px-3.5 py-2.5 opacity-[0.45]">
      <span className="text-body-sm text-white">{device.name}</span>
      <div className="mt-1 flex items-center justify-between gap-2">
        <span className="text-caption text-text-tertiary">{t('roomCard.unmeasuredHint')}</span>
        <button
          type="button"
          onClick={onConfigure}
          className="min-h-11 min-w-11 rounded-pill border border-white/[0.18] bg-white/10 px-2.5 py-0.5 text-caption text-white"
        >
          {t('roomCard.configureProfile')}
        </button>
      </div>
    </div>
  )
}
