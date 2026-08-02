import { createElement } from 'react'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { useReadingHistory } from '@/features/readings/hooks/useReadingHistory'
import type { ReadingHistoryPage, ReadingResponse } from '@/features/readings/api/readingApi'

vi.mock('@/features/readings/api/readingApi')
import { getReadingHistory } from '@/features/readings/api/readingApi'
const mockGetReadingHistory = vi.mocked(getReadingHistory)

const makeReading = (id: string): ReadingResponse => ({
  readingId: id,
  kwhValue: 120,
  readingDate: '2026-06-30T08:00:00+02:00',
  isCorrected: false,
  originalKwhValue: null,
  rowVersion: 'AQID',
})

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

  it('useReadingHistory_FlatIdProvided_ResolvesWithFirstPage', async () => {
    const page: ReadingHistoryPage = { items: [makeReading('reading-1')], totalCount: 1 }
    mockGetReadingHistory.mockResolvedValue(page)
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useReadingHistory('flat-1'), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data?.pages[0]).toEqual(page)
    expect(mockGetReadingHistory).toHaveBeenCalledWith('flat-1', { skip: 0, take: 20 })
  })

  it('useReadingHistory_FlatIdUndefined_DoesNotCallApi', () => {
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useReadingHistory(undefined), { wrapper })

    expect(result.current.fetchStatus).toBe('idle')
    expect(mockGetReadingHistory).not.toHaveBeenCalled()
  })

  it('useReadingHistory_TotalCountExceedsFirstPage_FetchNextPageLoadsSecondPage', async () => {
    const firstPage: ReadingHistoryPage = { items: [makeReading('reading-1')], totalCount: 2 }
    const secondPage: ReadingHistoryPage = { items: [makeReading('reading-2')], totalCount: 2 }
    mockGetReadingHistory.mockResolvedValueOnce(firstPage).mockResolvedValueOnce(secondPage)
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useReadingHistory('flat-1'), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.hasNextPage).toBe(true)

    await result.current.fetchNextPage()

    await waitFor(() => expect(result.current.data?.pages.length).toBe(2))
    expect(result.current.data?.pages[1]).toEqual(secondPage)
    expect(mockGetReadingHistory).toHaveBeenNthCalledWith(2, 'flat-1', { skip: 1, take: 20 })
  })

  it('useReadingHistory_AllItemsLoaded_HasNextPageIsFalse', async () => {
    const page: ReadingHistoryPage = { items: [makeReading('reading-1')], totalCount: 1 }
    mockGetReadingHistory.mockResolvedValue(page)
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useReadingHistory('flat-1'), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.hasNextPage).toBe(false)
  })
})
