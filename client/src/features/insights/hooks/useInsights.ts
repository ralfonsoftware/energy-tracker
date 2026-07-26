import { useQuery } from '@tanstack/react-query'
import { getInsights, type InsightsResponse } from '@/features/insights/api/insightsApi'

export const useInsights = (flatId: string | undefined) =>
  useQuery({
    queryKey: ['insights', flatId],
    queryFn: () => getInsights(flatId as string),
    enabled: !!flatId,
    refetchInterval: (query: { state: { data?: InsightsResponse; status: string } }) => {
      if (query.state.status === 'error') return false
      const status = query.state.data?.runStatus?.status
      return status === 'Pending' || status === 'Processing' ? 5000 : false
    },
  })
