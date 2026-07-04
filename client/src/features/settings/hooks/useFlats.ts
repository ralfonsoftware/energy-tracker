import { useQuery } from '@tanstack/react-query'
import { getFlats } from '@/features/settings/api/settingsApi'

export function useFlats() {
  return useQuery({
    queryKey: ['flats'],
    queryFn: getFlats,
    staleTime: 60 * 1_000,
  })
}
