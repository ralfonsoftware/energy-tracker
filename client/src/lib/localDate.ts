export function toLocalDateString(date: Date): string {
  const year = date.getFullYear()
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

// Extracts the LOCAL calendar date of the instant an ISO datetime names — never the
// UTC calendar date. Contract dates are civil dates with no meaningful time-of-day;
// using UTC parts here has caused the same off-by-one-day bug three times in this codebase.
export function parseLocalDate(isoDateTime: string): Date {
  const d = new Date(isoDateTime)
  return new Date(d.getFullYear(), d.getMonth(), d.getDate())
}

export function addMonths(date: Date, months: number): Date {
  const result = new Date(date.getFullYear(), date.getMonth(), date.getDate())
  result.setMonth(result.getMonth() + months)
  return result
}

export function isFutureLocalDate(isoDateTime: string): boolean {
  return toLocalDateString(parseLocalDate(isoDateTime)) > toLocalDateString(new Date())
}
