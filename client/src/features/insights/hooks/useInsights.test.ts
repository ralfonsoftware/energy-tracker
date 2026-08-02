import { createElement } from 'react'
import { renderHook, waitFor, act } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { vi, describe, it, expect } from 'vitest'
import { useInsights } from '@/features/insights/hooks/useInsights'
import type { InsightsResponse } from '@/features/insights/api/insightsApi'

vi.mock('@/features/insights/api/insightsApi')
import { getInsights } from '@/features/insights/api/insightsApi'
const mockGetInsights = vi.mocked(getInsights)

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children)
}

const completeResponse: InsightsResponse = {
  runStatus: { status: 'Complete', startedAt: '2026-07-01T00:00:00Z', completedAt: '2026-07-01T00:05:00Z' },
  insights: [],
}

const processingResponse: InsightsResponse = {
  runStatus: { status: 'Processing', startedAt: '2026-07-01T00:00:00Z', completedAt: null },
  insights: [],
}

describe('useInsights', () => {
  it('useInsights_WhenFlatIdUndefined_QueryIsDisabled', () => {
    const { result } = renderHook(() => useInsights(undefined), { wrapper: createWrapper() })
    expect(result.current.fetchStatus).toBe('idle')
    expect(mockGetInsights).not.toHaveBeenCalled()
  })

  it('useInsights_WhenFlatIdDefined_QueryFetchesInsights', async () => {
    mockGetInsights.mockResolvedValue(completeResponse)
    const { result } = renderHook(() => useInsights('flat-1'), { wrapper: createWrapper() })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual(completeResponse)
    expect(mockGetInsights).toHaveBeenCalledWith('flat-1', 'active')
  })

  it('useInsights_RunStatusComplete_DoesNotPollAgainAfterFiveSeconds', async () => {
    vi.useFakeTimers()
    try {
      mockGetInsights.mockResolvedValue(completeResponse)
      const { result } = renderHook(() => useInsights('flat-1'), { wrapper: createWrapper() })
      await vi.waitFor(() => expect(result.current.isSuccess).toBe(true))

      mockGetInsights.mockClear()
      await act(async () => {
        await vi.advanceTimersByTimeAsync(6000)
      })

      expect(mockGetInsights).not.toHaveBeenCalled()
    } finally {
      vi.useRealTimers()
    }
  })

  it('useInsights_RunStatusProcessing_PollsAgainAfterFiveSeconds', async () => {
    vi.useFakeTimers()
    try {
      mockGetInsights.mockResolvedValue(processingResponse)
      const { result } = renderHook(() => useInsights('flat-1'), { wrapper: createWrapper() })
      await vi.waitFor(() => expect(result.current.isSuccess).toBe(true))

      mockGetInsights.mockClear()
      await act(async () => {
        await vi.advanceTimersByTimeAsync(5000)
      })

      expect(mockGetInsights).toHaveBeenCalled()
    } finally {
      vi.useRealTimers()
    }
  })
})
