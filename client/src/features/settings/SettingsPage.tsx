import { lazy, Suspense, useState } from 'react'
import { Routes, Route } from 'react-router-dom'
import SettingsRoot from './components/SettingsRoot'
import { useUserSettings } from './hooks/useUserSettings'
import { usePatchFlat } from './hooks/usePatchFlat'

const FlatBaselineEdit = lazy(() => import('./components/FlatBaselineEdit'))
const TariffList = lazy(() =>
  import('@/features/tariffs/components/TariffList').then(m => ({ default: m.TariffList }))
)
const FlatStructureEditor = lazy(() =>
  import('@/features/flat-structure/components/FlatStructureEditor').then(m => ({
    default: m.FlatStructureEditor,
  }))
)

function FlatStructureSettingsRoute() {
  const { settings, isLoading, isError } = useUserSettings()
  if (isLoading || isError) return null
  return <FlatStructureEditor flatId={settings?.flatId} />
}

function TariffSettingsRoute() {
  const { settings, isLoading, isError } = useUserSettings()
  const { mutate: patchFlat, isPending: isSavingSpend, isError: isSpendSaveError } = usePatchFlat()
  const [missingFlatIdError, setMissingFlatIdError] = useState(false)
  if (isLoading || isError) return null
  return (
    <TariffList
      flatId={settings?.flatId}
      annualKwhBaseline={settings?.annualKwhBaseline}
      plannedAnnualSpend={settings?.plannedAnnualSpend}
      onSavePlannedAnnualSpend={value => {
        if (!settings?.flatId) {
          setMissingFlatIdError(true)
          return
        }
        setMissingFlatIdError(false)
        patchFlat({ flatId: settings.flatId, body: { plannedAnnualSpend: value } })
      }}
      isSavingPlannedAnnualSpend={isSavingSpend}
      isPlannedAnnualSpendSaveError={isSpendSaveError || (missingFlatIdError && !settings?.flatId)}
    />
  )
}

export default function SettingsPage() {
  return (
    <Routes>
      <Route path="/" element={<SettingsRoot />} />
      <Route path="flat" element={<Suspense fallback={null}><FlatBaselineEdit /></Suspense>} />
      <Route path="tariffs" element={<Suspense fallback={null}><TariffSettingsRoute /></Suspense>} />
      <Route path="structure" element={<Suspense fallback={null}><FlatStructureSettingsRoute /></Suspense>} />
    </Routes>
  )
}
