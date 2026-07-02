import { useMutation, useQueryClient } from '@tanstack/react-query'
import { patchReading } from '@/features/readings/api/readingApi'

export function usePatchReading(flatId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ readingId, kwhValue }: { readingId: string; kwhValue: number }) => {
      if (!flatId) throw new Error('flatId is required')
      return patchReading(flatId, readingId, { kwhValue })
    },
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ['readings', flatId] }),
        queryClient.invalidateQueries({ queryKey: ['dashboard', flatId] }),
      ]),
  })
}
