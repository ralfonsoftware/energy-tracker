import { useState, useRef } from 'react'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import { parseLocaleNumber } from '@/lib/localeNumber'
import { LocaleDropdown } from '@/components/LocaleDropdown'
import { contractSchema, type ContractFormValues } from '../schemas/onboardingSchema'
import { useCompleteOnboarding } from '../hooks/useCompleteOnboarding'
import type { CompleteOnboardingPayload } from '../api/onboardingApi'

export interface ContractInitialValues {
  annualKwhBaseline: string
  selectedPresetIndex: number | null
  pricePerKwh: string
  monthlyBaseFee: string
  providerName: string
  contractStartDate: string
  contractDurationMonths: number | null
  plannedAnnualSpend: string
  isSpendOverride: boolean
}

interface OnboardingContractProps {
  initialValues: ContractInitialValues
  flatName: string
  onComplete: (payload: CompleteOnboardingPayload) => void
  onBack: (values: ContractInitialValues) => void
}

const PRESETS = [
  { persons: 1, kwh: 1500 },
  { persons: 2, kwh: 2500 },
  { persons: 3, kwh: 3500 },
  { persons: 4, kwh: 4250 },
]

const DURATIONS = [1, 6, 12, 24] as const

export function OnboardingContract({ initialValues, flatName, onComplete, onBack }: OnboardingContractProps) {
  const { t } = useTranslation('onboarding')
  const { isPending, error: apiError, mutate: submitOnboarding } = useCompleteOnboarding()

  const [selectedPresetIndex, setSelectedPresetIndex] = useState<number | null>(initialValues.selectedPresetIndex)
  const [isSpendOverride, setIsSpendOverride] = useState(initialValues.isSpendOverride)
  const [submitError, setSubmitError] = useState<string | null>(null)

  const kwhInputRef = useRef<HTMLInputElement | null>(null)

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    setError,
    control,
    formState: { errors, touchedFields },
  } = useForm<ContractFormValues>({
    resolver: zodResolver(contractSchema),
    mode: 'onTouched',
    defaultValues: {
      annualKwhBaseline: initialValues.annualKwhBaseline,
      pricePerKwh: initialValues.pricePerKwh,
      monthlyBaseFee: initialValues.monthlyBaseFee,
      providerName: initialValues.providerName,
      contractStartDate: initialValues.contractStartDate,
      contractDurationMonths: initialValues.contractDurationMonths,
      plannedAnnualSpend: initialValues.plannedAnnualSpend,
    },
  })

  const kwhRaw = watch('annualKwhBaseline') ?? ''
  const priceRaw = watch('pricePerKwh') ?? ''
  const feeRaw = watch('monthlyBaseFee') ?? ''
  const spendRaw = watch('plannedAnnualSpend') ?? ''
  const selectedDuration = watch('contractDurationMonths')

  const kwhNum = parseLocaleNumber(kwhRaw, i18n.language)
  const priceNum = parseLocaleNumber(priceRaw, i18n.language)
  const feeNum = parseLocaleNumber(feeRaw, i18n.language)

  const autoCalcSpend =
    !isNaN(kwhNum) && !isNaN(priceNum) && !isNaN(feeNum)
      ? kwhNum * priceNum + feeNum * 12
      : null

  const isSubmitEnabled =
    kwhRaw.trim() !== '' && priceRaw.trim() !== '' && feeRaw.trim() !== ''

  const handlePresetClick = (index: number) => {
    setSelectedPresetIndex(index)
    const kwhStr = String(PRESETS[index].kwh)
    setValue('annualKwhBaseline', kwhStr)
    setTimeout(() => kwhInputRef.current?.focus(), 0)
  }

  const handleBack = () => {
    onBack({
      annualKwhBaseline: kwhRaw,
      selectedPresetIndex,
      pricePerKwh: priceRaw,
      monthlyBaseFee: feeRaw,
      providerName: watch('providerName') ?? '',
      contractStartDate: watch('contractStartDate') ?? '',
      contractDurationMonths: watch('contractDurationMonths') ?? null,
      plannedAnnualSpend: spendRaw,
      isSpendOverride,
    })
  }

  const onSubmit = (data: ContractFormValues) => {
    const kwhParsed = parseLocaleNumber(data.annualKwhBaseline, i18n.language)
    const priceParsed = parseLocaleNumber(data.pricePerKwh, i18n.language)
    const feeParsed = parseLocaleNumber(data.monthlyBaseFee, i18n.language)

    if (isNaN(kwhParsed) || kwhParsed <= 0) {
      setError('annualKwhBaseline', { message: t('contract.invalidNumber') })
      return
    }
    if (isNaN(priceParsed) || priceParsed <= 0) {
      setError('pricePerKwh', { message: t('contract.invalidNumber') })
      return
    }
    if (isNaN(feeParsed) || feeParsed < 0) {
      setError('monthlyBaseFee', { message: t('contract.invalidNumber') })
      return
    }

    const spendStr = data.plannedAnnualSpend?.trim()
    let plannedSpend: number | null = null
    if (isSpendOverride && spendStr) {
      const spendNum = parseLocaleNumber(spendStr, i18n.language)
      if (isNaN(spendNum) || spendNum < 0) {
        setError('plannedAnnualSpend', { message: t('contract.invalidNumber') })
        return
      }
      plannedSpend = Math.round(spendNum * 100) / 100
    } else if (autoCalcSpend !== null) {
      plannedSpend = Math.round(autoCalcSpend * 100) / 100
    }

    setSubmitError(null)
    submitOnboarding(
      {
        flatName,
        annualKwhBaseline: kwhParsed,
        plannedAnnualSpend: plannedSpend,
        pricePerKwh: priceParsed,
        monthlyBaseFee: feeParsed,
        providerName: data.providerName || undefined,
        contractStartDate: data.contractStartDate ? `${data.contractStartDate}T00:00:00Z` : undefined,
        contractDurationMonths: data.contractDurationMonths ?? undefined,
      },
      {
        onSuccess: () => onComplete({
          flatName,
          annualKwhBaseline: kwhParsed,
          plannedAnnualSpend: plannedSpend,
          pricePerKwh: priceParsed,
          monthlyBaseFee: feeParsed,
          providerName: data.providerName || undefined,
          contractStartDate: data.contractStartDate ? `${data.contractStartDate}T00:00:00Z` : undefined,
          contractDurationMonths: data.contractDurationMonths ?? undefined,
        }),
        onError: () => setSubmitError(t('contract.errorMessage')),
      }
    )
  }

  const inputClass =
    'w-full h-[52px] px-4 rounded-[12px] bg-white/[0.08] border text-white text-base placeholder:text-white/30 outline-none focus:border-white/60 focus:shadow-[0_0_0_3px_rgba(255,255,255,0.06)] transition-[border-color,box-shadow]'
  const sectionLabelClass =
    'text-[11px] font-semibold tracking-[0.08em] uppercase text-white/45'

  return (
    <div className="relative flex-1 flex flex-col" style={{ background: '#0f1235' }}>

      {/* Locale pill */}
      <div className="absolute top-4 right-4 z-20">
        <LocaleDropdown dimmed />
      </div>

      <form
        onSubmit={handleSubmit(onSubmit)}
        className="flex-1 flex flex-col px-6 overflow-y-auto"
      >
        {/* Back button */}
        <button
          type="button"
          onClick={handleBack}
          className="self-start mt-4 p-1 text-white/50 hover:text-white/80 transition-colors"
          aria-label={t('contract.back')}
        >
          ←
        </button>

        {/* Header */}
        <div className="mt-6 mb-6">
          <h1 className="text-[22px] font-semibold text-white tracking-tight mb-1.5">
            {t('contract.title')}
          </h1>
          <p className="text-sm text-white/50">{t('contract.subtitle')}</p>
        </div>

        {/* Annual Usage section */}
        <div className="mb-6">
          <p className={`${sectionLabelClass} mb-3`}>{t('contract.annualUsage')}</p>

          {/* Preset tile grid */}
          <div className="grid grid-cols-2 gap-2 mb-4">
            {PRESETS.map((_preset, index) => {
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
                    color: isSelected ? '#0f1235' : 'rgba(255,255,255,0.70)',
                    fontWeight: isSelected ? 600 : 500,
                  }}
                >
                  {t(`contract.preset${index + 1}` as 'contract.preset1')}
                </button>
              )
            })}
          </div>

          {/* kWh input */}
          <div className="flex flex-col gap-1.5">
            <label className={sectionLabelClass}>{t('contract.exactValue')}</label>
            <div className="relative">
              <Controller
                control={control}
                name="annualKwhBaseline"
                render={({ field }) => (
                  <input
                    {...field}
                    ref={(el) => {
                      field.ref(el)
                      kwhInputRef.current = el
                    }}
                    type="text"
                    inputMode="decimal"
                    placeholder="0"
                    style={{ borderColor: errors.annualKwhBaseline ? 'var(--color-accent-error)' : 'rgba(255,255,255,0.15)' }}
                    className={`${inputClass} pr-24`}
                    onChange={(e) => {
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
                {t('contract.kwhSuffix')}
              </span>
            </div>
            {touchedFields.annualKwhBaseline && errors.annualKwhBaseline && (
              <span className="text-xs text-[var(--color-accent-error)]">{errors.annualKwhBaseline.message}</span>
            )}
          </div>
        </div>

        {/* Tariff section */}
        <div className="mb-6">
          <p className={`${sectionLabelClass} mb-3`}>{t('contract.tariff')}</p>

          <div className="flex flex-col gap-4">
            {/* Monthly base fee — required */}
            <div className="flex flex-col gap-1.5">
              <label className={sectionLabelClass}>{t('contract.baseFee')}</label>
              <div className="relative">
                <input
                  type="text"
                  inputMode="decimal"
                  placeholder="0"
                  style={{ borderColor: errors.monthlyBaseFee ? 'var(--color-accent-error)' : 'rgba(255,255,255,0.15)' }}
                  className={`${inputClass} pr-20`}
                  {...register('monthlyBaseFee')}
                />
                <span
                  className="absolute right-4 top-1/2 -translate-y-1/2 text-sm pointer-events-none"
                  style={{ color: 'rgba(255,255,255,0.40)' }}
                >
                  {t('contract.baseFeeSuffix')}
                </span>
              </div>
              {touchedFields.monthlyBaseFee && errors.monthlyBaseFee && (
                <span className="text-xs text-[var(--color-accent-error)]">{errors.monthlyBaseFee.message}</span>
              )}
            </div>

            {/* Price per kWh — required */}
            <div className="flex flex-col gap-1.5">
              <label className={sectionLabelClass}>{t('contract.pricePerKwh')}</label>
              <input
                type="text"
                inputMode="decimal"
                placeholder="0"
                style={{ borderColor: errors.pricePerKwh ? 'var(--color-accent-error)' : 'rgba(255,255,255,0.15)' }}
                className={inputClass}
                {...register('pricePerKwh')}
              />
              {touchedFields.pricePerKwh && errors.pricePerKwh && (
                <span className="text-xs text-[var(--color-accent-error)]">{errors.pricePerKwh.message}</span>
              )}
            </div>

            {/* Provider name — optional */}
            <div className="flex flex-col gap-1.5">
              <div className="flex items-center gap-2">
                <label className={sectionLabelClass}>{t('contract.provider')}</label>
                <span
                  className="text-[10px] font-medium tracking-[0.04em] uppercase border rounded px-1"
                  style={{ color: 'rgba(255,255,255,0.30)', borderColor: 'rgba(255,255,255,0.20)' }}
                >
                  {t('contract.optional')}
                </span>
              </div>
              <input
                type="text"
                placeholder={t('contract.providerPlaceholder')}
                style={{ borderColor: 'rgba(255,255,255,0.15)' }}
                className={inputClass}
                {...register('providerName')}
              />
            </div>

            {/* Contract start — optional */}
            <div className="flex flex-col gap-1.5">
              <div className="flex items-center gap-2">
                <label className={sectionLabelClass}>{t('contract.contractStart')}</label>
                <span
                  className="text-[10px] font-medium tracking-[0.04em] uppercase border rounded px-1"
                  style={{ color: 'rgba(255,255,255,0.30)', borderColor: 'rgba(255,255,255,0.20)' }}
                >
                  {t('contract.optional')}
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
                <label className={sectionLabelClass}>{t('contract.contractDuration')}</label>
                <span
                  className="text-[10px] font-medium tracking-[0.04em] uppercase border rounded px-1"
                  style={{ color: 'rgba(255,255,255,0.30)', borderColor: 'rgba(255,255,255,0.20)' }}
                >
                  {t('contract.optional')}
                </span>
              </div>
              <div className="flex gap-2">
                {DURATIONS.map((months) => {
                  const isSelected = selectedDuration === months
                  return (
                    <button
                      key={months}
                      type="button"
                      onClick={() =>
                        setValue('contractDurationMonths', isSelected ? null : months)
                      }
                      className="flex-1 h-10 rounded-full text-xs font-medium transition-all"
                      style={{
                        border: isSelected ? '1px solid #ffffff' : '1px solid rgba(255,255,255,0.20)',
                        background: isSelected ? '#ffffff' : 'rgba(255,255,255,0.06)',
                        color: isSelected ? '#0f1235' : 'rgba(255,255,255,0.70)',
                        fontWeight: isSelected ? 600 : 500,
                      }}
                    >
                      {t(`contract.duration${months}` as 'contract.duration1')}
                    </button>
                  )
                })}
              </div>
            </div>
          </div>
        </div>

        {/* Budget card */}
        <div
          className="mb-6 p-4 rounded-2xl"
          style={{
            background: 'rgba(251,191,36,0.08)',
            border: '1px solid rgba(251,191,36,0.20)',
          }}
        >
          <p className={`${sectionLabelClass} mb-2`}>{t('contract.annualBudget')}</p>

          {autoCalcSpend !== null && !isSpendOverride ? (
            <>
              <p
                className="text-[28px] font-semibold text-white tracking-[-0.02em] mb-1"
                style={{ fontFeatureSettings: "'tnum'" }}
              >
                €{autoCalcSpend.toFixed(2)}
              </p>
              <p className="text-[12px] mb-2" style={{ color: 'rgba(255,255,255,0.50)', fontFeatureSettings: "'tnum'" }}>
                {kwhNum.toFixed(0)} × {priceNum.toFixed(4)} + {feeNum.toFixed(2)} × 12
              </p>
            </>
          ) : null}

          <div className="relative">
            <input
              type="text"
              inputMode="decimal"
              placeholder={autoCalcSpend !== null ? `~€${autoCalcSpend.toFixed(0)} / yr based on current tariff` : ''}
              style={{ borderColor: 'rgba(255,255,255,0.15)' }}
              className={inputClass}
              {...register('plannedAnnualSpend', {
                onChange: (e) => {
                  if (e.target.value !== '') setIsSpendOverride(true)
                },
                onBlur: (e) => {
                  if (e.target.value.trim() === '') {
                    setIsSpendOverride(false)
                    setValue('plannedAnnualSpend', '')
                  }
                },
              })}
            />
            {isSpendOverride && spendRaw.trim() !== '' && (
              <span
                className="absolute right-4 top-1/2 -translate-y-1/2 text-[11px] font-medium px-2 py-0.5 rounded border"
                style={{ color: 'rgba(255,255,255,0.50)', borderColor: 'rgba(255,255,255,0.20)', background: 'rgba(255,255,255,0.06)' }}
              >
                {t('contract.customBudget')}
              </span>
            )}
          </div>

          <p className="mt-2 text-[11px]" style={{ color: 'rgba(255,255,255,0.30)' }}>
            {t('contract.budgetNote')}
          </p>
        </div>

        {/* Spacer */}
        <div className="flex-1" />

        {/* Sticky CTA */}
        <div className="sticky bottom-0 pb-10 pt-4 flex flex-col gap-2">
          {(submitError || apiError) && (
            <p className="text-xs text-center text-[var(--color-accent-error)]">
              {submitError ?? t('contract.errorMessage')}
            </p>
          )}
          <button
            type="submit"
            disabled={!isSubmitEnabled || isPending}
            className="w-full h-14 rounded-full text-white text-[17px] font-semibold border transition-opacity disabled:opacity-40"
            style={{
              background: 'rgba(255,255,255,0.12)',
              borderColor: 'rgba(255,255,255,0.40)',
              boxShadow: '0 0 0 1px rgba(255,255,255,0.08) inset, 0 4px 24px rgba(99,102,241,0.25)',
            }}
          >
            {isPending ? (
              <span className="flex items-center justify-center gap-2">
                <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24" fill="none">
                  <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                  <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
                </svg>
                {t('contract.saving')}
              </span>
            ) : (
              t('contract.completeSetup')
            )}
          </button>
        </div>
      </form>
    </div>
  )
}
