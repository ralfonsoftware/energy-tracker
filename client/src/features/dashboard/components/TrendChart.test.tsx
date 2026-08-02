import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, it, expect, vi } from 'vitest'
import { TrendChart } from '@/features/dashboard/components/TrendChart'
import type { DashboardSummary } from '@/features/dashboard/api/dashboardApi'
import i18n from '@/lib/i18n'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (k: string, options?: Record<string, unknown>) => (options ? `${k}|${JSON.stringify(options)}` : k),
  }),
}))

vi.mock('@/features/readings/hooks/useReadingHistory', () => ({
  useReadingHistory: () => ({
    data: { pages: [{ items: [], totalCount: 0 }] },
    isLoading: false,
    isError: false,
    refetch: vi.fn(),
    fetchNextPage: vi.fn(),
    hasNextPage: false,
    isFetchingNextPage: false,
  }),
}))

vi.mock('@/features/readings/hooks/usePatchReading', () => ({
  usePatchReading: () => ({ mutate: vi.fn(), isPending: false, isError: false }),
}))

const sevenDays: DashboardSummary['dailyConsumption'] = [
  { date: '2026-06-24', kwhValue: 5, wasMeterReset: false },
  { date: '2026-06-25', kwhValue: 6, wasMeterReset: false },
  { date: '2026-06-26', kwhValue: 4, wasMeterReset: false },
  { date: '2026-06-27', kwhValue: 5, wasMeterReset: false },
  { date: '2026-06-28', kwhValue: 20, wasMeterReset: false },
  { date: '2026-06-29', kwhValue: 6, wasMeterReset: false },
  { date: '2026-06-30', kwhValue: 5, wasMeterReset: false },
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
    readingHistoryDays: 30,
    ...overrides,
  }
}

function makeDailyConsumption(days: number): DashboardSummary['dailyConsumption'] {
  const end = new Date('2026-06-30T00:00:00Z')
  return Array.from({ length: days }, (_, i) => {
    const date = new Date(end)
    date.setUTCDate(end.getUTCDate() - (days - 1 - i))
    return { date: date.toISOString().slice(0, 10), kwhValue: 5, wasMeterReset: false }
  })
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

  it('TrendChart_SpikeDayAlsoMeterResetDay_ThatBarUsesDistinctCombinedPatternFromResetOnly', () => {
    const withResetOnlyAndCombined = sevenDays.map(point => {
      if (point.date === '2026-06-27') return { ...point, wasMeterReset: true, kwhValue: 0 }
      if (point.date === '2026-06-28') return { ...point, wasMeterReset: true, kwhValue: 0 }
      return point
    })
    const { container } = render(
      <TrendChart
        dashboard={makeDashboard({ dailyConsumption: withResetOnlyAndCombined, spikeDays: ['2026-06-28'] })}
        flatId="flat-1"
      />
    )

    const bars = Array.from(container.querySelectorAll('.recharts-bar-rectangle path'))
    const patterns = Array.from(container.querySelectorAll('pattern'))
    const resetOnlyIndex = withResetOnlyAndCombined.findIndex(point => point.date === '2026-06-27')
    const combinedIndex = withResetOnlyAndCombined.findIndex(point => point.date === '2026-06-28')

    const resetOnlyFill = bars[resetOnlyIndex].getAttribute('fill')
    const combinedFill = bars[combinedIndex].getAttribute('fill')
    expect(resetOnlyFill).toMatch(/^url\(#.+\)$/)
    expect(combinedFill).toMatch(/^url\(#.+\)$/)
    expect(combinedFill).not.toBe(resetOnlyFill)

    const resetPatternId = resetOnlyFill?.match(/^url\(#(.+)\)$/)?.[1]
    const combinedPatternId = combinedFill?.match(/^url\(#(.+)\)$/)?.[1]
    const resetPatternEl = patterns.find(p => p.getAttribute('id') === resetPatternId)
    const combinedPatternEl = patterns.find(p => p.getAttribute('id') === combinedPatternId)
    expect(resetPatternEl?.querySelectorAll('line')).toHaveLength(1)
    expect(combinedPatternEl?.querySelectorAll('line')).toHaveLength(2)
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

  it('TrendChart_OneMeterResetDay_ThatBarUsesResetHatchFillAndOthersDoNot', () => {
    // kwhValue: 0 matches production reality — KpiCalculator always clamps a reset interval to 0 kWh.
    const withReset = sevenDays.map(point =>
      point.date === '2026-06-28' ? { ...point, kwhValue: 0, wasMeterReset: true } : point
    )
    const { container } = render(
      <TrendChart dashboard={makeDashboard({ dailyConsumption: withReset })} flatId="flat-1" />
    )

    const bars = Array.from(container.querySelectorAll('.recharts-bar-rectangle path'))
    const patternId = container.querySelector('pattern')?.getAttribute('id')
    const resetBars = bars.filter(bar => bar.getAttribute('fill') === `url(#${patternId})`)
    const normalBars = bars.filter(bar => bar.getAttribute('fill') === 'rgba(255,255,255,0.5)')
    expect(bars).toHaveLength(7)
    expect(resetBars).toHaveLength(1)
    expect(normalBars).toHaveLength(6)
  })

  it('TrendChart_NoMeterResetDays_NoAccessibleSummaryRendered', () => {
    render(<TrendChart dashboard={makeDashboard()} flatId="flat-1" />)

    expect(screen.queryByText(/trend\.meterResetSummary/)).not.toBeInTheDocument()
  })

  it('TrendChart_HasMeterResetDay_RendersAccessibleSummaryTextWithLocaleFormattedDate', () => {
    const withReset = sevenDays.map(point =>
      point.date === '2026-06-28' ? { ...point, kwhValue: 0, wasMeterReset: true } : point
    )
    render(<TrendChart dashboard={makeDashboard({ dailyConsumption: withReset })} flatId="flat-1" />)

    const expectedDate = new Intl.DateTimeFormat(i18n.language, {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      timeZone: 'UTC',
    }).format(new Date('2026-06-28'))
    expect(screen.getByText(`trend.meterResetSummary|${JSON.stringify({ dates: expectedDate })}`)).toBeInTheDocument()
  })

  it('TrendChart_NoSpikeDays_NoAccessibleSpikeSummaryRendered', () => {
    render(<TrendChart dashboard={makeDashboard()} flatId="flat-1" />)

    expect(screen.queryByText(/trend\.spikeSummary/)).not.toBeInTheDocument()
  })

  it('TrendChart_HasSpikeDay_RendersAccessibleSpikeSummaryTextWithLocaleFormattedDate', () => {
    render(<TrendChart dashboard={makeDashboard({ spikeDays: ['2026-06-28'] })} flatId="flat-1" />)

    const expectedDate = new Intl.DateTimeFormat(i18n.language, {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      timeZone: 'UTC',
    }).format(new Date('2026-06-28'))
    expect(screen.getByText(`trend.spikeSummary|${JSON.stringify({ dates: expectedDate })}`)).toBeInTheDocument()
  })

  it('TrendChart_SpikeAndResetSameDay_DateAppearsInBothAccessibleSummaries', () => {
    const withReset = sevenDays.map(point =>
      point.date === '2026-06-28' ? { ...point, kwhValue: 0, wasMeterReset: true } : point
    )
    render(
      <TrendChart dashboard={makeDashboard({ dailyConsumption: withReset, spikeDays: ['2026-06-28'] })} flatId="flat-1" />
    )

    const expectedDate = new Intl.DateTimeFormat(i18n.language, {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      timeZone: 'UTC',
    }).format(new Date('2026-06-28'))
    expect(screen.getByText(`trend.meterResetSummary|${JSON.stringify({ dates: expectedDate })}`)).toBeInTheDocument()
    expect(screen.getByText(`trend.spikeSummary|${JSON.stringify({ dates: expectedDate })}`)).toBeInTheDocument()
  })

  it('TrendChart_DefaultDays_CardTitleShowsSeven', () => {
    render(<TrendChart dashboard={makeDashboard()} flatId="flat-1" />)

    expect(screen.getByText(`trend.cardTitle|${JSON.stringify({ days: 7 })}`)).toBeInTheDocument()
  })

  it('TrendChart_Days30_CardTitleShowsThirty', () => {
    render(<TrendChart dashboard={makeDashboard()} flatId="flat-1" days={30} />)

    expect(screen.getByText(`trend.cardTitle|${JSON.stringify({ days: 30 })}`)).toBeInTheDocument()
  })

  it('TrendChart_Days30_XAxisUsesDayMonthLabelsNotNarrowWeekday', () => {
    const thirtyDays = makeDailyConsumption(30)
    const { container } = render(
      <TrendChart dashboard={makeDashboard({ dailyConsumption: thirtyDays })} flatId="flat-1" days={30} />
    )

    const expectedLabel = new Intl.DateTimeFormat(i18n.language, {
      day: 'numeric',
      month: 'short',
      timeZone: 'UTC',
    }).format(new Date(thirtyDays[0].date))
    expect(container.querySelectorAll('.recharts-bar-rectangle path')).toHaveLength(30)
    expect(screen.getByText(expectedLabel)).toBeInTheDocument()
  })

  it('TrendChart_Days90_XAxisShowsAroundSixSparseTickLabels', () => {
    const ninetyDays = makeDailyConsumption(90)
    const { container } = render(
      <TrendChart dashboard={makeDashboard({ dailyConsumption: ninetyDays })} flatId="flat-1" days={90} />
    )

    expect(container.querySelectorAll('.recharts-bar-rectangle path')).toHaveLength(90)
    const tickLabels = container.querySelectorAll('.recharts-cartesian-axis-tick-value')
    expect(tickLabels.length).toBeGreaterThanOrEqual(5)
    expect(tickLabels.length).toBeLessThanOrEqual(7)
  })

  it('TrendChart_Days7_XAxisShowsAllSevenLabels', () => {
    const { container } = render(<TrendChart dashboard={makeDashboard()} flatId="flat-1" days={7} />)

    const tickLabels = container.querySelectorAll('.recharts-cartesian-axis-tick-value')
    expect(tickLabels).toHaveLength(7)
  })
})
