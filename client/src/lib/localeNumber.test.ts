import { parseLocaleNumber, formatNumberForInput } from './localeNumber'

describe('parseLocaleNumber', () => {
  describe('de-DE locale', () => {
    it('parses decimal comma', () => {
      expect(parseLocaleNumber('1,27', 'de-DE')).toBe(1.27)
    })

    it('parses thousands dot', () => {
      expect(parseLocaleNumber('1.500', 'de-DE')).toBe(1500)
    })

    it('parses combined thousands and decimal', () => {
      expect(parseLocaleNumber('1.500,27', 'de-DE')).toBe(1500.27)
    })

    it('returns NaN for multi-comma input instead of silently truncating', () => {
      expect(parseLocaleNumber('1,234,56', 'de-DE')).toBeNaN()
    })

    it('returns NaN for a lone dot used as a decimal separator instead of silently stripping it', () => {
      expect(parseLocaleNumber('0.28', 'de-DE')).toBeNaN()
    })

    it('returns NaN when a dot appears after the decimal comma instead of silently stripping it', () => {
      expect(parseLocaleNumber('1,234.56', 'de-DE')).toBeNaN()
    })

    it('still parses a thousands dot followed by only 1-2 digits as invalid, not a truncated group', () => {
      expect(parseLocaleNumber('12.34', 'de-DE')).toBeNaN()
    })
  })

  describe('en-US locale', () => {
    it('parses decimal dot', () => {
      expect(parseLocaleNumber('1.27', 'en-US')).toBe(1.27)
    })

    it('parses thousands comma', () => {
      expect(parseLocaleNumber('1,500', 'en-US')).toBe(1500)
    })

    it('parses combined thousands and decimal', () => {
      expect(parseLocaleNumber('1,500.27', 'en-US')).toBe(1500.27)
    })
  })

  describe('edge cases', () => {
    it('returns NaN for invalid string', () => {
      expect(parseLocaleNumber('abc', 'en-US')).toBeNaN()
    })

    it('returns NaN for empty string', () => {
      expect(parseLocaleNumber('', 'en-US')).toBeNaN()
    })

    it('returns 0 for zero', () => {
      expect(parseLocaleNumber('0', 'de-DE')).toBe(0)
    })
  })
})

describe('formatNumberForInput', () => {
  it('formats a fractional value with a comma for de-DE', () => {
    expect(formatNumberForInput(120.5, 'de-DE')).toBe('120,5')
  })

  it('formats a fractional value with a dot for en-US', () => {
    expect(formatNumberForInput(120.5, 'en-US')).toBe('120.5')
  })

  it('formats an integer value unchanged for both locales', () => {
    expect(formatNumberForInput(3500, 'de-DE')).toBe('3500')
    expect(formatNumberForInput(3500, 'en-US')).toBe('3500')
  })

  it('round-trips through parseLocaleNumber without precision loss for de-DE', () => {
    const formatted = formatNumberForInput(120.53, 'de-DE')
    expect(parseLocaleNumber(formatted, 'de-DE')).toBe(120.53)
  })

  it('round-trips through parseLocaleNumber without precision loss for en-US', () => {
    const formatted = formatNumberForInput(120.53, 'en-US')
    expect(parseLocaleNumber(formatted, 'en-US')).toBe(120.53)
  })
})
