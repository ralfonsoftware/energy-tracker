import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import { useImportJobStatus } from '@/features/smart-plug-import/hooks/useImportJobStatus'
import { parseGapNotifications, type ImportErrorCategory } from '@/features/smart-plug-import/api/importApi'
import { useNavigate } from 'react-router-dom'

function parseDateOnly(dateOnly: string): Date {
  const [y, m, d] = dateOnly.split('-').map(Number)
  return new Date(y, m - 1, d)
}

function formatDateOnly(dateOnly: string): string {
  return new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' }).format(parseDateOnly(dateOnly))
}

const ERROR_KEY: Record<ImportErrorCategory, string> = {
  DataUnreadable: 'progress.errorDataUnreadable',
  ProcessingFailed: 'progress.errorProcessingFailed',
  ServiceUnavailable: 'progress.errorServiceUnavailable',
}

type Props = { flatId: string | undefined }

export function ImportProgressCard({ flatId }: Props) {
  const { t } = useTranslation('import')
  const navigate = useNavigate()
  const { jobs, dismiss } = useImportJobStatus(flatId)

  if (jobs.length === 0) return null

  return (
    <div className="mb-4 flex flex-col gap-2.5">
      {jobs.map(job => {
        if (job.isError) {
          return (
            <div
              key={job.importJobId}
              role="alert"
              className="rounded-2xl px-4 py-3.5 flex items-center gap-3"
              style={{ background: 'var(--color-residual-tint)', border: '1px solid rgba(251,191,36,0.2)' }}
            >
              <div className="flex-1">
                <div className="text-sm font-semibold text-white">{job.fileName}</div>
                <div className="text-xs text-white/60 mt-0.5">{t(ERROR_KEY.ServiceUnavailable)}</div>
              </div>
              <button
                type="button"
                onClick={() => { dismiss(job.importJobId); navigate('/decomposition/import') }}
                className="text-xs font-semibold"
                style={{ color: '#f59e0b' }}
              >
                {t('progress.retry')}
              </button>
            </div>
          )
        }

        const status = job.statusData?.status ?? 'Pending'
        const errorCategory = job.statusData?.errorCategory ?? null
        const gaps = job.statusData ? parseGapNotifications(job.statusData.gapNotifications) : []

        if (status === 'Failed' && errorCategory === 'DataUnreadable') {
          return (
            <div
              key={job.importJobId}
              role="alert"
              className="rounded-2xl px-4 py-3.5 flex items-center gap-3"
              style={{ background: 'rgba(255,255,255,0.07)', borderLeft: '3px solid var(--color-accent-error)', border: '1px solid rgba(255,255,255,0.12)' }}
            >
              <div className="flex-1">
                <div className="text-sm font-medium text-white">{job.fileName}</div>
                <div className="text-xs text-accent-error mt-0.5">{t(ERROR_KEY.DataUnreadable)}</div>
              </div>
              <button type="button" onClick={() => dismiss(job.importJobId)} className="text-xs font-semibold text-white/60">
                {t('remove')}
              </button>
            </div>
          )
        }

        if (status === 'Failed') {
          return (
            <div
              key={job.importJobId}
              role="alert"
              className="rounded-2xl px-4 py-3.5 flex items-center gap-3"
              style={{ background: 'var(--color-residual-tint)', border: '1px solid rgba(251,191,36,0.2)' }}
            >
              <div className="flex-1">
                <div className="text-sm font-semibold text-white">{job.fileName}</div>
                <div className="text-xs text-white/60 mt-0.5">{t(errorCategory ? ERROR_KEY[errorCategory] : ERROR_KEY.ProcessingFailed)}</div>
              </div>
              <button
                type="button"
                onClick={() => { dismiss(job.importJobId); navigate('/decomposition/import') }}
                className="text-xs font-semibold"
                style={{ color: '#f59e0b' }}
              >
                {t('progress.retry')}
              </button>
            </div>
          )
        }

        if (status === 'Complete' && gaps.length > 0) {
          return (
            <div key={job.importJobId} className="rounded-2xl px-4 py-3.5 flex items-center gap-3" style={{ background: 'rgba(255,255,255,0.07)', border: '1px solid rgba(255,255,255,0.12)' }}>
              <div className="flex-1 text-xs text-white/70">
                {gaps.map((gap, i) => (
                  <p key={i}>{t('progress.gapNotice', { range: `${formatDateOnly(gap.start)} – ${formatDateOnly(gap.end)}` })}</p>
                ))}
              </div>
              <button type="button" onClick={() => dismiss(job.importJobId)} className="text-xs font-semibold text-white/60">
                {t('progress.dismiss')}
              </button>
            </div>
          )
        }

        return (
          <div
            key={job.importJobId}
            className="rounded-2xl px-4 py-3.5 flex items-center gap-3"
            style={{ background: 'var(--color-residual-tint)', border: '1px solid rgba(251,191,36,0.2)' }}
          >
            <div className="w-4 h-4 rounded-full border-2 shrink-0 animate-spin" style={{ borderColor: 'rgba(245,158,11,0.25)', borderTopColor: '#f59e0b' }} />
            <div className="flex-1">
              <div className="text-sm font-semibold text-white">{t('progress.processingTitle')}</div>
              <div className="text-xs text-white/45 mt-0.5">{job.fileName}</div>
            </div>
          </div>
        )
      })}
    </div>
  )
}
