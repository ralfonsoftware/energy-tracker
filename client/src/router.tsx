import { lazy, Suspense } from 'react'
import { createBrowserRouter } from 'react-router-dom'
import AppShell from '@/components/AppShell'
import { OnboardingGate } from '@/features/onboarding/components/OnboardingGate'

const DashboardPage = lazy(() => import('@/features/dashboard/DashboardPage'))
const InsightsPage = lazy(() => import('@/features/insights/InsightsPage'))
const DecompositionPage = lazy(() => import('@/features/decomposition/DecompositionPage'))
const SettingsPage = lazy(() => import('@/features/settings/SettingsPage'))
const OnboardingPage = lazy(() => import('@/features/onboarding/OnboardingPage'))
const NotFoundPage = lazy(() => import('@/components/NotFoundPage'))

function Wrap({ Page }: { Page: React.ComponentType }) {
  return (
    <Suspense fallback={null}>
      <Page />
    </Suspense>
  )
}

export const routes = [
  {
    element: <OnboardingGate />,
    children: [
      {
        element: <AppShell />,
        children: [
          { path: '/', element: <Wrap Page={DashboardPage} /> },
          { path: '/insights', element: <Wrap Page={InsightsPage} /> },
          { path: '/decomposition/*', element: <Wrap Page={DecompositionPage} /> },
          { path: '/settings/*', element: <Wrap Page={SettingsPage} /> },
        ],
      },
    ],
  },
  { path: '/onboarding', element: <Wrap Page={OnboardingPage} /> },
  { path: '*', element: <Wrap Page={NotFoundPage} /> },
]

export const router = createBrowserRouter(routes)
