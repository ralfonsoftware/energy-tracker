import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { vi, describe, it, expect } from 'vitest'
import { DashboardGrid } from '@/features/dashboard/components/DashboardGrid'
import type { CostSummary, DashboardSummary } from '@/features/dashboard/api/dashboardApi'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

const baseCost: CostSummary = {
  dailyAvgCost: 3.45,
  weeklyAvgCost: 24.15,
  projectedMonthlyCost: 105.3,
  hasCostGap: false,
  coveredDays: 30,
  totalDays: 30,
  costDetailAvailable: true,
}

const baseDashboard: DashboardSummary = {
  dailyAvgKwh: 12.3,
  weeklyAvgKwh: 86.1,
  todayKwh: 11.5,
  dailyBudgetKwh: 13.7,
  lastReadingDate: '2026-06-30T08:00:00+02:00',
  spikeDays: [],
  cost: baseCost,
  lastKwhValue: 1234.5,
  dailyConsumption: [],
}

function renderGrid(dashboard: DashboardSummary | undefined, annualKwhBaseline: number | undefined = 2500) {
  return render(
    <MemoryRouter>
      <DashboardGrid dashboard={dashboard} annualKwhBaseline={annualKwhBaseline} />
    </MemoryRouter>
  )
}

describe('DashboardGrid', () => {
  it('DashboardGrid_NoData_AllTilesShowDash', () => {
    renderGrid(undefined)
    expect(screen.getAllByRole('status')).toHaveLength(4)
  })

  it('DashboardGrid_NoReadings_AllTilesShowDash', () => {
    const coldOpen: DashboardSummary = {
      dailyAvgKwh: 0,
      weeklyAvgKwh: 0,
      todayKwh: 0,
      dailyBudgetKwh: 6,
      lastReadingDate: null,
      spikeDays: [],
      cost: null,
      lastKwhValue: null,
      dailyConsumption: [],
    }
    renderGrid(coldOpen)
    expect(screen.getAllByText('—')).toHaveLength(4)
    expect(screen.getByText('lastRead.label lastRead.never')).toBeInTheDocument()
  })

  it('DashboardGrid_WithReadings_ShowsKpiValues', () => {
    renderGrid(baseDashboard)
    expect(screen.getByText('12.3 kWh')).toBeInTheDocument()
    expect(screen.queryByText('costGap.badgeLabel')).not.toBeInTheDocument()
  })

  it('DashboardGrid_CostNull_CostSublineShowsDash', () => {
    renderGrid({ ...baseDashboard, cost: null })
    expect(screen.getAllByText('—')).toHaveLength(3) // tile1 subline, tile2 subline, tile3 headline
    expect(screen.queryByText('costGap.badgeLabel')).not.toBeInTheDocument()
  })

  it('DashboardGrid_GapWithSufficientCoverage_ShowsValueAndBadge', () => {
    renderGrid({
      ...baseDashboard,
      cost: { ...baseCost, hasCostGap: true, costDetailAvailable: true, coveredDays: 20, totalDays: 30 },
    })
    expect(screen.getAllByText('costGap.badgeLabel')).toHaveLength(3)
    expect(screen.getByText('€3.45')).toBeInTheDocument()
  })

  it('DashboardGrid_GapInsufficientCoverage_SuppressesValue', () => {
    renderGrid({
      ...baseDashboard,
      cost: { ...baseCost, hasCostGap: true, costDetailAvailable: false, coveredDays: 3, totalDays: 30 },
    })
    expect(screen.getAllByText('costGap.badgeLabel')).toHaveLength(3)
    expect(screen.getAllByText('—')).toHaveLength(3) // tile1, tile2 sublines + tile3 headline suppressed
  })

  it('DashboardGrid_NoCostGap_NoBadgeRendered', () => {
    renderGrid(baseDashboard)
    expect(screen.queryByText('costGap.badgeLabel')).not.toBeInTheDocument()
  })

  it('DashboardGrid_NonCostKpis_NeverShowBadge', () => {
    renderGrid({
      ...baseDashboard,
      cost: { ...baseCost, hasCostGap: true, costDetailAvailable: true },
    })
    // Only the 3 cost-bearing surfaces (tile1 subline, tile2 subline, tile3 headline) show the badge —
    // Tile 4 (Today, non-cost) never resolves cost data so it cannot render one.
    expect(screen.getAllByText('costGap.badgeLabel')).toHaveLength(3)
    expect(screen.getByText('tile.todayVsBudget')).toBeInTheDocument()
  })
})
