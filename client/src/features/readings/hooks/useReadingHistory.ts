import { useInfiniteQuery } from '@tanstack/react-query'
import { getReadingHistory } from '@/features/readings/api/readingApi'

const PAGE_SIZE = 20

export function useReadingHistory(flatId: string | undefined) {
  return useInfiniteQuery({
    queryKey: ['readings', flatId],
    queryFn: ({ pageParam }) => getReadingHistory(flatId as string, { skip: pageParam, take: PAGE_SIZE }),
    initialPageParam: 0,
    getNextPageParam: (lastPage, allPages) => {
      const loaded = allPages.reduce((sum, page) => sum + page.items.length, 0)
      return loaded < lastPage.totalCount ? loaded : undefined
    },
    enabled: !!flatId,
  })
}
