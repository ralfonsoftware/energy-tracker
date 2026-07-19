import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useTranslation } from 'react-i18next'
import { parseLocaleNumber, formatNumberForInput } from '@/lib/localeNumber'
import { useSubmitGuard } from '@/lib/useSubmitGuard'
import { toLocalDateString, parseLocalDate } from '@/lib/localDate'
import { useCreateTariff } from '@/features/tariffs/hooks/useCreateTariff'
import { usePatchTariff } from '@/features/tariffs/hooks/usePatchTariff'
import { TariffLockIndicator } from '@/features/tariffs/components/TariffLockIndicator'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
  DialogClose,
} from '@/components/ui/dialog'
import { tariffFormSchema, type TariffFormValues } from '@/features/tariffs/schemas/tariffSchema'
import type { TariffResponse } from '@/features/tariffs/api/tariffApi'

type Props = {
  flatId: string | undefined
  tariff?: TariffResponse
  onClose: () => void
  onPendingChange?: (isPending: boolean) => void
  onSaveConflict?: () => void
}

const DURATIONS = [1, 6, 12, 24] as const

function isValidPrice(raw: string, locale: string) {
  const parsed = parseLocaleNumber(raw, locale)
  return Number.isFinite(parsed) && parsed > 0
}

function isValidFee(raw: string, locale: string) {
  const parsed = parseLocaleNumber(raw, locale)
  return Number.isFinite(parsed) && parsed >= 0
}

export function TariffForm({ flatId, tariff, onClose, onPendingChange, onSaveConflict }: Props) {
  const { t, i18n } = useTranslation('tariffs')
  const isEditMode = tariff !== undefined
  const { mutate: createMutate, isPending: isCreatePending } = useCreateTariff(flatId)
  const { mutateAsync: patchMutateAsync, isPending: isPatchPending } = usePatchTariff(flatId)
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [overrideConfirmed, setOverrideConfirmed] = useState(false)
  const [overrideDialogOpen, setOverrideDialogOpen] = useState(false)
  const isPending = isEditMode ? isPatchPending : isCreatePending
  const tryAcquire = useSubmitGuard(isPending)

  useEffect(() => {
    onPendingChange?.(isPending)
  }, [isPending, onPendingChange])

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    setError,
    formState: { errors, touchedFields, dirtyFields },
  } = useForm<TariffFormValues>({
    resolver: zodResolver(tariffFormSchema),
    mode: 'onBlur',
    defaultValues: tariff
      ? {
          contractStartDate: toLocalDateString(parseLocalDate(tariff.contractStartDate)),
          pricePerKwh: formatNumberForInput(tariff.pricePerKwh, i18n.language),
          monthlyBaseFee: formatNumberForInput(tariff.monthlyBaseFee, i18n.language),
          providerName: tariff.providerName ?? '',
          contractDurationMonths: tariff.contractDurationMonths ?? null,
        }
      : {
          contractStartDate: toLocalDateString(new Date()),
          pricePerKwh: '',
          monthlyBaseFee: '',
          providerName: '',
          contractDurationMonths: null,
        },
  })

  const contractStartDateRaw = watch('contractStartDate') ?? ''
  const priceRaw = watch('pricePerKwh') ?? ''
  const feeRaw = watch('monthlyBaseFee') ?? ''
  const selectedDuration = watch('contractDurationMonths')

  const isLockedAndNotOverridden = isEditMode && tariff.isLocked && !overrideConfirmed

  const priceFieldsDirty = !!dirtyFields.pricePerKwh || !!dirtyFields.monthlyBaseFee
  const contractFieldsDirty = !!dirtyFields.providerName || !!dirtyFields.contractDurationMonths
  const isAnyFieldDirty = priceFieldsDirty || contractFieldsDirty

  const isCreateSaveEnabled =
    contractStartDateRaw.trim() !== '' &&
    isValidPrice(priceRaw, i18n.language) &&
    isValidFee(feeRaw, i18n.language) &&
    !isPending

  const isEditSaveEnabled =
    isAnyFieldDirty &&
    (!priceFieldsDirty || (isValidPrice(priceRaw, i18n.language) && isValidFee(feeRaw, i18n.language))) &&
    !isPending

  const isSaveEnabled = isEditMode ? isEditSaveEnabled : isCreateSaveEnabled

  const onSubmitCreate = (data: TariffFormValues) => {
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
    createMutate(
      {
        contractStartDate: `${data.contractStartDate}T00:00:00Z`,
        pricePerKwh: priceParsed,
        monthlyBaseFee: feeParsed,
        providerName: data.providerName || undefined,
        contractDurationMonths: data.contractDurationMonths ?? undefined,
      },
      {
        onSuccess: () => onClose(),
        onError: () => setSubmitError(t('form.errorMessage')),
      }
    )
  }

  const onSubmitEdit = async (data: TariffFormValues) => {
    if (!tariff) return
    const priceParsed = parseLocaleNumber(data.pricePerKwh, i18n.language)
    const feeParsed = parseLocaleNumber(data.monthlyBaseFee, i18n.language)

    if (priceFieldsDirty) {
      if (!isValidPrice(data.pricePerKwh, i18n.language)) { setError('pricePerKwh', { message: t('form.invalidNumber') }); return }
      if (!isValidFee(data.monthlyBaseFee, i18n.language)) { setError('monthlyBaseFee', { message: t('form.invalidNumber') }); return }
    }
    if (!isAnyFieldDirty) return
    if (!tryAcquire()) return

    setSubmitError(null)
    try {
      await patchMutateAsync({
        tariffId: tariff.tariffId,
        body: {
          pricePerKwh: priceFieldsDirty ? priceParsed : undefined,
          monthlyBaseFee: priceFieldsDirty ? feeParsed : undefined,
          providerName: dirtyFields.providerName ? (data.providerName || null) : undefined,
          contractDurationMonths: dirtyFields.contractDurationMonths ? (data.contractDurationMonths ?? null) : undefined,
          lockOverride: overrideConfirmed || undefined,
          rowVersion: tariff.rowVersion,
        },
      })
      onClose()
    } catch {
      setSubmitError(t('form.errorMessage'))
      onSaveConflict?.()
    }
  }

  const onSubmit = isEditMode ? onSubmitEdit : onSubmitCreate

  const inputClass =
    'w-full h-[52px] px-4 rounded-[12px] bg-white/[0.08] border text-white text-base placeholder:text-white/30 outline-none focus:border-white/60 transition-[border-color,box-shadow] disabled:opacity-40 disabled:cursor-not-allowed'
  const sectionLabelClass = 'text-[11px] font-semibold tracking-[0.08em] uppercase text-white/45'
  const optionalTagClass =
    'text-[10px] font-medium tracking-[0.04em] uppercase border rounded px-1'
  const optionalTagStyle = { color: 'rgba(255,255,255,0.30)', borderColor: 'rgba(255,255,255,0.20)' }
  const suffixClass = 'absolute right-4 top-1/2 -translate-y-1/2 text-sm pointer-events-none'
  const suffixStyle = { color: 'rgba(255,255,255,0.40)' }

  const formatDate = (isoDate: string) =>
    new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' }).format(parseLocalDate(isoDate))

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-4 overflow-y-auto">
      <div aria-hidden="true" className="mx-auto mb-1 h-1 w-9 rounded-full bg-white/25" />
      <h2 className="text-lg font-semibold text-white">{t(isEditMode ? 'form.editTitle' : 'form.title')}</h2>

      {/* Contract start date */}
      <div className="flex flex-col gap-1.5">
        <label className={sectionLabelClass}>{t('form.contractStartDate')}</label>
        {isEditMode ? (
          <p className="text-sm text-white/70">{t('form.contractStartDateReadonly', { date: formatDate(tariff.contractStartDate) })}</p>
        ) : (
          <>
            <input
              type="date"
              style={{
                borderColor: errors.contractStartDate ? 'var(--color-accent-error)' : 'rgba(255,255,255,0.15)',
                colorScheme: 'dark',
              }}
              className={inputClass}
              {...register('contractStartDate')}
            />
            {touchedFields.contractStartDate && errors.contractStartDate && (
              <span className="text-xs text-accent-error">{errors.contractStartDate.message}</span>
            )}
          </>
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
            disabled={isLockedAndNotOverridden}
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
            disabled={isLockedAndNotOverridden}
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

      {isLockedAndNotOverridden && tariff?.contractStartDate && (
        <div className="-mt-2">
          <button
            type="button"
            onClick={() => setOverrideDialogOpen(true)}
            className="flex w-full items-center justify-between gap-2"
          >
            <TariffLockIndicator
              contractStartDate={tariff.contractStartDate}
              contractDurationMonths={tariff.contractDurationMonths}
            />
            <span className="text-xs underline shrink-0" style={{ color: '#d97706' }}>
              {t('form.editAnywayButton')}
            </span>
          </button>
        </div>
      )}

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
                onClick={() => setValue('contractDurationMonths', isSelected ? null : months, { shouldDirty: true })}
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

      <Dialog open={overrideDialogOpen} onOpenChange={setOverrideDialogOpen}>
        <DialogContent className="border border-white/[0.14] bg-[rgba(10,15,25,0.92)] backdrop-blur-[20px] backdrop-saturate-[1.8] text-white [&>button]:text-white/60 [&>button]:hover:text-white [&>button]:ring-offset-transparent [&>button]:focus:ring-white/40 [&>button]:data-[state=open]:bg-white/10">
          <DialogHeader>
            <DialogTitle className="text-white">{t('form.overrideDialogTitle')}</DialogTitle>
            <DialogDescription className="text-white/60">{t('form.overrideDialogDescription')}</DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <DialogClose asChild>
              <button type="button" className="h-11 px-4 rounded-full text-sm text-white/70">
                {t('form.overrideDialogCancel')}
              </button>
            </DialogClose>
            <button
              type="button"
              onClick={() => {
                setOverrideConfirmed(true)
                setOverrideDialogOpen(false)
              }}
              className="h-11 px-4 rounded-full text-sm font-semibold text-white"
              style={{ background: 'rgba(217,119,6,0.85)' }}
            >
              {t('form.overrideDialogConfirm')}
            </button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </form>
  )
}
