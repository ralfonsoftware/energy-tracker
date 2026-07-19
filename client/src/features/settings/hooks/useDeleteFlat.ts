import { useMutation, useQueryClient } from '@tanstack/react-query'
import { deleteFlat } from '@/features/settings/api/settingsApi'

export function useDeleteFlat() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ flatId, rowVersion }: { flatId: string; rowVersion: string }) =>
      deleteFlat(flatId, rowVersion),
    onSuccess: (_data, { flatId: deletedFlatId }) => {
      queryClient.invalidateQueries({ queryKey: ['flats'] })
      queryClient.invalidateQueries({ queryKey: ['settings'] })
      queryClient.invalidateQueries({ queryKey: ['dashboard', deletedFlatId] })
      queryClient.invalidateQueries({ queryKey: ['readings', deletedFlatId] })
      queryClient.invalidateQueries({ queryKey: ['tariffs', deletedFlatId] })
    },
  })
}
