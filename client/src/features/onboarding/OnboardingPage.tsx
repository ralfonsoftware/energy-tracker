import { useState } from 'react'
import { Navigate } from 'react-router-dom'
import { useUserSettings } from '@/features/settings/hooks/useUserSettings'
import { OnboardingIntro } from './components/OnboardingIntro'
import { OnboardingFlatName } from './components/OnboardingFlatName'

type OnboardingStep = 'intro' | 'flat-name' | 'contract'

const STEPS: OnboardingStep[] = ['intro', 'flat-name', 'contract']

function StepIndicator({ currentStep }: { currentStep: OnboardingStep }) {
  const currentIndex = STEPS.indexOf(currentStep)
  return (
    <div
      className="flex items-center justify-center gap-2 pt-4"
      role="status"
      aria-label={`Setup progress: step ${currentIndex + 1} of ${STEPS.length}`}
    >
      {STEPS.map((_, i) => (
        <span
          key={i}
          className={`w-2 h-2 rounded-full ${i <= currentIndex ? 'bg-white' : 'bg-white/25'}`}
        />
      ))}
    </div>
  )
}

export default function OnboardingPage() {
  const { settings, isLoading, isError } = useUserSettings()
  const [step, setStep] = useState<OnboardingStep>('intro')
  const [flatName, setFlatName] = useState('')

  if (!isLoading && !isError && settings?.hasFlat) return <Navigate to="/" replace />

  return (
    <div className="flex flex-col h-[100dvh]" style={{ background: '#0f1235' }}>
      <StepIndicator currentStep={step} />
      {step === 'intro' && (
        <OnboardingIntro onGetStarted={() => setStep('flat-name')} />
      )}
      {step === 'flat-name' && (
        <OnboardingFlatName
          initialValue={flatName}
          onContinue={(name) => { setFlatName(name); setStep('contract') }}
          onBack={() => setStep('intro')}
        />
      )}
      {/* 'contract' rendered in Story 2.4 */}
    </div>
  )
}
