import { useQuery } from '@tanstack/react-query'
import { getDecomposition } from '@/features/decomposition/api/decompositionApi'

export const useDecomposition = (
  flatId: string | undefined,
  startDate: string | undefined,
  endDate: string | undefined
) =>
  useQuery({
    queryKey: ['decomposition', flatId, { startDate, endDate }],
    queryFn: () => getDecomposition(flatId as string, startDate as string, endDate as string),
    enabled: !!flatId && !!startDate && !!endDate,
  })
