import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { useFlatStructure } from '@/features/flat-structure/hooks/useFlatStructure'
import { useUpdateFlatStructure } from '@/features/flat-structure/hooks/useUpdateFlatStructure'
import { RoomEditor } from './RoomEditor'
import { DeviceEditor } from './DeviceEditor'
import {
  toDraftRooms,
  createDefaultDraftRooms,
  toUpdateRequest,
  findPlugIdConflict,
  hasBlankName,
  type DraftRoom,
} from './draftModel'

type View =
  | { type: 'list' }
  | { type: 'room'; roomKey: string }
  | { type: 'device'; roomKey: string; powerPointKey: string; deviceKey: string | null }

type Props = {
  flatId: string | undefined
}

export function FlatStructureEditor({ flatId }: Props) {
  const { t } = useTranslation('flat-structure')
  const navigate = useNavigate()
  const { data, isLoading, isError, refetch } = useFlatStructure(flatId)
  const { mutate, isPending } = useUpdateFlatStructure(flatId)

  const [draftRooms, setDraftRooms] = useState<DraftRoom[]>([])
  const [view, setView] = useState<View>({ type: 'list' })
  const [showDefaultTemplateNote, setShowDefaultTemplateNote] = useState(false)
  const [saveError, setSaveError] = useState(false)
  const [saveSuccess, setSaveSuccess] = useState(false)
  const initializedFlatIdRef = useRef<string | undefined>(undefined)

  useEffect(() => {
    if (!data || initializedFlatIdRef.current === flatId) return
    initializedFlatIdRef.current = flatId
    if (data.hasDefaultTemplate && data.rooms.length === 0) {
      setDraftRooms(createDefaultDraftRooms(t))
      setShowDefaultTemplateNote(true)
    } else {
      setDraftRooms(toDraftRooms(data.rooms))
      setShowDefaultTemplateNote(false)
    }
    setView({ type: 'list' })
    setSaveError(false)
    setSaveSuccess(false)
  }, [data, flatId, t])

  const handleRenameRoom = (roomKey: string, name: string) => {
    setSaveSuccess(false)
    setDraftRooms(prev => prev.map(room => (room.key === roomKey ? { ...room, name } : room)))
  }

  const handleAddRoom = () => {
    setSaveSuccess(false)
    setDraftRooms(prev => [
      ...prev,
      { key: crypto.randomUUID(), name: t('editor.newRoomName'), powerPoints: [] },
    ])
  }

  const handleUpdateRoom = (roomKey: string, updated: DraftRoom) => {
    setSaveSuccess(false)
    setDraftRooms(prev => prev.map(room => (room.key === roomKey ? updated : room)))
  }

  const hasPlugIdConflict = findPlugIdConflict(draftRooms)
  const hasEmptyName = hasBlankName(draftRooms)

  const handleSave = () => {
    if (hasPlugIdConflict || hasEmptyName || isPending) return
    setSaveError(false)
    setSaveSuccess(false)
    mutate(toUpdateRequest(draftRooms), {
      onSuccess: () => setSaveSuccess(true),
      onError: () => setSaveError(true),
    })
  }

  if (isLoading) {
    return (
      <div className="flex-1 flex flex-col" style={{ background: '#111827', minHeight: '100vh' }}>
        <div className="px-6 pt-4">
          <button
            type="button"
            onClick={() => navigate('/settings')}
            className="text-white/50 hover:text-white/80 transition-colors mb-6"
          >
            ← {t('editor.back')}
          </button>
        </div>
        <div className="px-6 flex flex-col gap-2">
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} className="h-16 animate-pulse rounded-2xl bg-white/10" />
          ))}
        </div>
      </div>
    )
  }

  if (isError) {
    return (
      <div className="flex-1 flex flex-col" style={{ background: '#111827', minHeight: '100vh' }}>
        <div className="px-6 pt-4">
          <button
            type="button"
            onClick={() => navigate('/settings')}
            className="text-white/50 hover:text-white/80 transition-colors mb-6"
          >
            ← {t('editor.back')}
          </button>
          <p role="alert" className="text-sm text-accent-error">
            {t('editor.loadError')}
          </p>
          <button
            type="button"
            onClick={() => refetch()}
            className="mt-2 min-h-11 min-w-11 text-sm text-white/60 underline"
          >
            {t('editor.retry')}
          </button>
        </div>
      </div>
    )
  }

  if (view.type === 'device') {
    const room = draftRooms.find(r => r.key === view.roomKey)
    const powerPoint = room?.powerPoints.find(pp => pp.key === view.powerPointKey)
    const device = view.deviceKey
      ? powerPoint?.devices.find(d => d.key === view.deviceKey)
      : undefined
    const backToRoom = () => setView({ type: 'room', roomKey: view.roomKey })

    return (
      <DeviceEditor
        device={device}
        onCancel={backToRoom}
        onSave={savedDevice => {
          if (!room || !powerPoint) return
          const updatedDevices = view.deviceKey
            ? powerPoint.devices.map(d => (d.key === view.deviceKey ? savedDevice : d))
            : [...powerPoint.devices, savedDevice]
          handleUpdateRoom(room.key, {
            ...room,
            powerPoints: room.powerPoints.map(pp =>
              pp.key === powerPoint.key ? { ...pp, devices: updatedDevices } : pp
            ),
          })
          backToRoom()
        }}
      />
    )
  }

  if (view.type === 'room') {
    const room = draftRooms.find(r => r.key === view.roomKey)
    if (!room) return null
    return (
      <RoomEditor
        room={room}
        onChange={updated => handleUpdateRoom(room.key, updated)}
        onBack={() => setView({ type: 'list' })}
        onEditDevice={(powerPointKey, deviceKey) =>
          setView({ type: 'device', roomKey: room.key, powerPointKey, deviceKey })
        }
      />
    )
  }

  const plugCount = draftRooms
    .flatMap(room => room.powerPoints)
    .filter(pp => pp.plugId.trim() !== '').length

  return (
    <div className="flex-1 flex flex-col" style={{ background: '#111827', minHeight: '100vh' }}>
      <div className="px-6 pt-4">
        <div className="flex items-center justify-between mb-6">
          <button
            type="button"
            onClick={() => navigate('/settings')}
            className="text-white/50 hover:text-white/80 transition-colors"
          >
            ← {t('editor.back')}
          </button>
          <button
            type="button"
            onClick={handleSave}
            disabled={hasPlugIdConflict || hasEmptyName || isPending}
            className="px-3 py-1.5 text-xs font-semibold rounded-full disabled:opacity-40"
            style={{ background: 'rgba(255,255,255,0.12)', border: '1px solid rgba(255,255,255,0.40)', color: 'white' }}
          >
            {isPending ? t('editor.saving') : t('editor.save')}
          </button>
        </div>

        <h1 className="text-[22px] font-semibold text-white tracking-tight mb-1.5">{t('editor.title')}</h1>
        <p className="text-sm text-white/50 mb-4">
          {t('editor.subtitle', { roomCount: draftRooms.length, plugCount })}
        </p>

        {saveError && (
          <p role="alert" className="text-xs text-accent-error mb-2">
            {t('editor.saveError')}
          </p>
        )}
        {saveSuccess && !saveError && (
          <p className="text-xs mb-2" style={{ color: '#60a5fa' }}>
            {t('editor.saveSuccess')}
          </p>
        )}
        {hasPlugIdConflict && (
          <p role="alert" className="text-xs text-accent-error mb-2">
            {t('editor.plugIdConflict')}
          </p>
        )}
        {hasEmptyName && (
          <p role="alert" className="text-xs text-accent-error mb-2">
            {t('editor.blankNameError')}
          </p>
        )}
      </div>

      <div className="px-6 flex-1 pb-10">
        <ul className="flex flex-col gap-2">
          {draftRooms.map(room => (
            <li
              key={room.key}
              className="rounded-2xl p-4 flex items-center gap-2"
              style={{ background: 'rgba(255,255,255,0.07)', border: '1px solid rgba(255,255,255,0.12)' }}
            >
              <input
                type="text"
                value={room.name}
                onChange={e => handleRenameRoom(room.key, e.target.value)}
                placeholder={t('room.namePlaceholder')}
                aria-label={t('room.namePlaceholder')}
                className="flex-1 h-10 px-3 rounded-[10px] bg-white/[0.08] border text-white text-sm outline-none focus:border-white/60"
                style={{ borderColor: 'rgba(255,255,255,0.15)' }}
              />
              <button
                type="button"
                onClick={() => setView({ type: 'room', roomKey: room.key })}
                className="flex items-center gap-1 text-xs text-white/50 shrink-0"
              >
                {t('room.powerPointsSummary', { count: room.powerPoints.length })}
                <span aria-hidden="true">›</span>
              </button>
            </li>
          ))}
        </ul>

        <button
          type="button"
          onClick={handleAddRoom}
          className="mt-3 px-3 py-1.5 text-xs font-medium rounded-full"
          style={{
            background: 'rgba(255,255,255,0.10)',
            border: '1px solid rgba(255,255,255,0.12)',
            color: 'rgba(255,255,255,0.75)',
          }}
        >
          {t('editor.addRoom')}
        </button>

        {showDefaultTemplateNote && (
          <p className="mt-4 text-xs text-white/40">{t('editor.defaultTemplateNote')}</p>
        )}
      </div>
    </div>
  )
}
