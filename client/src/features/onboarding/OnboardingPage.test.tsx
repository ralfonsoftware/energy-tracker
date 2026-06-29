import { render, screen } from '@testing-library/react'
import { MemoryRouter, Routes, Route } from 'react-router-dom'
import { vi, describe, it, expect } from 'vitest'
import OnboardingPage from './OnboardingPage'

vi.mock('@/features/settings/hooks/useUserSettings')
import { useUserSettings } from '@/features/settings/hooks/useUserSettings'
const mockUseUserSettings = vi.mocked(useUserSettings)

vi.mock('./components/OnboardingIntro', () => ({
  OnboardingIntro: ({ onGetStarted }: { onGetStarted: () => void }) => (
    <button onClick={onGetStarted}>Get Started</button>
  ),
}))

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/onboarding']}>
      <Routes>
        <Route path="/onboarding" element={<OnboardingPage />} />
        <Route path="/" element={<div>Dashboard</div>} />
      </Routes>
    </MemoryRouter>
  )
}

describe('OnboardingPage', () => {
  it('redirects to / when hasFlat is true', () => {
    mockUseUserSettings.mockReturnValue({ settings: { locale: null, hasFlat: true }, isLoading: false, isError: false })
    renderPage()
    expect(screen.getByText('Dashboard')).toBeInTheDocument()
  })

  it('renders intro step when hasFlat is false', () => {
    mockUseUserSettings.mockReturnValue({ settings: { locale: null, hasFlat: false }, isLoading: false, isError: false })
    renderPage()
    expect(screen.getByRole('button', { name: 'Get Started' })).toBeInTheDocument()
  })

  it('does not redirect while loading', () => {
    mockUseUserSettings.mockReturnValue({ settings: undefined, isLoading: true, isError: false })
    renderPage()
    expect(screen.queryByText('Dashboard')).not.toBeInTheDocument()
  })

  it('does not redirect on settings fetch error', () => {
    mockUseUserSettings.mockReturnValue({ settings: undefined, isLoading: false, isError: true })
    renderPage()
    expect(screen.queryByText('Dashboard')).not.toBeInTheDocument()
  })
})
