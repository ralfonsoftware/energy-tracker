import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest'
import { LocaleSettings } from './LocaleSettings'
import i18n from '@/lib/i18n'

vi.mock('../hooks/useUpdateLocale')
import { useUpdateLocale } from '../hooks/useUpdateLocale'
const mockMutate = vi.fn()
vi.mocked(useUpdateLocale).mockReturnValue({ mutate: mockMutate } as any)

function renderInBackdropFilterCard() {
  let cardElement: HTMLElement | null = null
  const utils = render(
    <div
      data-testid="settings-card"
      ref={(el) => { cardElement = el }}
      style={{ backdropFilter: 'blur(20px)' }}
    >
      <LocaleSettings />
    </div>
  )
  return { ...utils, cardElement: cardElement as unknown as HTMLElement }
}

describe('LocaleSettings', () => {
  beforeEach(async () => {
    mockMutate.mockClear()
    await i18n.changeLanguage('en-US')
  })

  afterEach(async () => {
    // handleSelect mutates the real i18n singleton; restore the baseline so no
    // language selection leaks into a test run afterward in the same worker.
    await i18n.changeLanguage('en-US')
  })

  it('LocaleSettings_Rendered_ShowsCurrentLanguageOnRow', () => {
    renderInBackdropFilterCard()
    expect(screen.getByText('English')).toBeInTheDocument()
  })

  it('LocaleSettings_TriggerClicked_OpensListboxWithBothLocalesAndMarksTriggerExpanded', () => {
    renderInBackdropFilterCard()
    const trigger = screen.getByRole('button', { name: /Language/i })
    expect(trigger).toHaveAttribute('aria-expanded', 'false')
    fireEvent.click(trigger)
    expect(screen.getAllByRole('option')).toHaveLength(2)
    expect(trigger).toHaveAttribute('aria-expanded', 'true')
  })

  it('LocaleSettings_DropdownOpen_ListboxIsNotDescendantOfBackdropFilterCard', () => {
    const { cardElement } = renderInBackdropFilterCard()
    fireEvent.click(screen.getByRole('button', { name: /Language/i }))
    const listbox = screen.getByRole('listbox')
    expect(cardElement.contains(listbox)).toBe(false)
  })

  it('LocaleSettings_LocaleSelected_CallsUpdateLocaleMutationAndClosesDropdown', async () => {
    renderInBackdropFilterCard()
    fireEvent.click(screen.getByRole('button', { name: /Language/i }))
    fireEvent.click(screen.getByText('Deutsch'))
    expect(mockMutate).toHaveBeenCalledWith('de-DE', expect.objectContaining({ onError: expect.any(Function) }))
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument())
  })

  it('LocaleSettings_EscapeKeyPressedWhileOpen_ClosesDropdown', async () => {
    renderInBackdropFilterCard()
    fireEvent.click(screen.getByRole('button', { name: /Language/i }))
    expect(screen.getByRole('listbox')).toBeInTheDocument()
    fireEvent.keyDown(screen.getByRole('listbox'), { key: 'Escape' })
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument())
  })

  it('LocaleSettings_PointerDownOutsideDropdown_ClosesDropdown', async () => {
    renderInBackdropFilterCard()
    fireEvent.click(screen.getByRole('button', { name: /Language/i }))
    expect(screen.getByRole('listbox')).toBeInTheDocument()
    // Radix's outside-dismiss listener attaches via a 0ms setTimeout after open (to avoid
    // reacting to the very click that opened it), and defers the actual dismiss to the
    // subsequent "click" — mirroring the real browser pointerdown-then-click sequence.
    await new Promise(resolve => setTimeout(resolve, 0))
    fireEvent.pointerDown(document.body, { button: 0 })
    fireEvent.click(document.body)
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument())
  })
})
