import { useMutation, useQueryClient } from '@tanstack/react-query'
import { patchTariff, type PatchTariffRequest } from '@/features/tariffs/api/tariffApi'

export function usePatchTariff(flatId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ tariffId, body }: { tariffId: string; body: PatchTariffRequest }) => {
      if (!flatId) throw new Error('flatId is required')
      return patchTariff(flatId, tariffId, body)
    },
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ['tariffs', flatId] }),
        queryClient.invalidateQueries({ queryKey: ['dashboard', flatId] }),
      ]),
  })
}
