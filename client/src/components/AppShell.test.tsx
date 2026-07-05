import { render, screen, act } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import AppShell from './AppShell'

vi.mock('./EuroBurnGradient', () => ({
  EuroBurnGradient: () => <div data-testid="gradient" />,
}))
vi.mock('./BottomTabBar', () => ({
  BottomTabBar: () => <div data-testid="bottom-tab-bar" />,
}))
vi.mock('./SidebarNav', () => ({
  SidebarNav: () => <div data-testid="sidebar-nav" />,
}))
vi.mock('./Header', () => ({
  Header: () => <div data-testid="header" />,
}))

function renderShell() {
  const router = createMemoryRouter(
    [
      {
        element: <AppShell />,
        children: [
          { path: '/', element: <div>page-one</div> },
          { path: '/settings', element: <div>page-two</div> },
        ],
      },
    ],
    { initialEntries: ['/'] }
  )
  render(<RouterProvider router={router} />)
  return router
}

describe('AppShell', () => {
  beforeEach(() => {
    Element.prototype.scrollTo = vi.fn()
  })

  it('AppShell_RouteChanges_ResetsMainScrollPositionToTop', async () => {
    const router = renderShell()
    expect(screen.getByText('page-one')).toBeInTheDocument()

    // The effect also fires once on initial mount — clear that call so the
    // assertion below only proves the navigation itself triggered a reset,
    // not just that scrollTo was called at some point during the test.
    vi.mocked(Element.prototype.scrollTo).mockClear()

    await act(async () => {
      await router.navigate('/settings')
    })

    expect(screen.getByText('page-two')).toBeInTheDocument()
    expect(Element.prototype.scrollTo).toHaveBeenCalledExactlyOnceWith(0, 0)
  })

  it('AppShell_Rendered_MainHasSafeAreaAwareBottomClearance', () => {
    renderShell()

    const main = screen.getByText('page-one').closest('main')
    expect(main?.className).toContain('pb-[calc(84px_+_env(safe-area-inset-bottom,0px))]')
  })
})
