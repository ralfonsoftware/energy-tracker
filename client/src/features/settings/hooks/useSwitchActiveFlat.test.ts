import { createElement } from 'react'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { useSwitchActiveFlat } from '@/features/settings/hooks/useSwitchActiveFlat'
import type { UserSettings } from '@/features/settings/api/settingsApi'

vi.mock('@/features/settings/api/settingsApi')
import { updateUserSettings } from '@/features/settings/api/settingsApi'
const mockUpdateUserSettings = vi.mocked(updateUserSettings)

const sampleResponse: UserSettings = {
  locale: 'en-US',
  hasFlat: true,
  flatId: 'flat-2',
  flatName: 'Cabin',
  annualKwhBaseline: 1500,
  plannedAnnualSpend: null,
}

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const invalidateQueries = vi.spyOn(queryClient, 'invalidateQueries')
  const wrapper = ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children)
  return { wrapper, invalidateQueries }
}

describe('useSwitchActiveFlat', () => {
  beforeEach(() => {
    mockUpdateUserSettings.mockReset()
  })

  it('useSwitchActiveFlat_OnSuccess_CallsApiWithLocaleAndActiveFlatId', async () => {
    mockUpdateUserSettings.mockResolvedValue(sampleResponse)
    const { wrapper } = createWrapper()
    const { result } = renderHook(() => useSwitchActiveFlat(), { wrapper })

    result.current.mutate({ flatId: 'flat-2', locale: 'en-US', previousFlatId: 'flat-1' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(mockUpdateUserSettings).toHaveBeenCalledWith({ locale: 'en-US', activeFlatId: 'flat-2' })
  })

  it('useSwitchActiveFlat_OnSuccess_InvalidatesSettingsFlatsAndPreviousFlatScopedQueries', async () => {
    mockUpdateUserSettings.mockResolvedValue(sampleResponse)
    const { wrapper, invalidateQueries } = createWrapper()
    const { result } = renderHook(() => useSwitchActiveFlat(), { wrapper })

    result.current.mutate({ flatId: 'flat-2', locale: 'en-US', previousFlatId: 'flat-1' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['settings'] })
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['flats'] })
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['dashboard', 'flat-1'] })
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['readings', 'flat-1'] })
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['tariffs', 'flat-1'] })
  })

  it('useSwitchActiveFlat_OnSuccess_WithoutPreviousFlatId_InvalidatesOnlySettingsAndFlats', async () => {
    mockUpdateUserSettings.mockResolvedValue(sampleResponse)
    const { wrapper, invalidateQueries } = createWrapper()
    const { result } = renderHook(() => useSwitchActiveFlat(), { wrapper })

    result.current.mutate({ flatId: 'flat-2', locale: 'en-US', previousFlatId: undefined })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['settings'] })
    expect(invalidateQueries).toHaveBeenCalledWith({ queryKey: ['flats'] })
    expect(invalidateQueries).not.toHaveBeenCalledWith({ queryKey: ['dashboard', undefined] })
  })

  it('useSwitchActiveFlat_OnError_DoesNotInvalidateQueries', async () => {
    mockUpdateUserSettings.mockRejectedValue(new Error('boom'))
    const { wrapper, invalidateQueries } = createWrapper()
    const { result } = renderHook(() => useSwitchActiveFlat(), { wrapper })

    result.current.mutate({ flatId: 'flat-2', locale: 'en-US', previousFlatId: 'flat-1' })

    await waitFor(() => expect(result.current.isError).toBe(true))
    expect(invalidateQueries).not.toHaveBeenCalled()
  })
})
