import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import { parseLocaleNumber, formatNumberForInput } from '@/lib/localeNumber'
import type { ConsumptionApproach, SelfMeasuredPeriod } from '@/features/flat-structure/api/flatStructureApi'
import { StickyActionBar } from './StickyActionBar'
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
const cardStyle = {
  background: 'rgba(255,255,255,0.07)',
  border: '1px solid rgba(255,255,255,0.12)',
  borderRadius: '16px',
  backdropFilter: 'blur(20px) saturate(180%)',
}

const formatKwh = (value: number) =>
  `${new Intl.NumberFormat(i18n.language, { maximumFractionDigits: 2 }).format(value)} kWh`

const isValidKwhRaw = (raw: string, parsed: number, locale: string): boolean => {
  if (raw.trim() === '' || /[a-zA-Z]/.test(raw)) return false
  if (!Number.isFinite(parsed) || parsed < 0) return false
  const decimalSeparator = locale.startsWith('de') ? ',' : '.'
  const separatorIndex = raw.lastIndexOf(decimalSeparator)
  if (separatorIndex !== -1 && raw.length - separatorIndex - 1 > 4) return false
  return true
}

export function DeviceEditor({ device, onSave, onCancel }: Props) {
  const { t } = useTranslation('flat-structure')
  const [name, setName] = useState(device?.name ?? '')
  const [type, setType] = useState(device?.type ?? '')
  const [manufacturer, setManufacturer] = useState(device?.manufacturer ?? '')
  const [model, setModel] = useState(device?.model ?? '')
  const [approach, setApproach] = useState<ConsumptionApproach>(device?.consumptionApproach ?? 'None')
  const [configuring, setConfiguring] = useState(approach !== 'None')
  const [euLabelClass, setEuLabelClass] = useState(device?.euLabelClass ?? '')
  const [euAnnualKwhRaw, setEuAnnualKwhRaw] = useState(
    device?.euAnnualKwh !== undefined ? formatNumberForInput(device.euAnnualKwh, i18n.language) : ''
  )
  const [selfMeasuredKwhRaw, setSelfMeasuredKwhRaw] = useState(
    device?.selfMeasuredKwh !== undefined ? formatNumberForInput(device.selfMeasuredKwh, i18n.language) : ''
  )
  const [selfMeasuredPeriod, setSelfMeasuredPeriod] = useState<Exclude<SelfMeasuredPeriod, null>>(
    device?.selfMeasuredPeriod ?? 'Daily'
  )

  const parsedEuAnnualKwh = parseLocaleNumber(euAnnualKwhRaw, i18n.language)
  const parsedSelfMeasuredKwh = parseLocaleNumber(selfMeasuredKwhRaw, i18n.language)
  const euValid = approach !== 'EuLabel' || isValidKwhRaw(euAnnualKwhRaw, parsedEuAnnualKwh, i18n.language)
  const selfMeasuredValid =
    approach !== 'SelfMeasured' || isValidKwhRaw(selfMeasuredKwhRaw, parsedSelfMeasuredKwh, i18n.language)
  const isSaveEnabled = name.trim() !== '' && euValid && selfMeasuredValid

  const handleSave = () => {
    if (!isSaveEnabled) return
    onSave({
      key: device?.key ?? crypto.randomUUID(),
      name: name.trim(),
      type,
      manufacturer,
      model,
      consumptionApproach: approach,
      purchaseDate: device?.purchaseDate,
      euLabelClass: approach === 'EuLabel' ? euLabelClass.trim() || undefined : undefined,
      euAnnualKwh: approach === 'EuLabel' ? parsedEuAnnualKwh : undefined,
      selfMeasuredKwh: approach === 'SelfMeasured' ? parsedSelfMeasuredKwh : undefined,
      selfMeasuredPeriod: approach === 'SelfMeasured' ? selfMeasuredPeriod : undefined,
    })
  }

  return (
    <div className="flex-1 flex flex-col" style={{ background: '#111827', minHeight: '100vh' }}>
      <div className="px-6 pt-4 flex-1 flex flex-col">
        <h1 className="text-[22px] font-semibold text-white tracking-tight mb-6">{t('device.title')}</h1>

        <div className="flex flex-col gap-4 pb-10">
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

          {approach === 'None' && !configuring && (
            <div className="flex flex-col gap-2">
              <p className="text-xs text-white/40">{t('device.consumptionNote')}</p>
              <button
                type="button"
                onClick={() => setConfiguring(true)}
                className="self-start px-3 py-1.5 text-xs font-medium rounded-full"
                style={{
                  background: 'rgba(255,255,255,0.10)',
                  border: '1px solid rgba(255,255,255,0.12)',
                  color: 'rgba(255,255,255,0.75)',
                }}
              >
                {t('device.configureProfile')}
              </button>
            </div>
          )}

          {(approach !== 'None' || configuring) && (
            <div className="flex flex-col gap-3">
              <div className="flex items-center justify-between">
                <label className={sectionLabelClass}>{t('device.consumptionApproach.sectionLabel')}</label>
                {approach !== 'None' && (
                  <button
                    type="button"
                    onClick={() => setApproach('None')}
                    className="text-[11px] font-medium text-white/50 underline underline-offset-2"
                  >
                    {t('device.consumptionApproach.changeApproach')}
                  </button>
                )}
              </div>
              <div className="flex flex-col gap-2" role="radiogroup" aria-label={t('device.consumptionApproach.sectionLabel')}>
                <button
                  type="button"
                  role="radio"
                  aria-checked={approach === 'EuLabel'}
                  aria-label={t('device.consumptionApproach.euLabelTitle')}
                  onClick={() => setApproach('EuLabel')}
                  className="text-left p-4 flex flex-col gap-1"
                  style={cardStyle}
                >
                  <span className="text-sm font-semibold text-white">
                    {t('device.consumptionApproach.euLabelTitle')}
                  </span>
                  <span className="text-xs text-white/50">{t('device.consumptionApproach.euLabelSub')}</span>
                </button>
                <button
                  type="button"
                  role="radio"
                  aria-checked={approach === 'SelfMeasured'}
                  aria-label={t('device.consumptionApproach.selfMeasuredTitle')}
                  onClick={() => setApproach('SelfMeasured')}
                  className="text-left p-4 flex flex-col gap-1"
                  style={cardStyle}
                >
                  <span className="text-sm font-semibold text-white">
                    {t('device.consumptionApproach.selfMeasuredTitle')}
                  </span>
                  <span className="text-xs text-white/50">
                    {t('device.consumptionApproach.selfMeasuredSub')}
                  </span>
                </button>
              </div>

              {approach === 'EuLabel' && (
                <div className="flex flex-col gap-3">
                  <div className="flex flex-col gap-1.5">
                    <label className={sectionLabelClass}>{t('device.euLabel.classLabel')}</label>
                    <input
                      type="text"
                      value={euLabelClass}
                      onChange={e => setEuLabelClass(e.target.value)}
                      placeholder={t('device.euLabel.classPlaceholder')}
                      aria-label={t('device.euLabel.classLabel')}
                      className={inputClass}
                      style={inputStyle}
                    />
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <label className={sectionLabelClass}>{t('device.euLabel.annualKwhLabel')}</label>
                    <input
                      type="text"
                      inputMode="numeric"
                      value={euAnnualKwhRaw}
                      onChange={e => setEuAnnualKwhRaw(e.target.value)}
                      placeholder={t('device.euLabel.annualKwhPlaceholder')}
                      aria-label={t('device.euLabel.annualKwhLabel')}
                      className={inputClass}
                      style={inputStyle}
                    />
                    {isValidKwhRaw(euAnnualKwhRaw, parsedEuAnnualKwh, i18n.language) && (
                      <p className="text-xs text-white/40">
                        {t('device.euLabel.dailyEstimate', {
                          value: formatKwh(parsedEuAnnualKwh / 365),
                        })}
                      </p>
                    )}
                  </div>
                </div>
              )}

              {approach === 'SelfMeasured' && (
                <div className="flex flex-col gap-3">
                  <div className="flex gap-2" role="radiogroup" aria-label={t('device.consumptionApproach.selfMeasuredTitle')}>
                    <button
                      type="button"
                      role="radio"
                      aria-checked={selfMeasuredPeriod === 'Daily'}
                      onClick={() => setSelfMeasuredPeriod('Daily')}
                      className="flex-1 h-11 rounded-full text-sm font-medium border"
                      style={{
                        borderColor: 'rgba(255,255,255,0.20)',
                        background: selfMeasuredPeriod === 'Daily' ? 'rgba(255,255,255,0.12)' : 'transparent',
                        color: selfMeasuredPeriod === 'Daily' ? '#fff' : 'rgba(255,255,255,0.6)',
                      }}
                    >
                      {t('device.selfMeasured.periodDaily')}
                    </button>
                    <button
                      type="button"
                      role="radio"
                      aria-checked={selfMeasuredPeriod === 'Weekly'}
                      onClick={() => setSelfMeasuredPeriod('Weekly')}
                      className="flex-1 h-11 rounded-full text-sm font-medium border"
                      style={{
                        borderColor: 'rgba(255,255,255,0.20)',
                        background: selfMeasuredPeriod === 'Weekly' ? 'rgba(255,255,255,0.12)' : 'transparent',
                        color: selfMeasuredPeriod === 'Weekly' ? '#fff' : 'rgba(255,255,255,0.6)',
                      }}
                    >
                      {t('device.selfMeasured.periodWeekly')}
                    </button>
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <label className={sectionLabelClass}>
                      {selfMeasuredPeriod === 'Daily'
                        ? t('device.selfMeasured.kwhLabelDaily')
                        : t('device.selfMeasured.kwhLabelWeekly')}
                    </label>
                    <input
                      type="text"
                      inputMode="numeric"
                      value={selfMeasuredKwhRaw}
                      onChange={e => setSelfMeasuredKwhRaw(e.target.value)}
                      aria-label={
                        selfMeasuredPeriod === 'Daily'
                          ? t('device.selfMeasured.kwhLabelDaily')
                          : t('device.selfMeasured.kwhLabelWeekly')
                      }
                      className={inputClass}
                      style={inputStyle}
                    />
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      <StickyActionBar>
        <div className="flex gap-2">
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
      </StickyActionBar>
    </div>
  )
}
