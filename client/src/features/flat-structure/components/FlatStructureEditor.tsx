import { useEffect, useRef, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Check, Trash2 } from 'lucide-react'
import { useFlatStructure } from '@/features/flat-structure/hooks/useFlatStructure'
import { useSaveRoom } from '@/features/flat-structure/hooks/useSaveRoom'
import { useDeleteRoom } from '@/features/flat-structure/hooks/useDeleteRoom'
import { RoomEditor } from './RoomEditor'
import { DeviceEditor } from './DeviceEditor'
import {
  toDraftRooms,
  createDefaultDraftRooms,
  toRoomWritePayload,
  toKeyedRooms,
  hasBlankNameInRoom,
  isRoomDirty,
  hasPlugIdConflictForRoomSave,
  withRoomAppended,
  withRoomUpdated,
  withRoomRemoved,
  type DraftRoom,
  type KeyedRoomInput,
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
  const [searchParams] = useSearchParams()
  const powerPointId = searchParams.get('powerPointId')
  const { data, isLoading, isError, refetch } = useFlatStructure(flatId)
  const saveRoom = useSaveRoom(flatId)
  const deleteRoomMutation = useDeleteRoom(flatId)

  const [draftRooms, setDraftRooms] = useState<DraftRoom[]>([])
  const [lastSaved, setLastSaved] = useState<KeyedRoomInput[]>([])
  const [view, setView] = useState<View>({ type: 'list' })
  const [showDefaultTemplateNote, setShowDefaultTemplateNote] = useState(false)
  const [saveError, setSaveError] = useState(false)
  const [saveSuccess, setSaveSuccess] = useState(false)
  const [confirmDeleteRoomKey, setConfirmDeleteRoomKey] = useState<string | null>(null)
  const [savingRoomKeys, setSavingRoomKeys] = useState<Set<string>>(new Set())
  const initializedFlatIdRef = useRef<string | undefined>(undefined)

  useEffect(() => {
    if (!data || initializedFlatIdRef.current === flatId) return
    initializedFlatIdRef.current = flatId
    let seeded: DraftRoom[]
    if (data.hasDefaultTemplate && data.rooms.length === 0) {
      seeded = createDefaultDraftRooms(t)
      setDraftRooms(seeded)
      setLastSaved([])
      setShowDefaultTemplateNote(true)
    } else {
      seeded = toDraftRooms(data.rooms)
      setDraftRooms(seeded)
      setLastSaved(toKeyedRooms(seeded))
      setShowDefaultTemplateNote(false)
    }
    const matchedRoom = powerPointId
      ? seeded.find(room => room.powerPoints.some(pp => pp.powerPointId === powerPointId))
      : undefined
    setView(matchedRoom ? { type: 'room', roomKey: matchedRoom.key } : { type: 'list' })
    setSaveError(false)
    setSaveSuccess(false)
  }, [data, flatId, t, powerPointId])

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

  const refreshRoomRowVersionAfterConflict = (roomKey: string, roomId: string | undefined) => {
    if (!roomId) return
    refetch().then(result => {
      const refreshedRoom = result.data?.rooms.find(r => r.roomId === roomId)
      if (!refreshedRoom) return
      setDraftRooms(prev =>
        prev.map(r => (r.key === roomKey ? { ...r, rowVersion: refreshedRoom.rowVersion } : r))
      )
    })
  }

  const handleSaveRoom = (room: DraftRoom) => {
    const trimmedName = room.name.trim()
    const index = draftRooms.findIndex(r => r.key === room.key)
    const payload = { ...toRoomWritePayload(room, trimmedName), sortOrder: index }
    setSaveError(false)
    setSaveSuccess(false)
    setSavingRoomKeys(prev => new Set(prev).add(room.key))
    saveRoom.mutate(
      { roomId: room.roomId, rowVersion: room.rowVersion, ...payload },
      {
        onSuccess: response => {
          const savedInput = {
            name: response.name,
            sortOrder: response.sortOrder,
            powerPoints: response.powerPoints.map(pp => ({
              powerPointId: pp.powerPointId,
              name: pp.name,
              plugId: pp.plugId ?? undefined,
            })),
          }
          setLastSaved(prev =>
            room.originalName === undefined
              ? withRoomAppended(prev, room.key, savedInput)
              : withRoomUpdated(prev, room.key, savedInput)
          )
          setDraftRooms(prev =>
            prev.map(r =>
              r.key === room.key
                ? { ...r, originalName: trimmedName, roomId: response.roomId, rowVersion: response.rowVersion }
                : r
            )
          )
          setSaveSuccess(true)
          setSavingRoomKeys(prev => {
            const next = new Set(prev)
            next.delete(room.key)
            return next
          })
        },
        onError: (error: unknown) => {
          const isDeletedElsewhere = (error as { status?: number } | null)?.status === 404
          if (isDeletedElsewhere) {
            setDraftRooms(prev => prev.filter(r => r.key !== room.key))
            setLastSaved(prev => withRoomRemoved(prev, room.key))
          } else {
            if (room.originalName !== undefined) {
              setDraftRooms(prev =>
                prev.map(r => (r.key === room.key ? { ...r, name: room.originalName as string } : r))
              )
            }
            refreshRoomRowVersionAfterConflict(room.key, room.roomId)
          }
          setSaveError(true)
          setSavingRoomKeys(prev => {
            const next = new Set(prev)
            next.delete(room.key)
            return next
          })
        },
      }
    )
  }

  const handleDeleteRoom = (roomKey: string) => {
    const index = draftRooms.findIndex(r => r.key === roomKey)
    const room = draftRooms[index]
    setSaveSuccess(false)
    setSaveError(false)
    setDraftRooms(prev => prev.filter(r => r.key !== roomKey))
    setConfirmDeleteRoomKey(null)

    if (draftRooms.length - 1 === 0) return
    if (room.originalName === undefined) return

    deleteRoomMutation.mutate(
      { roomId: room.roomId!, rowVersion: room.rowVersion! },
      {
        onSuccess: () => {
          setLastSaved(prev => withRoomRemoved(prev, roomKey))
          setSaveSuccess(true)
        },
        onError: (error: unknown) => {
          const isAlreadyDeleted = (error as { status?: number } | null)?.status === 404
          if (isAlreadyDeleted) {
            setLastSaved(prev => withRoomRemoved(prev, roomKey))
          } else {
            setDraftRooms(prev => [...prev.slice(0, index), room, ...prev.slice(index)])
            refreshRoomRowVersionAfterConflict(roomKey, room.roomId)
          }
          setSaveError(true)
        },
      }
    )
  }

  const isBusy = savingRoomKeys.size > 0 || deleteRoomMutation.isPending

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

    if (!flatId || !room || !powerPoint || !powerPoint.powerPointId) {
      backToRoom()
      return null
    }

    return (
      <DeviceEditor
        device={device}
        flatId={flatId}
        powerPointId={powerPoint.powerPointId}
        onCancel={backToRoom}
        onSaved={savedDevice => {
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
    if (!room || !flatId) return null
    return (
      <RoomEditor
        flatId={flatId}
        room={room}
        onChange={updated => handleUpdateRoom(room.key, updated)}
        onBack={() => setView({ type: 'list' })}
        onEditDevice={(powerPointKey, deviceKey) =>
          setView({ type: 'device', roomKey: room.key, powerPointKey, deviceKey })
        }
        isDirty={isRoomDirty(room, lastSaved)}
        isPending={savingRoomKeys.has(room.key)}
        isSaveBlocked={hasBlankNameInRoom(room) || hasPlugIdConflictForRoomSave(room, lastSaved)}
        saveError={saveError}
        saveSuccess={saveSuccess}
        onSave={() => handleSaveRoom(room)}
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
      </div>

      <div className="px-6 flex-1 pb-10">
        <ul className="flex flex-col gap-2">
          {draftRooms.map(room => {
            const isDirty = isRoomDirty(room, lastSaved)
            const isSaveBlocked = hasBlankNameInRoom(room) || hasPlugIdConflictForRoomSave(room, lastSaved)
            const blockedByBlankName = hasBlankNameInRoom(room)
            const isSaving = savingRoomKeys.has(room.key)
            const saveLabel = `${isSaving ? t('editor.saving') : t('editor.save')}: ${room.name.trim()}`
            return (
            <li
              key={room.key}
              className="rounded-2xl p-4 flex flex-col gap-2"
              style={{ background: 'rgba(255,255,255,0.07)', border: '1px solid rgba(255,255,255,0.12)' }}
            >
              <div className="flex flex-col gap-2">
                <div className="flex items-center gap-2">
                  <input
                    type="text"
                    value={room.name}
                    onChange={e => handleRenameRoom(room.key, e.target.value)}
                    placeholder={t('room.namePlaceholder')}
                    aria-label={t('room.namePlaceholder')}
                    disabled={confirmDeleteRoomKey === room.key}
                    className="flex-1 h-10 px-3 rounded-[10px] bg-white/[0.08] border text-white text-sm outline-none focus:border-white/60 disabled:opacity-60"
                    style={{ borderColor: 'rgba(255,255,255,0.15)' }}
                  />
                  {confirmDeleteRoomKey === room.key ? (
                    <div className="flex items-center gap-2 shrink-0">
                      <button
                        type="button"
                        onClick={() => setConfirmDeleteRoomKey(null)}
                        disabled={isBusy}
                        className="px-3 py-1.5 text-xs font-medium rounded-full text-white/70 disabled:opacity-40"
                      >
                        {t('confirm.cancel')}
                      </button>
                      <button
                        type="button"
                        onClick={() => handleDeleteRoom(room.key)}
                        disabled={isBusy}
                        className="px-3 py-1.5 text-xs font-semibold rounded-full text-accent-error disabled:opacity-40"
                      >
                        {t('confirm.delete')}
                      </button>
                    </div>
                  ) : (
                    <div className="flex items-center gap-2 shrink-0">
                      <button
                        type="button"
                        onClick={() => handleSaveRoom(room)}
                        disabled={!isDirty || isSaving || isSaveBlocked}
                        aria-label={saveLabel}
                        title={saveLabel}
                        className="min-h-11 min-w-11 flex items-center justify-center rounded-full disabled:opacity-40 shrink-0"
                        style={{ background: 'rgba(255,255,255,0.12)', border: '1px solid rgba(255,255,255,0.40)', color: 'white' }}
                      >
                        {isSaving ? (
                          <div
                            className="w-4 h-4 rounded-full border-2 border-white/20 border-t-white/70 animate-spin"
                            aria-hidden="true"
                          />
                        ) : (
                          <Check className="h-4 w-4" aria-hidden="true" />
                        )}
                      </button>
                      <button
                        type="button"
                        onClick={() => setConfirmDeleteRoomKey(room.key)}
                        disabled={isBusy}
                        aria-label={t('room.delete')}
                        title={t('room.delete')}
                        className="min-h-11 min-w-11 flex items-center justify-center rounded-full shrink-0 text-white/50 hover:text-accent-error transition-colors"
                      >
                        <Trash2 className="h-4 w-4" aria-hidden="true" />
                      </button>
                    </div>
                  )}
                </div>
                {confirmDeleteRoomKey !== room.key && (
                  <button
                    type="button"
                    onClick={() => {
                      setSaveError(false)
                      setSaveSuccess(false)
                      setView({ type: 'room', roomKey: room.key })
                    }}
                    className="flex items-center gap-1 text-xs text-white/50 shrink-0"
                  >
                    {t('room.powerPointsSummary', { count: room.powerPoints.length })}
                    <span aria-hidden="true">›</span>
                  </button>
                )}
                {isSaveBlocked && confirmDeleteRoomKey !== room.key && (
                  <p role="alert" className="text-xs text-accent-error">
                    {blockedByBlankName ? t('editor.blankNameError') : t('editor.plugIdConflict')}
                  </p>
                )}
              </div>
              {confirmDeleteRoomKey === room.key && (
                <span className="text-xs text-white/60">{t('room.deletePrompt')}</span>
              )}
            </li>
            )
          })}
        </ul>

        <button
          type="button"
          onClick={handleAddRoom}
          disabled={isBusy}
          className="mt-3 px-3 py-1.5 text-xs font-medium rounded-full disabled:opacity-40"
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
