import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { BottomTabBar } from './BottomTabBar'

function renderWithRouter(ui: React.ReactElement, { initialEntries = ['/'] } = {}) {
  return render(<MemoryRouter initialEntries={initialEntries}>{ui}</MemoryRouter>)
}

describe('BottomTabBar', () => {
  it('renders 4 tabs with correct labels', () => {
    renderWithRouter(<BottomTabBar />)
    expect(screen.getByText('Dashboard')).toBeInTheDocument()
    expect(screen.getByText('Insights')).toBeInTheDocument()
    expect(screen.getByText('Decomposition')).toBeInTheDocument()
    expect(screen.getByText('Settings')).toBeInTheDocument()
  })

  it('each tab link has an accessible name via aria-label', () => {
    renderWithRouter(<BottomTabBar />)
    expect(screen.getByRole('link', { name: 'Dashboard' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Insights' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Decomposition' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Settings' })).toBeInTheDocument()
  })

  it('nav container has role="navigation" and aria-label', () => {
    renderWithRouter(<BottomTabBar />)
    const nav = screen.getByRole('navigation', { name: 'Bottom navigation' })
    expect(nav).toBeInTheDocument()
  })

  it('nav height and paddingBottom account for the safe-area inset', () => {
    // jsdom's CSS engine mangles/drops `env()`/`calc()` values when read back through
    // the style object or the serialized `style` attribute, so we spy on the raw
    // property setters to verify the exact values React assigns instead.
    const probe = document.createElement('div')
    const styleProto = Object.getPrototypeOf(probe.style)
    const heightSetter = vi.spyOn(styleProto, 'height', 'set')
    const paddingBottomSetter = vi.spyOn(styleProto, 'paddingBottom', 'set')

    try {
      renderWithRouter(<BottomTabBar />)

      expect(heightSetter).toHaveBeenCalledWith('calc(72px + env(safe-area-inset-bottom, 0px))')
      expect(paddingBottomSetter).toHaveBeenCalledWith('env(safe-area-inset-bottom, 0px)')
    } finally {
      heightSetter.mockRestore()
      paddingBottomSetter.mockRestore()
    }
  })
})
