import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { TariffList } from '@/features/tariffs/components/TariffList'
import type { TariffResponse } from '@/features/tariffs/api/tariffApi'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (k: string, opts?: Record<string, unknown>) =>
      opts?.date ? `${k}:${opts.date}` : opts?.total ? `${k}:${opts.total}` : k,
    i18n: { language: 'en-US' },
  }),
}))

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom')
  return { ...actual, useNavigate: () => mockNavigate }
})
const mockNavigate = vi.fn()

vi.mock('@/features/tariffs/hooks/useTariffs')
import { useTariffs } from '@/features/tariffs/hooks/useTariffs'
const mockUseTariffs = vi.mocked(useTariffs)

vi.mock('@/features/tariffs/hooks/useCreateTariff')
import { useCreateTariff } from '@/features/tariffs/hooks/useCreateTariff'
const mockUseCreateTariff = vi.mocked(useCreateTariff)

vi.mock('@/features/tariffs/hooks/usePatchTariff')
import { usePatchTariff } from '@/features/tariffs/hooks/usePatchTariff'
const mockUsePatchTariff = vi.mocked(usePatchTariff)

const pastTariff: TariffResponse = {
  tariffId: 'tariff-past',
  contractStartDate: '2020-01-01T00:00:00Z',
  pricePerKwh: 0.2285,
  monthlyBaseFee: 12,
  providerName: 'E.ON',
  contractDurationMonths: null,
  isLocked: false,
}

const futureTariff: TariffResponse = {
  tariffId: 'tariff-future',
  contractStartDate: '2099-01-01T00:00:00Z',
  pricePerKwh: 0.31,
  monthlyBaseFee: 15,
  providerName: null,
  contractDurationMonths: null,
  isLocked: false,
}

function setupTariffs(options?: { isLoading?: boolean; isError?: boolean; data?: TariffResponse[] }) {
  const refetch = vi.fn()
  mockUseTariffs.mockReturnValue({
    data: options?.data,
    isLoading: options?.isLoading ?? false,
    isError: options?.isError ?? false,
    refetch,
  } as unknown as ReturnType<typeof useTariffs>)
  return { refetch }
}

function renderList(
  flatId: string | undefined,
  overrides?: {
    annualKwhBaseline?: number
    plannedAnnualSpend?: number | null
    onSavePlannedAnnualSpend?: (value: number) => void
    isSavingPlannedAnnualSpend?: boolean
    isPlannedAnnualSpendSaveError?: boolean
  }
) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <TariffList
          flatId={flatId}
          annualKwhBaseline={overrides?.annualKwhBaseline}
          plannedAnnualSpend={overrides?.plannedAnnualSpend}
          onSavePlannedAnnualSpend={overrides?.onSavePlannedAnnualSpend ?? vi.fn()}
          isSavingPlannedAnnualSpend={overrides?.isSavingPlannedAnnualSpend ?? false}
          isPlannedAnnualSpendSaveError={overrides?.isPlannedAnnualSpendSaveError ?? false}
        />
      </MemoryRouter>
    </QueryClientProvider>
  )
}

describe('TariffList', () => {
  beforeEach(() => {
    mockUseTariffs.mockReset()
    mockNavigate.mockReset()
    mockUseCreateTariff.mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
    } as unknown as ReturnType<typeof useCreateTariff>)
    mockUsePatchTariff.mockReturnValue({
      mutateAsync: vi.fn(),
      isPending: false,
    } as unknown as ReturnType<typeof usePatchTariff>)
  })

  it('TariffList_Loading_RendersSkeletonWithNoListItems', () => {
    setupTariffs({ isLoading: true })

    renderList('flat-1')

    expect(screen.queryAllByRole('listitem')).toHaveLength(0)
  })

  it('TariffList_LoadError_ShowsAlertAndRetryCallsRefetch', async () => {
    const user = userEvent.setup()
    const { refetch } = setupTariffs({ isError: true })

    renderList('flat-1')

    expect(screen.getByRole('alert')).toHaveTextContent('list.loadError')
    await user.click(screen.getByRole('button', { name: 'list.retry' }))

    expect(refetch).toHaveBeenCalled()
  })

  it('TariffList_EmptyList_RendersEmptyStateTextAndNoListItems', () => {
    setupTariffs({ data: [] })

    renderList('flat-1')

    expect(screen.getByText('list.empty')).toBeInTheDocument()
    expect(screen.queryAllByRole('listitem')).toHaveLength(0)
  })

  it('TariffList_PopulatedList_RendersInGivenOrderWithFormattedValues', () => {
    setupTariffs({ data: [futureTariff, pastTariff] })

    renderList('flat-1')

    const items = screen.getAllByRole('listitem')
    expect(items).toHaveLength(2)
    // API order preserved — futureTariff first, no client re-sort
    expect(items[0]).toHaveTextContent('list.upcomingLabel')
    expect(items[1]).toHaveTextContent('E.ON')
  })

  it('TariffList_FutureDatedEntry_ShowsUpcomingLabel', () => {
    setupTariffs({ data: [futureTariff] })

    renderList('flat-1')

    expect(screen.getByText(/list\.upcomingLabel/)).toBeInTheDocument()
  })

  it('TariffList_PastDatedEntry_DoesNotShowUpcomingLabel', () => {
    setupTariffs({ data: [pastTariff] })

    renderList('flat-1')

    expect(screen.queryByText(/list\.upcomingLabel/)).not.toBeInTheDocument()
  })

  it('TariffList_ContractStartsTodayViewedWestOfUtc_DoesNotShowUpcomingLabel', () => {
    // Frozen "now" = 2026-06-15T01:00:00Z, which is 2026-06-14 22:00 local in
    // America/Sao_Paulo (UTC-3) — local "today" is 2026-06-14, UTC "today" is 2026-06-15.
    // A tariff whose contractStartDate is "2026-06-15T00:00:00Z" (local calendar date
    // 2026-06-14, i.e. active today) must NOT be labeled upcoming. The old UTC-extraction
    // comparison would wrongly compare UTC "2026-06-15" against local "2026-06-14" and
    // label it upcoming.
    vi.useFakeTimers()
    vi.setSystemTime(new Date('2026-06-15T01:00:00Z'))
    vi.stubEnv('TZ', 'America/Sao_Paulo')

    try {
      const todayTariff: TariffResponse = {
        tariffId: 'tariff-today',
        contractStartDate: '2026-06-15T00:00:00Z',
        pricePerKwh: 0.25,
        monthlyBaseFee: 10,
        providerName: 'Vattenfall',
        contractDurationMonths: null,
        isLocked: false,
      }
      setupTariffs({ data: [todayTariff] })

      renderList('flat-1')

      expect(screen.queryByText(/list\.upcomingLabel/)).not.toBeInTheDocument()
    } finally {
      vi.useRealTimers()
      vi.unstubAllEnvs()
    }
  })

  it('TariffList_AddTariffButton_OpensSheetWithTariffForm', async () => {
    const user = userEvent.setup()
    setupTariffs({ data: [] })

    renderList('flat-1')
    await user.click(screen.getByRole('button', { name: 'list.addButton' }))

    expect(screen.getByRole('button', { name: 'form.saveButton' })).toBeInTheDocument()
  })

  it('TariffList_FlatIdUndefined_AddTariffButtonDisabled', () => {
    setupTariffs({ data: [] })

    renderList(undefined)

    expect(screen.getByRole('button', { name: 'list.addButton' })).toBeDisabled()
  })

  it('TariffList_BackButton_NavigatesToSettings', async () => {
    const user = userEvent.setup()
    setupTariffs({ data: [] })

    renderList('flat-1')
    await user.click(screen.getByRole('button', { name: /list\.back/ }))

    expect(mockNavigate).toHaveBeenCalledWith('/settings')
  })

  it('TariffList_TapRow_OpensEditSheetWithPrefilledTariffData', async () => {
    const user = userEvent.setup()
    setupTariffs({ data: [pastTariff] })

    renderList('flat-1')
    await user.click(screen.getByText('E.ON'))

    expect(screen.getByText('form.editTitle')).toBeInTheDocument()
    const priceInput = document.querySelector('input[name="pricePerKwh"]') as HTMLInputElement
    expect(priceInput.value).toBe('0.2285')
  })

  it('TariffList_PlannedAnnualSpendSection_RendersCurrentValueAndSavesParsedNumber', async () => {
    const user = userEvent.setup()
    const onSave = vi.fn()
    setupTariffs({ data: [pastTariff] })

    renderList('flat-1', { plannedAnnualSpend: 1200, annualKwhBaseline: 2500, onSavePlannedAnnualSpend: onSave })

    const input = screen.getByLabelText('budget.title') as HTMLInputElement
    expect(input.value).toBe('1200')

    await user.clear(input)
    await user.type(input, '1500')
    await user.click(screen.getByRole('button', { name: 'budget.saveButton' }))

    expect(onSave).toHaveBeenCalledWith(1500)
  })

  it('TariffList_PlannedAnnualSpendHelperText_ShownWhenActiveTariffExists', () => {
    setupTariffs({ data: [pastTariff] })

    renderList('flat-1', { annualKwhBaseline: 2500 })

    expect(screen.getByText(/budget\.helperText/)).toBeInTheDocument()
  })

  it('TariffList_PlannedAnnualSpendHelperText_OmittedWhenOnlyUpcomingTariffs', () => {
    setupTariffs({ data: [futureTariff] })

    renderList('flat-1', { annualKwhBaseline: 2500 })

    expect(screen.queryByText(/budget\.helperText/)).not.toBeInTheDocument()
  })

  it('TariffList_PlannedAnnualSpendHelperText_OmittedWhenListEmpty', () => {
    setupTariffs({ data: [] })

    renderList('flat-1', { annualKwhBaseline: 2500 })

    expect(screen.queryByText(/budget\.helperText/)).not.toBeInTheDocument()
  })

  it('TariffList_PlannedAnnualSpendHelperText_IncludesComputedAutoDerivedTotal', () => {
    setupTariffs({ data: [pastTariff] })

    renderList('flat-1', { annualKwhBaseline: 2500 })

    // 2500 kWh * 0.2285 €/kWh + 12 €/mo * 12 = 715.25
    expect(screen.getByText(/budget\.helperText:/)).toHaveTextContent('715.25')
  })

  it('TariffList_PlannedAnnualSpendSaveFails_SaveButtonStaysEnabledForRetry', async () => {
    const user = userEvent.setup()
    const onSave = vi.fn()
    setupTariffs({ data: [pastTariff] })
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const tree = (isError: boolean) => (
      <QueryClientProvider client={queryClient}>
        <MemoryRouter>
          <TariffList
            flatId="flat-1"
            plannedAnnualSpend={1200}
            onSavePlannedAnnualSpend={onSave}
            isSavingPlannedAnnualSpend={false}
            isPlannedAnnualSpendSaveError={isError}
          />
        </MemoryRouter>
      </QueryClientProvider>
    )

    const { rerender } = render(tree(false))
    const input = screen.getByLabelText('budget.title') as HTMLInputElement
    await user.clear(input)
    await user.type(input, '99999')
    await user.click(screen.getByRole('button', { name: 'budget.saveButton' }))

    rerender(tree(true))

    expect(screen.getByRole('button', { name: 'budget.saveButton' })).toBeEnabled()
  })

  it('TariffList_PlannedAnnualSpendPropChangesWhileNotDirty_InputResyncs', () => {
    setupTariffs({ data: [pastTariff] })
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const tree = (plannedAnnualSpend: number) => (
      <QueryClientProvider client={queryClient}>
        <MemoryRouter>
          <TariffList
            flatId="flat-1"
            plannedAnnualSpend={plannedAnnualSpend}
            onSavePlannedAnnualSpend={vi.fn()}
            isSavingPlannedAnnualSpend={false}
            isPlannedAnnualSpendSaveError={false}
          />
        </MemoryRouter>
      </QueryClientProvider>
    )

    const { rerender } = render(tree(1200))
    rerender(tree(1500))

    const input = screen.getByLabelText('budget.title') as HTMLInputElement
    expect(input.value).toBe('1500')
  })

  it('TariffList_EditSheetDismissedWhilePatchPending_StaysOpen', async () => {
    const user = userEvent.setup()
    mockUsePatchTariff.mockReturnValue({
      mutateAsync: vi.fn(),
      isPending: true,
    } as unknown as ReturnType<typeof usePatchTariff>)
    setupTariffs({ data: [pastTariff] })

    renderList('flat-1')
    await user.click(screen.getByText('E.ON'))
    expect(screen.getByText('form.editTitle')).toBeInTheDocument()

    await user.keyboard('{Escape}')

    expect(screen.getByText('form.editTitle')).toBeInTheDocument()
  })
})
