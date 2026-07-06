import { useTranslation } from 'react-i18next'
import { PowerPointEditor } from './PowerPointEditor'
import type { DraftRoom } from './draftModel'

type Props = {
  room: DraftRoom
  onChange: (updated: DraftRoom) => void
  onBack: () => void
  onEditDevice: (powerPointKey: string, deviceKey: string | null) => void
}

export function RoomEditor({ room, onChange, onBack, onEditDevice }: Props) {
  const { t } = useTranslation('flat-structure')

  const handleAddPowerPoint = () => {
    onChange({
      ...room,
      powerPoints: [
        ...room.powerPoints,
        { key: crypto.randomUUID(), name: '', plugId: '', devices: [] },
      ],
    })
  }

  return (
    <div className="flex-1 flex flex-col" style={{ background: '#111827', minHeight: '100vh' }}>
      <div className="px-6 pt-4">
        <button
          type="button"
          onClick={onBack}
          className="text-white/50 hover:text-white/80 transition-colors mb-4"
        >
          ← {t('editor.back')}
        </button>
        <h1 className="text-[22px] font-semibold text-white tracking-tight mb-6">{room.name}</h1>
      </div>

      <div className="px-6 flex-1 pb-10 flex flex-col gap-3">
        {room.powerPoints.map(powerPoint => (
          <PowerPointEditor
            key={powerPoint.key}
            powerPoint={powerPoint}
            onChange={updated =>
              onChange({
                ...room,
                powerPoints: room.powerPoints.map(pp => (pp.key === powerPoint.key ? updated : pp)),
              })
            }
            onEditDevice={deviceKey => onEditDevice(powerPoint.key, deviceKey)}
            onDelete={() =>
              onChange({
                ...room,
                powerPoints: room.powerPoints.filter(pp => pp.key !== powerPoint.key),
              })
            }
          />
        ))}

        <button
          type="button"
          onClick={handleAddPowerPoint}
          className="self-start px-3 py-1.5 text-xs font-medium rounded-full"
          style={{
            background: 'rgba(255,255,255,0.10)',
            border: '1px solid rgba(255,255,255,0.12)',
            color: 'rgba(255,255,255,0.75)',
          }}
        >
          {t('room.addPowerPoint')}
        </button>
      </div>
    </div>
  )
}
