import { render, screen, fireEvent } from '@testing-library/react'
import { vi, describe, it, expect } from 'vitest'
import { InsightsPeriodSelector } from '@/features/insights/components/InsightsPeriodSelector'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

describe('InsightsPeriodSelector', () => {
  it('InsightsPeriodSelector_TriggerClicked_ListsAllThreeOptions', () => {
    render(<InsightsPeriodSelector value={30} onChange={vi.fn()} />)
    fireEvent.click(screen.getByRole('button', { name: /period.thirtyDays/ }))
    expect(screen.getAllByRole('option')).toHaveLength(3)
  })

  it('InsightsPeriodSelector_SevenDaysOptionSelected_CallsOnChangeWithSeven', () => {
    const onChange = vi.fn()
    render(<InsightsPeriodSelector value={30} onChange={onChange} />)
    fireEvent.click(screen.getByRole('button', { name: /period.thirtyDays/ }))
    fireEvent.click(screen.getByRole('option', { name: 'period.sevenDays' }))
    expect(onChange).toHaveBeenCalledWith(7)
  })

  it('InsightsPeriodSelector_ThirtyDaysOptionSelected_CallsOnChangeWithThirty', () => {
    const onChange = vi.fn()
    render(<InsightsPeriodSelector value={7} onChange={onChange} />)
    fireEvent.click(screen.getByRole('button', { name: /period.sevenDays/ }))
    fireEvent.click(screen.getByRole('option', { name: 'period.thirtyDays' }))
    expect(onChange).toHaveBeenCalledWith(30)
  })

  it('InsightsPeriodSelector_NinetyDaysOptionSelected_CallsOnChangeWithNinety', () => {
    const onChange = vi.fn()
    render(<InsightsPeriodSelector value={7} onChange={onChange} />)
    fireEvent.click(screen.getByRole('button', { name: /period.sevenDays/ }))
    fireEvent.click(screen.getByRole('option', { name: 'period.ninetyDays' }))
    expect(onChange).toHaveBeenCalledWith(90)
  })

  it('InsightsPeriodSelector_CurrentValue_IsMarkedAriaSelected', () => {
    render(<InsightsPeriodSelector value={90} onChange={vi.fn()} />)
    fireEvent.click(screen.getByRole('button', { name: /period.ninetyDays/ }))
    expect(screen.getByRole('option', { name: 'period.ninetyDays' })).toHaveAttribute('aria-selected', 'true')
    expect(screen.getByRole('option', { name: 'period.sevenDays' })).toHaveAttribute('aria-selected', 'false')
  })
})
