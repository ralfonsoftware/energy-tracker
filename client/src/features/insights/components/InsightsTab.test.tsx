import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { InsightsTab } from '@/features/insights/components/InsightsTab'
import type { DashboardSummary } from '@/features/dashboard/api/dashboardApi'
import type { InsightsResponse } from '@/features/insights/api/insightsApi'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (k: string, options?: Record<string, unknown>) => (options ? `${k}|${JSON.stringify(options)}` : k),
  }),
}))

vi.mock('@/features/dashboard/hooks/useDashboard')
import { useDashboard } from '@/features/dashboard/hooks/useDashboard'
const mockUseDashboard = vi.mocked(useDashboard)

vi.mock('@/features/insights/hooks/useInsights')
import { useInsights } from '@/features/insights/hooks/useInsights'
const mockUseInsights = vi.mocked(useInsights)

vi.mock('@/features/insights/hooks/useTriggerInsights')
import { useTriggerInsights } from '@/features/insights/hooks/useTriggerInsights'
const mockUseTriggerInsights = vi.mocked(useTriggerInsights)

const sevenDaysConsumption = Array.from({ length: 30 }, (_, i) => ({
  date: `2026-06-${String(i + 1).padStart(2, '0')}`,
  kwhValue: 5,
  wasMeterReset: false,
}))

function makeDashboard(overrides: Partial<DashboardSummary> = {}): DashboardSummary {
  return {
    dailyAvgKwh: 5,
    weeklyAvgKwh: 35,
    todayKwh: 5,
    dailyBudgetKwh: 10,
    lastReadingDate: '2026-06-30T00:00:00+02:00',
    spikeDays: [],
    cost: null,
    lastKwhValue: 100,
    dailyConsumption: sevenDaysConsumption,
    readingHistoryDays: 30,
    ...overrides,
  }
}

function mockDashboard(readingHistoryDays: number, isPending = false) {
  mockUseDashboard.mockImplementation(
    () =>
      ({
        data: isPending ? undefined : makeDashboard({ readingHistoryDays }),
        isPending,
        isError: false,
        refetch: vi.fn(),
      }) as unknown as ReturnType<typeof useDashboard>
  )
}

function mockInsights(overrides: {
  data?: InsightsResponse
  isPending?: boolean
  isError?: boolean
  refetch?: ReturnType<typeof vi.fn>
}) {
  mockUseInsights.mockReturnValue({
    data: overrides.data,
    isPending: overrides.isPending ?? false,
    isError: overrides.isError ?? false,
    refetch: overrides.refetch ?? vi.fn(),
  } as unknown as ReturnType<typeof useInsights>)
}

const mockMutate = vi.fn()

describe('InsightsTab', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockUseTriggerInsights.mockReturnValue({
      mutate: mockMutate,
      isPending: false,
    } as unknown as ReturnType<typeof useTriggerInsights>)
  })

  it('InsightsTab_Loading_RendersSkeletonBlocks', () => {
    mockDashboard(30, true)
    mockInsights({ isPending: true })

    const { container } = render(<InsightsTab flatId="flat-1" />)

    expect(container.querySelectorAll('.animate-pulse').length).toBeGreaterThan(0)
  })

  it('InsightsTab_Error_RendersAlertAndRetryCallsBothRefetches', async () => {
    const user = userEvent.setup()
    mockDashboard(30)
    const refetchInsights = vi.fn()
    mockInsights({ isError: true, refetch: refetchInsights })

    render(<InsightsTab flatId="flat-1" />)
    expect(screen.getByRole('alert')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'retry' }))

    expect(refetchInsights).toHaveBeenCalled()
  })

  it('InsightsTab_RunPending_ShowsProgressAndPriorCardsRemainVisible', () => {
    mockDashboard(30)
    mockInsights({
      data: {
        runStatus: { status: 'Pending', startedAt: '2026-07-20T00:00:00Z', completedAt: null },
        insights: [
          {
            insightId: 'i-1',
            deviceId: null,
            createdAt: '2026-07-19T00:00:00Z',
            type: 'Standby',
            data: { deviceName: 'Coffee Machine', meanStandbyWatts: 4, estimatedMonthlyKwh: 2, estimatedMonthlyCost: 0.5 },
          },
        ],
      },
    })

    render(<InsightsTab flatId="flat-1" />)

    expect(screen.getByText('progress.label')).toBeInTheDocument()
    expect(screen.getByText('Coffee Machine')).toBeInTheDocument()
  })

  it('InsightsTab_InsightsPresent_RendersCardsRegardlessOfEmptyStateSignals', () => {
    mockDashboard(10)
    mockInsights({
      data: {
        runStatus: { status: 'Complete', startedAt: '2026-07-19T00:00:00Z', completedAt: '2026-07-19T00:05:00Z' },
        insights: [
          {
            insightId: 'i-1',
            deviceId: null,
            createdAt: '2026-07-19T00:00:00Z',
            type: 'Standby',
            data: { deviceName: 'Coffee Machine', meanStandbyWatts: 4, estimatedMonthlyKwh: 2, estimatedMonthlyCost: 0.5 },
          },
        ],
      },
    })

    render(<InsightsTab flatId="flat-1" />)

    expect(screen.getByText('Coffee Machine')).toBeInTheDocument()
    expect(screen.queryByText('emptyState.insufficientData')).not.toBeInTheDocument()
  })

  it('InsightsTab_NoInsightsAndReadingHistoryUnderThirtyDays_ShowsInsufficientDataMessage', () => {
    mockDashboard(10)
    mockInsights({ data: { runStatus: null, insights: [] } })

    render(<InsightsTab flatId="flat-1" />)

    expect(screen.getByText('emptyState.insufficientData')).toBeInTheDocument()
  })

  it('InsightsTab_NoInsightsAndReadingHistoryAtLeastThirtyDaysNoActiveRun_ShowsNoFindingsMessage', () => {
    mockDashboard(30)
    mockInsights({
      data: {
        runStatus: { status: 'Complete', startedAt: '2026-07-19T00:00:00Z', completedAt: '2026-07-19T00:05:00Z' },
        insights: [],
      },
    })

    render(<InsightsTab flatId="flat-1" />)

    expect(screen.getByText('emptyState.noFindings')).toBeInTheDocument()
  })

  it('InsightsTab_RunStatusNull_TreatedAsNotRunning_ShowsNoFindingsWhenHistorySufficient', () => {
    mockDashboard(30)
    mockInsights({ data: { runStatus: null, insights: [] } })

    render(<InsightsTab flatId="flat-1" />)

    expect(screen.getByText('emptyState.noFindings')).toBeInTheDocument()
    expect(screen.queryByText('progress.label')).not.toBeInTheDocument()
  })

  it('InsightsTab_RunProcessing_RefreshButtonIsDisabled', () => {
    mockDashboard(30)
    mockInsights({
      data: { runStatus: { status: 'Processing', startedAt: '2026-07-19T00:00:00Z', completedAt: null }, insights: [] },
    })

    render(<InsightsTab flatId="flat-1" />)

    expect(screen.getByRole('button', { name: 'refreshButton' })).toBeDisabled()
  })

  it('InsightsTab_RunComplete_RefreshButtonIsEnabledAndClickTriggersMutation', async () => {
    const user = userEvent.setup()
    mockDashboard(30)
    mockInsights({
      data: { runStatus: { status: 'Complete', startedAt: '2026-07-19T00:00:00Z', completedAt: '2026-07-19T00:05:00Z' }, insights: [] },
    })

    render(<InsightsTab flatId="flat-1" />)

    const button = screen.getByRole('button', { name: 'refreshButton' })
    expect(button).not.toBeDisabled()
    await user.click(button)

    expect(mockMutate).toHaveBeenCalled()
  })

  it('InsightsTab_SwitchingPeriodSelector_DoesNotChangeWhichEmptyStateIsShown', async () => {
    const user = userEvent.setup()
    mockDashboard(30)
    mockInsights({ data: { runStatus: null, insights: [] } })

    render(<InsightsTab flatId="flat-1" />)
    expect(screen.getByText('emptyState.noFindings')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /period.thirtyDays/ }))
    await user.click(screen.getByRole('option', { name: 'period.sevenDays' }))

    expect(screen.getByText('emptyState.noFindings')).toBeInTheDocument()
    expect(screen.queryByText('emptyState.insufficientData')).not.toBeInTheDocument()
  })
})
