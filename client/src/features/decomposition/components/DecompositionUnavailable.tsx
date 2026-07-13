import { useTranslation } from 'react-i18next'
import { Upload } from 'lucide-react'

type Props = { onImport: () => void }

export function DecompositionUnavailable({ onImport }: Props) {
  const { t } = useTranslation('decomposition')

  return (
    <div className="rounded-card border border-glass-border bg-glass-surface flex flex-col items-center gap-3.5 px-6 py-9 text-center">
      <Upload size={48} className="text-text-tertiary" aria-hidden="true" />
      <span className="text-body text-white">{t('unavailable.heading')}</span>
      <span className="text-body-sm text-white/55">{t('unavailable.body')}</span>
      <button
        type="button"
        onClick={onImport}
        className="mt-1 w-full rounded-card border border-white/[0.18] bg-white/10 px-4 py-3.5 text-body-sm font-semibold text-white"
      >
        {t('unavailable.cta')}
      </button>
    </div>
  )
}
