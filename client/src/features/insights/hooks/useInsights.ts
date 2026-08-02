import { useQuery } from '@tanstack/react-query'
import { getInsights, type InsightsResponse, type InsightsStatus } from '@/features/insights/api/insightsApi'

export const useInsights = (flatId: string | undefined, status: InsightsStatus = 'active') =>
  useQuery({
    queryKey: ['insights', flatId, status],
    queryFn: () => getInsights(flatId as string, status),
    enabled: !!flatId,
    refetchInterval: (query: { state: { data?: InsightsResponse; status: string } }) => {
      if (query.state.status === 'error') return false
      const status = query.state.data?.runStatus?.status
      return status === 'Pending' || status === 'Processing' ? 5000 : false
    },
  })
