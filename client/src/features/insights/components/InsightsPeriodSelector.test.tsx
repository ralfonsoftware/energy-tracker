import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { vi, describe, it, expect } from 'vitest'
import userEvent from '@testing-library/user-event'
import { InsightsPeriodSelector, type InsightsPeriod } from '@/features/insights/components/InsightsPeriodSelector'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

const labelToValue: Record<string, InsightsPeriod> = {
  'period.sevenDays': 7,
  'period.thirtyDays': 30,
  'period.ninetyDays': 90,
}

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

  it('InsightsPeriodSelector_ArrowDownThenEnterPressedWhileOpen_MovesFocusAndSelectsNextOption', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn()
    render(<InsightsPeriodSelector value={30} onChange={onChange} />)
    await user.click(screen.getByRole('button', { name: /period.thirtyDays/ }))
    const options = screen.getAllByRole('option')
    const activeIndex = options.findIndex(o => o.getAttribute('aria-selected') === 'true')
    expect(options[activeIndex]).toHaveFocus()
    await user.keyboard('{ArrowDown}')
    const nextOption = options[activeIndex + 1]
    expect(nextOption).toHaveFocus()
    await user.keyboard('{Enter}')
    expect(onChange).toHaveBeenCalledWith(labelToValue[nextOption.textContent ?? ''])
  })

  it('InsightsPeriodSelector_HomeThenEndPressedWhileOpen_MovesFocusToFirstThenLastOption', async () => {
    const user = userEvent.setup()
    render(<InsightsPeriodSelector value={30} onChange={vi.fn()} />)
    await user.click(screen.getByRole('button', { name: /period.thirtyDays/ }))
    const options = screen.getAllByRole('option')
    await user.keyboard('{Home}')
    expect(options[0]).toHaveFocus()
    await user.keyboard('{End}')
    expect(options[options.length - 1]).toHaveFocus()
  })

  it('InsightsPeriodSelector_EscapeKeyPressedWhileOpen_ClosesDropdown', async () => {
    render(<InsightsPeriodSelector value={30} onChange={vi.fn()} />)
    fireEvent.click(screen.getByRole('button', { name: /period.thirtyDays/ }))
    expect(screen.getByRole('listbox')).toBeInTheDocument()
    fireEvent.keyDown(screen.getByRole('listbox'), { key: 'Escape' })
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument())
  })
})
