import { useTranslation } from 'react-i18next'
import { X } from 'lucide-react'
import type { DetectedFileType } from '@/features/smart-plug-import/api/importApi'

export type DeviceOption = { plugId: string; label: string; roomName: string }

type Props = {
  fileName: string
  detectedType: DetectedFileType
  deviceOptions: DeviceOption[]
  selectedPlugId: string | null
  isAutoMatched: boolean
  onSelectPlugId: (plugId: string) => void
  onRemove: () => void
}

export function FileListItem({
  fileName, detectedType, deviceOptions, selectedPlugId, isAutoMatched, onSelectPlugId, onRemove,
}: Props) {
  const { t } = useTranslation('import')
  const badgeStyle =
    detectedType === 'EveHome'
      ? { background: 'rgba(59,130,246,0.18)', border: '1px solid rgba(59,130,246,0.35)', color: '#93c5fd' }
      : { background: 'rgba(20,184,166,0.18)', border: '1px solid rgba(20,184,166,0.35)', color: '#5eead4' }

  return (
    <div
      className="rounded-2xl p-4 mb-2.5"
      style={{ background: 'rgba(255,255,255,0.07)', border: '1px solid rgba(255,255,255,0.12)' }}
    >
      <div className="flex items-start gap-2.5 mb-2.5">
        <div className="flex-1 min-w-0">
          <div className="text-[13px] font-medium text-white break-words">{fileName}</div>
          <span
            className="inline-flex items-center rounded-full text-[11px] font-semibold px-2.5 py-0.5 mt-1"
            style={badgeStyle}
          >
            {t(detectedType === 'EveHome' ? 'fileType.eveHome' : 'fileType.meross')}
          </span>
        </div>
        <button type="button" onClick={onRemove} aria-label={t('remove')} className="shrink-0 text-white/50 hover:text-accent-error transition-colors">
          <X className="h-4 w-4" aria-hidden="true" />
        </button>
      </div>
      <div className="flex items-center gap-2 rounded-[10px] px-2.5 py-2" style={{ background: 'rgba(255,255,255,0.04)', border: '1px solid rgba(255,255,255,0.08)' }}>
        <span className="text-xs text-white/40 shrink-0">{t('association.label')}</span>
        <select
          value={selectedPlugId ?? ''}
          onChange={e => onSelectPlugId(e.target.value)}
          aria-label={t('association.label')}
          className="flex-1 bg-transparent text-white text-sm outline-none"
        >
          <option value="" disabled>{t('association.placeholder')}</option>
          {deviceOptions.map(opt => (
            <option key={opt.plugId} value={opt.plugId} style={{ color: '#111827' }}>
              {opt.label} — {opt.roomName}
            </option>
          ))}
        </select>
        {isAutoMatched && selectedPlugId && (
          <span className="text-[11px] font-semibold text-[#4ade80] shrink-0">{t('association.auto')}</span>
        )}
      </div>
    </div>
  )
}
