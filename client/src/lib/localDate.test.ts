import { toLocalDateString, parseLocalDate, addMonths, isFutureLocalDate } from './localDate'

describe('localDate', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
  })

  describe('toLocalDateString', () => {
    it('toLocalDateString_MidYearDate_FormatsAsYyyyMmDdPaddedWithZeros', () => {
      expect(toLocalDateString(new Date(2026, 2, 5))).toBe('2026-03-05')
    })

    it('toLocalDateString_SingleDigitDay_PadsDayWithLeadingZero', () => {
      expect(toLocalDateString(new Date(2026, 6, 3))).toBe('2026-07-03')
    })

    it('toLocalDateString_NewYearBoundary_RollsYearForward', () => {
      expect(toLocalDateString(new Date(2025, 11, 31))).toBe('2025-12-31')
      expect(toLocalDateString(new Date(2026, 0, 1))).toBe('2026-01-01')
    })
  })

  describe('parseLocalDate', () => {
    it('parseLocalDate_UtcMidnightSameTimezone_RoundTripsToSameCalendarDate', () => {
      vi.stubEnv('TZ', 'UTC')

      const result = parseLocalDate('2026-01-01T00:00:00Z')

      expect(toLocalDateString(result)).toBe('2026-01-01')
    })

    it('parseLocalDate_UtcMidnightWestOfUtc_UsesLocalCalendarDateNotUtc', () => {
      // 2026-01-01T00:00:00Z is 2025-12-31 21:00 local time in UTC-3 (America/Sao_Paulo).
      vi.stubEnv('TZ', 'America/Sao_Paulo')

      const result = parseLocalDate('2026-01-01T00:00:00Z')

      expect(toLocalDateString(result)).toBe('2025-12-31')
    })
  })

  describe('addMonths', () => {
    it('addMonths_OneMonthAcrossLocalDateBoundaryWestOfUtc_AddsFromLocalDateNotUtc', () => {
      // 2026-01-01T02:00:00Z is 2025-12-31 23:00 local time in UTC-3 (America/Sao_Paulo).
      vi.stubEnv('TZ', 'America/Sao_Paulo')

      const start = parseLocalDate('2026-01-01T02:00:00Z')
      const result = addMonths(start, 1)

      expect(toLocalDateString(result)).toBe('2026-01-31')
    })

    it('addMonths_TwelveMonths_AdvancesYearByOne', () => {
      const start = new Date(2026, 0, 15)
      const result = addMonths(start, 12)

      expect(toLocalDateString(result)).toBe('2027-01-15')
    })
  })

  describe('isFutureLocalDate', () => {
    it('isFutureLocalDate_FarFutureDate_ReturnsTrueInAnyTimezone', () => {
      vi.stubEnv('TZ', 'America/Sao_Paulo')

      expect(isFutureLocalDate('2099-01-01T00:00:00Z')).toBe(true)
    })

    it('isFutureLocalDate_Today_ReturnsFalse', () => {
      vi.stubEnv('TZ', 'UTC')

      expect(isFutureLocalDate(`${toLocalDateString(new Date())}T00:00:00Z`)).toBe(false)
    })

    it('isFutureLocalDate_PastDate_ReturnsFalse', () => {
      expect(isFutureLocalDate('2020-01-01T00:00:00Z')).toBe(false)
    })
  })
})
