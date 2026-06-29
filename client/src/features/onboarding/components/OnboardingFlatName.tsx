import { useState, useEffect, useRef } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import { useUpdateLocale } from '@/features/settings/hooks/useUpdateLocale'
import { flatNameSchema, type FlatNameFormValues } from '../schemas/onboardingSchema'

interface OnboardingFlatNameProps {
  initialValue: string
  onContinue: (name: string) => void
  onBack: () => void
}

export function OnboardingFlatName({ initialValue, onContinue, onBack }: OnboardingFlatNameProps) {
  const { t } = useTranslation('onboarding')
  const { mutate: updateLocale } = useUpdateLocale()
  const [isLocaleOpen, setIsLocaleOpen] = useState(false)
  const dropdownRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (!dropdownRef.current?.contains(e.target as Node)) setIsLocaleOpen(false)
    }
    document.addEventListener('mousedown', handler)
    return () => document.removeEventListener('mousedown', handler)
  }, [])

  const {
    register,
    handleSubmit,
    watch,
    formState: { errors, touchedFields },
  } = useForm<FlatNameFormValues>({
    resolver: zodResolver(flatNameSchema),
    defaultValues: { name: initialValue },
    mode: 'onTouched',
  })

  const nameValue = watch('name')
  const isContinueEnabled = nameValue.trim().length > 0

  const onSubmit = (data: FlatNameFormValues) => {
    onContinue(data.name.trim())
  }

  const currentLabel = i18n.language.startsWith('de') ? t('locale.de') : t('locale.en')
  const locales = [
    { value: 'de-DE', label: t('locale.de') },
    { value: 'en-US', label: t('locale.en') },
  ] as const

  return (
    <div className="relative flex-1 flex flex-col" style={{ background: '#0f1235' }}>

      {/* Locale pill — reduced opacity in Step 1 per UX mockup */}
      <div ref={dropdownRef} className="absolute top-4 right-4 z-20" style={{ opacity: 0.7 }}>
        <button
          className="px-3 py-1.5 rounded-full text-sm text-white/80 bg-white/10 border border-white/20"
          onClick={() => setIsLocaleOpen(v => !v)}
          aria-label={t('locale.label')}
          aria-expanded={isLocaleOpen}
        >
          {currentLabel} ▾
        </button>
        {isLocaleOpen && (
          <div className="absolute right-0 mt-1 min-w-[80px] bg-white/10 backdrop-blur border border-white/20 rounded-xl overflow-hidden">
            {locales.map(({ value, label }) => (
              <button
                key={value}
                className="block w-full px-4 py-2 text-sm text-left text-white/80 hover:bg-white/10"
                onClick={() => {
                  const prev = i18n.language
                  i18n.changeLanguage(value)
                  updateLocale(value, { onError: () => i18n.changeLanguage(prev) })
                  setIsLocaleOpen(false)
                }}
              >
                {label}
              </button>
            ))}
          </div>
        )}
      </div>

      {/* Scrollable form wrapper — enables sticky CTA to stay above keyboard */}
      <form
        onSubmit={handleSubmit(onSubmit)}
        className="flex-1 flex flex-col px-6 overflow-y-auto"
      >
        {/* Back button */}
        <button
          type="button"
          onClick={onBack}
          className="self-start mt-4 p-1 text-white/50 hover:text-white/80 transition-colors"
          aria-label={t('flatName.back')}
        >
          ←
        </button>

        {/* Header */}
        <div className="mt-6 mb-8">
          <h1 className="text-[22px] font-semibold text-white tracking-tight mb-1.5">
            {t('flatName.title')}
          </h1>
          <p className="text-sm text-white/50">{t('flatName.subtitle')}</p>
        </div>

        {/* Input field */}
        <div className="flex flex-col gap-1.5">
          <label
            htmlFor="flat-name-input"
            className="text-[11px] font-semibold tracking-[0.08em] uppercase text-white/45"
          >
            {t('flatName.label')}
          </label>
          <input
            id="flat-name-input"
            type="text"
            autoFocus
            placeholder={t('flatName.placeholder')}
            style={{ borderColor: 'rgba(255,255,255,0.15)' }}
            className="w-full h-[52px] px-4 rounded-[12px] bg-white/[0.08] border text-white text-base placeholder:text-white/30 outline-none focus:border-white/60 focus:shadow-[0_0_0_3px_rgba(255,255,255,0.06)] transition-[border-color,box-shadow]"
            {...register('name')}
          />
          {touchedFields.name && errors.name && (
            <span className="text-xs text-[var(--color-accent-error)]">{t('flatName.nameRequired')}</span>
          )}
        </div>

        {/* Spacer pushes CTA to bottom */}
        <div className="flex-1" />

        {/* Sticky CTA — stays above soft keyboard */}
        <div className="sticky bottom-0 pb-10 pt-4">
          <button
            type="submit"
            disabled={!isContinueEnabled}
            className="w-full h-14 rounded-full text-white text-[17px] font-semibold border transition-opacity disabled:opacity-40"
            style={{
              background: 'rgba(255,255,255,0.12)',
              borderColor: 'rgba(255,255,255,0.40)',
              boxShadow: '0 0 0 1px rgba(255,255,255,0.08) inset, 0 4px 24px rgba(99,102,241,0.25)',
            }}
          >
            {t('flatName.continue')}
          </button>
        </div>
      </form>
    </div>
  )
}
