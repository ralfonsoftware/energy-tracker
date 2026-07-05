import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { DraftDevice } from './draftModel'

type Props = {
  device: DraftDevice | undefined
  onSave: (device: DraftDevice) => void
  onCancel: () => void
}

const inputClass =
  'w-full h-[52px] px-4 rounded-[12px] bg-white/[0.08] border text-white text-base placeholder:text-white/30 outline-none focus:border-white/60'
const inputStyle = { borderColor: 'rgba(255,255,255,0.15)' }
const sectionLabelClass = 'text-[11px] font-semibold tracking-[0.08em] uppercase text-white/45'

export function DeviceEditor({ device, onSave, onCancel }: Props) {
  const { t } = useTranslation('flat-structure')
  const [name, setName] = useState(device?.name ?? '')
  const [type, setType] = useState(device?.type ?? '')
  const [manufacturer, setManufacturer] = useState(device?.manufacturer ?? '')
  const [model, setModel] = useState(device?.model ?? '')

  const isSaveEnabled = name.trim() !== ''

  const handleSave = () => {
    if (!isSaveEnabled) return
    onSave({
      key: device?.key ?? crypto.randomUUID(),
      name: name.trim(),
      type,
      manufacturer,
      model,
      // A new device (no prior `device` prop) has no consumption profile yet;
      // an existing device keeps whatever it already had — this story's UI
      // doesn't expose these fields, so they must pass through untouched.
      consumptionApproach: device?.consumptionApproach ?? 'None',
      purchaseDate: device?.purchaseDate,
      euLabelClass: device?.euLabelClass,
      euAnnualKwh: device?.euAnnualKwh,
      selfMeasuredKwh: device?.selfMeasuredKwh,
      selfMeasuredPeriod: device?.selfMeasuredPeriod,
    })
  }

  return (
    <div className="flex-1 flex flex-col" style={{ background: '#111827', minHeight: '100vh' }}>
      <div className="px-6 pt-4 flex-1 flex flex-col">
        <h1 className="text-[22px] font-semibold text-white tracking-tight mb-6">{t('device.title')}</h1>

        <div className="flex flex-col gap-4">
          <div className="flex flex-col gap-1.5">
            <label className={sectionLabelClass}>{t('device.namePlaceholder')}</label>
            <input
              type="text"
              value={name}
              onChange={e => setName(e.target.value)}
              placeholder={t('device.namePlaceholder')}
              aria-label={t('device.namePlaceholder')}
              className={inputClass}
              style={inputStyle}
              autoFocus
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <label className={sectionLabelClass}>{t('device.typePlaceholder')}</label>
            <input
              type="text"
              value={type}
              onChange={e => setType(e.target.value)}
              placeholder={t('device.typePlaceholder')}
              aria-label={t('device.typePlaceholder')}
              className={inputClass}
              style={inputStyle}
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <label className={sectionLabelClass}>{t('device.manufacturerPlaceholder')}</label>
            <input
              type="text"
              value={manufacturer}
              onChange={e => setManufacturer(e.target.value)}
              placeholder={t('device.manufacturerPlaceholder')}
              aria-label={t('device.manufacturerPlaceholder')}
              className={inputClass}
              style={inputStyle}
            />
          </div>

          <div className="flex flex-col gap-1.5">
            <label className={sectionLabelClass}>{t('device.modelPlaceholder')}</label>
            <input
              type="text"
              value={model}
              onChange={e => setModel(e.target.value)}
              placeholder={t('device.modelPlaceholder')}
              aria-label={t('device.modelPlaceholder')}
              className={inputClass}
              style={inputStyle}
            />
          </div>

          <p className="text-xs text-white/40">{t('device.consumptionNote')}</p>
        </div>

        <div className="flex-1" />

        <div className="flex gap-2 pb-10">
          <button
            type="button"
            onClick={onCancel}
            className="flex-1 h-14 rounded-full text-white/70 text-[17px] font-semibold border"
            style={{ borderColor: 'rgba(255,255,255,0.20)' }}
          >
            {t('device.cancel')}
          </button>
          <button
            type="button"
            onClick={handleSave}
            disabled={!isSaveEnabled}
            className="flex-1 h-14 rounded-full text-white text-[17px] font-semibold border disabled:opacity-40"
            style={{ background: 'rgba(255,255,255,0.12)', borderColor: 'rgba(255,255,255,0.40)' }}
          >
            {t('device.save')}
          </button>
        </div>
      </div>
    </div>
  )
}
