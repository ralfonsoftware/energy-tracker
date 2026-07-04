import { createElement } from 'react'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { useFlats } from '@/features/settings/hooks/useFlats'
import type { FlatSummary } from '@/features/settings/api/settingsApi'

vi.mock('@/features/settings/api/settingsApi')
import { getFlats } from '@/features/settings/api/settingsApi'
const mockGetFlats = vi.mocked(getFlats)

const sampleFlats: FlatSummary[] = [
  { flatId: 'flat-1', name: 'Home', annualKwhBaseline: 2500, spikeThreshold: 2, plannedAnnualSpend: null },
  { flatId: 'flat-2', name: 'Cabin', annualKwhBaseline: 1500, spikeThreshold: 2, plannedAnnualSpend: 800 },
]

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children)
  return { wrapper }
}

describe('useFlats', () => {
  beforeEach(() => {
    mockGetFlats.mockReset()
  })

  it('useFlats_Success_ResolvesWithMockedList', async () => {
    mockGetFlats.mockResolvedValue(sampleFlats)
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useFlats(), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual(sampleFlats)
    expect(mockGetFlats).toHaveBeenCalled()
  })
})
