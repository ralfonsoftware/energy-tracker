import { createElement } from 'react'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { useFlatStructure } from '@/features/flat-structure/hooks/useFlatStructure'
import type { FlatStructureResponse } from '@/features/flat-structure/api/flatStructureApi'

vi.mock('@/features/flat-structure/api/flatStructureApi')
import { getFlatStructure } from '@/features/flat-structure/api/flatStructureApi'
const mockGetFlatStructure = vi.mocked(getFlatStructure)

const sampleResponse: FlatStructureResponse = {
  flatId: 'flat-1',
  hasDefaultTemplate: false,
  rooms: [],
}

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children)
  return { wrapper, queryClient }
}

describe('useFlatStructure', () => {
  beforeEach(() => {
    mockGetFlatStructure.mockReset()
  })

  it('useFlatStructure_FlatIdProvided_ResolvesWithMockedStructure', async () => {
    mockGetFlatStructure.mockResolvedValue(sampleResponse)
    const { wrapper, queryClient } = createWrapper()
    const { result } = renderHook(() => useFlatStructure('flat-1'), { wrapper })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual(sampleResponse)
    expect(mockGetFlatStructure).toHaveBeenCalledWith('flat-1')
    expect(queryClient.getQueryData(['flat-structure', 'flat-1'])).toEqual(sampleResponse)
  })

  it('useFlatStructure_FlatIdUndefined_DoesNotCallApi', () => {
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useFlatStructure(undefined), { wrapper })

    expect(result.current.fetchStatus).toBe('idle')
    expect(mockGetFlatStructure).not.toHaveBeenCalled()
  })
})
