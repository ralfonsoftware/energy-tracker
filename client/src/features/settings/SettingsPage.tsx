import { useTranslation } from 'react-i18next'

export default function SettingsPage() {
  const { t } = useTranslation('settings')
  return <div>{t('title')}</div>
}
