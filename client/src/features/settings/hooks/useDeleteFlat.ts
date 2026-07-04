import { useMutation, useQueryClient } from '@tanstack/react-query'
import { deleteFlat } from '@/features/settings/api/settingsApi'

export function useDeleteFlat() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (flatId: string) => deleteFlat(flatId),
    onSuccess: (_data, deletedFlatId) => {
      queryClient.invalidateQueries({ queryKey: ['flats'] })
      queryClient.invalidateQueries({ queryKey: ['settings'] })
      queryClient.invalidateQueries({ queryKey: ['dashboard', deletedFlatId] })
      queryClient.invalidateQueries({ queryKey: ['readings', deletedFlatId] })
      queryClient.invalidateQueries({ queryKey: ['tariffs', deletedFlatId] })
    },
  })
}
