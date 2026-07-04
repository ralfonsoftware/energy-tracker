import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useDeleteFlat } from '../hooks/useDeleteFlat'

type Props = {
  flatId: string
  flatName: string
  onCancel: () => void
}

export function FlatDeleteConfirm({ flatId, flatName, onCancel }: Props) {
  const { t } = useTranslation('settings')
  const { mutate: deleteFlat, isPending } = useDeleteFlat()
  const [typedValue, setTypedValue] = useState('')
  const [error, setError] = useState<string | null>(null)

  const isMatch = typedValue === flatName

  const handleDelete = () => {
    if (!isMatch) return
    setError(null)
    deleteFlat(flatId, {
      onSuccess: onCancel,
      onError: () => setError(t('account.deleteFlat.error')),
    })
  }

  return (
    <div className="px-4 py-4">
      <p id="deleteFlatConfirmPrompt" className="text-white/55 text-sm mb-3">{t('account.deleteFlat.prompt', { flatName })}</p>
      <input
        type="text"
        aria-labelledby="deleteFlatConfirmPrompt"
        value={typedValue}
        onChange={e => setTypedValue(e.target.value)}
        className="w-full h-10 px-3 rounded-lg text-white text-sm mb-4 outline-none"
        style={{ background: 'rgba(255,255,255,0.08)', border: '1px solid rgba(255,255,255,0.15)' }}
      />
      {error && (
        <p className="text-sm text-red-400 mb-3">{error}</p>
      )}
      <div className="flex gap-2">
        <button
          onClick={onCancel}
          className="flex-1 h-10 rounded-full text-sm font-medium text-white/70"
          style={{ background: 'rgba(255,255,255,0.08)', border: '1px solid rgba(255,255,255,0.15)' }}
        >
          {t('account.deleteFlat.cancel')}
        </button>
        <button
          onClick={handleDelete}
          disabled={!isMatch || isPending}
          className="flex-1 h-10 rounded-full text-sm font-medium disabled:opacity-40"
          style={{ background: 'rgba(239,68,68,0.15)', border: '1px solid rgba(239,68,68,0.4)', color: '#ef4444' }}
        >
          {t('account.deleteFlat.deleteButton')}
        </button>
      </div>
    </div>
  )
}
