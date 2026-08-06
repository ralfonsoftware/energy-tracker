import { useMutation, useQueryClient } from '@tanstack/react-query'
import {
  createDevice,
  updateDevice,
  type DeviceWriteInput,
} from '@/features/flat-structure/api/flatStructureApi'

export type SaveDeviceInput = DeviceWriteInput & { deviceId?: string; rowVersion?: string }

export function useSaveDevice(flatId: string | undefined, powerPointId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ deviceId, rowVersion, ...body }: SaveDeviceInput) => {
      if (!flatId || !powerPointId) throw new Error('flatId and powerPointId are required')
      if (deviceId) {
        if (!rowVersion) throw new Error('rowVersion is required to update a device')
        return updateDevice(flatId, powerPointId, deviceId, { ...body, rowVersion })
      }
      return createDevice(flatId, powerPointId, body)
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['flat-structure', flatId] }),
  })
}
