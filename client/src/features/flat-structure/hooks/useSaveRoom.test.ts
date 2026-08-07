import { createElement } from 'react'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { useSaveRoom } from '@/features/flat-structure/hooks/useSaveRoom'
import type { RoomResponse } from '@/features/flat-structure/api/flatStructureApi'

vi.mock('@/features/flat-structure/api/flatStructureApi')
import { createRoom, updateRoom } from '@/features/flat-structure/api/flatStructureApi'
const mockCreateRoom = vi.mocked(createRoom)
const mockUpdateRoom = vi.mocked(updateRoom)

const sampleResponse: RoomResponse = {
  roomId: 'room-1',
  name: 'Living Room',
  sortOrder: 0,
  powerPoints: [],
  rowVersion: 'AQID',
}

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const invalidateQueries = vi.spyOn(queryClient, 'invalidateQueries')
  const wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children)
  return { wrapper, invalidateQueries }
}

describe('useSaveRoom', () => {
  beforeEach(() => {
    mockCreateRoom.mockReset()
    mockUpdateRoom.mockReset()
  })

  it('useSaveRoom_NoRoomId_CallsCreateRoomWithFlatId', async () => {
    mockCreateRoom.mockResolvedValue(sampleResponse)
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useSaveRoom('flat-1'), { wrapper })

    result.current.mutate({ name: 'Living Room', sortOrder: 0, powerPoints: [] })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(mockCreateRoom).toHaveBeenCalledWith('flat-1', { name: 'Living Room', sortOrder: 0, powerPoints: [] })
    expect(mockUpdateRoom).not.toHaveBeenCalled()
  })

  it('useSaveRoom_WithRoomIdAndRowVersion_CallsUpdateRoom', async () => {
    mockUpdateRoom.mockResolvedValue(sampleResponse)
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useSaveRoom('flat-1'), { wrapper })

    result.current.mutate({
      roomId: 'room-1',
      rowVersion: 'AQID',
      name: 'Living Room',
      sortOrder: 0,
      powerPoints: [],
    })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(mockUpdateRoom).toHaveBeenCalledWith('flat-1', 'room-1', {
      name: 'Living Room',
      sortOrder: 0,
      powerPoints: [],
      rowVersion: 'AQID',
    })
    expect(mockCreateRoom).not.toHaveBeenCalled()
  })

  it('useSaveRoom_WithRoomIdButMissingRowVersion_RejectsWithoutCallingApi', async () => {
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useSaveRoom('flat-1'), { wrapper })

    result.current.mutate({ roomId: 'room-1', name: 'Living Room', sortOrder: 0, powerPoints: [] })

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(mockCreateRoom).not.toHaveBeenCalled()
    expect(mockUpdateRoom).not.toHaveBeenCalled()
  })

  it('useSaveRoom_OnSuccess_InvalidatesFlatStructureQueryScopedToFlatId', async () => {
    mockCreateRoom.mockResolvedValue(sampleResponse)
    const { wrapper, invalidateQueries } = createWrapper()
    const { result } = renderHook(() => useSaveRoom('flat-1'), { wrapper })

    result.current.mutate({ name: 'Living Room', sortOrder: 0, powerPoints: [] })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['flat-structure', 'flat-1'] })
  })

  it('useSaveRoom_MissingFlatId_RejectsWithoutCallingApi', async () => {
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useSaveRoom(undefined), { wrapper })

    result.current.mutate({ name: 'Living Room', sortOrder: 0, powerPoints: [] })

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(mockCreateRoom).not.toHaveBeenCalled()
  })
})
