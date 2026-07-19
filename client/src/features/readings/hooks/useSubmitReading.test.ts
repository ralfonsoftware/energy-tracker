import { createElement } from 'react'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { useSubmitReading } from '@/features/readings/hooks/useSubmitReading'
import type { ReadingResponse } from '@/features/readings/api/readingApi'

vi.mock('@/features/readings/api/readingApi')
import { submitReading } from '@/features/readings/api/readingApi'
const mockSubmitReading = vi.mocked(submitReading)

const sampleResponse: ReadingResponse = {
  readingId: 'reading-1',
  kwhValue: 120,
  readingDate: '2026-06-30T08:00:00+02:00',
  isCorrected: false,
  originalKwhValue: null,
  rowVersion: 'AQID',
}

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const invalidateQueries = vi.spyOn(queryClient, 'invalidateQueries')
  const wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children)
  return { wrapper, invalidateQueries }
}

describe('useSubmitReading', () => {
  beforeEach(() => {
    mockSubmitReading.mockReset()
  })

  it('useSubmitReading_OnSuccess_InvalidatesDashboardQuery', async () => {
    mockSubmitReading.mockResolvedValue(sampleResponse)
    const { wrapper, invalidateQueries } = createWrapper()
    const { result } = renderHook(() => useSubmitReading('flat-1'), { wrapper })

    result.current.mutate({ kwhValue: 120, readingDate: '2026-06-30T08:00:00+02:00' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['dashboard', 'flat-1'] })
  })

  it('useSubmitReading_WhenFlatIdUndefined_MutationRejects', async () => {
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useSubmitReading(undefined), { wrapper })

    result.current.mutate({ kwhValue: 120, readingDate: '2026-06-30T08:00:00+02:00' })

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(mockSubmitReading).not.toHaveBeenCalled()
  })

  it('useSubmitReading_OnSuccess_CallsOnSuccessImmediateBeforeInvalidationResolves', async () => {
    mockSubmitReading.mockResolvedValue(sampleResponse)
    const { wrapper, invalidateQueries } = createWrapper()
    const callOrder: string[] = []
    invalidateQueries.mockImplementation(async () => {
      callOrder.push('invalidate')
    })
    const onSuccessImmediate = vi.fn(() => callOrder.push('immediate'))
    const { result } = renderHook(() => useSubmitReading('flat-1', onSuccessImmediate), { wrapper })

    result.current.mutate({ kwhValue: 120, readingDate: '2026-06-30T08:00:00+02:00' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(onSuccessImmediate).toHaveBeenCalled()
    expect(callOrder).toEqual(['immediate', 'invalidate'])
  })
})
