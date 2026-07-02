import { createElement } from 'react'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { useTariffs } from '@/features/tariffs/hooks/useTariffs'
import type { TariffResponse } from '@/features/tariffs/api/tariffApi'

vi.mock('@/features/tariffs/api/tariffApi')
import { getTariffs } from '@/features/tariffs/api/tariffApi'
const mockGetTariffs = vi.mocked(getTariffs)

const sampleResponse: TariffResponse[] = [
  {
    tariffId: 'tariff-1',
    effectiveDate: '2026-06-01T00:00:00Z',
    pricePerKwh: 0.2285,
    monthlyBaseFee: 12,
    providerName: 'E.ON',
    contractStartDate: null,
    contractDurationMonths: null,
    isLocked: false,
  },
]

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children)
  return { wrapper }
}

describe('useTariffs', () => {
  beforeEach(() => {
    mockGetTariffs.mockReset()
  })

  it('useTariffs_FlatIdProvided_ResolvesWithMockedList', async () => {
    mockGetTariffs.mockResolvedValue(sampleResponse)
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useTariffs('flat-1'), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual(sampleResponse)
    expect(mockGetTariffs).toHaveBeenCalledWith('flat-1')
  })

  it('useTariffs_FlatIdUndefined_DoesNotCallApi', () => {
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useTariffs(undefined), { wrapper })

    expect(result.current.fetchStatus).toBe('idle')
    expect(mockGetTariffs).not.toHaveBeenCalled()
  })
})
