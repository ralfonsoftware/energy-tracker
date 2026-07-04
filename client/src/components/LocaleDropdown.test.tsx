import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest'
import { LocaleDropdown } from './LocaleDropdown'
import i18n from '@/lib/i18n'

vi.mock('@/features/settings/hooks/useUpdateLocale')
import { useUpdateLocale } from '@/features/settings/hooks/useUpdateLocale'
const mockMutate = vi.fn()
vi.mocked(useUpdateLocale).mockReturnValue({ mutate: mockMutate } as any)

describe('LocaleDropdown', () => {
  beforeEach(async () => {
    mockMutate.mockClear()
    await i18n.changeLanguage('en-US')
  })

  afterEach(async () => {
    // handleSelect mutates the real i18n singleton; restore the baseline so no
    // language selection leaks into a test run afterward in the same worker.
    await i18n.changeLanguage('en-US')
  })

  it('LocaleDropdown_Rendered_ShowsCurrentLanguageOnTrigger', () => {
    render(<LocaleDropdown />)
    expect(screen.getByRole('button', { name: 'Language' })).toBeInTheDocument()
  })

  it('LocaleDropdown_TriggerClicked_OpensDropdownListingBothLocalesAndMarksTriggerExpanded', () => {
    render(<LocaleDropdown />)
    const trigger = screen.getByRole('button', { name: 'Language' })
    expect(trigger).toHaveAttribute('aria-expanded', 'false')
    fireEvent.click(trigger)
    expect(screen.getAllByRole('option')).toHaveLength(2)
    expect(trigger).toHaveAttribute('aria-expanded', 'true')
  })

  it('LocaleDropdown_LocaleSelected_CallsUpdateLocaleMutationAndClosesDropdown', async () => {
    render(<LocaleDropdown />)
    fireEvent.click(screen.getByRole('button', { name: 'Language' }))
    fireEvent.click(screen.getByText('DE'))
    expect(mockMutate).toHaveBeenCalledWith('de-DE', expect.objectContaining({ onError: expect.any(Function) }))
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument())
  })

  it('LocaleDropdown_EscapeKeyPressedWhileOpen_ClosesDropdown', async () => {
    render(<LocaleDropdown />)
    fireEvent.click(screen.getByRole('button', { name: 'Language' }))
    expect(screen.getByRole('listbox')).toBeInTheDocument()
    fireEvent.keyDown(screen.getByRole('listbox'), { key: 'Escape' })
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument())
  })

  it('LocaleDropdown_PointerDownOutsideDropdown_ClosesDropdown', async () => {
    render(<LocaleDropdown />)
    fireEvent.click(screen.getByRole('button', { name: 'Language' }))
    expect(screen.getByRole('listbox')).toBeInTheDocument()
    // Radix's outside-dismiss listener attaches via a 0ms setTimeout after open (to avoid
    // reacting to the very click that opened it), and defers the actual dismiss to the
    // subsequent "click" — mirroring the real browser pointerdown-then-click sequence.
    await new Promise(resolve => setTimeout(resolve, 0))
    fireEvent.pointerDown(document.body, { button: 0 })
    fireEvent.click(document.body)
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument())
  })

  it('LocaleDropdown_DimmedProp_AppliesReducedOpacityToTriggerAndOpenMenu', () => {
    render(<LocaleDropdown dimmed />)
    const trigger = screen.getByRole('button', { name: 'Language' })
    expect(trigger).toHaveStyle({ opacity: '0.7' })
    fireEvent.click(trigger)
    expect(screen.getByRole('listbox')).toHaveStyle({ opacity: '0.7' })
  })
})
