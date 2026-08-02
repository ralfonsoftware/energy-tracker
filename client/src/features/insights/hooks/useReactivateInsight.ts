import { useMutation, useQueryClient } from '@tanstack/react-query'
import { patchInsight } from '@/features/insights/api/insightsApi'

export function useReactivateInsight(flatId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ insightId, rowVersion }: { insightId: string; rowVersion: string }) => {
      if (!flatId) throw new Error('flatId is required')
      return patchInsight(flatId, insightId, false, rowVersion)
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['insights', flatId] })
    },
  })
}
