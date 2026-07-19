import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { Compass } from 'lucide-react'

export default function NotFoundPage() {
  const { t } = useTranslation('common')
  const navigate = useNavigate()

  return (
    <div
      className="flex flex-col items-center justify-center gap-3.5 h-[100dvh] px-6 text-center"
      style={{ background: '#111827' }}
    >
      <Compass size={48} className="text-text-tertiary" aria-hidden="true" />
      <h1 className="text-body text-white">{t('notFound.heading')}</h1>
      <p className="text-body-sm text-white/55">{t('notFound.body')}</p>
      <button
        type="button"
        onClick={() => navigate('/', { replace: true })}
        className="mt-1 w-full max-w-xs rounded-card border border-white/[0.18] bg-white/10 px-4 py-3.5 text-body-sm font-semibold text-white"
      >
        {t('notFound.cta')}
      </button>
    </div>
  )
}
