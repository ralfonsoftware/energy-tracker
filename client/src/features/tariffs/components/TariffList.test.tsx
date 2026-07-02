import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { TariffList } from '@/features/tariffs/components/TariffList'
import type { TariffResponse } from '@/features/tariffs/api/tariffApi'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (k: string, opts?: Record<string, unknown>) => (opts?.date ? `${k}:${opts.date}` : k),
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

const pastTariff: TariffResponse = {
  tariffId: 'tariff-past',
  effectiveDate: '2020-01-01T00:00:00Z',
  pricePerKwh: 0.2285,
  monthlyBaseFee: 12,
  providerName: 'E.ON',
  contractStartDate: null,
  contractDurationMonths: null,
  isLocked: false,
}

const futureTariff: TariffResponse = {
  tariffId: 'tariff-future',
  effectiveDate: '2099-01-01T00:00:00Z',
  pricePerKwh: 0.31,
  monthlyBaseFee: 15,
  providerName: null,
  contractStartDate: null,
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

function renderList(flatId: string | undefined) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <TariffList flatId={flatId} />
      </MemoryRouter>
    </QueryClientProvider>
  )
}

describe('TariffList', () => {
  beforeEach(() => {
    mockUseTariffs.mockReset()
    mockNavigate.mockReset()
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
})
