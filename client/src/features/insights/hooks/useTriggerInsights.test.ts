import { createElement } from 'react'
import { renderHook, waitFor, act } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { useTriggerInsights } from '@/features/insights/hooks/useTriggerInsights'

vi.mock('@/features/insights/api/insightsApi')
import { triggerInsights } from '@/features/insights/api/insightsApi'
const mockTriggerInsights = vi.mocked(triggerInsights)

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const invalidateQueries = vi.spyOn(queryClient, 'invalidateQueries')
  const wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children)
  return { wrapper, invalidateQueries }
}

describe('useTriggerInsights', () => {
  beforeEach(() => {
    mockTriggerInsights.mockReset()
  })

  it('useTriggerInsights_OnSuccess_InvalidatesInsightsQuery', async () => {
    mockTriggerInsights.mockResolvedValue({ runId: 'run-1' })
    const { wrapper, invalidateQueries } = createWrapper()
    const { result } = renderHook(() => useTriggerInsights('flat-1'), { wrapper })

    await act(async () => {
      await result.current.mutateAsync()
    })

    await waitFor(() =>
      expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['insights', 'flat-1'] })
    )
    expect(mockTriggerInsights).toHaveBeenCalledWith('flat-1')
  })

  it('useTriggerInsights_FlatIdUndefined_MutationRejects', async () => {
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useTriggerInsights(undefined), { wrapper })

    await act(async () => {
      await expect(result.current.mutateAsync()).rejects.toThrow('flatId is required')
    })
    expect(mockTriggerInsights).not.toHaveBeenCalled()
  })
})
