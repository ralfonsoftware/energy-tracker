import { Lock } from 'lucide-react'
import { useTranslation } from 'react-i18next'

type Props = {
  contractStartDate: string
  contractDurationMonths: number
}

export function TariffLockIndicator({ contractStartDate, contractDurationMonths }: Props) {
  const { t, i18n } = useTranslation('tariffs')

  const start = new Date(contractStartDate)
  const lockedUntil = new Date(start.getFullYear(), start.getMonth(), start.getDate())
  lockedUntil.setMonth(lockedUntil.getMonth() + contractDurationMonths)

  const formattedDate = new Intl.DateTimeFormat(i18n.language, { month: 'long', year: 'numeric' }).format(
    lockedUntil
  )

  return (
    <div className="flex items-center gap-2 text-xs" style={{ color: '#d97706' }}>
      <Lock className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
      <span>{t('form.lockedLabel', { date: formattedDate })}</span>
    </div>
  )
}
