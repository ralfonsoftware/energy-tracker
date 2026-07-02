import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import { Sheet, SheetTrigger, SheetContent } from '@/components/ui/sheet'
import { useTariffs } from '@/features/tariffs/hooks/useTariffs'
import { TariffForm } from '@/features/tariffs/components/TariffForm'
import type { TariffResponse } from '@/features/tariffs/api/tariffApi'

type Props = { flatId: string | undefined }

const formatDate = (isoDate: string) =>
  new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' }).format(new Date(isoDate))

const formatPricePerKwh = (value: number) =>
  new Intl.NumberFormat(i18n.language, {
    style: 'currency',
    currency: 'EUR',
    minimumFractionDigits: 4,
    maximumFractionDigits: 4,
  }).format(value)

const formatCurrency = (value: number) =>
  new Intl.NumberFormat(i18n.language, { style: 'currency', currency: 'EUR' }).format(value)

const toUtcDateString = (isoDate: string) => {
  const d = new Date(isoDate)
  const y = d.getUTCFullYear()
  const m = String(d.getUTCMonth() + 1).padStart(2, '0')
  const day = String(d.getUTCDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

const todayLocalDateString = () => {
  const now = new Date()
  const y = now.getFullYear()
  const m = String(now.getMonth() + 1).padStart(2, '0')
  const day = String(now.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

const isUpcoming = (effectiveDate: string) => toUtcDateString(effectiveDate) > todayLocalDateString()

export function TariffList({ flatId }: Props) {
  const { t } = useTranslation('tariffs')
  const navigate = useNavigate()
  const { data, isLoading, isError, refetch } = useTariffs(flatId)
  const [addOpen, setAddOpen] = useState(false)

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
          <Sheet open={addOpen} onOpenChange={setAddOpen}>
            <SheetTrigger asChild>
              <button
                type="button"
                disabled={!flatId}
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
              className="rounded-t-sheet border-t border-white/[0.14] bg-[rgba(10,15,25,0.92)] backdrop-blur-[20px] backdrop-saturate-[1.8] px-6 pb-8 pt-3 [&>button]:right-2 [&>button]:top-2 [&>button]:flex [&>button]:h-11 [&>button]:w-11 [&>button]:items-center [&>button]:justify-center"
            >
              <TariffForm flatId={flatId} onClose={() => setAddOpen(false)} />
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
              <TariffRow key={tariff.tariffId} tariff={tariff} />
            ))}
          </ul>
        )}
      </div>
    </div>
  )
}

function TariffRow({ tariff }: { tariff: TariffResponse }) {
  const { t } = useTranslation('tariffs')
  const upcoming = isUpcoming(tariff.effectiveDate)

  return (
    <li
      className="p-4 rounded-2xl"
      style={{ background: 'rgba(255,255,255,0.07)', border: '1px solid rgba(255,255,255,0.12)' }}
    >
      <div className="flex items-center justify-between">
        <span className="text-sm text-white">
          {upcoming
            ? t('list.upcomingLabel', { date: formatDate(tariff.effectiveDate) })
            : formatDate(tariff.effectiveDate)}
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
    </li>
  )
}
