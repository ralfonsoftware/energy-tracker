import { render, screen } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { OnboardingGate } from './OnboardingGate'

vi.mock('@/features/settings/hooks/useUserSettings')
import { useUserSettings } from '@/features/settings/hooks/useUserSettings'
const mockUseUserSettings = vi.mocked(useUserSettings)

function renderGate(initialPath = '/') {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route element={<OnboardingGate />}>
          <Route path="/" element={<div>Main content</div>} />
          <Route path="/insights" element={<div>Insights page</div>} />
          <Route path="/decomposition" element={<div>Decomposition page</div>} />
          <Route path="/settings/*" element={<div>Settings page</div>} />
        </Route>
        <Route path="/onboarding" element={<div>Onboarding page</div>} />
      </Routes>
    </MemoryRouter>
  )
}

describe('OnboardingGate', () => {
  beforeEach(() => {
    mockUseUserSettings.mockReset()
  })

  it('renders nothing while loading', () => {
    mockUseUserSettings.mockReturnValue({ settings: undefined, isLoading: true, isError: false })
    const { container } = renderGate()
    expect(container.firstChild).toBeNull()
  })

  it('redirects to /onboarding when hasFlat is false', () => {
    mockUseUserSettings.mockReturnValue({ settings: { locale: null, hasFlat: false }, isLoading: false, isError: false })
    renderGate()
    expect(screen.getByText('Onboarding page')).toBeInTheDocument()
    expect(screen.queryByText('Main content')).not.toBeInTheDocument()
  })

  it('renders children via Outlet when hasFlat is true', () => {
    mockUseUserSettings.mockReturnValue({ settings: { locale: null, hasFlat: true }, isLoading: false, isError: false })
    renderGate()
    expect(screen.getByText('Main content')).toBeInTheDocument()
  })

  it('renders nothing on settings fetch error', () => {
    mockUseUserSettings.mockReturnValue({ settings: undefined, isLoading: false, isError: true })
    const { container } = renderGate()
    expect(container.firstChild).toBeNull()
  })

  it.each(['/insights', '/decomposition', '/settings'])(
    'redirects to /onboarding when hasFlat is false on %s',
    (path) => {
      mockUseUserSettings.mockReturnValue({ settings: { locale: null, hasFlat: false }, isLoading: false, isError: false })
      renderGate(path)
      expect(screen.getByText('Onboarding page')).toBeInTheDocument()
    }
  )
})
