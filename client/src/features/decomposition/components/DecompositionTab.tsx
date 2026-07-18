import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { useDecomposition } from '@/features/decomposition/hooks/useDecomposition'
import { resolvePeriodRange, type PeriodOption } from '@/features/decomposition/lib/periods'
import type { RoomDecomposition } from '@/features/decomposition/api/decompositionApi'
import { PeriodSelector } from '@/features/decomposition/components/PeriodSelector'
import { ResidualCard } from '@/features/decomposition/components/ResidualCard'
import { RoomCard } from '@/features/decomposition/components/RoomCard'
import { DecompositionUnavailable } from '@/features/decomposition/components/DecompositionUnavailable'

type Props = { flatId: string | undefined }

type CustomRange = { startDate: string; endDate: string }

function sortRoomsByKwh(rooms: RoomDecomposition[]): RoomDecomposition[] {
  return [...rooms].sort((a, b) => b.kwh - a.kwh)
}

export function DecompositionTab({ flatId }: Props) {
  const { t } = useTranslation('decomposition')
  const navigate = useNavigate()
  const [period, setPeriod] = useState<PeriodOption>('thisMonth')
  const [customRange, setCustomRange] = useState<CustomRange | null>(null)

  const { startDate, endDate } =
    period === 'custom'
      ? { startDate: customRange?.startDate, endDate: customRange?.endDate }
      : resolvePeriodRange(period)

  const isCustomRangeIncomplete = period === 'custom' && (!startDate || !endDate)

  const { data, isPending, isError, refetch } = useDecomposition(flatId, startDate, endDate)

  return (
    <div className="flex flex-col gap-3">
      <PeriodSelector
        value={period}
        customRange={customRange}
        onChange={setPeriod}
        onCustomRangeChange={setCustomRange}
      />

      {isCustomRangeIncomplete && (
        <p className="text-body-sm text-text-secondary">{t('period.selectRange')}</p>
      )}

      {!isCustomRangeIncomplete && isPending && (
        <div className="flex flex-col gap-3">
          <div className="h-[94px] animate-pulse rounded-card border border-glass-border bg-white/10" />
          <div className="h-[52px] animate-pulse rounded-card border border-glass-border bg-white/10" />
          <div className="h-[52px] animate-pulse rounded-card border border-glass-border bg-white/10" />
        </div>
      )}

      {!isCustomRangeIncomplete && !isPending && isError && (
        <div>
          <p role="alert" className="text-body-sm text-accent-error">
            {t('loadError')}
          </p>
          <button
            type="button"
            onClick={() => refetch()}
            className="mt-2 min-h-11 min-w-11 text-body-sm text-text-secondary underline"
          >
            {t('retry')}
          </button>
        </div>
      )}

      {!isPending && !isError && data?.isUnavailable && (
        <DecompositionUnavailable onImport={() => navigate('/decomposition/import')} />
      )}

      {!isPending && !isError && data !== undefined && !data.isUnavailable && (
        <div className="flex flex-col gap-3">
          {data.hasInterpolatedData && (
            <div
              className="rounded-2xl px-3.5 py-2.5"
              style={{ background: 'rgba(96,165,250,0.08)', border: '1px solid rgba(96,165,250,0.2)' }}
            >
              <span className="text-body-sm text-white/55">{t('interpolatedBanner')}</span>
            </div>
          )}
          <ResidualCard kwh={data.residual.kwh} cost={data.residual.cost} totalKwh={data.totalKwh} />
          {/* API orders rooms by Room.SortOrder; we re-sort by kWh for display here — not a bug, don't "fix" the backend ordering */}
          {sortRoomsByKwh(data.rooms).map(room => (
            <RoomCard
              key={room.roomId}
              room={room}
              onConfigureDevice={powerPointId => navigate(`/settings/structure?powerPointId=${powerPointId}`)}
            />
          ))}
        </div>
      )}
    </div>
  )
}
