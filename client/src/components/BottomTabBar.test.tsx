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
})
