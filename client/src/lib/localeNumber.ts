export function parseLocaleNumber(value: string, locale: string): number {
  const isDE = locale.startsWith('de')
  const normalized = isDE
    ? value.replace(/\./g, '').replace(',', '.')
    : value.replace(/,/g, '')
  return parseFloat(normalized)
}

export function formatNumberForInput(value: number, locale: string): string {
  const isDE = locale.startsWith('de')
  return isDE ? String(value).replace('.', ',') : String(value)
}
