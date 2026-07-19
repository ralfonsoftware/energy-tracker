import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { usePatchFlat } from '../hooks/usePatchFlat'
import type { UserSettings } from '../api/settingsApi'

interface FlatSettingsCardProps {
  settings: UserSettings
}

const rowClass = 'flex items-center justify-between px-4 py-[13px] min-h-[48px] border-b last:border-b-0'
const rowBorderStyle = { borderColor: 'rgba(255,255,255,0.06)' }

export function FlatSettingsCard({ settings }: FlatSettingsCardProps) {
  const { t } = useTranslation('settings')
  const navigate = useNavigate()
  const { mutate: patchFlat, isPending } = usePatchFlat()

  const [isEditing, setIsEditing] = useState(false)
  const [editName, setEditName] = useState(settings.flatName ?? '')
  const [editError, setEditError] = useState<string | null>(null)

  const handleStartEdit = () => {
    setEditName(settings.flatName ?? '')
    setEditError(null)
    setIsEditing(true)
  }

  const handleCancel = () => {
    setEditName(settings.flatName ?? '')
    setEditError(null)
    setIsEditing(false)
  }

  const handleSaveName = () => {
    if (!editName.trim() || !settings.flatId) return
    if (!settings.flatRowVersion) {
      setEditError(t('flat.saveError'))
      return
    }
    setEditError(null)
    patchFlat(
      { flatId: settings.flatId, body: { name: editName.trim(), rowVersion: settings.flatRowVersion } },
      {
        onSuccess: () => setIsEditing(false),
        onError: () => {
          setEditError(t('flat.saveError'))
          setIsEditing(true)
        },
      }
    )
  }

  const cardStyle = {
    background: 'rgba(255,255,255,0.07)',
    border: '1px solid rgba(255,255,255,0.12)',
    borderRadius: '16px',
    backdropFilter: 'blur(20px)',
  }

  return (
    <div style={cardStyle}>
      {/* Flat name row */}
      <div className={rowClass} style={rowBorderStyle}>
        {isEditing ? (
          <div className="flex-1 flex items-center gap-2">
            <input
              type="text"
              value={editName}
              onChange={e => setEditName(e.target.value)}
              placeholder={t('flat.namePlaceholder')}
              className="flex-1 bg-white/10 text-white text-[15px] rounded-lg px-3 py-1.5 outline-none border border-white/20 focus:border-white/50"
              aria-label={t('flat.namePlaceholder')}
              onKeyDown={e => {
                if (e.key === 'Enter') handleSaveName()
                if (e.key === 'Escape') handleCancel()
              }}
              autoFocus
            />
            <button
              onClick={handleSaveName}
              disabled={isPending || !editName.trim()}
              className="text-sm font-medium px-3 py-1.5 rounded-lg bg-white/15 text-white disabled:opacity-40"
            >
              {isPending ? '…' : t('flat.save')}
            </button>
            <button
              onClick={handleCancel}
              className="text-sm px-3 py-1.5 rounded-lg text-white/50"
            >
              ✕
            </button>
          </div>
        ) : (
          <button
            className="flex-1 flex items-center justify-between text-left"
            onClick={handleStartEdit}
          >
            <span className="text-white text-[15px]">{settings.flatName}</span>
            <span style={{ color: 'rgba(255,255,255,0.3)' }}>›</span>
          </button>
        )}
      </div>

      {editError && (
        <div className="px-4 py-2">
          <p className="text-xs text-red-400">{editError}</p>
        </div>
      )}

      {/* Pills row */}
      <div className={`${rowClass} gap-2`} style={{ ...rowBorderStyle, borderBottom: 'none' }}>
        <button
          onClick={() => navigate('/settings/flat')}
          className="px-3 py-1.5 text-xs font-medium rounded-full"
          style={{
            background: 'rgba(255,255,255,0.10)',
            border: '1px solid rgba(255,255,255,0.12)',
            color: 'rgba(255,255,255,0.75)',
          }}
        >
          {t('flat.kwhBaselineLink')}
        </button>
        <button
          onClick={() => navigate('/settings/tariffs')}
          className="px-3 py-1.5 text-xs font-medium rounded-full"
          style={{
            background: 'rgba(255,255,255,0.10)',
            border: '1px solid rgba(255,255,255,0.12)',
            color: 'rgba(255,255,255,0.75)',
          }}
        >
          {t('flat.tariffLink')}
        </button>
        <button
          type="button"
          onClick={() => navigate('/settings/structure')}
          className="px-3 py-1.5 text-xs font-medium rounded-full"
          style={{
            background: 'rgba(255,255,255,0.10)',
            border: '1px solid rgba(255,255,255,0.12)',
            color: 'rgba(255,255,255,0.75)',
          }}
        >
          {t('flat.structureLink')}
        </button>
      </div>
    </div>
  )
}
