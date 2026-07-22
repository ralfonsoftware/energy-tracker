import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { FlatSwitcher } from './FlatSwitcher'
import type { UserSettings, FlatSummary } from '@/features/settings/api/settingsApi'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

vi.mock('@/features/settings/hooks/useUserSettings')
import { useUserSettings } from '@/features/settings/hooks/useUserSettings'
const mockUseUserSettings = vi.mocked(useUserSettings)

vi.mock('@/features/settings/hooks/useFlats')
import { useFlats } from '@/features/settings/hooks/useFlats'
const mockUseFlats = vi.mocked(useFlats)

vi.mock('@/features/settings/hooks/useSwitchActiveFlat')
import { useSwitchActiveFlat } from '@/features/settings/hooks/useSwitchActiveFlat'
const mockUseSwitchActiveFlat = vi.mocked(useSwitchActiveFlat)

vi.mock('@/features/settings/components/AddFlatForm', () => ({
  AddFlatForm: ({ open }: { open: boolean }) => (open ? <div>add-flat-form</div> : null),
}))

const defaultSettings: UserSettings = {
  locale: 'en-US',
  hasFlat: true,
  flatId: 'flat-1',
  flatName: 'Home',
  annualKwhBaseline: 2500,
  plannedAnnualSpend: null,
}

const flats: FlatSummary[] = [
  { flatId: 'flat-1', name: 'Home', annualKwhBaseline: 2500, spikeThreshold: 2, plannedAnnualSpend: null },
  { flatId: 'flat-2', name: 'Cabin', annualKwhBaseline: 1500, spikeThreshold: 2, plannedAnnualSpend: 800 },
]

const mockMutate = vi.fn()

function setup(
  settingsOverride: Partial<UserSettings> = {},
  isLoading = false,
  flatsOverride: { data?: FlatSummary[]; isError?: boolean } = {},
  switchOverride: { isPending?: boolean } = {}
) {
  mockUseUserSettings.mockReturnValue({
    settings: { ...defaultSettings, ...settingsOverride },
    isLoading,
    isError: false,
    refetch: vi.fn(),
  } as unknown as ReturnType<typeof useUserSettings>)
  mockUseFlats.mockReturnValue({
    data: flatsOverride.data ?? flats,
    isLoading: false,
    isError: flatsOverride.isError ?? false,
  } as unknown as ReturnType<typeof useFlats>)
  mockUseSwitchActiveFlat.mockReturnValue({
    mutate: mockMutate,
    isPending: switchOverride.isPending ?? false,
  } as unknown as ReturnType<typeof useSwitchActiveFlat>)
  return render(<FlatSwitcher />)
}

describe('FlatSwitcher', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('FlatSwitcher_Rendered_ShowsActiveFlatNameOnTrigger', () => {
    setup()
    expect(screen.getByRole('button', { name: /Home/ })).toBeInTheDocument()
  })

  it('FlatSwitcher_TriggerClicked_OpensDropdownListingAllFlats', () => {
    setup()
    fireEvent.click(screen.getByRole('button', { name: /Home/ }))
    expect(screen.getAllByRole('option')).toHaveLength(2)
    expect(screen.getByText('Cabin')).toBeInTheDocument()
  })

  it('FlatSwitcher_DropdownOpen_ActiveFlatRowIsAriaSelected', () => {
    setup()
    fireEvent.click(screen.getByRole('button', { name: /Home/ }))
    const homeOption = screen.getByRole('option', { name: 'Home' })
    const cabinOption = screen.getByRole('option', { name: 'Cabin' })
    expect(homeOption).toHaveAttribute('aria-selected', 'true')
    expect(cabinOption).toHaveAttribute('aria-selected', 'false')
  })

  it('FlatSwitcher_DifferentFlatSelected_CallsSwitchMutationWithFlatIdLocaleAndPreviousFlatId', () => {
    setup()
    fireEvent.click(screen.getByRole('button', { name: /Home/ }))
    fireEvent.click(screen.getByRole('option', { name: 'Cabin' }))
    expect(mockMutate).toHaveBeenCalledWith({ flatId: 'flat-2', locale: 'en-US', previousFlatId: 'flat-1' })
  })

  it('FlatSwitcher_DifferentFlatSelected_ClosesDropdown', () => {
    setup()
    fireEvent.click(screen.getByRole('button', { name: /Home/ }))
    fireEvent.click(screen.getByRole('option', { name: 'Cabin' }))
    expect(screen.queryByRole('listbox')).not.toBeInTheDocument()
  })

  it('FlatSwitcher_ActiveFlatSelected_DoesNotCallMutation', () => {
    setup()
    fireEvent.click(screen.getByRole('button', { name: /Home/ }))
    fireEvent.click(screen.getByRole('option', { name: 'Home' }))
    expect(mockMutate).not.toHaveBeenCalled()
  })

  it('FlatSwitcher_AddFlatRowClicked_OpensAddFlatForm', () => {
    setup()
    fireEvent.click(screen.getByRole('button', { name: /Home/ }))
    fireEvent.click(screen.getByText('flatSwitcher.addFlat'))
    expect(screen.getByText('add-flat-form')).toBeInTheDocument()
  })

  it('FlatSwitcher_SettingsLoading_ShowsLoadingPlaceholderOnTrigger', () => {
    setup({}, true)
    expect(screen.getByRole('button', { name: /flatSwitcher.loading/ })).toBeInTheDocument()
  })

  it('FlatSwitcher_ActiveFlatNameMissing_ShowsErrorFallbackOnTrigger', () => {
    setup({ flatName: undefined })
    expect(screen.getByRole('button', { name: /flatSwitcher.error/ })).toBeInTheDocument()
  })

  it('FlatSwitcher_FlatsFetchFails_ShowsErrorRowInsteadOfEmptyList', () => {
    setup({}, false, { isError: true })
    fireEvent.click(screen.getByRole('button', { name: /Home/ }))
    expect(screen.getByText('flatSwitcher.error')).toBeInTheDocument()
    expect(screen.queryAllByRole('option')).toHaveLength(0)
  })

  it('FlatSwitcher_SwitchAlreadyPending_SelectingFlatDoesNotCallMutationAgain', () => {
    setup({}, false, {}, { isPending: true })
    fireEvent.click(screen.getByRole('button', { name: /Home/ }))
    fireEvent.click(screen.getByRole('option', { name: 'Cabin' }))
    expect(mockMutate).not.toHaveBeenCalled()
  })

  it('FlatSwitcher_EscapeKeyPressedWhileOpen_ClosesDropdown', async () => {
    setup()
    fireEvent.click(screen.getByRole('button', { name: /Home/ }))
    expect(screen.getByRole('listbox')).toBeInTheDocument()
    fireEvent.keyDown(screen.getByRole('listbox'), { key: 'Escape' })
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument())
  })

  it('FlatSwitcher_PointerDownOutsideDropdown_ClosesDropdown', async () => {
    setup()
    fireEvent.click(screen.getByRole('button', { name: /Home/ }))
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
