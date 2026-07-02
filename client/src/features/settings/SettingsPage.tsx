import { lazy, Suspense } from 'react'
import { Routes, Route } from 'react-router-dom'
import SettingsRoot from './components/SettingsRoot'
import { useUserSettings } from './hooks/useUserSettings'

const FlatBaselineEdit = lazy(() => import('./components/FlatBaselineEdit'))
const TariffList = lazy(() =>
  import('@/features/tariffs/components/TariffList').then(m => ({ default: m.TariffList }))
)

function TariffSettingsRoute() {
  const { settings, isLoading, isError } = useUserSettings()
  if (isLoading || isError) return null
  return <TariffList flatId={settings?.flatId} />
}

export default function SettingsPage() {
  return (
    <Routes>
      <Route path="/" element={<SettingsRoot />} />
      <Route path="flat" element={<Suspense fallback={null}><FlatBaselineEdit /></Suspense>} />
      <Route path="tariffs" element={<Suspense fallback={null}><TariffSettingsRoute /></Suspense>} />
    </Routes>
  )
}
