import { render, screen, fireEvent, act } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import { FlatSettingsCard } from './FlatSettingsCard'
import type { UserSettings } from '../api/settingsApi'

vi.mock('../hooks/usePatchFlat')
import { usePatchFlat } from '../hooks/usePatchFlat'
const mockUsePatchFlat = vi.mocked(usePatchFlat)

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom')
  return { ...actual, useNavigate: () => mockNavigate }
})
const mockNavigate = vi.fn()

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

const defaultSettings: UserSettings = {
  locale: 'en-US',
  hasFlat: true,
  flatId: 'flat-123',
  flatName: 'My Flat',
  annualKwhBaseline: 2500,
  plannedAnnualSpend: null,
}

const mockMutate = vi.fn()

function renderCard(settings = defaultSettings) {
  return render(
    <MemoryRouter>
      <FlatSettingsCard settings={settings} />
    </MemoryRouter>
  )
}

describe('FlatSettingsCard', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockUsePatchFlat.mockReturnValue({ mutate: mockMutate, isPending: false } as unknown as ReturnType<typeof usePatchFlat>)
  })

  it('renders flat name when not editing', () => {
    renderCard()
    expect(screen.getByText('My Flat')).toBeInTheDocument()
  })

  it('clicking flat name shows inline input pre-filled with name', () => {
    renderCard()
    fireEvent.click(screen.getByText('My Flat'))
    const input = screen.getByRole('textbox')
    expect(input).toBeInTheDocument()
    expect((input as HTMLInputElement).value).toBe('My Flat')
  })

  it('cancel button hides input and restores previous name', () => {
    renderCard()
    fireEvent.click(screen.getByText('My Flat'))
    expect(screen.getByRole('textbox')).toBeInTheDocument()
    const cancelBtn = screen.getByText('✕')
    fireEvent.click(cancelBtn)
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument()
    expect(screen.getByText('My Flat')).toBeInTheDocument()
  })

  it('save calls patchFlat with trimmed name', () => {
    renderCard()
    fireEvent.click(screen.getByText('My Flat'))
    const input = screen.getByRole('textbox')
    fireEvent.change(input, { target: { value: '  New Flat Name  ' } })
    fireEvent.click(screen.getByText('flat.save'))
    expect(mockMutate).toHaveBeenCalledWith(
      { flatId: 'flat-123', body: { name: 'New Flat Name' } },
      expect.any(Object)
    )
  })

  it('kWh Baseline pill navigates to /settings/flat', () => {
    renderCard()
    const pill = screen.getByText('flat.kwhBaselineLink')
    fireEvent.click(pill)
    expect(mockNavigate).toHaveBeenCalledWith('/settings/flat')
  })

  it('Tariff pill navigates to /settings/tariffs', () => {
    renderCard()
    const pill = screen.getByText('flat.tariffLink')
    fireEvent.click(pill)
    expect(mockNavigate).toHaveBeenCalledWith('/settings/tariffs')
  })

  it('shows error message and keeps editing on PATCH failure', () => {
    renderCard()
    fireEvent.click(screen.getByText('My Flat'))
    const input = screen.getByRole('textbox')
    fireEvent.change(input, { target: { value: 'New Name' } })
    fireEvent.click(screen.getByText('flat.save'))
    const [, callbacks] = mockMutate.mock.calls[0] as [unknown, { onError: () => void }]
    act(() => { callbacks.onError() })
    expect(screen.getByText('flat.saveError')).toBeInTheDocument()
    expect(screen.getByRole('textbox')).toBeInTheDocument()
  })
})
