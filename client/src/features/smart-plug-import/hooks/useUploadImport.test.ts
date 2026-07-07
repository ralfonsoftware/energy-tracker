import { createElement } from 'react'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { useUploadImport } from '@/features/smart-plug-import/hooks/useUploadImport'
import type { UploadImportResponse } from '@/features/smart-plug-import/api/importApi'

vi.mock('@/features/smart-plug-import/api/importApi')
import { uploadImport } from '@/features/smart-plug-import/api/importApi'
const mockUploadImport = vi.mocked(uploadImport)

const sampleResponse: UploadImportResponse = { importJobId: 'job-1' }

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children)
  return { wrapper, queryClient }
}

describe('useUploadImport', () => {
  beforeEach(() => {
    mockUploadImport.mockReset()
  })

  it('useUploadImport_OnSuccess_AppendsJobToActiveImportJobsCache', async () => {
    mockUploadImport.mockResolvedValue(sampleResponse)
    const { wrapper, queryClient } = createWrapper()
    const { result } = renderHook(() => useUploadImport('flat-1'), { wrapper })

    const file = new File(['x'], 'meross-kitchen.csv')
    result.current.mutate({ file, plugId: 'plug-1' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(queryClient.getQueryData(['import-jobs', 'flat-1'])).toEqual([
      { importJobId: 'job-1', fileName: 'meross-kitchen.csv' },
    ])
  })

  it('useUploadImport_WhenFlatIdUndefined_MutationRejectsWithoutCallingApi', async () => {
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useUploadImport(undefined), { wrapper })

    const file = new File(['x'], 'meross-kitchen.csv')
    result.current.mutate({ file, plugId: 'plug-1' })

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(mockUploadImport).not.toHaveBeenCalled()
  })
})
