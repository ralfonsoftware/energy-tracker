import { useQuery } from '@tanstack/react-query'
import { getFlatStructure } from '@/features/flat-structure/api/flatStructureApi'

export function useFlatStructure(flatId: string | undefined) {
  return useQuery({
    queryKey: ['flat-structure', flatId],
    queryFn: () => getFlatStructure(flatId as string),
    enabled: !!flatId,
  })
}
