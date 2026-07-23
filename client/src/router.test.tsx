import { render, screen, act } from '@testing-library/react'
import { vi } from 'vitest'
import { createMemoryRouter, Outlet, RouterProvider } from 'react-router-dom'
import { OnboardingGate } from '@/features/onboarding/components/OnboardingGate'
import { useUserSettings } from '@/features/settings/hooks/useUserSettings'
import { routes } from './router'

vi.mock('@/features/settings/hooks/useUserSettings')

function renderTestRouter(initialPath: string) {
  const router = createMemoryRouter(
    [
      {
        element: <OnboardingGate />,
        children: [
          {
            element: <Outlet />,
            children: [{ path: '/', element: <div>dashboard-stub</div> }],
          },
        ],
      },
      { path: '/onboarding', element: <div>onboarding-stub</div> },
      { path: '*', element: <div>notfound-stub</div> },
    ],
    { initialEntries: [initialPath] }
  )
  render(<RouterProvider router={router} />)
  return router
}

describe('router', () => {
  it('Router_UnmatchedPathWithFlat_RendersNotFoundPage', async () => {
    vi.mocked(useUserSettings).mockReturnValue({
      settings: { hasFlat: true },
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof useUserSettings>)

    await act(async () => {
      renderTestRouter('/this-does-not-exist')
    })

    expect(screen.getByText('notfound-stub')).toBeInTheDocument()
  })

  it('Router_UnmatchedPathWithoutFlat_RendersNotFoundPageNotOnboardingRedirect', async () => {
    vi.mocked(useUserSettings).mockReturnValue({
      settings: { hasFlat: false },
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof useUserSettings>)

    await act(async () => {
      renderTestRouter('/this-does-not-exist')
    })

    expect(screen.getByText('notfound-stub')).toBeInTheDocument()
    expect(screen.queryByText('onboarding-stub')).not.toBeInTheDocument()
  })

  it('Router_KnownPath_StillRendersItsOwnPage', async () => {
    vi.mocked(useUserSettings).mockReturnValue({
      settings: { hasFlat: true },
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof useUserSettings>)

    await act(async () => {
      renderTestRouter('/')
    })

    expect(screen.getByText('dashboard-stub')).toBeInTheDocument()
    expect(screen.queryByText('notfound-stub')).not.toBeInTheDocument()
  })
})

describe('routes (real route config from router.tsx)', () => {
  it('Routes_TopLevel_OnboardingGateThenOnboardingThenCatchAllInOrder', () => {
    expect(routes).toHaveLength(3)
    expect(routes[0].path).toBeUndefined()
    expect(routes[0].children).toHaveLength(1)
    expect(routes[1].path).toBe('/onboarding')
    expect(routes[2].path).toBe('*')
  })

  it('Routes_AppShellChildren_FourPagesInOrder', () => {
    const appShellEntry = routes[0].children?.[0]
    expect(appShellEntry?.path).toBeUndefined()
    const appShellChildren = appShellEntry?.children ?? []

    expect(appShellChildren.map(route => route.path)).toEqual([
      '/',
      '/insights',
      '/decomposition/*',
      '/settings/*',
    ])
  })
})
