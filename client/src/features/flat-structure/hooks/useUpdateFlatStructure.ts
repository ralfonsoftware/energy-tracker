import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  updateFlatStructure,
  type UpdateFlatStructureRequest,
} from '@/features/flat-structure/api/flatStructureApi'

export function useUpdateFlatStructure(flatId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: UpdateFlatStructureRequest) => {
      if (!flatId) throw new Error('flatId is required')
      return updateFlatStructure(flatId, body)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['flat-structure', flatId] }),
  })
}
