import { useQuery } from '@tanstack/react-query'
import { getDashboard } from '@/features/dashboard/api/dashboardApi'

export const useDashboard = (flatId: string | undefined, days = 7) =>
  useQuery({
    queryKey: ['dashboard', flatId, { days }],
    queryFn: () => getDashboard(flatId as string, days),
    enabled: !!flatId,
  })
