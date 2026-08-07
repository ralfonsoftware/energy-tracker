import { createElement } from 'react'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { useDeleteRoom } from '@/features/flat-structure/hooks/useDeleteRoom'

vi.mock('@/features/flat-structure/api/flatStructureApi')
import { deleteRoom } from '@/features/flat-structure/api/flatStructureApi'
const mockDeleteRoom = vi.mocked(deleteRoom)

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const invalidateQueries = vi.spyOn(queryClient, 'invalidateQueries')
  const wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children)
  return { wrapper, invalidateQueries }
}

describe('useDeleteRoom', () => {
  beforeEach(() => {
    mockDeleteRoom.mockReset()
  })

  it('useDeleteRoom_ValidInput_CallsDeleteRoomWithFlatIdRoomIdAndRowVersion', async () => {
    mockDeleteRoom.mockResolvedValue(undefined)
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useDeleteRoom('flat-1'), { wrapper })

    result.current.mutate({ roomId: 'room-1', rowVersion: 'AQID' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(mockDeleteRoom).toHaveBeenCalledWith('flat-1', 'room-1', 'AQID')
  })

  it('useDeleteRoom_OnSuccess_InvalidatesFlatStructureQueryScopedToFlatId', async () => {
    mockDeleteRoom.mockResolvedValue(undefined)
    const { wrapper, invalidateQueries } = createWrapper()
    const { result } = renderHook(() => useDeleteRoom('flat-1'), { wrapper })

    result.current.mutate({ roomId: 'room-1', rowVersion: 'AQID' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['flat-structure', 'flat-1'] })
  })

  it('useDeleteRoom_MissingFlatId_RejectsWithoutCallingApi', async () => {
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useDeleteRoom(undefined), { wrapper })

    result.current.mutate({ roomId: 'room-1', rowVersion: 'AQID' })

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(mockDeleteRoom).not.toHaveBeenCalled()
  })
})
