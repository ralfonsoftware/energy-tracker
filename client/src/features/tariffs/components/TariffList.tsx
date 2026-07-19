import { useCallback, useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import { parseLocaleNumber, formatNumberForInput } from '@/lib/localeNumber'
import { parseLocalDate, isFutureLocalDate } from '@/lib/localDate'
import { Sheet, SheetTrigger, SheetContent } from '@/components/ui/sheet'
import { useTariffs } from '@/features/tariffs/hooks/useTariffs'
import { TariffForm } from '@/features/tariffs/components/TariffForm'
import type { TariffResponse } from '@/features/tariffs/api/tariffApi'

type Props = {
  flatId: string | undefined
  annualKwhBaseline?: number
  plannedAnnualSpend?: number | null
  onSavePlannedAnnualSpend: (value: number) => void
  isSavingPlannedAnnualSpend: boolean
  isPlannedAnnualSpendSaveError: boolean
}

const formatDate = (isoDate: string) =>
  new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' }).format(parseLocalDate(isoDate))

const formatPricePerKwh = (value: number) =>
  new Intl.NumberFormat(i18n.language, {
    style: 'currency',
    currency: 'EUR',
    minimumFractionDigits: 4,
    maximumFractionDigits: 4,
  }).format(value)

const formatCurrency = (value: number) =>
  new Intl.NumberFormat(i18n.language, { style: 'currency', currency: 'EUR' }).format(value)

const sectionLabelClass = 'text-[11px] font-semibold tracking-[0.08em] uppercase text-white/45'

export function TariffList({
  flatId,
  annualKwhBaseline,
  plannedAnnualSpend,
  onSavePlannedAnnualSpend,
  isSavingPlannedAnnualSpend,
  isPlannedAnnualSpendSaveError,
}: Props) {
  const { t } = useTranslation('tariffs')
  const navigate = useNavigate()
  const { data, isLoading, isError, refetch } = useTariffs(flatId)
  const [addOpen, setAddOpen] = useState(false)
  const [editingTariff, setEditingTariff] = useState<TariffResponse | null>(null)
  const [formPending, setFormPending] = useState(false)
  const handleFormPendingChange = useCallback((pending: boolean) => setFormPending(pending), [])

  const activeTariff = (data ?? []).find(tariff => !isFutureLocalDate(tariff.contractStartDate))

  const closeSheet = () => {
    setAddOpen(false)
    setEditingTariff(null)
  }

  return (
    <div className="flex-1 flex flex-col" style={{ background: '#111827', minHeight: '100vh' }}>
      <div className="px-6 pt-4">
        <button
          type="button"
          onClick={() => navigate('/settings')}
          className="text-white/50 hover:text-white/80 transition-colors mb-6"
        >
          ← {t('list.back')}
        </button>

        <div className="flex items-center justify-between mb-6">
          <h1 className="text-[22px] font-semibold text-white tracking-tight">{t('list.title')}</h1>
          <Sheet
            open={addOpen || editingTariff !== null}
            onOpenChange={open => {
              if (!open && formPending) return
              if (!open) closeSheet()
            }}
          >
            <SheetTrigger asChild>
              <button
                type="button"
                disabled={!flatId}
                onClick={() => setAddOpen(true)}
                className="px-3 py-1.5 text-xs font-medium rounded-full disabled:opacity-40"
                style={{
                  background: 'rgba(255,255,255,0.10)',
                  border: '1px solid rgba(255,255,255,0.12)',
                  color: 'rgba(255,255,255,0.75)',
                }}
              >
                {t('list.addButton')}
              </button>
            </SheetTrigger>
            <SheetContent
              side="bottom"
              className="rounded-t-sheet border-t border-white/[0.14] bg-[rgba(10,15,25,0.92)] backdrop-blur-[20px] backdrop-saturate-[1.8] px-6 pb-8 pt-3 [&>button]:right-3 [&>button]:top-3 [&>button]:flex [&>button]:h-11 [&>button]:w-11 [&>button]:items-center [&>button]:justify-center [&>button]:opacity-100 [&>button]:text-white/60 [&>button]:hover:text-white [&>button]:ring-offset-transparent [&>button]:focus:ring-white/40 [&>button]:data-[state=open]:bg-white/10 [&>button_svg]:h-5 [&>button_svg]:w-5"
            >
              <TariffForm
                flatId={flatId}
                tariff={editingTariff ?? undefined}
                onClose={closeSheet}
                onPendingChange={handleFormPendingChange}
                onSaveConflict={() => {
                  refetch().then(result => {
                    if (!editingTariff) return
                    const fresh = result.data?.find(t => t.tariffId === editingTariff.tariffId)
                    if (fresh) setEditingTariff(fresh)
                  })
                }}
              />
            </SheetContent>
          </Sheet>
        </div>
      </div>

      <div className="px-6 flex-1 pb-10">
        {isLoading && (
          <div className="flex flex-col gap-2">
            {Array.from({ length: 3 }).map((_, i) => (
              <div key={i} className="h-16 animate-pulse rounded-2xl bg-white/10" />
            ))}
          </div>
        )}
        {isError && (
          <div>
            <p role="alert" className="text-sm text-accent-error">
              {t('list.loadError')}
            </p>
            <button
              type="button"
              onClick={() => refetch()}
              className="mt-2 min-h-11 min-w-11 text-sm text-white/60 underline"
            >
              {t('list.retry')}
            </button>
          </div>
        )}
        {!isLoading && !isError && (data ?? []).length === 0 && (
          <p className="text-sm text-white/50">{t('list.empty')}</p>
        )}
        {!isLoading && !isError && (data ?? []).length > 0 && (
          <ul className="flex flex-col gap-2">
            {(data ?? []).map(tariff => (
              <TariffRow key={tariff.tariffId} tariff={tariff} onEdit={() => setEditingTariff(tariff)} />
            ))}
          </ul>
        )}

        {!isLoading && !isError && (
          <PlannedAnnualSpendSection
            plannedAnnualSpend={plannedAnnualSpend}
            activeTariff={activeTariff}
            annualKwhBaseline={annualKwhBaseline}
            onSave={onSavePlannedAnnualSpend}
            isSaving={isSavingPlannedAnnualSpend}
            isSaveError={isPlannedAnnualSpendSaveError}
          />
        )}
      </div>
    </div>
  )
}

function TariffRow({ tariff, onEdit }: { tariff: TariffResponse; onEdit: () => void }) {
  const { t } = useTranslation('tariffs')
  const upcoming = isFutureLocalDate(tariff.contractStartDate)

  return (
    <li
      className="rounded-2xl"
      style={{ background: 'rgba(255,255,255,0.07)', border: '1px solid rgba(255,255,255,0.12)' }}
    >
      <button type="button" onClick={onEdit} className="w-full p-4 text-left">
        <div className="flex items-center justify-between">
          <span className="text-sm text-white">
            {upcoming
              ? t('list.upcomingLabel', { date: formatDate(tariff.contractStartDate) })
              : formatDate(tariff.contractStartDate)}
          </span>
          <span className="text-sm text-white">
            {formatPricePerKwh(tariff.pricePerKwh)}
            {t('list.kwhUnit')}
          </span>
        </div>
        <div className="flex items-center justify-between mt-1">
          <span className="text-xs text-white/50">{tariff.providerName}</span>
          <span className="text-xs text-white/50">
            {formatCurrency(tariff.monthlyBaseFee)}
            {t('list.monthUnit')}
          </span>
        </div>
      </button>
    </li>
  )
}

type PlannedAnnualSpendSectionProps = {
  plannedAnnualSpend?: number | null
  activeTariff?: TariffResponse
  annualKwhBaseline?: number
  onSave: (value: number) => void
  isSaving: boolean
  isSaveError: boolean
}

function PlannedAnnualSpendSection({
  plannedAnnualSpend,
  activeTariff,
  annualKwhBaseline,
  onSave,
  isSaving,
  isSaveError,
}: PlannedAnnualSpendSectionProps) {
  const { t, i18n } = useTranslation('tariffs')
  const [raw, setRaw] = useState(
    plannedAnnualSpend != null ? formatNumberForInput(plannedAnnualSpend, i18n.language) : ''
  )
  const [dirty, setDirty] = useState(false)
  const wasSavingRef = useRef(false)
  const submittedValueRef = useRef<number | null>(null)

  useEffect(() => {
    if (!dirty) {
      setRaw(plannedAnnualSpend != null ? formatNumberForInput(plannedAnnualSpend, i18n.language) : '')
    }
  }, [plannedAnnualSpend, i18n.language, dirty])

  const parsed = parseLocaleNumber(raw, i18n.language)

  useEffect(() => {
    if (wasSavingRef.current && !isSaving && !isSaveError && submittedValueRef.current === parsed) {
      setDirty(false)
    }
    wasSavingRef.current = isSaving
  }, [isSaving, isSaveError, parsed])

  const isSaveEnabled = dirty && Number.isFinite(parsed) && parsed > 0 && !isSaving

  const handleSave = () => {
    if (!isSaveEnabled) return
    submittedValueRef.current = parsed
    onSave(parsed)
  }

  const helperText =
    activeTariff && annualKwhBaseline != null
      ? t('budget.helperText', {
          kwh: new Intl.NumberFormat(i18n.language).format(annualKwhBaseline),
          price: formatPricePerKwh(activeTariff.pricePerKwh),
          fee: formatCurrency(activeTariff.monthlyBaseFee),
          total: formatCurrency(annualKwhBaseline * activeTariff.pricePerKwh + activeTariff.monthlyBaseFee * 12),
        })
      : null

  return (
    <div
      className="mt-6 p-4 rounded-2xl"
      style={{ background: 'rgba(255,255,255,0.05)', border: '1px solid rgba(255,255,255,0.10)' }}
    >
      <h2 className={sectionLabelClass}>{t('budget.title')}</h2>
      <div className="mt-3 flex items-center gap-2">
        <div className="relative flex-1">
          <input
            type="text"
            inputMode="decimal"
            placeholder="0"
            value={raw}
            onChange={e => {
              setRaw(e.target.value)
              setDirty(true)
            }}
            aria-label={t('budget.title')}
            className="w-full h-[52px] px-4 rounded-[12px] bg-white/[0.08] border text-white text-base placeholder:text-white/30 outline-none focus:border-white/60"
            style={{ borderColor: 'rgba(255,255,255,0.15)' }}
          />
          <span
            className="absolute right-4 top-1/2 -translate-y-1/2 text-sm pointer-events-none"
            style={{ color: 'rgba(255,255,255,0.40)' }}
          >
            {t('budget.spendSuffix')}
          </span>
        </div>
        <button
          type="button"
          onClick={handleSave}
          disabled={!isSaveEnabled}
          className="h-[52px] px-4 rounded-full text-sm font-semibold text-white disabled:opacity-40"
          style={{ background: 'rgba(255,255,255,0.12)', border: '1px solid rgba(255,255,255,0.40)' }}
        >
          {isSaving ? t('budget.savingLabel') : t('budget.saveButton')}
        </button>
      </div>
      {helperText && <p className="mt-2 text-xs text-white/40">{helperText}</p>}
      {isSaveError && (
        <p role="alert" className="mt-2 text-xs text-accent-error">
          {t('budget.errorMessage')}
        </p>
      )}
    </div>
  )
}
