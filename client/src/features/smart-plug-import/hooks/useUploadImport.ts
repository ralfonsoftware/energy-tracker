import { useMutation, useQueryClient } from '@tanstack/react-query'
import { uploadImport } from '@/features/smart-plug-import/api/importApi'

export type ActiveImportJob = { importJobId: string; fileName: string }

export function useUploadImport(flatId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ file, plugId }: { file: File; plugId: string }) => {
      if (!flatId) throw new Error('flatId is required')
      return uploadImport(flatId, file, plugId)
    },
    onSuccess: (data, variables) => {
      queryClient.setQueryData<ActiveImportJob[]>(['import-jobs', flatId], (prev = []) => [
        ...prev,
        { importJobId: data.importJobId, fileName: variables.file.name },
      ])
    },
  })
}
