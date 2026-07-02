import { useTranslation } from 'react-i18next'

type Props = { flatId: string | undefined }

export function ReadingHistorySheet({ flatId: _flatId }: Props) {
  const { t } = useTranslation('readings')

  return (
    <div>
      <div aria-hidden="true" className="mx-auto mb-4 h-1 w-9 rounded-full bg-white/25" />
      <h2 className="text-body text-text-primary">{t('history.title')}</h2>
      <p className="mt-2 text-body-sm text-text-tertiary">{t('history.comingSoon')}</p>
    </div>
  )
}
