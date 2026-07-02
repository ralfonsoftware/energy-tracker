import { createElement } from 'react'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { useCreateTariff } from '@/features/tariffs/hooks/useCreateTariff'
import type { TariffResponse } from '@/features/tariffs/api/tariffApi'

vi.mock('@/features/tariffs/api/tariffApi')
import { createTariff } from '@/features/tariffs/api/tariffApi'
const mockCreateTariff = vi.mocked(createTariff)

const sampleResponse: TariffResponse = {
  tariffId: 'tariff-1',
  effectiveDate: '2026-07-02T00:00:00Z',
  pricePerKwh: 0.28,
  monthlyBaseFee: 10,
  providerName: null,
  contractStartDate: null,
  contractDurationMonths: null,
  isLocked: false,
}

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const invalidateQueries = vi.spyOn(queryClient, 'invalidateQueries')
  const wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children)
  return { wrapper, invalidateQueries }
}

describe('useCreateTariff', () => {
  beforeEach(() => {
    mockCreateTariff.mockReset()
  })

  it('useCreateTariff_OnSuccess_InvalidatesTariffsAndDashboardQueries', async () => {
    mockCreateTariff.mockResolvedValue(sampleResponse)
    const { wrapper, invalidateQueries } = createWrapper()
    const { result } = renderHook(() => useCreateTariff('flat-1'), { wrapper })

    result.current.mutate({ effectiveDate: '2026-07-02T00:00:00Z', pricePerKwh: 0.28, monthlyBaseFee: 10 })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['tariffs', 'flat-1'] })
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['dashboard', 'flat-1'] })
  })

  it('useCreateTariff_WhenFlatIdUndefined_MutationRejectsWithoutCallingApi', async () => {
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useCreateTariff(undefined), { wrapper })

    result.current.mutate({ effectiveDate: '2026-07-02T00:00:00Z', pricePerKwh: 0.28, monthlyBaseFee: 10 })

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(mockCreateTariff).not.toHaveBeenCalled()
  })
})
