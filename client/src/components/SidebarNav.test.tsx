import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { SidebarNav } from './SidebarNav'

function renderWithRouter(ui: React.ReactElement, { initialEntries = ['/'] } = {}) {
  return render(<MemoryRouter initialEntries={initialEntries}>{ui}</MemoryRouter>)
}

describe('SidebarNav', () => {
  it('renders 4 nav items with correct accessible names', () => {
    renderWithRouter(<SidebarNav />)
    expect(screen.getByRole('link', { name: 'Dashboard' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Insights' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Decomposition' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Settings' })).toBeInTheDocument()
  })

  it('nav container has role="navigation" and aria-label', () => {
    renderWithRouter(<SidebarNav />)
    const nav = screen.getByRole('navigation', { name: 'Sidebar navigation' })
    expect(nav).toBeInTheDocument()
  })
})
