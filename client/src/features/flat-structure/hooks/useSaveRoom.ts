import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  createRoom,
  updateRoom,
  type CreateRoomInput,
} from '@/features/flat-structure/api/flatStructureApi'

export type SaveRoomInput = CreateRoomInput & { roomId?: string; rowVersion?: string }

export function useSaveRoom(flatId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ roomId, rowVersion, ...body }: SaveRoomInput) => {
      if (!flatId) throw new Error('flatId is required')
      if (roomId) {
        if (!rowVersion) throw new Error('rowVersion is required to update a room')
        return updateRoom(flatId, roomId, { ...body, rowVersion })
      }
      return createRoom(flatId, body)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['flat-structure', flatId] }),
  })
}
