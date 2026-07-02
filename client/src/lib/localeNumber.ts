export function parseLocaleNumber(value: string, locale: string): number {
  const isDE = locale.startsWith('de')
  if (isDE) {
    const commaCount = (value.match(/,/g) ?? []).length
    if (commaCount > 1) return NaN
    if (value.includes('.')) {
      const commaIndex = value.indexOf(',')
      const integerPart = commaIndex === -1 ? value : value.slice(0, commaIndex)
      if (!/^-?\d{1,3}(\.\d{3})+$/.test(integerPart)) return NaN
    }
    return parseFloat(value.replace(/\./g, '').replace(',', '.'))
  }
  return parseFloat(value.replace(/,/g, ''))
}

export function formatNumberForInput(value: number, locale: string): string {
  const isDE = locale.startsWith('de')
  return isDE ? String(value).replace('.', ',') : String(value)
}
