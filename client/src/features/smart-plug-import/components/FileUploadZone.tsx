import { useRef, type DragEvent, type ChangeEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Upload } from 'lucide-react'

const ACCEPTED_EXTENSIONS = ['.xlsx', '.csv']

function filterAccepted(files: FileList): File[] {
  return Array.from(files).filter(f =>
    ACCEPTED_EXTENSIONS.some(ext => f.name.toLowerCase().endsWith(ext))
  )
}

type Props = { onFilesSelected: (files: File[]) => void }

export function FileUploadZone({ onFilesSelected }: Props) {
  const { t } = useTranslation('import')
  const inputRef = useRef<HTMLInputElement>(null)

  const handleDrop = (e: DragEvent<HTMLDivElement>) => {
    e.preventDefault()
    const accepted = filterAccepted(e.dataTransfer.files)
    if (accepted.length > 0) onFilesSelected(accepted)
  }

  const handleChange = (e: ChangeEvent<HTMLInputElement>) => {
    if (e.target.files) {
      const accepted = filterAccepted(e.target.files)
      if (accepted.length > 0) onFilesSelected(accepted)
    }
    e.target.value = ''
  }

  return (
    <div
      onDragOver={e => e.preventDefault()}
      onDrop={handleDrop}
      className="flex flex-col items-center gap-3 text-center rounded-2xl px-6 py-8 mb-4"
      style={{ background: 'rgba(255,255,255,0.04)', border: '1.5px dashed rgba(255,255,255,0.25)' }}
    >
      <Upload className="w-8 h-8 text-white/50" aria-hidden="true" />
      <span className="text-[15px] font-medium text-white">{t('uploadZone.primary')}</span>
      <span className="text-[13px] text-white/40">{t('uploadZone.subtitle')}</span>
      <button
        type="button"
        onClick={() => inputRef.current?.click()}
        className="px-6 py-2.5 rounded-full text-sm font-medium text-white"
        style={{ background: 'rgba(255,255,255,0.10)', border: '1px solid rgba(255,255,255,0.18)' }}
      >
        {t('uploadZone.chooseFiles')}
      </button>
      <span className="text-xs text-white/25 mt-1">{t('uploadZone.tip')}</span>
      <input
        ref={inputRef}
        type="file"
        accept=".xlsx,.csv"
        multiple
        className="hidden"
        aria-label={t('uploadZone.chooseFiles')}
        onChange={handleChange}
      />
    </div>
  )
}
