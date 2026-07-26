import { createElement } from 'react'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { vi, describe, it, expect } from 'vitest'
import { useDashboard } from '@/features/dashboard/hooks/useDashboard'
import type { DashboardSummary } from '@/features/dashboard/api/dashboardApi'

vi.mock('@/features/dashboard/api/dashboardApi')
import { getDashboard } from '@/features/dashboard/api/dashboardApi'
const mockGetDashboard = vi.mocked(getDashboard)

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return ({ children }: { children: React.ReactNode }) =>
    createElement(QueryClientProvider, { client: queryClient }, children)
}

const sampleDashboard: DashboardSummary = {
  dailyAvgKwh: 5,
  weeklyAvgKwh: 35,
  todayKwh: 5,
  dailyBudgetKwh: 6,
  lastReadingDate: '2026-06-30T00:00:00+02:00',
  spikeDays: [],
  cost: null,
  lastKwhValue: 100,
  dailyConsumption: [],
  readingHistoryDays: 10,
}

describe('useDashboard', () => {
  it('useDashboard_WhenFlatIdUndefined_QueryIsDisabled', () => {
    const { result } = renderHook(() => useDashboard(undefined), { wrapper: createWrapper() })
    expect(result.current.fetchStatus).toBe('idle')
    expect(mockGetDashboard).not.toHaveBeenCalled()
  })

  it('useDashboard_WhenFlatIdDefined_QueryFetchesDashboardWithDefaultSevenDays', async () => {
    mockGetDashboard.mockResolvedValue(sampleDashboard)
    const { result } = renderHook(() => useDashboard('flat-1'), { wrapper: createWrapper() })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual(sampleDashboard)
    expect(mockGetDashboard).toHaveBeenCalledWith('flat-1', 7)
  })

  it('useDashboard_DaysArgProvided_QueryFetchesDashboardWithThatWindow', async () => {
    mockGetDashboard.mockResolvedValue(sampleDashboard)
    const { result } = renderHook(() => useDashboard('flat-1', 30), { wrapper: createWrapper() })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(mockGetDashboard).toHaveBeenCalledWith('flat-1', 30)
  })

  it('useDashboard_DifferentDaysArgsSharingAQueryClient_UseDistinctCacheEntries', async () => {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    const wrapper = ({ children }: { children: React.ReactNode }) =>
      createElement(QueryClientProvider, { client: queryClient }, children)
    mockGetDashboard.mockImplementation((_flatId, days = 7) =>
      Promise.resolve({ ...sampleDashboard, readingHistoryDays: days })
    )

    const { result: sevenDayResult } = renderHook(() => useDashboard('flat-1', 7), { wrapper })
    const { result: thirtyDayResult } = renderHook(() => useDashboard('flat-1', 30), { wrapper })
    await waitFor(() => expect(sevenDayResult.current.isSuccess).toBe(true))
    await waitFor(() => expect(thirtyDayResult.current.isSuccess).toBe(true))

    expect(sevenDayResult.current.data?.readingHistoryDays).toBe(7)
    expect(thirtyDayResult.current.data?.readingHistoryDays).toBe(30)
    expect(queryClient.getQueryCache().findAll({ queryKey: ['dashboard', 'flat-1'] })).toHaveLength(2)
  })
})
