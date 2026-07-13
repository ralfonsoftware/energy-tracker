import { render, screen, fireEvent } from '@testing-library/react'
import { PeriodSelector } from '@/features/decomposition/components/PeriodSelector'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

function setup(overrides: Partial<React.ComponentProps<typeof PeriodSelector>> = {}) {
  const onChange = vi.fn()
  const onCustomRangeChange = vi.fn()
  const props = {
    value: 'thisMonth' as const,
    customRange: null,
    onChange,
    onCustomRangeChange,
    ...overrides,
  }
  render(<PeriodSelector {...props} />)
  return { onChange, onCustomRangeChange }
}

describe('PeriodSelector', () => {
  it('PeriodSelector_TriggerClicked_ListsAllFiveOptions', () => {
    setup()
    fireEvent.click(screen.getByRole('button', { name: /period.thisMonth/ }))
    expect(screen.getAllByRole('option')).toHaveLength(5)
  })

  it('PeriodSelector_CustomRangeSelected_RevealsStartAndEndDateInputs', () => {
    setup({ value: 'custom', customRange: { startDate: '2026-06-01', endDate: '2026-06-17' } })
    expect(screen.getByLabelText('period.customStartLabel')).toHaveValue('2026-06-01')
    expect(screen.getByLabelText('period.customEndLabel')).toHaveValue('2026-06-17')
  })

  it('PeriodSelector_NonCustomOption_DoesNotShowDateInputs', () => {
    setup()
    expect(screen.queryByLabelText('period.customStartLabel')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('period.customEndLabel')).not.toBeInTheDocument()
  })

  it('PeriodSelector_NonCustomOptionSelected_CallsOnChangeWithOption', () => {
    const { onChange } = setup()
    fireEvent.click(screen.getByRole('button', { name: /period.thisMonth/ }))
    fireEvent.click(screen.getByRole('option', { name: 'period.thisWeek' }))
    expect(onChange).toHaveBeenCalledWith('thisWeek')
  })

  it('PeriodSelector_CustomRangeOptionSelected_CallsOnChangeWithCustom', () => {
    const { onChange } = setup()
    fireEvent.click(screen.getByRole('button', { name: /period.thisMonth/ }))
    fireEvent.click(screen.getByRole('option', { name: 'period.custom' }))
    expect(onChange).toHaveBeenCalledWith('custom')
  })

  it('PeriodSelector_CustomDateInputChanged_CallsOnCustomRangeChange', () => {
    const { onCustomRangeChange } = setup({
      value: 'custom',
      customRange: { startDate: '2026-06-01', endDate: '2026-06-17' },
    })
    const startInput = screen.getByLabelText('period.customStartLabel')
    fireEvent.change(startInput, { target: { value: '2026-06-05' } })
    expect(onCustomRangeChange).toHaveBeenCalledWith({ startDate: '2026-06-05', endDate: '2026-06-17' })
  })

  it('PeriodSelector_StartDateAfterEndDate_SwapsToKeepRangeValid', () => {
    const { onCustomRangeChange } = setup({
      value: 'custom',
      customRange: { startDate: '2026-06-01', endDate: '2026-06-17' },
    })
    const startInput = screen.getByLabelText('period.customStartLabel')
    fireEvent.change(startInput, { target: { value: '2026-06-20' } })
    expect(onCustomRangeChange).toHaveBeenCalledWith({ startDate: '2026-06-17', endDate: '2026-06-20' })
  })

  it('PeriodSelector_EndDateBeforeStartDate_SwapsToKeepRangeValid', () => {
    const { onCustomRangeChange } = setup({
      value: 'custom',
      customRange: { startDate: '2026-06-01', endDate: '2026-06-17' },
    })
    const endInput = screen.getByLabelText('period.customEndLabel')
    fireEvent.change(endInput, { target: { value: '2026-05-20' } })
    expect(onCustomRangeChange).toHaveBeenCalledWith({ startDate: '2026-05-20', endDate: '2026-06-01' })
  })
})
