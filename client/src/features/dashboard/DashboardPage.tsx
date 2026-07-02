import { useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { useUserSettings } from '@/features/settings/hooks/useUserSettings'
import { useDashboard } from '@/features/dashboard/hooks/useDashboard'
import { EuroBurnGradient } from '@/features/dashboard/components/EuroBurnGradient'
import { DashboardGrid } from '@/features/dashboard/components/DashboardGrid'
import { TrendChart } from '@/features/dashboard/components/TrendChart'
import { EnterReadingCta } from '@/features/readings/components/EnterReadingCta'

export default function DashboardPage() {
  const { t } = useTranslation('common')
  const { settings, isError: isSettingsError } = useUserSettings()
  const { data: dashboard, isPending, isError: isDashboardError } = useDashboard(settings?.flatId)
  const isError = isSettingsError || isDashboardError
  const isColdOpen = dashboard === undefined || dashboard.lastReadingDate === null
  const animateArmedRef = useRef(false)

  return (
    <div className="relative min-h-screen">
      <EuroBurnGradient
        todayKwh={dashboard?.todayKwh ?? 0}
        dailyBudgetKwh={dashboard?.dailyBudgetKwh ?? 0}
        isColdOpen={isColdOpen}
      />
      <div className="relative z-10 px-4 pt-4 md:flex md:justify-end">
        <EnterReadingCta
          flatId={settings?.flatId}
          lastKwhValue={dashboard?.lastKwhValue ?? null}
          onSubmitSuccess={() => {
            animateArmedRef.current = true
          }}
        />
      </div>
      {isError ? (
        <p
          role="alert"
          className="relative z-10 mx-4 mt-6 rounded-card border border-glass-border bg-glass-surface px-4 py-3 text-body-sm text-text-primary"
        >
          {t('errors.networkError')}
        </p>
      ) : (
        <>
          <DashboardGrid
            dashboard={isPending ? undefined : dashboard}
            annualKwhBaseline={settings?.annualKwhBaseline}
            animateArmedRef={animateArmedRef}
          />
          <TrendChart dashboard={isPending ? undefined : dashboard} flatId={settings?.flatId} />
        </>
      )}
    </div>
  )
}
