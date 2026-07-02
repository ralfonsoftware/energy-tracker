import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useTranslation } from 'react-i18next'
import { parseLocaleNumber } from '@/lib/localeNumber'
import { useSubmitGuard } from '@/lib/useSubmitGuard'
import { useCreateTariff } from '@/features/tariffs/hooks/useCreateTariff'
import { tariffFormSchema, type TariffFormValues } from '@/features/tariffs/schemas/tariffSchema'

type Props = {
  flatId: string | undefined
  onClose: () => void
}

const DURATIONS = [1, 6, 12, 24] as const

function todayIsoDate() {
  const now = new Date()
  const year = now.getFullYear()
  const month = String(now.getMonth() + 1).padStart(2, '0')
  const day = String(now.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function isValidPrice(raw: string, locale: string) {
  const parsed = parseLocaleNumber(raw, locale)
  return Number.isFinite(parsed) && parsed > 0
}

function isValidFee(raw: string, locale: string) {
  const parsed = parseLocaleNumber(raw, locale)
  return Number.isFinite(parsed) && parsed >= 0
}

export function TariffForm({ flatId, onClose }: Props) {
  const { t, i18n } = useTranslation('tariffs')
  const { mutate, isPending } = useCreateTariff(flatId)
  const [submitError, setSubmitError] = useState<string | null>(null)
  const tryAcquire = useSubmitGuard(isPending)

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    setError,
    formState: { errors, touchedFields },
  } = useForm<TariffFormValues>({
    resolver: zodResolver(tariffFormSchema),
    mode: 'onBlur',
    defaultValues: {
      effectiveDate: todayIsoDate(),
      pricePerKwh: '',
      monthlyBaseFee: '',
      providerName: '',
      contractStartDate: '',
      contractDurationMonths: null,
    },
  })

  const effectiveDateRaw = watch('effectiveDate') ?? ''
  const priceRaw = watch('pricePerKwh') ?? ''
  const feeRaw = watch('monthlyBaseFee') ?? ''
  const selectedDuration = watch('contractDurationMonths')

  const isSaveEnabled =
    effectiveDateRaw.trim() !== '' &&
    isValidPrice(priceRaw, i18n.language) &&
    isValidFee(feeRaw, i18n.language) &&
    !isPending

  const onSubmit = (data: TariffFormValues) => {
    const priceParsed = parseLocaleNumber(data.pricePerKwh, i18n.language)
    const feeParsed = parseLocaleNumber(data.monthlyBaseFee, i18n.language)

    if (!Number.isFinite(priceParsed) || priceParsed <= 0) {
      setError('pricePerKwh', { message: t('form.invalidNumber') })
      return
    }
    if (!Number.isFinite(feeParsed) || feeParsed < 0) {
      setError('monthlyBaseFee', { message: t('form.invalidNumber') })
      return
    }
    if (!tryAcquire()) return

    setSubmitError(null)
    mutate(
      {
        effectiveDate: `${data.effectiveDate}T00:00:00Z`,
        pricePerKwh: priceParsed,
        monthlyBaseFee: feeParsed,
        providerName: data.providerName || undefined,
        contractStartDate: data.contractStartDate ? `${data.contractStartDate}T00:00:00Z` : undefined,
        contractDurationMonths: data.contractDurationMonths ?? undefined,
      },
      {
        onSuccess: () => onClose(),
        onError: () => setSubmitError(t('form.errorMessage')),
      }
    )
  }

  const inputClass =
    'w-full h-[52px] px-4 rounded-[12px] bg-white/[0.08] border text-white text-base placeholder:text-white/30 outline-none focus:border-white/60 transition-[border-color,box-shadow]'
  const sectionLabelClass = 'text-[11px] font-semibold tracking-[0.08em] uppercase text-white/45'
  const optionalTagClass =
    'text-[10px] font-medium tracking-[0.04em] uppercase border rounded px-1'
  const optionalTagStyle = { color: 'rgba(255,255,255,0.30)', borderColor: 'rgba(255,255,255,0.20)' }
  const suffixClass = 'absolute right-4 top-1/2 -translate-y-1/2 text-sm pointer-events-none'
  const suffixStyle = { color: 'rgba(255,255,255,0.40)' }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4 overflow-y-auto">
      <div aria-hidden="true" className="mx-auto mb-1 h-1 w-9 rounded-full bg-white/25" />
      <h2 className="text-lg font-semibold text-white">{t('form.title')}</h2>

      {/* Effective date */}
      <div className="flex flex-col gap-1.5">
        <label className={sectionLabelClass}>{t('form.effectiveDate')}</label>
        <input
          type="date"
          style={{
            borderColor: errors.effectiveDate ? 'var(--color-accent-error)' : 'rgba(255,255,255,0.15)',
            colorScheme: 'dark',
          }}
          className={inputClass}
          {...register('effectiveDate')}
        />
        {touchedFields.effectiveDate && errors.effectiveDate && (
          <span className="text-xs text-accent-error">{errors.effectiveDate.message}</span>
        )}
      </div>

      {/* Price per kWh */}
      <div className="flex flex-col gap-1.5">
        <label className={sectionLabelClass}>{t('form.pricePerKwh')}</label>
        <div className="relative">
          <input
            type="text"
            inputMode="numeric"
            placeholder="0"
            style={{ borderColor: errors.pricePerKwh ? 'var(--color-accent-error)' : 'rgba(255,255,255,0.15)' }}
            className={`${inputClass} pr-20`}
            {...register('pricePerKwh')}
          />
          <span className={suffixClass} style={suffixStyle}>
            {t('form.priceSuffix')}
          </span>
        </div>
        {touchedFields.pricePerKwh && errors.pricePerKwh && (
          <span className="text-xs text-accent-error">{errors.pricePerKwh.message}</span>
        )}
      </div>

      {/* Monthly base fee */}
      <div className="flex flex-col gap-1.5">
        <label className={sectionLabelClass}>{t('form.monthlyBaseFee')}</label>
        <div className="relative">
          <input
            type="text"
            inputMode="decimal"
            placeholder="0"
            style={{ borderColor: errors.monthlyBaseFee ? 'var(--color-accent-error)' : 'rgba(255,255,255,0.15)' }}
            className={`${inputClass} pr-16`}
            {...register('monthlyBaseFee')}
          />
          <span className={suffixClass} style={suffixStyle}>
            {t('form.baseFeeSuffix')}
          </span>
        </div>
        {touchedFields.monthlyBaseFee && errors.monthlyBaseFee && (
          <span className="text-xs text-accent-error">{errors.monthlyBaseFee.message}</span>
        )}
      </div>

      {/* Provider name — optional */}
      <div className="flex flex-col gap-1.5">
        <div className="flex items-center gap-2">
          <label className={sectionLabelClass}>{t('form.providerName')}</label>
          <span className={optionalTagClass} style={optionalTagStyle}>
            {t('form.optional')}
          </span>
        </div>
        <input
          type="text"
          placeholder={t('form.providerPlaceholder')}
          style={{ borderColor: 'rgba(255,255,255,0.15)' }}
          className={inputClass}
          {...register('providerName')}
        />
      </div>

      {/* Contract start date — optional */}
      <div className="flex flex-col gap-1.5">
        <div className="flex items-center gap-2">
          <label className={sectionLabelClass}>{t('form.contractStartDate')}</label>
          <span className={optionalTagClass} style={optionalTagStyle}>
            {t('form.optional')}
          </span>
        </div>
        <input
          type="date"
          style={{ borderColor: 'rgba(255,255,255,0.15)', colorScheme: 'dark' }}
          className={inputClass}
          {...register('contractStartDate')}
        />
      </div>

      {/* Contract duration — optional */}
      <div className="flex flex-col gap-1.5">
        <div className="flex items-center gap-2">
          <label className={sectionLabelClass}>{t('form.contractDuration')}</label>
          <span className={optionalTagClass} style={optionalTagStyle}>
            {t('form.optional')}
          </span>
        </div>
        <div className="flex gap-2">
          {DURATIONS.map(months => {
            const isSelected = selectedDuration === months
            return (
              <button
                key={months}
                type="button"
                onClick={() => setValue('contractDurationMonths', isSelected ? null : months)}
                className="flex-1 h-10 rounded-full text-xs font-medium transition-all"
                style={{
                  border: isSelected ? '1px solid #ffffff' : '1px solid rgba(255,255,255,0.20)',
                  background: isSelected ? '#ffffff' : 'rgba(255,255,255,0.06)',
                  color: isSelected ? '#0f1235' : 'rgba(255,255,255,0.70)',
                  fontWeight: isSelected ? 600 : 500,
                }}
              >
                {t(`form.duration${months}` as 'form.duration1')}
              </button>
            )
          })}
        </div>
      </div>

      {submitError && (
        <p role="alert" className="text-xs text-center text-accent-error">
          {submitError}
        </p>
      )}

      <button
        type="submit"
        disabled={!isSaveEnabled}
        className="mt-2 h-14 w-full rounded-full text-white text-[17px] font-semibold border transition-opacity disabled:opacity-40"
        style={{
          background: 'rgba(255,255,255,0.12)',
          borderColor: 'rgba(255,255,255,0.40)',
        }}
      >
        {isPending ? t('form.saving') : t('form.saveButton')}
      </button>
    </form>
  )
}
