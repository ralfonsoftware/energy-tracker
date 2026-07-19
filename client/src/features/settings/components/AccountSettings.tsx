import { useState, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { useAuthMe } from '../hooks/useAuthMe'
import { useUserSettings } from '../hooks/useUserSettings'
import { FlatDeleteConfirm } from './FlatDeleteConfirm'

export function AccountSettings() {
  const { t } = useTranslation('settings')
  const { data: authMe } = useAuthMe()
  const { settings, refetch } = useUserSettings()
  const [showConfirm, setShowConfirm] = useState(false)
  const [showDeleteFlatConfirm, setShowDeleteFlatConfirm] = useState(false)

  const email = authMe?.clientPrincipal?.userDetails ?? ''
  const hasFlat = Boolean(settings?.flatId && settings.flatName)

  useEffect(() => {
    if (showDeleteFlatConfirm && !hasFlat) {
      setShowDeleteFlatConfirm(false)
    }
  }, [showDeleteFlatConfirm, hasFlat])

  const handleSignOut = () => {
    window.location.href = '/.auth/logout'
  }

  const rowClass = 'flex items-center justify-between px-4 py-[13px] min-h-[48px] border-b last:border-b-0'
  const rowBorderStyle = { borderColor: 'rgba(255,255,255,0.06)' }

  if (showConfirm) {
    return (
      <div className="px-4 py-4">
        <p className="text-white text-[15px] font-medium mb-1">{t('account.signOutConfirm.title')}</p>
        <p className="text-white/55 text-sm mb-4">{t('account.signOutConfirm.body')}</p>
        <div className="flex gap-2">
          <button
            onClick={() => setShowConfirm(false)}
            className="flex-1 h-10 rounded-full text-sm font-medium text-white/70"
            style={{ background: 'rgba(255,255,255,0.08)', border: '1px solid rgba(255,255,255,0.15)' }}
          >
            {t('account.signOutConfirm.cancel')}
          </button>
          <button
            onClick={handleSignOut}
            className="flex-1 h-10 rounded-full text-sm font-medium"
            style={{ background: 'rgba(239,68,68,0.15)', border: '1px solid rgba(239,68,68,0.4)', color: '#ef4444' }}
          >
            {t('account.signOutConfirm.confirm')}
          </button>
        </div>
      </div>
    )
  }

  if (showDeleteFlatConfirm && settings?.flatId && settings.flatName && settings.flatRowVersion) {
    return (
      <FlatDeleteConfirm
        flatId={settings.flatId}
        flatName={settings.flatName}
        flatRowVersion={settings.flatRowVersion}
        onCancel={() => setShowDeleteFlatConfirm(false)}
        onDeleteConflict={() => refetch()}
      />
    )
  }

  return (
    <>
      {email && (
        <div className={rowClass} style={rowBorderStyle}>
          <span className="text-white/55 text-[14px]">{email}</span>
        </div>
      )}
      <div className={rowClass} style={hasFlat ? rowBorderStyle : { ...rowBorderStyle, borderBottom: 'none' }}>
        <button
          className="text-[15px] font-medium"
          style={{ color: '#ef4444' }}
          onClick={() => setShowConfirm(true)}
        >
          {t('account.signOut')}
        </button>
      </div>
      {hasFlat && (
        <div className={rowClass} style={{ ...rowBorderStyle, borderBottom: 'none' }}>
          <button
            className="text-[15px] font-medium"
            style={{ color: '#ef4444' }}
            onClick={() => setShowDeleteFlatConfirm(true)}
          >
            {t('account.deleteFlat.button')}
          </button>
        </div>
      )}
    </>
  )
}
