import { createElement } from 'react'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { useSaveDevice } from '@/features/flat-structure/hooks/useSaveDevice'
import type { DeviceResponse } from '@/features/flat-structure/api/flatStructureApi'

vi.mock('@/features/flat-structure/api/flatStructureApi')
import { createDevice, updateDevice } from '@/features/flat-structure/api/flatStructureApi'
const mockCreateDevice = vi.mocked(createDevice)
const mockUpdateDevice = vi.mocked(updateDevice)

const sampleResponse: DeviceResponse = {
  deviceId: 'device-1',
  name: 'Toaster',
  type: null,
  manufacturer: null,
  model: null,
  purchaseDate: null,
  inUseSince: null,
  decommissionedDate: null,
  consumptionApproach: 'None',
  euLabelClass: null,
  euAnnualKwh: null,
  selfMeasuredKwh: null,
  selfMeasuredPeriod: null,
  rowVersion: 'AQID',
}

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const invalidateQueries = vi.spyOn(queryClient, 'invalidateQueries')
  const wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children)
  return { wrapper, invalidateQueries }
}

describe('useSaveDevice', () => {
  beforeEach(() => {
    mockCreateDevice.mockReset()
    mockUpdateDevice.mockReset()
  })

  it('useSaveDevice_NoDeviceId_CallsCreateDeviceWithFlatIdAndPowerPointId', async () => {
    mockCreateDevice.mockResolvedValue(sampleResponse)
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useSaveDevice('flat-1', 'pp-1'), { wrapper })

    result.current.mutate({ name: 'Toaster', consumptionApproach: 'None' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(mockCreateDevice).toHaveBeenCalledWith('flat-1', 'pp-1', { name: 'Toaster', consumptionApproach: 'None' })
    expect(mockUpdateDevice).not.toHaveBeenCalled()
  })

  it('useSaveDevice_WithDeviceIdAndRowVersion_CallsUpdateDevice', async () => {
    mockUpdateDevice.mockResolvedValue(sampleResponse)
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useSaveDevice('flat-1', 'pp-1'), { wrapper })

    result.current.mutate({
      deviceId: 'device-1',
      rowVersion: 'AQID',
      name: 'Toaster',
      consumptionApproach: 'None',
    })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(mockUpdateDevice).toHaveBeenCalledWith('flat-1', 'pp-1', 'device-1', {
      name: 'Toaster',
      consumptionApproach: 'None',
      rowVersion: 'AQID',
    })
    expect(mockCreateDevice).not.toHaveBeenCalled()
  })

  it('useSaveDevice_WithDeviceIdButMissingRowVersion_RejectsWithoutCallingApi', async () => {
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useSaveDevice('flat-1', 'pp-1'), { wrapper })

    result.current.mutate({ deviceId: 'device-1', name: 'Toaster', consumptionApproach: 'None' })

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(mockCreateDevice).not.toHaveBeenCalled()
    expect(mockUpdateDevice).not.toHaveBeenCalled()
  })

  it('useSaveDevice_OnSuccess_InvalidatesFlatStructureQueryScopedToFlatId', async () => {
    mockCreateDevice.mockResolvedValue(sampleResponse)
    const { wrapper, invalidateQueries } = createWrapper()
    const { result } = renderHook(() => useSaveDevice('flat-1', 'pp-1'), { wrapper })

    result.current.mutate({ name: 'Toaster', consumptionApproach: 'None' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['flat-structure', 'flat-1'] })
  })

  it('useSaveDevice_MissingFlatIdOrPowerPointId_RejectsWithoutCallingApi', async () => {
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useSaveDevice(undefined, 'pp-1'), { wrapper })

    result.current.mutate({ name: 'Toaster', consumptionApproach: 'None' })

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(mockCreateDevice).not.toHaveBeenCalled()
  })
})
