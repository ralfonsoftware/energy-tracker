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
}

describe('useDashboard', () => {
  it('useDashboard_WhenFlatIdUndefined_QueryIsDisabled', () => {
    const { result } = renderHook(() => useDashboard(undefined), { wrapper: createWrapper() })
    expect(result.current.fetchStatus).toBe('idle')
    expect(mockGetDashboard).not.toHaveBeenCalled()
  })

  it('useDashboard_WhenFlatIdDefined_QueryFetchesDashboard', async () => {
    mockGetDashboard.mockResolvedValue(sampleDashboard)
    const { result } = renderHook(() => useDashboard('flat-1'), { wrapper: createWrapper() })
    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(result.current.data).toEqual(sampleDashboard)
    expect(mockGetDashboard).toHaveBeenCalledWith('flat-1')
  })
})
