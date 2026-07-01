import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'

type Props = { coveredDays: number; totalDays: number }

export function CostGapBadge({ coveredDays, totalDays }: Props) {
  const { t } = useTranslation('dashboard')
  const navigate = useNavigate()

  return (
    <Popover>
      <PopoverTrigger asChild>
        <button
          type="button"
          className="inline-flex items-center gap-1 text-label-caps text-accent-tariff-locked"
        >
          <span aria-hidden="true">⚠</span>
          {t('costGap.badgeLabel', { covered: coveredDays, total: totalDays })}
        </button>
      </PopoverTrigger>
      <PopoverContent className="rounded-card border-glass-border bg-[#1a1f2e]/95 backdrop-blur-xl text-text-primary shadow-lg">
        <p className="text-body-sm text-text-primary">{t('costGap.popoverBody')}</p>
        <button
          type="button"
          onClick={() => navigate('/settings')}
          className="mt-2 text-body-sm text-accent-info underline"
        >
          {t('costGap.popoverCta')}
        </button>
      </PopoverContent>
    </Popover>
  )
}
