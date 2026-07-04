import { useState, useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import { Sheet, SheetTrigger } from '@/components/ui/sheet'
import { useUserSettings } from '@/features/settings/hooks/useUserSettings'
import { useFlats } from '@/features/settings/hooks/useFlats'
import { useSwitchActiveFlat } from '@/features/settings/hooks/useSwitchActiveFlat'
import { AddFlatForm } from '@/features/settings/components/AddFlatForm'

export function FlatSwitcher() {
  const { t } = useTranslation('common')
  const { settings, isLoading } = useUserSettings()
  const { data: flats, isError: isFlatsError } = useFlats()
  const { mutate: switchFlat, isPending: isSwitching } = useSwitchActiveFlat()
  const [isOpen, setIsOpen] = useState(false)
  const [isAddFlatOpen, setIsAddFlatOpen] = useState(false)
  const dropdownRef = useRef<HTMLDivElement>(null)

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

  const handleSelect = (flatId: string) => {
    if (isSwitching) return
    setIsOpen(false)
    if (flatId === settings?.flatId) return
    switchFlat({ flatId, locale: settings?.locale ?? i18n.language, previousFlatId: settings?.flatId })
  }

  const handleAddFlatOpenChange = (open: boolean) => {
    setIsAddFlatOpen(open)
    setIsOpen(false)
  }

  return (
    <div ref={dropdownRef} className="relative">
      <button
        type="button"
        onClick={() => setIsOpen(v => !v)}
        aria-expanded={isOpen}
        aria-haspopup="listbox"
        className="flex items-center gap-1 px-3 py-1.5 rounded-full text-sm text-white/80 bg-white/10 border border-white/20"
      >
        {isLoading ? t('flatSwitcher.loading') : (settings?.flatName ?? t('flatSwitcher.error'))} ▾
      </button>
      <Sheet open={isAddFlatOpen} onOpenChange={handleAddFlatOpenChange}>
        {isOpen && (
          <div
            role="listbox"
            className="absolute left-0 mt-1 min-w-[180px] bg-white/10 backdrop-blur border border-white/20 rounded-xl overflow-hidden z-10"
          >
            {isFlatsError ? (
              <p className="px-4 py-2 text-sm text-white/50">{t('flatSwitcher.error')}</p>
            ) : (
              (flats ?? []).map(flat => {
                const isActive = flat.flatId === settings?.flatId
                return (
                  <button
                    key={flat.flatId}
                    type="button"
                    role="option"
                    aria-selected={isActive}
                    onClick={() => handleSelect(flat.flatId)}
                    className="block w-full px-4 py-2 text-sm text-left text-white/80 hover:bg-white/10"
                    style={isActive ? { background: 'rgba(255,255,255,0.14)', fontWeight: 600 } : undefined}
                  >
                    {flat.name}
                  </button>
                )
              })
            )}
            <div className="border-t border-white/10" />
            <SheetTrigger asChild>
              <button
                type="button"
                className="block w-full px-4 py-2 text-sm text-left text-white/80 hover:bg-white/10"
              >
                {t('flatSwitcher.addFlat')}
              </button>
            </SheetTrigger>
          </div>
        )}
        <AddFlatForm open={isAddFlatOpen} onOpenChange={handleAddFlatOpenChange} />
      </Sheet>
    </div>
  )
}
