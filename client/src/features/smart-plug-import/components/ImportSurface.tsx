import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { detectFileType, type DetectedFileType } from '@/features/smart-plug-import/api/importApi'
import { useUploadImport } from '@/features/smart-plug-import/hooks/useUploadImport'
import { FileUploadZone } from './FileUploadZone'
import { FileListItem, type DeviceOption } from './FileListItem'

type ImportRoom = { name: string; powerPoints: { plugId: string | null; name: string; devices: { name: string }[] }[] }

type PendingFile = {
  key: string
  file: File
  detectedType: DetectedFileType
  plugId: string | null
  isAutoMatched: boolean
}

function buildDeviceOptions(rooms: ImportRoom[]): DeviceOption[] {
  return rooms.flatMap(room =>
    room.powerPoints
      .filter(pp => pp.plugId)
      .map(pp => ({ plugId: pp.plugId as string, label: pp.name, roomName: room.name }))
  )
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}

function matchesFileName(lower: string, candidate: string): boolean {
  const boundary = new RegExp(`(?:^|[^a-z0-9])${escapeRegExp(candidate.toLowerCase())}(?:[^a-z0-9]|$)`)
  return boundary.test(lower)
}

function autoMatchPlugId(fileName: string, rooms: ImportRoom[]): string | null {
  const lower = fileName.toLowerCase()
  for (const room of rooms) {
    for (const pp of room.powerPoints) {
      if (!pp.plugId) continue
      const candidates = [pp.name, ...pp.devices.map(d => d.name)]
      if (candidates.some(candidate => matchesFileName(lower, candidate))) return pp.plugId
    }
  }
  return null
}

type Props = { flatId: string | undefined; rooms: ImportRoom[] }

export function ImportSurface({ flatId, rooms }: Props) {
  const { t } = useTranslation(['import', 'common'])
  const navigate = useNavigate()
  const [pendingFiles, setPendingFiles] = useState<PendingFile[]>([])
  const [submitError, setSubmitError] = useState<string | null>(null)
  const { mutateAsync, isPending } = useUploadImport(flatId)
  const deviceOptions = buildDeviceOptions(rooms)

  const handleFilesSelected = (files: File[]) => {
    const additions: PendingFile[] = files.flatMap(file => {
      const detectedType = detectFileType(file.name)
      if (!detectedType) return []
      const plugId = autoMatchPlugId(file.name, rooms)
      return [{ key: crypto.randomUUID(), file, detectedType, plugId, isAutoMatched: plugId !== null }]
    })
    setPendingFiles(prev => [...prev, ...additions])
  }

  const allAssociated = pendingFiles.length > 0 && pendingFiles.every(f => f.plugId)

  const handleUpload = async () => {
    setSubmitError(null)
    const results = await Promise.allSettled(
      pendingFiles.map(pf => mutateAsync({ file: pf.file, plugId: pf.plugId as string }).then(() => pf.key))
    )
    const succeededKeys = new Set(
      results.filter((r): r is PromiseFulfilledResult<string> => r.status === 'fulfilled').map(r => r.value)
    )
    let hasRemaining = false
    setPendingFiles(prev => {
      const remaining = prev.filter(pf => !succeededKeys.has(pf.key))
      hasRemaining = remaining.length > 0
      return remaining
    })
    if (hasRemaining) {
      setSubmitError(t('surface.partialFailure'))
    } else {
      navigate('/decomposition')
    }
  }

  return (
    <div className="flex-1 flex flex-col px-4 pt-4" style={{ background: '#111827', minHeight: '100vh' }}>
      <div className="flex items-center justify-center relative mb-5">
        <button
          type="button"
          disabled={isPending}
          onClick={() => navigate('/decomposition')}
          className="absolute left-0 text-sm text-white/55 disabled:opacity-40"
        >
          ‹ {t('common:nav.decomposition')}
        </button>
        <span className="text-[15px] font-semibold text-white">{t('surface.title')}</span>
      </div>

      {deviceOptions.length === 0 && (
        <p role="alert" className="text-xs text-white/40 text-center mb-4 px-2">
          {t('surface.noDevicesAvailable')}
        </p>
      )}

      <FileUploadZone onFilesSelected={handleFilesSelected} />

      {pendingFiles.length > 0 && (
        <>
          <div className="text-[11px] font-semibold tracking-[0.10em] uppercase text-white/35 mb-2.5 px-1">
            {t('surface.selectedFiles', { count: pendingFiles.length })}
          </div>
          {pendingFiles.map(pf => (
            <FileListItem
              key={pf.key}
              fileName={pf.file.name}
              detectedType={pf.detectedType}
              deviceOptions={deviceOptions}
              selectedPlugId={pf.plugId}
              isAutoMatched={pf.isAutoMatched}
              onSelectPlugId={plugId =>
                setPendingFiles(prev => prev.map(f => (f.key === pf.key ? { ...f, plugId, isAutoMatched: false } : f)))
              }
              onRemove={() => { if (!isPending) setPendingFiles(prev => prev.filter(f => f.key !== pf.key)) }}
            />
          ))}
          {submitError && (
            <p role="alert" className="text-xs text-accent-error mb-2">{submitError}</p>
          )}
          <button
            type="button"
            disabled={!allAssociated || isPending || !flatId}
            onClick={handleUpload}
            className="w-full py-3.5 rounded-2xl text-[15px] font-semibold text-white disabled:opacity-40 mt-1"
            style={{ background: 'rgba(255,255,255,0.12)', border: '1px solid rgba(255,255,255,0.45)' }}
          >
            {isPending ? '…' : t('surface.uploadButton', { count: pendingFiles.length })}
          </button>
          <p className="text-xs text-white/30 text-center mt-2.5 mb-6">{t('surface.uploadNote')}</p>
        </>
      )}
    </div>
  )
}
