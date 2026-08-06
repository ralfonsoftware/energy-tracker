import { useMutation, useQueryClient } from '@tanstack/react-query'
import { deleteDevice } from '@/features/flat-structure/api/flatStructureApi'

export type DeleteDeviceInput = {
  powerPointId: string
  deviceId: string
  rowVersion: string
}

export function useDeleteDevice(flatId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ powerPointId, deviceId, rowVersion }: DeleteDeviceInput) => {
      if (!flatId) throw new Error('flatId is required')
      return deleteDevice(flatId, powerPointId, deviceId, rowVersion)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['flat-structure', flatId] }),
  })
}
