import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import { Popover, PopoverTrigger, PopoverContent } from '@/components/ui/popover'
import { useUpdateLocale } from '../hooks/useUpdateLocale'

const LOCALES = [
  { value: 'de-DE', label: 'Deutsch' },
  { value: 'en-US', label: 'English' },
] as const

export function LocaleSettings() {
  const { t } = useTranslation('settings')
  const { mutate: updateLocale } = useUpdateLocale()
  const [isOpen, setIsOpen] = useState(false)

  const currentLabel = i18n.language.startsWith('de') ? t('locale.de') : t('locale.en')

  return (
    <Popover open={isOpen} onOpenChange={setIsOpen}>
      <PopoverTrigger asChild>
        <button
          className="w-full flex items-center justify-between px-4 py-[13px] min-h-[48px] text-left"
          aria-haspopup="listbox"
        >
          <span className="text-white text-[15px]">{t('locale.title')}</span>
          <span className="flex items-center gap-1" style={{ color: 'rgba(255,255,255,0.45)', fontSize: '14px' }}>
            {currentLabel}
            <span style={{ color: 'rgba(255,255,255,0.3)' }}>›</span>
          </span>
        </button>
      </PopoverTrigger>

      <PopoverContent
        role="listbox"
        align="end"
        sideOffset={4}
        className="w-auto min-w-[120px] p-0 rounded-xl overflow-hidden"
        style={{
          background: 'rgba(30,30,50,0.95)',
          backdropFilter: 'blur(20px)',
          border: '1px solid rgba(255,255,255,0.15)',
        }}
      >
        {LOCALES.map(({ value, label }) => {
          const isSelected = i18n.language === value || i18n.language.startsWith(value.split('-')[0])
          return (
            <button
              key={value}
              role="option"
              aria-selected={isSelected}
              className="block w-full px-4 py-2.5 text-sm text-left hover:bg-white/10 transition-colors"
              style={{ color: isSelected ? '#ffffff' : 'rgba(255,255,255,0.70)' }}
              onClick={() => {
                const prev = i18n.language
                i18n.changeLanguage(value)
                updateLocale(value, { onError: () => i18n.changeLanguage(prev) })
                setIsOpen(false)
              }}
            >
              {label}
            </button>
          )
        })}
      </PopoverContent>
    </Popover>
  )
}
