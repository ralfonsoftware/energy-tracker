import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi } from 'vitest'
import { TrendChart } from '@/features/dashboard/components/TrendChart'
import type { DashboardSummary } from '@/features/dashboard/api/dashboardApi'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

vi.mock('@/features/readings/hooks/useReadingHistory', () => ({
  useReadingHistory: () => ({ data: [], isLoading: false, isError: false, refetch: vi.fn() }),
}))

vi.mock('@/features/readings/hooks/usePatchReading', () => ({
  usePatchReading: () => ({ mutate: vi.fn(), isPending: false, isError: false }),
}))

const sevenDays: DashboardSummary['dailyConsumption'] = [
  { date: '2026-06-24', kwhValue: 5 },
  { date: '2026-06-25', kwhValue: 6 },
  { date: '2026-06-26', kwhValue: 4 },
  { date: '2026-06-27', kwhValue: 5 },
  { date: '2026-06-28', kwhValue: 20 },
  { date: '2026-06-29', kwhValue: 6 },
  { date: '2026-06-30', kwhValue: 5 },
]

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
    dailyConsumption: sevenDays,
    ...overrides,
  }
}

describe('TrendChart', () => {
  it('TrendChart_Loading_RendersSevenSkeletonBarsNoChart', () => {
    const { container } = render(<TrendChart dashboard={undefined} flatId="flat-1" />)

    expect(container.querySelectorAll('.animate-pulse')).toHaveLength(7)
    expect(container.querySelector('.recharts-wrapper')).not.toBeInTheDocument()
  })

  it('TrendChart_ZeroOrOneReadingState_RendersNothing', () => {
    const { container } = render(
      <TrendChart dashboard={makeDashboard({ dailyConsumption: [] })} flatId="flat-1" />
    )

    expect(container.firstChild).toBeNull()
    expect(screen.queryByLabelText('trend.historyIconLabel')).not.toBeInTheDocument()
  })

  it('TrendChart_NoSpikeDays_AllBarsUseNonSpikeFillColor', () => {
    const { container } = render(<TrendChart dashboard={makeDashboard()} flatId="flat-1" />)

    const bars = container.querySelectorAll('.recharts-bar-rectangle path')
    expect(bars.length).toBeGreaterThan(0)
    bars.forEach(bar => {
      expect(bar.getAttribute('fill')).toBe('rgba(255,255,255,0.5)')
    })
  })

  it('TrendChart_OneSpikeDayMatchingDailyConsumption_ThatBarUsesSpikeFillColor', () => {
    const { container } = render(
      <TrendChart dashboard={makeDashboard({ spikeDays: ['2026-06-28'] })} flatId="flat-1" />
    )

    const bars = Array.from(container.querySelectorAll('.recharts-bar-rectangle path'))
    const spikeBars = bars.filter(bar => bar.getAttribute('fill') === 'var(--color-accent-spike)')
    const nonSpikeBars = bars.filter(bar => bar.getAttribute('fill') === 'rgba(255,255,255,0.5)')
    expect(spikeBars).toHaveLength(1)
    expect(nonSpikeBars).toHaveLength(6)
  })

  it('TrendChart_Rendered_HistoryIconHasAriaLabelAnd44x44TapTarget', () => {
    render(<TrendChart dashboard={makeDashboard()} flatId="flat-1" />)

    const icon = screen.getByLabelText('trend.historyIconLabel')
    expect(icon).toBeInTheDocument()
    expect(icon.className).toContain('h-11')
    expect(icon.className).toContain('w-11')
  })

  it('TrendChart_HistoryIconClicked_OpensReadingHistorySheet', async () => {
    const user = userEvent.setup()
    render(<TrendChart dashboard={makeDashboard()} flatId="flat-1" />)

    await user.click(screen.getByLabelText('trend.historyIconLabel'))

    expect(screen.getByText('history.title')).toBeInTheDocument()
  })
})
