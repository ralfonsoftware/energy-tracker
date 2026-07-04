import { createElement } from 'react'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { useDeleteFlat } from '@/features/settings/hooks/useDeleteFlat'

vi.mock('@/features/settings/api/settingsApi')
import { deleteFlat } from '@/features/settings/api/settingsApi'
const mockDeleteFlat = vi.mocked(deleteFlat)

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const invalidateQueries = vi.spyOn(queryClient, 'invalidateQueries')
  const wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children)
  return { wrapper, invalidateQueries }
}

describe('useDeleteFlat', () => {
  beforeEach(() => {
    mockDeleteFlat.mockReset()
  })

  it('useDeleteFlat_OnSuccess_CallsApiAndInvalidatesFlatScopedAndListQueries', async () => {
    mockDeleteFlat.mockResolvedValue(undefined)
    const { wrapper, invalidateQueries } = createWrapper()
    const { result } = renderHook(() => useDeleteFlat(), { wrapper })

    result.current.mutate('flat-1')

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(mockDeleteFlat).toHaveBeenCalledWith('flat-1')
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['flats'] })
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['settings'] })
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['dashboard', 'flat-1'] })
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['readings', 'flat-1'] })
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['tariffs', 'flat-1'] })
  })

  it('useDeleteFlat_OnError_DoesNotInvalidateQueries', async () => {
    mockDeleteFlat.mockRejectedValue(new Error('boom'))
    const { wrapper, invalidateQueries } = createWrapper()
    const { result } = renderHook(() => useDeleteFlat(), { wrapper })

    result.current.mutate('flat-1')

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(invalidateQueries).not.toHaveBeenCalled()
  })
})
