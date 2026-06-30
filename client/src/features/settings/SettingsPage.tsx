import { lazy, Suspense } from 'react'
import { Routes, Route } from 'react-router-dom'
import SettingsRoot from './components/SettingsRoot'

const FlatBaselineEdit = lazy(() => import('./components/FlatBaselineEdit'))

export default function SettingsPage() {
  return (
    <Routes>
      <Route path="/" element={<SettingsRoot />} />
      <Route path="flat" element={<Suspense fallback={null}><FlatBaselineEdit /></Suspense>} />
    </Routes>
  )
}
