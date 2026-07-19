import { createElement } from 'react'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { usePatchReading } from '@/features/readings/hooks/usePatchReading'
import type { ReadingResponse } from '@/features/readings/api/readingApi'

vi.mock('@/features/readings/api/readingApi')
import { patchReading } from '@/features/readings/api/readingApi'
const mockPatchReading = vi.mocked(patchReading)

const sampleResponse: ReadingResponse = {
  readingId: 'reading-1',
  kwhValue: 120,
  readingDate: '2026-06-30T08:00:00+02:00',
  isCorrected: true,
  originalKwhValue: 100,
  rowVersion: 'AQID',
}

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const invalidateQueries = vi.spyOn(queryClient, 'invalidateQueries')
  const wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children)
  return { wrapper, invalidateQueries }
}

describe('usePatchReading', () => {
  beforeEach(() => {
    mockPatchReading.mockReset()
  })

  it('usePatchReading_OnSuccess_InvalidatesReadingsAndDashboardQueries', async () => {
    mockPatchReading.mockResolvedValue(sampleResponse)
    const { wrapper, invalidateQueries } = createWrapper()
    const { result } = renderHook(() => usePatchReading('flat-1'), { wrapper })

    result.current.mutate({ readingId: 'reading-1', kwhValue: 120, rowVersion: 'AQID' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['readings', 'flat-1'] })
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['dashboard', 'flat-1'] })
  })

  it('usePatchReading_WhenFlatIdUndefined_MutationRejects', async () => {
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => usePatchReading(undefined), { wrapper })

    result.current.mutate({ readingId: 'reading-1', kwhValue: 120, rowVersion: 'AQID' })

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(mockPatchReading).not.toHaveBeenCalled()
  })
})
