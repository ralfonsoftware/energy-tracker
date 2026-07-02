import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import { parseLocaleNumber, formatNumberForInput } from '@/lib/localeNumber'
import { useSubmitGuard } from '@/lib/useSubmitGuard'
import { useReadingHistory } from '@/features/readings/hooks/useReadingHistory'
import { usePatchReading } from '@/features/readings/hooks/usePatchReading'
import type { ReadingResponse } from '@/features/readings/api/readingApi'

type Props = { flatId: string | undefined }

const formatNumber = (value: number) =>
  new Intl.NumberFormat(i18n.language, { maximumFractionDigits: 1 }).format(value)

const formatKwh = (value: number) => `${formatNumber(value)} kWh`

const formatDateTime = (isoDate: string) =>
  new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(isoDate))

export function ReadingHistorySheet({ flatId }: Props) {
  const { t } = useTranslation('readings')
  const { data, isLoading, isError, refetch } = useReadingHistory(flatId)
  const { mutate, isPending, isError: isSaveError } = usePatchReading(flatId)
  const [editingReading, setEditingReading] = useState<ReadingResponse | null>(null)

  if (editingReading) {
    return (
      <ReadingEditView
        reading={editingReading}
        isPending={isPending}
        isError={isSaveError}
        onBack={() => setEditingReading(null)}
        onSave={kwhValue =>
          mutate(
            { readingId: editingReading.readingId, kwhValue },
            { onSuccess: () => setEditingReading(null) }
          )
        }
      />
    )
  }

  return (
    <div>
      <div aria-hidden="true" className="mx-auto mb-4 h-1 w-9 rounded-full bg-white/25" />
      <h2 className="text-body text-text-primary">{t('history.title')}</h2>
      {isLoading && (
        <div className="mt-4 flex flex-col gap-2">
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} className="h-11 animate-pulse rounded-input bg-white/10" />
          ))}
        </div>
      )}
      {isError && (
        <div className="mt-4">
          <p role="alert" className="text-body-sm text-accent-error">
            {t('history.loadError')}
          </p>
          <button
            type="button"
            onClick={() => refetch()}
            className="mt-2 min-h-11 min-w-11 text-body-sm text-text-secondary underline"
          >
            {t('history.retry')}
          </button>
        </div>
      )}
      {!isLoading && !isError && (data ?? []).length === 0 && (
        <p className="mt-4 text-body-sm text-text-tertiary">{t('history.empty')}</p>
      )}
      {!isLoading && !isError && (data ?? []).length > 0 && (
        <ul className="mt-4 flex flex-col divide-y divide-white/10">
          {(data ?? []).map(reading => (
            <li key={reading.readingId}>
              <button
                type="button"
                onClick={() => setEditingReading(reading)}
                className="flex min-h-11 w-full flex-col items-start justify-center gap-0.5 py-2 text-left"
              >
                <span className="flex w-full items-center justify-between text-body-sm text-text-primary">
                  <span>{formatDateTime(reading.readingDate)}</span>
                  <span>{formatKwh(reading.kwhValue)}</span>
                </span>
                {reading.isCorrected && reading.originalKwhValue !== null && (
                  <span className="text-caption text-text-tertiary">
                    {t('history.correctedNote', { value: formatNumber(reading.originalKwhValue) })}
                  </span>
                )}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

type EditViewProps = {
  reading: ReadingResponse
  isPending: boolean
  isError: boolean
  onBack: () => void
  onSave: (kwhValue: number) => void
}

function ReadingEditView({ reading, isPending, isError, onBack, onSave }: EditViewProps) {
  const { t } = useTranslation('readings')
  const inputRef = useRef<HTMLInputElement | null>(null)
  const tryAcquire = useSubmitGuard(isPending)
  const [kwhRaw, setKwhRaw] = useState(formatNumberForInput(reading.kwhValue, i18n.language))

  useEffect(() => {
    inputRef.current?.focus()
  }, [])

  const parsed = parseLocaleNumber(kwhRaw, i18n.language)
  const isSaveEnabled = !isNaN(parsed) && parsed > 0 && !isPending

  const handleSave = () => {
    if (!tryAcquire()) return
    onSave(parsed)
  }

  return (
    <div>
      <div aria-hidden="true" className="mx-auto mb-4 h-1 w-9 rounded-full bg-white/25" />
      <button
        type="button"
        onClick={onBack}
        className="mb-3 min-h-11 min-w-11 text-body-sm text-text-secondary"
      >
        {t('history.backToList')}
      </button>
      <p className="mb-2 text-caption text-text-tertiary">{formatDateTime(reading.readingDate)}</p>
      <input
        ref={inputRef}
        type="text"
        inputMode="numeric"
        value={kwhRaw}
        onChange={e => setKwhRaw(e.target.value)}
        className="h-14 w-full rounded-input border border-white/15 bg-white/[0.08] px-4 text-2xl text-text-primary outline-none focus:border-white/60"
      />
      {isError && (
        <p role="alert" aria-live="polite" className="mt-2 text-body-sm text-accent-error">
          {t('history.editSaveError')}
        </p>
      )}
      <button
        type="button"
        disabled={!isSaveEnabled}
        onClick={handleSave}
        className="mt-4 h-14 w-full rounded-pill border border-white/40 bg-white/[0.12] text-body text-text-primary disabled:opacity-40"
      >
        {t('history.editSaveButton')}
      </button>
    </div>
  )
}
