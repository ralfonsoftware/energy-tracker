import { useEffect, useRef } from 'react'
import { useQuery, useQueries, useQueryClient } from '@tanstack/react-query'
import { getImportStatus, parseGapNotifications } from '@/features/smart-plug-import/api/importApi'
import type { ActiveImportJob } from './useUploadImport'

export function useImportJobStatus(flatId: string | undefined) {
  const queryClient = useQueryClient()
  const handledCompleteRef = useRef(new Set<string>())

  const { data: activeJobs = [] } = useQuery<ActiveImportJob[]>({
    queryKey: ['import-jobs', flatId],
    queryFn: () => [],
    enabled: false,
    initialData: [],
    staleTime: Infinity,
  })

  const results = useQueries({
    queries: activeJobs.map(job => ({
      queryKey: ['import-job-status', flatId, job.importJobId],
      queryFn: () => getImportStatus(flatId as string, job.importJobId),
      enabled: !!flatId,
      refetchInterval: (query: { state: { data?: { status: string }; status: string } }) => {
        const status = query.state.data?.status
        if (status === 'Complete' || status === 'Failed') return false
        if (query.state.status === 'error') return false
        return 3000
      },
    })),
  })

  useEffect(() => {
    activeJobs.forEach((job, index) => {
      const data = results[index]?.data
      if (data?.status !== 'Complete') return
      if (handledCompleteRef.current.has(job.importJobId)) return
      handledCompleteRef.current.add(job.importJobId)

      queryClient.invalidateQueries({ queryKey: ['decomposition', flatId] })

      if (parseGapNotifications(data.gapNotifications).length === 0) {
        queryClient.setQueryData<ActiveImportJob[]>(['import-jobs', flatId], prev =>
          (prev ?? []).filter(j => j.importJobId !== job.importJobId)
        )
      }
    })
  }, [results, activeJobs, flatId, queryClient])

  const dismiss = (importJobId: string) => {
    handledCompleteRef.current.delete(importJobId)
    queryClient.setQueryData<ActiveImportJob[]>(['import-jobs', flatId], prev =>
      (prev ?? []).filter(j => j.importJobId !== importJobId)
    )
  }

  return {
    jobs: activeJobs.map((job, index) => ({
      ...job,
      statusData: results[index]?.data,
      isError: results[index]?.isError ?? false,
    })),
    dismiss,
  }
}
