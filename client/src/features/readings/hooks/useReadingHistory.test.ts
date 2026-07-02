import { createElement } from 'react'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { useReadingHistory } from '@/features/readings/hooks/useReadingHistory'
import type { ReadingResponse } from '@/features/readings/api/readingApi'

vi.mock('@/features/readings/api/readingApi')
import { getReadingHistory } from '@/features/readings/api/readingApi'
const mockGetReadingHistory = vi.mocked(getReadingHistory)

const sampleResponse: ReadingResponse[] = [
  {
    readingId: 'reading-1',
    kwhValue: 120,
    readingDate: '2026-06-30T08:00:00+02:00',
    isCorrected: false,
    originalKwhValue: null,
  },
]

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children)
  return { wrapper }
}

describe('useReadingHistory', () => {
  beforeEach(() => {
    mockGetReadingHistory.mockReset()
  })

  it('useReadingHistory_FlatIdProvided_ResolvesWithMockedList', async () => {
    mockGetReadingHistory.mockResolvedValue(sampleResponse)
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useReadingHistory('flat-1'), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual(sampleResponse)
    expect(mockGetReadingHistory).toHaveBeenCalledWith('flat-1')
  })

  it('useReadingHistory_FlatIdUndefined_DoesNotCallApi', () => {
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useReadingHistory(undefined), { wrapper })

    expect(result.current.fetchStatus).toBe('idle')
    expect(mockGetReadingHistory).not.toHaveBeenCalled()
  })
})
