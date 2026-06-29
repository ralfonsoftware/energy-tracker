import { render, screen, fireEvent } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { OnboardingIntro } from './OnboardingIntro'

vi.mock('@/features/settings/hooks/useUpdateLocale')
import { useUpdateLocale } from '@/features/settings/hooks/useUpdateLocale'
const mockMutate = vi.fn()
vi.mocked(useUpdateLocale).mockReturnValue({ mutate: mockMutate } as any)

describe('OnboardingIntro', () => {
  beforeEach(() => {
    mockMutate.mockClear()
  })

  it('renders app name, value prop, and CTA button', () => {
    render(<OnboardingIntro onGetStarted={() => {}} />)
    expect(screen.getByText('Energy Tracker')).toBeInTheDocument()
    expect(screen.getByText('Know what your energy costs, every day.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Get Started' })).toBeInTheDocument()
  })

  it('calls onGetStarted when CTA button is clicked', () => {
    const onGetStarted = vi.fn()
    render(<OnboardingIntro onGetStarted={onGetStarted} />)
    fireEvent.click(screen.getByRole('button', { name: 'Get Started' }))
    expect(onGetStarted).toHaveBeenCalledTimes(1)
  })

  it('opens locale dropdown and calls updateLocale mutation on selection', () => {
    render(<OnboardingIntro onGetStarted={() => {}} />)
    fireEvent.click(screen.getByRole('button', { name: 'Language' }))
    fireEvent.click(screen.getByText('DE'))
    expect(mockMutate).toHaveBeenCalledWith('de-DE', expect.objectContaining({ onError: expect.any(Function) }))
  })
})
