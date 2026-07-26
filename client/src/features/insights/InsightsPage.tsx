import { useUserSettings } from '@/features/settings/hooks/useUserSettings'
import { InsightsTab } from '@/features/insights/components/InsightsTab'

export default function InsightsPage() {
  const { settings } = useUserSettings()

  return (
    <div className="pt-4">
      <InsightsTab flatId={settings?.flatId} />
    </div>
  )
}
