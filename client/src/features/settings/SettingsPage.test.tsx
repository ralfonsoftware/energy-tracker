import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import SettingsPage from './SettingsPage'
import type { UserSettings } from './api/settingsApi'

const tariffListSpy = vi.fn()
vi.mock('@/features/tariffs/components/TariffList', () => ({
  TariffList: (props: unknown) => {
    tariffListSpy(props)
    return null
  },
}))

vi.mock('./hooks/useUserSettings')
import { useUserSettings } from './hooks/useUserSettings'
const mockUseUserSettings = vi.mocked(useUserSettings)

vi.mock('./hooks/usePatchFlat')
import { usePatchFlat } from './hooks/usePatchFlat'
const mockUsePatchFlat = vi.mocked(usePatchFlat)

const settings: UserSettings = {
  locale: 'en-US',
  hasFlat: true,
  flatId: 'flat-123',
  flatName: 'My Flat',
  annualKwhBaseline: 2500,
  plannedAnnualSpend: 1200,
}

const mockPatchFlatMutate = vi.fn()

function renderTariffsRoute() {
  return render(
    <MemoryRouter initialEntries={['/tariffs']}>
      <SettingsPage />
    </MemoryRouter>
  )
}

describe('SettingsPage TariffSettingsRoute', () => {
  beforeEach(() => {
    tariffListSpy.mockReset()
    mockPatchFlatMutate.mockReset()
    mockUseUserSettings.mockReturnValue({ settings, isLoading: false, isError: false })
    mockUsePatchFlat.mockReturnValue({
      mutate: mockPatchFlatMutate,
      isPending: false,
      isError: false,
    } as unknown as ReturnType<typeof usePatchFlat>)
  })

  it('SettingsPage_TariffsRoute_PassesFlatIdBaselineAndPlannedSpendToTariffList', async () => {
    renderTariffsRoute()

    await waitFor(() => expect(tariffListSpy).toHaveBeenCalled())
    const props = tariffListSpy.mock.calls[0][0] as Record<string, unknown>
    expect(props.flatId).toBe('flat-123')
    expect(props.annualKwhBaseline).toBe(2500)
    expect(props.plannedAnnualSpend).toBe(1200)
    expect(props.isSavingPlannedAnnualSpend).toBe(false)
    expect(props.isPlannedAnnualSpendSaveError).toBe(false)
  })

  it('SettingsPage_TariffsRoute_OnSavePlannedAnnualSpendCallsPatchFlatWithFlatId', async () => {
    renderTariffsRoute()

    await waitFor(() => expect(tariffListSpy).toHaveBeenCalled())
    const props = tariffListSpy.mock.calls[0][0] as { onSavePlannedAnnualSpend: (value: number) => void }
    props.onSavePlannedAnnualSpend(1500)

    expect(mockPatchFlatMutate).toHaveBeenCalledWith({ flatId: 'flat-123', body: { plannedAnnualSpend: 1500 } })
  })

  it('SettingsPage_TariffsRouteLoading_RendersNothing', () => {
    mockUseUserSettings.mockReturnValue({ settings: undefined, isLoading: true, isError: false })

    renderTariffsRoute()

    expect(screen.queryByTestId('tariff-list-mock')).not.toBeInTheDocument()
    expect(tariffListSpy).not.toHaveBeenCalled()
  })
})
