import { useEffect, useRef, useState } from 'react'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import { parseLocaleNumber } from '@/lib/localeNumber'
import { useSubmitGuard } from '@/lib/useSubmitGuard'
import { SheetContent } from '@/components/ui/sheet'
import { useSubmitReading } from '@/features/readings/hooks/useSubmitReading'
import { readingSheetSchema, type ReadingSheetFormValues } from '@/features/readings/schemas/readingSchema'

type Props = {
  flatId: string | undefined
  lastKwhValue: number | null
  open: boolean
  onOpenChange: (open: boolean) => void
  onSubmitSuccess?: () => void
}

function toDatetimeLocal(date: Date) {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

export function EnterReadingSheet({ flatId, lastKwhValue, open, onOpenChange, onSubmitSuccess }: Props) {
  const { t } = useTranslation('readings')
  const { mutate, isPending, isError } = useSubmitReading(flatId, onSubmitSuccess)
  const [submitError, setSubmitError] = useState(false)
  const inputRef = useRef<HTMLInputElement | null>(null)
  const tryAcquire = useSubmitGuard(isPending)

  const { control, handleSubmit, watch, reset } = useForm<ReadingSheetFormValues>({
    resolver: zodResolver(readingSheetSchema),
    defaultValues: { kwhValue: '', readingDate: toDatetimeLocal(new Date()) },
  })

  useEffect(() => {
    if (open) {
      reset({ kwhValue: '', readingDate: toDatetimeLocal(new Date()) })
      setSubmitError(false)
    }
  }, [open, reset])

  const kwhRaw = watch('kwhValue') ?? ''
  const kwhParsed = parseLocaleNumber(kwhRaw, i18n.language)
  const isLower = !isNaN(kwhParsed) && lastKwhValue !== null && kwhParsed < lastKwhValue
  const isSaveEnabled = !isNaN(kwhParsed) && kwhParsed > 0 && !isPending && flatId !== undefined

  const onSubmit = (data: ReadingSheetFormValues) => {
    const parsed = parseLocaleNumber(data.kwhValue, i18n.language)
    if (isNaN(parsed) || parsed <= 0) return
    const readingDate = new Date(data.readingDate)
    if (isNaN(readingDate.getTime())) return
    if (!tryAcquire()) return

    setSubmitError(false)
    mutate(
      { kwhValue: parsed, readingDate: readingDate.toISOString() },
      {
        onSuccess: () => onOpenChange(false),
        onError: () => setSubmitError(true),
      }
    )
  }

  return (
    <SheetContent
      side="bottom"
      onOpenAutoFocus={event => {
        event.preventDefault()
        inputRef.current?.focus()
      }}
      className="rounded-t-sheet border-t border-white/[0.14] bg-[rgba(10,15,25,0.92)] backdrop-blur-[20px] backdrop-saturate-[1.8] px-6 pb-8 pt-3 [&>button]:right-2 [&>button]:top-2 [&>button]:flex [&>button]:h-11 [&>button]:w-11 [&>button]:items-center [&>button]:justify-center [&>button]:text-white/60 [&>button]:hover:text-white [&>button]:ring-offset-transparent [&>button]:focus:ring-white/40 [&>button]:data-[state=open]:bg-white/10"
    >
      <div aria-hidden="true" className="mx-auto mb-4 h-1 w-9 rounded-full bg-white/25" />
      <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-3">
        <Controller
          control={control}
          name="kwhValue"
          render={({ field }) => (
            <input
              {...field}
              ref={el => {
                field.ref(el)
                inputRef.current = el
              }}
              type="text"
              inputMode="numeric"
              placeholder="0"
              className="h-14 w-full rounded-input border border-white/15 bg-white/[0.08] px-4 text-2xl text-text-primary outline-none focus:border-white/60"
            />
          )}
        />
        {isLower && (
          <p role="status" aria-live="polite" className="text-body-sm text-accent-over-budget">
            {t('sheet.lowerWarning', { value: lastKwhValue })}
          </p>
        )}
        <Controller
          control={control}
          name="readingDate"
          render={({ field }) => (
            <input
              {...field}
              type="datetime-local"
              style={{ colorScheme: 'dark' }}
              className="h-12 w-full rounded-input border border-white/15 bg-white/[0.08] px-4 text-text-primary outline-none"
            />
          )}
        />
        <p className="text-caption text-text-tertiary">{t('sheet.dateHint')}</p>
        {(submitError || isError) && (
          <p role="alert" aria-live="polite" className="text-body-sm text-accent-error">
            {t('sheet.saveError')}
          </p>
        )}
        <button
          type="submit"
          disabled={!isSaveEnabled}
          className="mt-2 h-14 w-full rounded-pill border border-white/40 bg-white/[0.12] text-body text-text-primary disabled:opacity-40"
        >
          {t('sheet.saveButton')}
        </button>
      </form>
    </SheetContent>
  )
}
