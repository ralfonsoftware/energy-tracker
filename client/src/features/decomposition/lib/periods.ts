import { toLocalDateString } from '@/lib/localDate'

export type PeriodOption = 'thisWeek' | 'thisMonth' | 'lastMonth' | 'thisYear' | 'custom'

export function resolvePeriodRange(
  option: Exclude<PeriodOption, 'custom'>,
  today: Date = new Date()
): { startDate: string; endDate: string } {
  const endDate = toLocalDateString(today)

  switch (option) {
    case 'thisWeek': {
      const dayOfWeek = today.getDay()
      const diffToMonday = dayOfWeek === 0 ? 6 : dayOfWeek - 1
      const monday = new Date(today.getFullYear(), today.getMonth(), today.getDate() - diffToMonday)
      return { startDate: toLocalDateString(monday), endDate }
    }
    case 'thisMonth': {
      const start = new Date(today.getFullYear(), today.getMonth(), 1)
      return { startDate: toLocalDateString(start), endDate }
    }
    case 'lastMonth': {
      const start = new Date(today.getFullYear(), today.getMonth() - 1, 1)
      const end = new Date(today.getFullYear(), today.getMonth(), 0)
      return { startDate: toLocalDateString(start), endDate: toLocalDateString(end) }
    }
    case 'thisYear': {
      const start = new Date(today.getFullYear(), 0, 1)
      return { startDate: toLocalDateString(start), endDate }
    }
  }
}
