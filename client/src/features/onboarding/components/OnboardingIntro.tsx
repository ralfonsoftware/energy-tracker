import { useTranslation } from 'react-i18next'
import { LocaleDropdown } from '@/components/LocaleDropdown'

interface OnboardingIntroProps {
  onGetStarted: () => void
}

export function OnboardingIntro({ onGetStarted }: OnboardingIntroProps) {
  const { t } = useTranslation('onboarding')

  return (
    <div className="relative flex-1 flex flex-col" style={{ background: '#0f1235' }}>

      {/* Locale pill dropdown — absolute top-right */}
      <div className="absolute top-4 right-4 z-20">
        <LocaleDropdown />
      </div>

      {/* Main content column */}
      <div className="flex-1 flex flex-col items-center px-6 pb-10">

        {/* App icon + name + tagline */}
        <div className="mt-20 flex flex-col items-center gap-6">
          <div
            className="w-20 h-20 rounded-full bg-white/10 border border-white/[0.18] flex items-center justify-center"
            style={{ boxShadow: '0 8px 32px rgba(99,102,241,0.30)' }}
          >
            <svg width="28" height="28" viewBox="0 0 24 24" fill="none"
                 stroke="white" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2" />
            </svg>
          </div>
          <div className="flex flex-col items-center gap-3">
            <span className="text-[28px] font-semibold text-white tracking-tight">
              {t('intro.title')}
            </span>
            <p className="text-lg text-white/70 text-center max-w-[280px] leading-snug">
              {t('intro.valueProp')}
            </p>
          </div>
        </div>

        {/* Push bottom content down */}
        <div className="flex-1" />

        {/* Info note card */}
        <div className="w-full bg-white/[0.08] border border-white/15 rounded-2xl p-4 flex gap-2.5 items-start mb-7">
          <span className="text-base flex-shrink-0 mt-px" aria-hidden="true">⚡</span>
          <span className="text-sm text-white/50 leading-relaxed">
            {t('intro.tariffHint')}
          </span>
        </div>

        {/* CTA button + duration hint */}
        <div className="w-full flex flex-col items-center gap-3">
          <button
            onClick={onGetStarted}
            className="w-full h-14 rounded-full bg-white/[0.12] border border-white/40 text-white text-[17px] font-semibold"
            style={{ boxShadow: '0 0 0 1px rgba(255,255,255,0.08) inset, 0 4px 24px rgba(99,102,241,0.25)' }}
          >
            {t('intro.getStarted')}
          </button>
          <span className="text-xs text-white/30">{t('intro.duration')}</span>
        </div>

      </div>
    </div>
  )
}
