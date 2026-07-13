import { lazy, Suspense } from 'react'
import { Routes, Route, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Upload } from 'lucide-react'
import { useUserSettings } from '@/features/settings/hooks/useUserSettings'
import { useFlatStructure } from '@/features/flat-structure/hooks/useFlatStructure'
import { ImportProgressCard } from '@/features/smart-plug-import/components/ImportProgressCard'
import { DecompositionTab } from '@/features/decomposition/components/DecompositionTab'

const ImportSurface = lazy(() =>
  import('@/features/smart-plug-import/components/ImportSurface').then(m => ({ default: m.ImportSurface }))
)

function DecompositionRoot() {
  const { t } = useTranslation(['decomposition', 'common'])
  const navigate = useNavigate()
  const { settings } = useUserSettings()

  return (
    <div className="px-4 pt-4">
      <div className="flex items-center justify-between mb-4">
        <h1 className="text-lg font-semibold text-white">{t('common:nav.decomposition')}</h1>
        <button
          type="button"
          onClick={() => navigate('/decomposition/import')}
          aria-label={t('importButton')}
          className="text-white/60 hover:text-white transition-colors"
        >
          <Upload className="w-5 h-5" aria-hidden="true" />
        </button>
      </div>
      <ImportProgressCard flatId={settings?.flatId} />
      <DecompositionTab flatId={settings?.flatId} />
    </div>
  )
}

function ImportRoute() {
  const { settings } = useUserSettings()
  const { data: flatStructure } = useFlatStructure(settings?.flatId)
  return <ImportSurface flatId={settings?.flatId} rooms={flatStructure?.rooms ?? []} />
}

export default function DecompositionPage() {
  return (
    <Routes>
      <Route path="/" element={<DecompositionRoot />} />
      <Route path="import" element={<Suspense fallback={null}><ImportRoute /></Suspense>} />
    </Routes>
  )
}
