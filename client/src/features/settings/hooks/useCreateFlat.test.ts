import { createElement } from 'react'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { useCreateFlat } from '@/features/settings/hooks/useCreateFlat'
import type { FlatSummary } from '@/features/settings/api/settingsApi'

vi.mock('@/features/settings/api/settingsApi')
import { createFlat } from '@/features/settings/api/settingsApi'
const mockCreateFlat = vi.mocked(createFlat)

const sampleResponse: FlatSummary = {
  flatId: 'flat-3',
  name: 'New Flat',
  annualKwhBaseline: 2500,
  spikeThreshold: 2,
  plannedAnnualSpend: null,
}

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const invalidateQueries = vi.spyOn(queryClient, 'invalidateQueries')
  const wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children)
  return { wrapper, invalidateQueries }
}

describe('useCreateFlat', () => {
  beforeEach(() => {
    mockCreateFlat.mockReset()
  })

  it('useCreateFlat_OnSuccess_CallsApiAndDoesNotInvalidateAnyQueries', async () => {
    mockCreateFlat.mockResolvedValue(sampleResponse)
    const { wrapper, invalidateQueries } = createWrapper()
    const { result } = renderHook(() => useCreateFlat(), { wrapper })

    result.current.mutate({ name: 'New Flat', annualKwhBaseline: 2500, plannedAnnualSpend: null })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(mockCreateFlat).toHaveBeenCalledWith({ name: 'New Flat', annualKwhBaseline: 2500, plannedAnnualSpend: null })
    expect(invalidateQueries).not.toHaveBeenCalled()
  })
})
