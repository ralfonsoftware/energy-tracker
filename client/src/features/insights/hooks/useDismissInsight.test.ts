import { createElement } from 'react'
import { renderHook, waitFor, act } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { useDismissInsight } from '@/features/insights/hooks/useDismissInsight'

vi.mock('@/features/insights/api/insightsApi')
import { patchInsight } from '@/features/insights/api/insightsApi'
const mockPatchInsight = vi.mocked(patchInsight)

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const invalidateQueries = vi.spyOn(queryClient, 'invalidateQueries')
  const wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children)
  return { wrapper, invalidateQueries }
}

describe('useDismissInsight', () => {
  beforeEach(() => {
    mockPatchInsight.mockReset()
  })

  it('useDismissInsight_OnSuccess_InvalidatesInsightsQueryAndCallsPatchWithTrue', async () => {
    mockPatchInsight.mockResolvedValue({ insightId: 'i-1', isDismissed: true, dismissedAt: '2026-08-02T00:00:00Z', rowVersion: 'rv-2' })
    const { wrapper, invalidateQueries } = createWrapper()
    const { result } = renderHook(() => useDismissInsight('flat-1'), { wrapper })

    await act(async () => {
      await result.current.mutateAsync({ insightId: 'i-1', rowVersion: 'rv-1' })
    })

    await waitFor(() =>
      expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['insights', 'flat-1'] })
    )
    expect(mockPatchInsight).toHaveBeenCalledWith('flat-1', 'i-1', true, 'rv-1')
  })

  it('useDismissInsight_FlatIdUndefined_MutationRejects', async () => {
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useDismissInsight(undefined), { wrapper })

    await act(async () => {
      await expect(result.current.mutateAsync({ insightId: 'i-1', rowVersion: 'rv-1' })).rejects.toThrow('flatId is required')
    })
    expect(mockPatchInsight).not.toHaveBeenCalled()
  })
})
