import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import { useUserSettings } from '../hooks/useUserSettings'
import { FlatSettingsCard } from './FlatSettingsCard'
import { LocaleSettings } from './LocaleSettings'
import { AccountSettings } from './AccountSettings'

const sectionLabelClass =
  'text-[11px] font-semibold tracking-[0.08em] uppercase mb-2 px-1'

const cardStyle = {
  background: 'rgba(255,255,255,0.07)',
  border: '1px solid rgba(255,255,255,0.12)',
  borderRadius: '16px',
  backdropFilter: 'blur(20px)',
}

export default function SettingsRoot() {
  const { t } = useTranslation('settings')
  const { settings, isLoading } = useUserSettings()

  const kwhFormatted = settings?.annualKwhBaseline != null
    ? new Intl.NumberFormat(i18n.language).format(settings.annualKwhBaseline) + ' kWh'
    : null

  if (isLoading) {
    return (
      <div className="flex-1 flex items-center justify-center" style={{ background: '#111827' }}>
        <div className="w-6 h-6 border-2 border-white/20 border-t-white/70 rounded-full animate-spin" />
      </div>
    )
  }

  return (
    <div className="flex-1 flex flex-col px-5 pt-8 pb-10" style={{ background: '#111827' }}>
      <h1 className="text-[28px] font-bold text-white tracking-tight mb-8">{t('title')}</h1>

      {/* My Flats section */}
      {settings?.hasFlat && settings.flatName && (
        <div className="mb-6">
          <p className={sectionLabelClass} style={{ color: 'rgba(255,255,255,0.35)' }}>
            {t('flat.sectionTitle')}
          </p>
          <FlatSettingsCard settings={settings} />
          {kwhFormatted && (
            <p className="mt-2 px-1 text-xs" style={{ color: 'rgba(255,255,255,0.35)' }}>
              {kwhFormatted} / yr
            </p>
          )}
        </div>
      )}

      {/* App Settings section */}
      <div className="mb-6">
        <p className={sectionLabelClass} style={{ color: 'rgba(255,255,255,0.35)' }}>
          {t('appSettings.title')}
        </p>
        <div style={cardStyle}>
          <LocaleSettings />
        </div>
      </div>

      {/* Account section */}
      <div>
        <p className={sectionLabelClass} style={{ color: 'rgba(255,255,255,0.35)' }}>
          {t('account.title')}
        </p>
        <div style={cardStyle}>
          <AccountSettings />
        </div>
      </div>
    </div>
  )
}
