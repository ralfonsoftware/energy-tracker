import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { OnboardingFlatName } from './OnboardingFlatName'

vi.mock('@/features/settings/hooks/useUpdateLocale')
import { useUpdateLocale } from '@/features/settings/hooks/useUpdateLocale'
vi.mocked(useUpdateLocale).mockReturnValue({ mutate: vi.fn() } as any)

vi.mock('@/lib/i18n', () => ({ default: { language: 'en-US', changeLanguage: vi.fn() } }))

const onContinue = vi.fn()
const onBack = vi.fn()

function renderComponent(initialValue = '') {
  return render(
    <OnboardingFlatName initialValue={initialValue} onContinue={onContinue} onBack={onBack} />
  )
}

describe('OnboardingFlatName', () => {
  beforeEach(() => {
    onContinue.mockReset()
    onBack.mockReset()
  })

  it('renders title and input field', () => {
    renderComponent()
    expect(screen.getByRole('heading', { level: 1 })).toBeInTheDocument()
    expect(screen.getByRole('textbox')).toBeInTheDocument()
  })

  it('Continue button is disabled when input is empty', () => {
    renderComponent()
    expect(screen.getByRole('button', { name: /continue/i })).toBeDisabled()
  })

  it('Continue button is disabled for whitespace-only input and no error shown before blur', async () => {
    renderComponent()
    await userEvent.type(screen.getByRole('textbox'), '   ')
    expect(screen.getByRole('button', { name: /continue/i })).toBeDisabled()
    expect(screen.queryByText(/please enter a flat name/i)).not.toBeInTheDocument()
  })

  it('Continue button is enabled when a non-empty name is typed', async () => {
    renderComponent()
    await userEvent.type(screen.getByRole('textbox'), 'My Flat')
    expect(screen.getByRole('button', { name: /continue/i })).not.toBeDisabled()
  })

  it('calls onContinue with trimmed name on form submit', async () => {
    renderComponent()
    await userEvent.type(screen.getByRole('textbox'), ' My Flat ')
    await userEvent.click(screen.getByRole('button', { name: /continue/i }))
    expect(onContinue).toHaveBeenCalledWith('My Flat')
  })

  it('pre-populates input from initialValue', () => {
    renderComponent('Wohnung 3B')
    expect(screen.getByRole('textbox')).toHaveValue('Wohnung 3B')
  })

  it('Continue is enabled when initialValue is non-empty', () => {
    renderComponent('Wohnung 3B')
    expect(screen.getByRole('button', { name: /continue/i })).not.toBeDisabled()
  })

  it('calls onBack when back button is clicked', async () => {
    renderComponent()
    await userEvent.click(screen.getByRole('button', { name: /back/i }))
    expect(onBack).toHaveBeenCalledOnce()
  })
})
