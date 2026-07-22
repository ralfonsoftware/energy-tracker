import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { AlertTriangle } from 'lucide-react'

type Props = { onRecover: () => void }

export function ErrorFallback({ onRecover }: Props) {
  const { t } = useTranslation('common')
  const navigate = useNavigate()

  return (
    <div
      className="flex flex-col items-center justify-center gap-3.5 h-[100dvh] px-6 text-center"
      style={{ background: '#111827' }}
    >
      <AlertTriangle size={48} className="text-text-tertiary" aria-hidden="true" />
      <h1 className="text-body text-white">{t('errorBoundary.heading')}</h1>
      <p className="text-body-sm text-white/55">{t('errorBoundary.body')}</p>
      <button
        type="button"
        onClick={() => {
          onRecover()
          navigate('/', { replace: true })
        }}
        className="mt-1 w-full max-w-xs rounded-card border border-white/[0.18] bg-white/10 px-4 py-3.5 text-body-sm font-semibold text-white"
      >
        {t('errorBoundary.cta')}
      </button>
    </div>
  )
}
