import { resolvePeriodRange } from './periods'

const today = new Date(2026, 5, 17) // Wednesday, June 17, 2026

describe('resolvePeriodRange', () => {
  it('resolvePeriodRange_ThisWeek_ReturnsMondayOfCurrentWeekThroughToday', () => {
    expect(resolvePeriodRange('thisWeek', today)).toEqual({
      startDate: '2026-06-15',
      endDate: '2026-06-17',
    })
  })

  it('resolvePeriodRange_ThisMonth_ReturnsFirstOfMonthThroughToday', () => {
    expect(resolvePeriodRange('thisMonth', today)).toEqual({
      startDate: '2026-06-01',
      endDate: '2026-06-17',
    })
  })

  it('resolvePeriodRange_LastMonth_ReturnsFullPreviousCalendarMonth', () => {
    expect(resolvePeriodRange('lastMonth', today)).toEqual({
      startDate: '2026-05-01',
      endDate: '2026-05-31',
    })
  })

  it('resolvePeriodRange_ThisYear_ReturnsJanFirstThroughToday', () => {
    expect(resolvePeriodRange('thisYear', today)).toEqual({
      startDate: '2026-01-01',
      endDate: '2026-06-17',
    })
  })

  it('resolvePeriodRange_ThisWeek_WhenTodayIsMonday_StartAndEndAreSameDay', () => {
    const monday = new Date(2026, 5, 15)
    expect(resolvePeriodRange('thisWeek', monday)).toEqual({
      startDate: '2026-06-15',
      endDate: '2026-06-15',
    })
  })

  it('resolvePeriodRange_LastMonth_WhenCurrentMonthIsJanuary_ReturnsPreviousDecember', () => {
    const januaryToday = new Date(2026, 0, 10)
    expect(resolvePeriodRange('lastMonth', januaryToday)).toEqual({
      startDate: '2025-12-01',
      endDate: '2025-12-31',
    })
  })
})
