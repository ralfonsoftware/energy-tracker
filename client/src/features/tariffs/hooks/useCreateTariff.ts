import { useMutation, useQueryClient } from '@tanstack/react-query'
import { createTariff, type CreateTariffRequest } from '@/features/tariffs/api/tariffApi'

export function useCreateTariff(flatId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: CreateTariffRequest) => {
      if (!flatId) throw new Error('flatId is required')
      return createTariff(flatId, body)
    },
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ['tariffs', flatId] }),
        queryClient.invalidateQueries({ queryKey: ['dashboard', flatId] }),
      ]),
  })
}
