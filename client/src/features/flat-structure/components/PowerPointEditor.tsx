import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Trash2 } from 'lucide-react'
import type { DraftPowerPoint } from './draftModel'

type Props = {
  powerPoint: DraftPowerPoint
  onChange: (updated: DraftPowerPoint) => void
  onEditDevice: (deviceKey: string | null) => void
  onDelete: () => void
}

const inputClass =
  'w-full h-11 px-3 rounded-[10px] bg-white/[0.08] border text-white text-sm placeholder:text-white/30 outline-none focus:border-white/60'
const inputStyle = { borderColor: 'rgba(255,255,255,0.15)' }
const sectionLabelClass = 'text-[11px] font-semibold tracking-[0.08em] uppercase text-white/45'

export function PowerPointEditor({ powerPoint, onChange, onEditDevice, onDelete }: Props) {
  const { t } = useTranslation('flat-structure')
  const [confirmingDelete, setConfirmingDelete] = useState(false)
  const [confirmDeleteDeviceKey, setConfirmDeleteDeviceKey] = useState<string | null>(null)

  return (
    <div
      className="rounded-2xl p-4 flex flex-col gap-3"
      style={{
        background: 'rgba(255,255,255,0.07)',
        border: '1px solid rgba(255,255,255,0.12)',
        borderRadius: '16px',
        backdropFilter: 'blur(20px) saturate(180%)',
      }}
    >
      {confirmingDelete ? (
        <div className="flex items-center justify-between gap-2">
          <span className="text-sm text-white/80">{t('powerPoint.deletePrompt')}</span>
          <div className="flex items-center gap-2 shrink-0">
            <button
              type="button"
              onClick={() => setConfirmingDelete(false)}
              className="px-3 py-1.5 text-xs font-medium rounded-full text-white/70"
            >
              {t('confirm.cancel')}
            </button>
            <button
              type="button"
              onClick={onDelete}
              className="px-3 py-1.5 text-xs font-semibold rounded-full text-accent-error"
            >
              {t('confirm.delete')}
            </button>
          </div>
        </div>
      ) : (
        <div className="flex items-center gap-2">
          <input
            type="text"
            value={powerPoint.name}
            onChange={e => onChange({ ...powerPoint, name: e.target.value })}
            placeholder={t('powerPoint.namePlaceholder')}
            aria-label={t('powerPoint.namePlaceholder')}
            className={`${inputClass} flex-1`}
            style={inputStyle}
          />
          <button
            type="button"
            onClick={() => setConfirmingDelete(true)}
            aria-label={t('powerPoint.delete')}
            className="shrink-0 text-white/50 hover:text-accent-error transition-colors"
          >
            <Trash2 className="h-4 w-4" aria-hidden="true" />
          </button>
        </div>
      )}

      {!confirmingDelete && (
        <>
          <div className="flex flex-col gap-1">
            <label className={sectionLabelClass}>{t('powerPoint.plugIdLabel')}</label>
            <input
              type="text"
              value={powerPoint.plugId}
              onChange={e => onChange({ ...powerPoint, plugId: e.target.value })}
              placeholder={t('powerPoint.plugIdPlaceholder')}
              aria-label={t('powerPoint.plugIdLabel')}
              className={inputClass}
              style={inputStyle}
            />
          </div>

          {powerPoint.devices.length > 0 && (
            <ul className="flex flex-col gap-2">
              {powerPoint.devices.map(device =>
                confirmDeleteDeviceKey === device.key ? (
                  <li key={device.key} className="flex items-center justify-between gap-2">
                    <span className="text-sm text-white/80">{t('device.deletePrompt')}</span>
                    <div className="flex items-center gap-2 shrink-0">
                      <button
                        type="button"
                        onClick={() => setConfirmDeleteDeviceKey(null)}
                        className="text-xs text-white/70"
                      >
                        {t('confirm.cancel')}
                      </button>
                      <button
                        type="button"
                        onClick={() =>
                          onChange({
                            ...powerPoint,
                            devices: powerPoint.devices.filter(d => d.key !== device.key),
                          })
                        }
                        className="text-xs font-semibold text-accent-error"
                      >
                        {t('confirm.delete')}
                      </button>
                    </div>
                  </li>
                ) : (
                  <li key={device.key} className="flex items-center justify-between gap-2">
                    <span className="text-sm text-white">{device.name}</span>
                    <div className="flex items-center gap-2 shrink-0">
                      <button
                        type="button"
                        onClick={() => onEditDevice(device.key)}
                        className="text-xs underline"
                        style={{ color: '#60a5fa' }}
                      >
                        {t('powerPoint.editDevice')}
                      </button>
                      <button
                        type="button"
                        onClick={() => setConfirmDeleteDeviceKey(device.key)}
                        aria-label={t('device.delete')}
                        className="text-white/50 hover:text-accent-error transition-colors"
                      >
                        <Trash2 className="h-4 w-4" aria-hidden="true" />
                      </button>
                    </div>
                  </li>
                )
              )}
            </ul>
          )}

          <button
            type="button"
            onClick={() => onEditDevice(null)}
            className="self-start px-3 py-1.5 text-xs font-medium rounded-full"
            style={{
              background: 'rgba(255,255,255,0.10)',
              border: '1px solid rgba(255,255,255,0.12)',
              color: 'rgba(255,255,255,0.75)',
            }}
          >
            {t('powerPoint.addDevice')}
          </button>
        </>
      )}
    </div>
  )
}
