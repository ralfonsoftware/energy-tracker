import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Popover, PopoverTrigger, PopoverContent } from '@/components/ui/popover'
import type { PeriodOption } from '@/features/decomposition/lib/periods'

type CustomRange = { startDate: string; endDate: string }

type Props = {
  value: PeriodOption
  customRange: CustomRange | null
  onChange: (option: PeriodOption) => void
  onCustomRangeChange: (range: CustomRange) => void
}

const OPTIONS: PeriodOption[] = ['thisWeek', 'thisMonth', 'lastMonth', 'thisYear', 'custom']

const dateInputClass =
  'w-full h-[52px] px-4 rounded-[12px] bg-white/[0.08] border text-white text-base placeholder:text-white/30 outline-none focus:border-white/60 transition-[border-color,box-shadow] disabled:opacity-40 disabled:cursor-not-allowed'
const dateInputStyle = { borderColor: 'rgba(255,255,255,0.15)', colorScheme: 'dark' as const }

export function PeriodSelector({ value, customRange, onChange, onCustomRangeChange }: Props) {
  const { t } = useTranslation('decomposition')
  const [isOpen, setIsOpen] = useState(false)

  const handleSelect = (option: PeriodOption) => {
    setIsOpen(false)
    onChange(option)
  }

  return (
    <div className="flex flex-col gap-2">
      <Popover open={isOpen} onOpenChange={setIsOpen}>
        <PopoverTrigger asChild>
          <button
            type="button"
            aria-haspopup="listbox"
            className="flex items-center justify-between gap-1 px-4 py-3 rounded-input text-sm font-medium text-white/85 bg-white/[0.07] border border-white/[0.12]"
          >
            {t(`period.${value}`)} ▾
          </button>
        </PopoverTrigger>
        <PopoverContent
          role="listbox"
          align="start"
          sideOffset={4}
          className="w-auto min-w-[180px] p-0 bg-white/10 backdrop-blur border border-white/20 rounded-xl overflow-hidden z-50"
        >
          {OPTIONS.map(option => (
            <button
              key={option}
              type="button"
              role="option"
              aria-selected={option === value}
              onClick={() => handleSelect(option)}
              className="block w-full px-4 py-2 text-sm text-left text-white/80 hover:bg-white/10"
            >
              {t(`period.${option}`)}
            </button>
          ))}
        </PopoverContent>
      </Popover>

      {value === 'custom' && (
        <div className="flex gap-2">
          <div className="flex-1 flex flex-col gap-1">
            <label htmlFor="decomposition-period-start" className="sr-only">
              {t('period.customStartLabel')}
            </label>
            <input
              id="decomposition-period-start"
              type="date"
              value={customRange?.startDate ?? ''}
              onChange={e => {
                const startDate = e.target.value
                const endDate = customRange?.endDate ?? ''
                onCustomRangeChange(
                  endDate && startDate > endDate
                    ? { startDate: endDate, endDate: startDate }
                    : { startDate, endDate }
                )
              }}
              className={dateInputClass}
              style={dateInputStyle}
            />
          </div>
          <div className="flex-1 flex flex-col gap-1">
            <label htmlFor="decomposition-period-end" className="sr-only">
              {t('period.customEndLabel')}
            </label>
            <input
              id="decomposition-period-end"
              type="date"
              value={customRange?.endDate ?? ''}
              onChange={e => {
                const endDate = e.target.value
                const startDate = customRange?.startDate ?? ''
                onCustomRangeChange(
                  startDate && endDate < startDate
                    ? { startDate: endDate, endDate: startDate }
                    : { startDate, endDate }
                )
              }}
              className={dateInputClass}
              style={dateInputStyle}
            />
          </div>
        </div>
      )}
    </div>
  )
}
