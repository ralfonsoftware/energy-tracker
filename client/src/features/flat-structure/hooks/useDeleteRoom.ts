import { useMutation, useQueryClient } from '@tanstack/react-query'
import { deleteRoom } from '@/features/flat-structure/api/flatStructureApi'

export type DeleteRoomInput = {
  roomId: string
  rowVersion: string
}

export function useDeleteRoom(flatId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ roomId, rowVersion }: DeleteRoomInput) => {
      if (!flatId) throw new Error('flatId is required')
      return deleteRoom(flatId, roomId, rowVersion)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['flat-structure', flatId] }),
  })
}
