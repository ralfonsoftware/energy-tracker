import { useMutation, useQueryClient } from '@tanstack/react-query'
import { triggerInsights } from '@/features/insights/api/insightsApi'

export function useTriggerInsights(flatId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => {
      if (!flatId) throw new Error('flatId is required')
      return triggerInsights(flatId)
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['insights', flatId] })
    },
  })
}
