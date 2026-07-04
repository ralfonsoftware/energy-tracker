import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import { Popover, PopoverTrigger, PopoverContent } from '@/components/ui/popover'
import { useUpdateLocale } from '@/features/settings/hooks/useUpdateLocale'

interface LocaleDropdownProps {
  dimmed?: boolean
}

const LOCALES = [
  { value: 'de-DE' as const, labelKey: 'locale.de' as const },
  { value: 'en-US' as const, labelKey: 'locale.en' as const },
]

export function LocaleDropdown({ dimmed = false }: LocaleDropdownProps) {
  const { t } = useTranslation('onboarding')
  const { mutate: updateLocale } = useUpdateLocale()
  const [isOpen, setIsOpen] = useState(false)

  const currentLabel = i18n.language.startsWith('de') ? t('locale.de') : t('locale.en')

  const handleSelect = (value: string) => {
    const prev = i18n.language
    i18n.changeLanguage(value)
    updateLocale(value, { onError: () => i18n.changeLanguage(prev) })
    setIsOpen(false)
  }

  return (
    <Popover open={isOpen} onOpenChange={setIsOpen}>
      <PopoverTrigger asChild>
        <button
          className="px-3 py-1.5 rounded-full text-sm text-white/80 bg-white/10 border border-white/20"
          style={dimmed ? { opacity: 0.7 } : undefined}
          aria-label={t('locale.label')}
          aria-haspopup="listbox"
        >
          {currentLabel} ▾
        </button>
      </PopoverTrigger>
      <PopoverContent
        role="listbox"
        align="end"
        sideOffset={4}
        style={dimmed ? { opacity: 0.7 } : undefined}
        className="w-auto min-w-[80px] p-0 bg-white/10 backdrop-blur border border-white/20 rounded-xl overflow-hidden z-50"
      >
        {LOCALES.map(({ value, labelKey }) => {
          const isSelected = i18n.language.startsWith(value.split('-')[0])
          return (
            <button
              key={value}
              role="option"
              aria-selected={isSelected}
              className="block w-full px-4 py-2 text-sm text-left text-white/80 hover:bg-white/10"
              onClick={() => handleSelect(value)}
            >
              {t(labelKey)}
            </button>
          )
        })}
      </PopoverContent>
    </Popover>
  )
}
