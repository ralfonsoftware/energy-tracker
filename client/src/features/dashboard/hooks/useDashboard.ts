import { useQuery } from '@tanstack/react-query'
import { getDashboard } from '@/features/dashboard/api/dashboardApi'

export const useDashboard = (flatId: string | undefined) =>
  useQuery({
    queryKey: ['dashboard', flatId],
    queryFn: () => getDashboard(flatId as string),
    enabled: !!flatId,
  })
