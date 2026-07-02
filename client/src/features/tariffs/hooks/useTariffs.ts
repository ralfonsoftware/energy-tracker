import { useQuery } from '@tanstack/react-query'
import { getTariffs } from '@/features/tariffs/api/tariffApi'

export function useTariffs(flatId: string | undefined) {
  return useQuery({
    queryKey: ['tariffs', flatId],
    queryFn: () => getTariffs(flatId as string),
    enabled: !!flatId,
  })
}
