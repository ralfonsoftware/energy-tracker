import { useMutation, useQueryClient } from '@tanstack/react-query'
import { submitReading } from '@/features/readings/api/readingApi'

export function useSubmitReading(flatId: string | undefined, onSuccessImmediate?: () => void) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: { kwhValue: number; readingDate: string }) => {
      if (!flatId) throw new Error('flatId is required')
      return submitReading(flatId, body)
    },
    onSuccess: async () => {
      onSuccessImmediate?.()
      await queryClient.invalidateQueries({ queryKey: ['dashboard', flatId] })
    },
  })
}
