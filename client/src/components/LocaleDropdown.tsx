import { useState, useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
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
  const dropdownRef = useRef<HTMLDivElement>(null)

  const currentLabel = i18n.language.startsWith('de') ? t('locale.de') : t('locale.en')

  useEffect(() => {
    const close = (e: MouseEvent | TouchEvent) => {
      if (!dropdownRef.current?.contains(e.target as Node)) setIsOpen(false)
    }
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setIsOpen(false)
    }
    document.addEventListener('mousedown', close)
    document.addEventListener('touchstart', close)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('mousedown', close)
      document.removeEventListener('touchstart', close)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [])

  const handleSelect = (value: string) => {
    const prev = i18n.language
    i18n.changeLanguage(value)
    updateLocale(value, { onError: () => i18n.changeLanguage(prev) })
    setIsOpen(false)
  }

  return (
    <div ref={dropdownRef} className="relative" style={dimmed ? { opacity: 0.7 } : undefined}>
      <button
        className="px-3 py-1.5 rounded-full text-sm text-white/80 bg-white/10 border border-white/20"
        onClick={() => setIsOpen(v => !v)}
        aria-label={t('locale.label')}
        aria-expanded={isOpen}
        aria-haspopup="listbox"
      >
        {currentLabel} ▾
      </button>
      {isOpen && (
        <div
          className="absolute right-0 mt-1 min-w-[80px] bg-white/10 backdrop-blur border border-white/20 rounded-xl overflow-hidden"
          role="listbox"
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
        </div>
      )}
    </div>
  )
}
