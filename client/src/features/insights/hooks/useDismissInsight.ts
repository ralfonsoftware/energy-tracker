import { useMutation, useQueryClient } from '@tanstack/react-query'
import { patchInsight } from '@/features/insights/api/insightsApi'

export function useDismissInsight(flatId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ insightId, rowVersion }: { insightId: string; rowVersion: string }) => {
      if (!flatId) throw new Error('flatId is required')
      return patchInsight(flatId, insightId, true, rowVersion)
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['insights', flatId] })
    },
  })
}
