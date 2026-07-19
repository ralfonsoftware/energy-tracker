import { createElement } from 'react'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { useUpdateFlatStructure } from '@/features/flat-structure/hooks/useUpdateFlatStructure'
import type { FlatStructureResponse } from '@/features/flat-structure/api/flatStructureApi'

vi.mock('@/features/flat-structure/api/flatStructureApi')
import { updateFlatStructure } from '@/features/flat-structure/api/flatStructureApi'
const mockUpdateFlatStructure = vi.mocked(updateFlatStructure)

const sampleResponse: FlatStructureResponse = {
  flatId: 'flat-1',
  hasDefaultTemplate: false,
  rooms: [],
  rowVersion: 'AQID',
}

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const invalidateQueries = vi.spyOn(queryClient, 'invalidateQueries')
  const wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children)
  return { wrapper, invalidateQueries }
}

describe('useUpdateFlatStructure', () => {
  beforeEach(() => {
    mockUpdateFlatStructure.mockReset()
  })

  it('useUpdateFlatStructure_OnSuccess_InvalidatesFlatStructureQueryOnly', async () => {
    mockUpdateFlatStructure.mockResolvedValue(sampleResponse)
    const { wrapper, invalidateQueries } = createWrapper()
    const { result } = renderHook(() => useUpdateFlatStructure('flat-1'), { wrapper })

    result.current.mutate({ rooms: [], rowVersion: 'AQID' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['flat-structure', 'flat-1'] })
    expect(invalidateQueries).not.toHaveBeenCalledWith({ queryKey: ['dashboard', 'flat-1'] })
  })

  it('useUpdateFlatStructure_WhenFlatIdUndefined_MutationRejectsWithoutCallingApi', async () => {
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useUpdateFlatStructure(undefined), { wrapper })

    result.current.mutate({ rooms: [], rowVersion: 'AQID' })

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(mockUpdateFlatStructure).not.toHaveBeenCalled()
  })
})
