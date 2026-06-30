import { parseLocaleNumber } from './localeNumber'

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
