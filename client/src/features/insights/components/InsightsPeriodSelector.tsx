import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Popover, PopoverTrigger, PopoverContent } from '@/components/ui/popover'

export type InsightsPeriod = 7 | 30 | 90

type Props = {
  value: InsightsPeriod
  onChange: (days: InsightsPeriod) => void
}

const OPTIONS: InsightsPeriod[] = [7, 30, 90]

const OPTION_KEY: Record<InsightsPeriod, string> = {
  7: 'period.sevenDays',
  30: 'period.thirtyDays',
  90: 'period.ninetyDays',
}

export function InsightsPeriodSelector({ value, onChange }: Props) {
  const { t } = useTranslation('insights')
  const [isOpen, setIsOpen] = useState(false)

  const handleSelect = (option: InsightsPeriod) => {
    setIsOpen(false)
    onChange(option)
  }

  return (
    <Popover open={isOpen} onOpenChange={setIsOpen}>
      <PopoverTrigger asChild>
        <button
          type="button"
          aria-haspopup="listbox"
          className="flex min-h-11 min-w-11 items-center justify-between gap-1 px-4 py-3 rounded-input text-sm font-medium text-white/85 bg-white/[0.07] border border-white/[0.12]"
        >
          {t(OPTION_KEY[value])} ▾
        </button>
      </PopoverTrigger>
      <PopoverContent
        role="listbox"
        align="end"
        sideOffset={4}
        className="w-auto min-w-[160px] p-0 bg-white/10 backdrop-blur border border-white/20 rounded-xl overflow-hidden z-50"
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
            {t(OPTION_KEY[option])}
          </button>
        ))}
      </PopoverContent>
    </Popover>
  )
}
