import { createElement } from 'react'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { usePatchTariff } from '@/features/tariffs/hooks/usePatchTariff'
import type { TariffResponse } from '@/features/tariffs/api/tariffApi'

vi.mock('@/features/tariffs/api/tariffApi')
import { patchTariff } from '@/features/tariffs/api/tariffApi'
const mockPatchTariff = vi.mocked(patchTariff)

const sampleResponse: TariffResponse = {
  tariffId: 'tariff-1',
  contractStartDate: '2026-07-02T00:00:00Z',
  pricePerKwh: 0.28,
  monthlyBaseFee: 10,
  providerName: null,
  contractDurationMonths: null,
  isLocked: false,
  rowVersion: 'AQID',
}

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const invalidateQueries = vi.spyOn(queryClient, 'invalidateQueries')
  const wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children)
  return { wrapper, invalidateQueries }
}

describe('usePatchTariff', () => {
  beforeEach(() => {
    mockPatchTariff.mockReset()
  })

  it('usePatchTariff_OnSuccess_InvalidatesTariffsAndDashboardQueries', async () => {
    mockPatchTariff.mockResolvedValue(sampleResponse)
    const { wrapper, invalidateQueries } = createWrapper()
    const { result } = renderHook(() => usePatchTariff('flat-1'), { wrapper })

    result.current.mutate({ tariffId: 'tariff-1', body: { pricePerKwh: 0.3, rowVersion: 'AQID' } })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['tariffs', 'flat-1'] })
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['dashboard', 'flat-1'] })
  })

  it('usePatchTariff_WhenFlatIdUndefined_MutationRejectsWithoutCallingApi', async () => {
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => usePatchTariff(undefined), { wrapper })

    result.current.mutate({ tariffId: 'tariff-1', body: { pricePerKwh: 0.3, rowVersion: 'AQID' } })

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(mockPatchTariff).not.toHaveBeenCalled()
  })
})
