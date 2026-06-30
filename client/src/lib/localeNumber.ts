export function parseLocaleNumber(value: string, locale: string): number {
  const isDE = locale.startsWith('de')
  const normalized = isDE
    ? value.replace(/\./g, '').replace(',', '.')
    : value.replace(/,/g, '')
  return parseFloat(normalized)
}
