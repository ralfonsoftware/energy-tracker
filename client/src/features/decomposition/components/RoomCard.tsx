import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import type { DeviceDecomposition, RoomDecomposition } from '@/features/decomposition/api/decompositionApi'
import { DeviceCard } from '@/features/decomposition/components/DeviceCard'
import { SmartStripCard } from '@/features/decomposition/components/SmartStripCard'

type Props = { room: RoomDecomposition; onConfigureDevice: (powerPointId: string) => void }

const formatNumber = (value: number) =>
  new Intl.NumberFormat(i18n.language, { maximumFractionDigits: 1 }).format(value)

const formatKwh = (value: number) => `${formatNumber(value)} kWh`

const formatCurrency = (value: number) =>
  new Intl.NumberFormat(i18n.language, { style: 'currency', currency: 'EUR' }).format(value)

function isNoneApproachNonStrip(device: DeviceDecomposition): boolean {
  return device.approach === 'None' && !device.isSmartStrip
}

function partitionAndSortDevices(devices: DeviceDecomposition[]): DeviceDecomposition[] {
  const visible = devices.filter(device => !isNoneApproachNonStrip(device))
  const measured = visible
    .filter(device => device.approach === 'Measured' || device.isSmartStrip)
    .sort((a, b) => b.kwh - a.kwh)
  const estimated = visible
    .filter(device => device.approach !== 'Measured' && !device.isSmartStrip)
    .sort((a, b) => b.kwh - a.kwh)
  return [...measured, ...estimated]
}

export function RoomCard({ room, onConfigureDevice }: Props) {
  const { t } = useTranslation('decomposition')
  const isDirectConsumptionOnly =
    room.devices.length === 0 || room.devices.every(isNoneApproachNonStrip)

  return (
    <div className="rounded-card border border-glass-border bg-glass-surface overflow-hidden">
      <div className="flex items-center justify-between px-4 py-3">
        <span className="text-body text-white">{room.roomName}</span>
        <span className="text-body-sm text-white/45">
          {formatKwh(room.kwh)} · {formatCurrency(room.cost)}
        </span>
      </div>
      {isDirectConsumptionOnly ? (
        <div className="flex items-center justify-between px-4 pb-3.5">
          <span className="text-body-sm text-white/55">{t('roomCard.directConsumption')}</span>
          <span className="text-body-sm text-white/55">{formatKwh(room.kwh)}</span>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-2 px-4 pb-3.5 md:grid-cols-2 lg:grid-cols-3">
          {partitionAndSortDevices(room.devices).map(device =>
            device.isSmartStrip ? (
              <div key={device.deviceId} className="md:col-span-full">
                <SmartStripCard device={device} onConfigure={() => onConfigureDevice(device.deviceId)} />
              </div>
            ) : (
              <DeviceCard key={device.deviceId} device={device} />
            )
          )}
        </div>
      )}
    </div>
  )
}
