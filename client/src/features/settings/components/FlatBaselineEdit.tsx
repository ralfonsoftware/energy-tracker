import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import { useUserSettings } from '../hooks/useUserSettings'
import { usePatchFlat } from '../hooks/usePatchFlat'
import { baselineEditSchema, type BaselineEditFormValues } from '../schemas/settingsSchema'

const PRESETS = [
  { persons: 1, kwh: 1500 },
  { persons: 2, kwh: 2500 },
  { persons: 3, kwh: 3500 },
  { persons: 4, kwh: 4250 },
]

function parseLocaleNumber(value: string, locale: string): number {
  const isDE = locale.startsWith('de')
  const normalized = isDE
    ? value.replace(/\./g, '').replace(',', '.')
    : value.replace(/,/g, '')
  return parseFloat(normalized)
}

export default function FlatBaselineEdit() {
  const { t } = useTranslation('settings')
  const navigate = useNavigate()
  const { settings } = useUserSettings()
  const { mutate: patchFlat, isPending } = usePatchFlat()

  const initialPresetIndex =
    settings?.annualKwhBaseline != null
      ? PRESETS.findIndex(p => p.kwh === settings.annualKwhBaseline)
      : null

  const [selectedPresetIndex, setSelectedPresetIndex] = useState<number | null>(
    initialPresetIndex !== -1 ? initialPresetIndex : null
  )
  const [submitError, setSubmitError] = useState<string | null>(null)

  const {
    handleSubmit,
    watch,
    setValue,
    control,
    formState: { errors },
  } = useForm<BaselineEditFormValues>({
    resolver: zodResolver(baselineEditSchema),
    defaultValues: {
      annualKwhBaseline: settings?.annualKwhBaseline != null
        ? String(settings.annualKwhBaseline)
        : '',
      plannedAnnualSpend: settings?.plannedAnnualSpend != null
        ? String(settings.plannedAnnualSpend)
        : '',
    },
  })

  const kwhRaw = watch('annualKwhBaseline') ?? ''

  const handlePresetClick = (index: number) => {
    setSelectedPresetIndex(index)
    setValue('annualKwhBaseline', String(PRESETS[index].kwh))
  }

  const onSubmit = (data: BaselineEditFormValues) => {
    if (!settings?.flatId) return

    const kwhParsed = parseLocaleNumber(data.annualKwhBaseline, i18n.language)
    if (isNaN(kwhParsed) || kwhParsed <= 0) {
      setSubmitError(t('baselineEdit.errorBanner'))
      return
    }

    let plannedAnnualSpend: number | null | undefined = undefined
    const spendStr = data.plannedAnnualSpend?.trim()
    if (spendStr) {
      const spendNum = parseLocaleNumber(spendStr, i18n.language)
      if (isNaN(spendNum) || spendNum < 0) {
        setSubmitError(t('baselineEdit.errorBanner'))
        return
      }
      plannedAnnualSpend = Math.round(spendNum * 100) / 100
    } else {
      plannedAnnualSpend = null
    }

    setSubmitError(null)
    patchFlat(
      {
        flatId: settings.flatId,
        body: {
          annualKwhBaseline: kwhParsed,
          plannedAnnualSpend,
        },
      },
      {
        onSuccess: () => navigate('/settings'),
        onError: () => setSubmitError(t('baselineEdit.errorBanner')),
      }
    )
  }

  const inputClass =
    'w-full h-[52px] px-4 rounded-[12px] text-white text-base placeholder:text-white/30 outline-none focus:border-white/60 transition-[border-color,box-shadow]'

  const sectionLabelClass = 'text-[11px] font-semibold tracking-[0.08em] uppercase text-white/45'

  return (
    <div className="flex-1 flex flex-col" style={{ background: '#111827', minHeight: '100vh' }}>
      <div className="px-6 pt-4">
        <button
          type="button"
          onClick={() => navigate('/settings')}
          className="text-white/50 hover:text-white/80 transition-colors mb-6"
        >
          ← {t('baselineEdit.back')}
        </button>

        <h1 className="text-[22px] font-semibold text-white tracking-tight mb-1.5">
          {t('baselineEdit.title')}
        </h1>
        <p className="text-sm text-white/50 mb-6">{t('baselineEdit.subtitle')}</p>
      </div>

      <form onSubmit={handleSubmit(onSubmit)} className="flex-1 flex flex-col px-6">
        {/* Preset tile grid */}
        <div className="mb-4">
          <p className={`${sectionLabelClass} mb-3`}>{t('baselineEdit.title')}</p>
          <div className="grid grid-cols-2 gap-2 mb-4">
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
                  {preset.persons} {preset.persons === 1 ? t('baselineEdit.presetPerson') : t('baselineEdit.presetPersons')} · {preset.kwh.toLocaleString()} kWh
                </button>
              )
            })}
          </div>
        </div>

        {/* kWh input */}
        <div className="mb-4">
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
                  style={{
                    background: 'rgba(255,255,255,0.08)',
                    border: `1px solid ${errors.annualKwhBaseline ? '#ef4444' : 'rgba(255,255,255,0.15)'}`,
                  }}
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
          {errors.annualKwhBaseline && (
            <p className="mt-1 text-xs text-red-400">{errors.annualKwhBaseline.message}</p>
          )}
        </div>

        {/* Planned spend */}
        <div className="mb-6">
          <div className="flex items-center gap-2 mb-2">
            <p className={sectionLabelClass}>{t('baselineEdit.plannedSpendLabel')}</p>
            <span
              className="text-[10px] font-medium tracking-[0.04em] uppercase border rounded px-1"
              style={{ color: 'rgba(255,255,255,0.30)', borderColor: 'rgba(255,255,255,0.20)' }}
            >
              {t('baselineEdit.optional')}
            </span>
          </div>
          <div className="relative">
            <Controller
              control={control}
              name="plannedAnnualSpend"
              render={({ field }) => (
                <input
                  {...field}
                  type="text"
                  inputMode="decimal"
                  placeholder="0"
                  className={`${inputClass} pr-16`}
                  style={{
                    background: 'rgba(255,255,255,0.08)',
                    border: '1px solid rgba(255,255,255,0.15)',
                  }}
                />
              )}
            />
            <span
              className="absolute right-4 top-1/2 -translate-y-1/2 text-sm pointer-events-none"
              style={{ color: 'rgba(255,255,255,0.40)' }}
            >
              € / yr
            </span>
          </div>
        </div>

        <div className="flex-1" />

        {submitError && (
          <div
            className="mb-4 px-4 py-3 rounded-xl text-sm text-red-400"
            style={{ background: 'rgba(239,68,68,0.1)', border: '1px solid rgba(239,68,68,0.3)' }}
          >
            {submitError}
          </div>
        )}

        <div className="pb-10">
          <button
            type="submit"
            disabled={isPending || !kwhRaw.trim()}
            className="w-full h-14 rounded-full text-white text-[17px] font-semibold border transition-opacity disabled:opacity-40"
            style={{
              background: 'rgba(255,255,255,0.12)',
              borderColor: 'rgba(255,255,255,0.40)',
            }}
          >
            {isPending ? '…' : t('baselineEdit.saveButton')}
          </button>
        </div>
      </form>
    </div>
  )
}
