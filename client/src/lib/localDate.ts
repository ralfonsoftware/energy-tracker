function assertValidDate(date: Date, context: string): void {
  if (Number.isNaN(date.getTime())) {
    throw new Error(`${context}: resulting date is invalid`)
  }
}

export function toLocalDateString(date: Date): string {
  assertValidDate(date, 'toLocalDateString')
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
  const result = new Date(d.getFullYear(), d.getMonth(), d.getDate())
  assertValidDate(result, `parseLocalDate('${isoDateTime}')`)
  return result
}

// Converts a YYYY-MM-DD calendar date (e.g. from <input type="date">) into the ISO
// instant for LOCAL midnight on that date — the write-side counterpart to parseLocalDate's
// local-calendar-date extraction, so a date picked and later read back via parseLocalDate
// resolves to the same calendar day regardless of the user's timezone.
export function toLocalMidnightIsoString(yyyyMmDd: string): string {
  const result = new Date(`${yyyyMmDd}T00:00:00`)
  assertValidDate(result, `toLocalMidnightIsoString('${yyyyMmDd}')`)
  return result.toISOString()
}

export function addMonths(date: Date, months: number): Date {
  const year = date.getFullYear()
  const targetMonthIndex = date.getMonth() + months
  const lastValidDayOfTargetMonth = new Date(year, targetMonthIndex + 1, 0).getDate()
  const clampedDay = Math.min(date.getDate(), lastValidDayOfTargetMonth)
  return new Date(year, targetMonthIndex, clampedDay)
}

export function isFutureLocalDate(isoDateTime: string): boolean {
  return toLocalDateString(parseLocalDate(isoDateTime)) > toLocalDateString(new Date())
}
