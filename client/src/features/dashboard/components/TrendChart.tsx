import { useMemo, useState } from 'react'
import { BarChart, Bar, XAxis, Cell, ResponsiveContainer } from 'recharts'
import { History } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import { Sheet, SheetTrigger, SheetContent } from '@/components/ui/sheet'
import { ReadingHistorySheet } from '@/features/readings/components/ReadingHistorySheet'
import type { DashboardSummary } from '@/features/dashboard/api/dashboardApi'

type Props = {
  dashboard: DashboardSummary | undefined
  flatId: string | undefined
}

export function TrendChart({ dashboard, flatId }: Props) {
  const { t } = useTranslation('dashboard')
  const [historyOpen, setHistoryOpen] = useState(false)

  const spikeSet = useMemo(() => new Set(dashboard?.spikeDays ?? []), [dashboard?.spikeDays])
  const chartData = useMemo(
    () =>
      (dashboard?.dailyConsumption ?? []).map(point => ({
        date: point.date,
        kwh: point.kwhValue,
        label: new Intl.DateTimeFormat(i18n.language, { weekday: 'narrow', timeZone: 'UTC' }).format(
          new Date(point.date)
        ),
      })),
    [dashboard?.dailyConsumption]
  )

  // No trend data can exist yet with 0 or 1 total readings — hide the card rather than
  // render a misleading all-zero 7-day chart (mirrors the KPI tiles' cold-open dash state).
  if (dashboard !== undefined && chartData.length === 0) return null

  return (
    <div className="relative z-10 mx-4 mb-6 rounded-card border border-glass-border bg-glass-surface p-4 backdrop-blur-[20px] backdrop-saturate-[180%]">
      <div className="mb-3 flex items-center justify-between">
        <span className="text-label-caps text-text-tertiary">{t('trend.cardTitle')}</span>
        <Sheet open={historyOpen} onOpenChange={setHistoryOpen}>
          <SheetTrigger asChild>
            <button
              type="button"
              aria-label={t('trend.historyIconLabel')}
              className="flex h-11 w-11 items-center justify-center text-text-secondary"
            >
              <History size={20} />
            </button>
          </SheetTrigger>
          <SheetContent
            side="bottom"
            className="rounded-t-sheet border-t border-white/[0.14] bg-[rgba(10,15,25,0.92)] backdrop-blur-[20px] backdrop-saturate-[1.8] px-6 pb-8 pt-3 [&>button]:right-2 [&>button]:top-2 [&>button]:flex [&>button]:h-11 [&>button]:w-11 [&>button]:items-center [&>button]:justify-center [&>button]:text-white/60 [&>button]:hover:text-white [&>button]:ring-offset-transparent [&>button]:focus:ring-white/40 [&>button]:data-[state=open]:bg-white/10"
          >
            <ReadingHistorySheet flatId={flatId} />
          </SheetContent>
        </Sheet>
      </div>
      {dashboard === undefined ? (
        <div className="flex h-[90px] items-end gap-1.5">
          {Array.from({ length: 7 }).map((_, i) => (
            <div key={i} className="h-1/2 flex-1 animate-pulse rounded-t bg-white/10" />
          ))}
        </div>
      ) : (
        <ResponsiveContainer width="100%" height={90}>
          <BarChart data={chartData} barCategoryGap={6}>
            <XAxis
              dataKey="label"
              axisLine={false}
              tickLine={false}
              interval={0}
              tick={{ fill: 'rgba(255,255,255,0.35)', fontSize: 10 }}
            />
            <Bar dataKey="kwh" radius={[4, 4, 2, 2]} isAnimationActive={false}>
              {chartData.map(entry => (
                <Cell
                  key={entry.date}
                  fill={spikeSet.has(entry.date) ? 'var(--color-accent-spike)' : 'rgba(255,255,255,0.5)'}
                />
              ))}
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      )}
    </div>
  )
}
