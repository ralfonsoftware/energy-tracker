import { render, screen } from '@testing-library/react'
import { vi, describe, it, expect } from 'vitest'
import { InsightCard } from '@/features/insights/components/InsightCard'
import type { InsightDto } from '@/features/insights/api/insightsApi'
import i18n from '@/lib/i18n'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (k: string, options?: Record<string, unknown>) => (options ? `${k}|${JSON.stringify(options)}` : k),
  }),
}))

const formatCurrency = (value: number) =>
  new Intl.NumberFormat(i18n.language, { style: 'currency', currency: 'EUR' }).format(value)
const formatKwh = (value: number) => new Intl.NumberFormat(i18n.language, { maximumFractionDigits: 1 }).format(value)

const standby: InsightDto = {
  insightId: 'i-1',
  deviceId: 'd-1',
  createdAt: '2026-07-20T00:00:00Z',
  type: 'Standby',
  data: {
    deviceName: 'Coffee Machine',
    meanStandbyWatts: 4.2,
    estimatedMonthlyKwh: 3.0,
    estimatedMonthlyCost: 0.9,
  },
}

const replacement: InsightDto = {
  insightId: 'i-2',
  deviceId: 'd-2',
  createdAt: '2026-07-20T00:00:00Z',
  type: 'Replacement',
  data: {
    deviceName: 'Old Fridge',
    estimatedAnnualKwh: 450,
    estimatedAnnualCost: 135,
    suggestedClass: 'A+++',
    estimatedSavingsEur: 60,
  },
}

const budget: InsightDto = {
  insightId: 'i-3',
  deviceId: null,
  createdAt: '2026-07-20T00:00:00Z',
  type: 'Budget',
  data: {
    projectedAnnualCost: 1200,
    plannedAnnualSpend: 1000,
    overspendEur: 200,
  },
}

const invoiceDeviationAbove: InsightDto = {
  insightId: 'i-4',
  deviceId: null,
  createdAt: '2026-07-20T00:00:00Z',
  type: 'InvoiceDeviation',
  data: {
    projectedAnnualKwh: 4200,
    baselineKwh: 3600,
    deviationPct: 16.7,
    impliedDeltaEur: 150,
    direction: 'above',
  },
}

describe('InsightCard', () => {
  it('InsightCard_StandbyType_RendersDeviceNameWattsAndCost', () => {
    render(<InsightCard insight={standby} />)

    expect(screen.getByText('Coffee Machine')).toBeInTheDocument()
    expect(screen.getByText(`card.standby.watts|${JSON.stringify({ watts: 4.2 })}`)).toBeInTheDocument()
    expect(
      screen.getByText(`card.standby.cost|${JSON.stringify({ cost: formatCurrency(0.9) })}`)
    ).toBeInTheDocument()
  })

  it('InsightCard_ReplacementType_RendersDeviceNameCostSavingsAndSuggestedClass', () => {
    render(<InsightCard insight={replacement} />)

    expect(screen.getByText('Old Fridge')).toBeInTheDocument()
    expect(
      screen.getByText(`card.replacement.annualCost|${JSON.stringify({ cost: formatCurrency(135) })}`)
    ).toBeInTheDocument()
    expect(
      screen.getByText(`card.replacement.savings|${JSON.stringify({ savings: formatCurrency(60) })}`)
    ).toBeInTheDocument()
    expect(screen.getByText('A+++')).toBeInTheDocument()
  })

  it('InsightCard_BudgetType_RendersProjectedPlannedAndOverspendWithErrorAccentBorder', () => {
    const { container } = render(<InsightCard insight={budget} />)

    expect(
      screen.getByText(`card.budget.projected|${JSON.stringify({ cost: formatCurrency(1200) })}`)
    ).toBeInTheDocument()
    expect(
      screen.getByText(`card.budget.planned|${JSON.stringify({ cost: formatCurrency(1000) })}`)
    ).toBeInTheDocument()
    expect(
      screen.getByText(`card.budget.overspend|${JSON.stringify({ cost: formatCurrency(200) })}`)
    ).toBeInTheDocument()
    expect(container.firstChild).toHaveClass('border-accent-error')
  })

  it('InsightCard_InvoiceDeviationTypeAbove_RendersProjectedBaselineAndDelta', () => {
    render(<InsightCard insight={invoiceDeviationAbove} />)

    expect(
      screen.getByText(`card.invoiceDeviation.projected|${JSON.stringify({ kwh: formatKwh(4200) })}`)
    ).toBeInTheDocument()
    expect(
      screen.getByText(`card.invoiceDeviation.baseline|${JSON.stringify({ kwh: formatKwh(3600) })}`)
    ).toBeInTheDocument()
    expect(screen.getByText('card.invoiceDeviation.above')).toBeInTheDocument()
  })

  it('InsightCard_InvoiceDeviationTypeBelow_RendersBelowDirectionLabel', () => {
    const below: InsightDto = {
      ...invoiceDeviationAbove,
      data: { ...invoiceDeviationAbove.data, direction: 'below' },
    }
    render(<InsightCard insight={below} />)

    expect(screen.getByText('card.invoiceDeviation.below')).toBeInTheDocument()
  })
})
