import { useTranslation } from 'react-i18next'

export function InsightDiscoveryProgress() {
  const { t } = useTranslation('insights')

  return (
    <div
      role="status"
      className="mb-3 flex items-center gap-3 rounded-2xl px-4 py-3.5"
      style={{ background: 'var(--color-residual-tint)', border: '1px solid rgba(251,191,36,0.2)' }}
    >
      <div
        className="h-4 w-4 shrink-0 animate-spin rounded-full border-2"
        style={{ borderColor: 'rgba(245,158,11,0.25)', borderTopColor: '#f59e0b' }}
      />
      <div className="text-sm font-semibold text-white">{t('progress.label')}</div>
    </div>
  )
}
