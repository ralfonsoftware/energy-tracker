import { createElement } from 'react'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useDecomposition } from '@/features/decomposition/hooks/useDecomposition'
import type { DecompositionResponse } from '@/features/decomposition/api/decompositionApi'

vi.mock('@/features/decomposition/api/decompositionApi')
import { getDecomposition } from '@/features/decomposition/api/decompositionApi'
const mockGetDecomposition = vi.mocked(getDecomposition)

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children)
}

const sampleResponse: DecompositionResponse = {
  period: { startDate: '2026-06-01', endDate: '2026-06-17' },
  totalKwh: 100,
  totalCost: 25,
  isUnavailable: false,
  hasInterpolatedData: false,
  residual: { kwh: 10, cost: 2.5 },
  rooms: [],
}

describe('useDecomposition', () => {
  it('useDecomposition_WhenFlatIdUndefined_QueryIsDisabled', () => {
    const { result } = renderHook(() => useDecomposition(undefined, '2026-06-01', '2026-06-17'), {
      wrapper: createWrapper(),
    })
    expect(result.current.fetchStatus).toBe('idle')
    expect(mockGetDecomposition).not.toHaveBeenCalled()
  })

  it('useDecomposition_WhenDatesUndefined_QueryIsDisabled', () => {
    const { result } = renderHook(() => useDecomposition('flat-1', undefined, undefined), {
      wrapper: createWrapper(),
    })
    expect(result.current.fetchStatus).toBe('idle')
    expect(mockGetDecomposition).not.toHaveBeenCalled()
  })

  it('useDecomposition_WhenFlatIdAndDatesDefined_QueryFetchesDecomposition', async () => {
    mockGetDecomposition.mockResolvedValue(sampleResponse)
    const { result } = renderHook(
      () => useDecomposition('flat-1', '2026-06-01', '2026-06-17'),
      { wrapper: createWrapper() }
    )
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual(sampleResponse)
    expect(mockGetDecomposition).toHaveBeenCalledWith('flat-1', '2026-06-01', '2026-06-17')
  })
})
