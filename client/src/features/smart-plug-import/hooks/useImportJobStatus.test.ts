import { createElement } from 'react'
import { renderHook, waitFor, act } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { useImportJobStatus } from '@/features/smart-plug-import/hooks/useImportJobStatus'
import type { ImportJobStatusResponse } from '@/features/smart-plug-import/api/importApi'

vi.mock('@/features/smart-plug-import/api/importApi', async () => {
  const actual = await vi.importActual<typeof import('@/features/smart-plug-import/api/importApi')>(
    '@/features/smart-plug-import/api/importApi'
  )
  return { ...actual, getImportStatus: vi.fn() }
})
import { getImportStatus } from '@/features/smart-plug-import/api/importApi'
const mockGetImportStatus = vi.mocked(getImportStatus)

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const invalidateQueries = vi.spyOn(queryClient, 'invalidateQueries')
  const wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children)
  return { wrapper, queryClient, invalidateQueries }
}

describe('useImportJobStatus', () => {
  beforeEach(() => {
    mockGetImportStatus.mockReset()
  })

  it('useImportJobStatus_OnCompleteNoGaps_InvalidatesDecompositionAndRemovesJob', async () => {
    const { wrapper, queryClient, invalidateQueries } = createWrapper()
    queryClient.setQueryData(['import-jobs', 'flat-1'], [{ importJobId: 'job-1', fileName: 'meross.csv' }])

    const processing: ImportJobStatusResponse = {
      importJobId: 'job-1', status: 'Processing', createdAt: '2026-07-07T00:00:00Z', completedAt: null, errorCategory: null, gapNotifications: null,
    }
    const complete: ImportJobStatusResponse = {
      importJobId: 'job-1', status: 'Complete', createdAt: '2026-07-07T00:00:00Z', completedAt: '2026-07-07T00:05:00Z', errorCategory: null, gapNotifications: null,
    }
    mockGetImportStatus.mockResolvedValueOnce(processing)

    const { result } = renderHook(() => useImportJobStatus('flat-1'), { wrapper })

    await waitFor(() => expect(result.current.jobs[0]?.statusData?.status).toBe('Processing'))

    mockGetImportStatus.mockResolvedValue(complete)
    await act(async () => {
      await queryClient.invalidateQueries({ queryKey: ['import-job-status', 'flat-1', 'job-1'] })
    })

    await waitFor(() => expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['decomposition', 'flat-1'] }))
    await waitFor(() => expect(queryClient.getQueryData(['import-jobs', 'flat-1'])).toEqual([]))
  })

  it('useImportJobStatus_CompleteWithGapNotifications_JobRemainsUntilDismissed', async () => {
    const { wrapper, queryClient } = createWrapper()
    queryClient.setQueryData(['import-jobs', 'flat-1'], [{ importJobId: 'job-2', fileName: 'eve.xlsx' }])

    const completeWithGaps: ImportJobStatusResponse = {
      importJobId: 'job-2', status: 'Complete', createdAt: '2026-07-07T00:00:00Z', completedAt: '2026-07-07T00:05:00Z', errorCategory: null,
      gapNotifications: JSON.stringify([{ plugId: 'plug-1', start: '2026-07-01', end: '2026-07-02' }]),
    }
    mockGetImportStatus.mockResolvedValue(completeWithGaps)

    const { result } = renderHook(() => useImportJobStatus('flat-1'), { wrapper })

    await waitFor(() => expect(result.current.jobs[0]?.statusData?.status).toBe('Complete'))
    expect(queryClient.getQueryData(['import-jobs', 'flat-1'])).toEqual([{ importJobId: 'job-2', fileName: 'eve.xlsx' }])

    act(() => {
      result.current.dismiss('job-2')
    })

    await waitFor(() => expect(queryClient.getQueryData(['import-jobs', 'flat-1'])).toEqual([]))
  })
})
