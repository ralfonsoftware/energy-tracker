import i18n from '@/lib/i18n'
import type { RoomDecomposition } from '@/features/decomposition/api/decompositionApi'

type Props = { room: RoomDecomposition }

const formatNumber = (value: number) =>
  new Intl.NumberFormat(i18n.language, { maximumFractionDigits: 1 }).format(value)

const formatKwh = (value: number) => `${formatNumber(value)} kWh`

const formatCurrency = (value: number) =>
  new Intl.NumberFormat(i18n.language, { style: 'currency', currency: 'EUR' }).format(value)

export function RoomCard({ room }: Props) {
  return (
    <div className="rounded-card border border-glass-border bg-glass-surface overflow-hidden">
      <div className="flex items-center justify-between px-4 py-3">
        <span className="text-body text-white">{room.roomName}</span>
        <span className="text-body-sm text-white/45">
          {formatKwh(room.kwh)} · {formatCurrency(room.cost)}
        </span>
      </div>
    </div>
  )
}
