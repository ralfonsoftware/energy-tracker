import { useState, useEffect } from 'react'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import { parseLocaleNumber } from '@/lib/localeNumber'
import { SheetContent } from '@/components/ui/sheet'
import { useUserSettings } from '../hooks/useUserSettings'
import { useCreateFlat } from '../hooks/useCreateFlat'
import { useSwitchActiveFlat } from '../hooks/useSwitchActiveFlat'
import { addFlatSchema, type AddFlatFormValues } from '../schemas/settingsSchema'
import type { FlatSummary } from '../api/settingsApi'

const PRESETS = [
  { persons: 1, kwh: 1500 },
  { persons: 2, kwh: 2500 },
  { persons: 3, kwh: 3500 },
  { persons: 4, kwh: 4250 },
]

type Props = {
  open: boolean
  onOpenChange: (open: boolean) => void
}

export function AddFlatForm({ open }: Props) {
  const { t } = useTranslation('settings')
  const { settings } = useUserSettings()
  const { mutate: createFlat, isPending: isCreating } = useCreateFlat()
  const { mutate: switchFlat } = useSwitchActiveFlat()

  const [selectedPresetIndex, setSelectedPresetIndex] = useState<number | null>(null)
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [tariffPromptShown, setTariffPromptShown] = useState(false)

  const {
    handleSubmit,
    watch,
    setValue,
    control,
    reset,
  } = useForm<AddFlatFormValues>({
    resolver: zodResolver(addFlatSchema),
    defaultValues: { name: '', annualKwhBaseline: '' },
  })

  useEffect(() => {
    if (open) {
      reset({ name: '', annualKwhBaseline: '' })
      setSelectedPresetIndex(null)
      setSubmitError(null)
      setTariffPromptShown(false)
    }
  }, [open, reset])

  const nameRaw = watch('name') ?? ''
  const kwhRaw = watch('annualKwhBaseline') ?? ''

  const handlePresetClick = (index: number) => {
    setSelectedPresetIndex(index)
    setValue('annualKwhBaseline', String(PRESETS[index].kwh))
  }

  const onSubmit = (data: AddFlatFormValues) => {
    const kwhParsed = parseLocaleNumber(data.annualKwhBaseline, i18n.language)
    if (!Number.isFinite(kwhParsed) || kwhParsed <= 0) {
      setSubmitError(t('common:errors.validationNumber'))
      return
    }

    setSubmitError(null)
    const flatId = settings?.flatId
    const locale = settings?.locale ?? i18n.language
    createFlat(
      { name: data.name.trim(), annualKwhBaseline: kwhParsed, plannedAnnualSpend: null },
      {
        onSuccess: (created: FlatSummary) => {
          switchFlat(
            { flatId: created.flatId, locale, previousFlatId: flatId },
            {
              onSuccess: () => setTariffPromptShown(true),
              onError: () => setSubmitError(t('addFlat.error')),
            }
          )
        },
        onError: () => setSubmitError(t('addFlat.error')),
      }
    )
  }

  const inputClass =
    'w-full h-[52px] px-4 rounded-[12px] text-white text-base placeholder:text-white/30 outline-none focus:border-white/60 transition-[border-color,box-shadow]'

  if (tariffPromptShown) {
    return (
      <SheetContent
        side="bottom"
        className="rounded-t-sheet border-t border-white/[0.14] bg-[rgba(10,15,25,0.92)] backdrop-blur-[20px] backdrop-saturate-[1.8] px-6 pb-8 pt-3 [&>button]:right-2 [&>button]:top-2 [&>button]:flex [&>button]:h-11 [&>button]:w-11 [&>button]:items-center [&>button]:justify-center [&>button]:text-white/60 [&>button]:hover:text-white [&>button]:ring-offset-transparent [&>button]:focus:ring-white/40 [&>button]:data-[state=open]:bg-white/10"
      >
        <div aria-hidden="true" className="mx-auto mb-4 h-1 w-9 rounded-full bg-white/25" />
        <p className="text-white text-[15px]">{t('addFlat.tariffPrompt')}</p>
      </SheetContent>
    )
  }

  return (
    <SheetContent
      side="bottom"
      className="rounded-t-sheet border-t border-white/[0.14] bg-[rgba(10,15,25,0.92)] backdrop-blur-[20px] backdrop-saturate-[1.8] px-6 pb-8 pt-3 [&>button]:right-2 [&>button]:top-2 [&>button]:flex [&>button]:h-11 [&>button]:w-11 [&>button]:items-center [&>button]:justify-center [&>button]:text-white/60 [&>button]:hover:text-white [&>button]:ring-offset-transparent [&>button]:focus:ring-white/40 [&>button]:data-[state=open]:bg-white/10"
    >
      <div aria-hidden="true" className="mx-auto mb-4 h-1 w-9 rounded-full bg-white/25" />
      <h2 className="text-[18px] font-semibold text-white mb-4">{t('addFlat.title')}</h2>
      <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4">
        <div>
          <label htmlFor="addFlatName" className="sr-only">
            {t('addFlat.nameLabel')}
          </label>
          <Controller
            control={control}
            name="name"
            render={({ field }) => (
              <input
                {...field}
                id="addFlatName"
                type="text"
                placeholder={t('addFlat.namePlaceholder')}
                aria-label={t('addFlat.nameLabel')}
                className={inputClass}
                style={{ background: 'rgba(255,255,255,0.08)', border: '1px solid rgba(255,255,255,0.15)' }}
              />
            )}
          />
        </div>

        <div className="grid grid-cols-2 gap-2">
          {PRESETS.map((preset, index) => {
            const isSelected = selectedPresetIndex === index
            return (
              <button
                key={index}
                type="button"
                onClick={() => handlePresetClick(index)}
                className="h-11 rounded-full text-xs font-medium transition-all"
                style={{
                  border: isSelected ? '1px solid #ffffff' : '1px solid rgba(255,255,255,0.20)',
                  background: isSelected ? '#ffffff' : 'rgba(255,255,255,0.06)',
                  color: isSelected ? '#111827' : 'rgba(255,255,255,0.70)',
                  fontWeight: isSelected ? 600 : 500,
                }}
              >
                {preset.persons} {preset.persons === 1 ? t('baselineEdit.presetPerson') : t('baselineEdit.presetPersons')} · {preset.kwh.toLocaleString(i18n.language)} kWh
              </button>
            )
          })}
        </div>

        <div className="relative">
          <Controller
            control={control}
            name="annualKwhBaseline"
            render={({ field }) => (
              <input
                {...field}
                type="text"
                inputMode="decimal"
                placeholder="0"
                className={`${inputClass} pr-24`}
                style={{ background: 'rgba(255,255,255,0.08)', border: '1px solid rgba(255,255,255,0.15)' }}
                onChange={e => {
                  setSelectedPresetIndex(null)
                  field.onChange(e)
                }}
              />
            )}
          />
          <span
            className="absolute right-4 top-1/2 -translate-y-1/2 text-sm pointer-events-none"
            style={{ color: 'rgba(255,255,255,0.40)' }}
          >
            kWh / yr
          </span>
        </div>

        {submitError && (
          <p role="alert" className="text-sm text-red-400">
            {submitError}
          </p>
        )}

        <button
          type="submit"
          disabled={isCreating || !nameRaw.trim() || !kwhRaw.trim()}
          className="h-14 w-full rounded-full text-white text-[17px] font-semibold border transition-opacity disabled:opacity-40"
          style={{ background: 'rgba(255,255,255,0.12)', borderColor: 'rgba(255,255,255,0.40)' }}
        >
          {t('addFlat.submit')}
        </button>
      </form>
    </SheetContent>
  )
}
