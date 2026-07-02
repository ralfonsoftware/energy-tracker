import { useQuery } from '@tanstack/react-query'
import { getReadingHistory } from '@/features/readings/api/readingApi'

export function useReadingHistory(flatId: string | undefined) {
  return useQuery({
    queryKey: ['readings', flatId],
    queryFn: () => getReadingHistory(flatId as string),
    enabled: !!flatId,
  })
}
