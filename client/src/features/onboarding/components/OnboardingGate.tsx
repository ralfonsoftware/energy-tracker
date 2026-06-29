import { Navigate, Outlet } from 'react-router-dom'
import { useUserSettings } from '@/features/settings/hooks/useUserSettings'

export function OnboardingGate() {
  const { settings, isLoading, isError } = useUserSettings()
  if (isLoading || isError) return null
  if (!settings?.hasFlat) return <Navigate to="/onboarding" replace />
  return <Outlet />
}
