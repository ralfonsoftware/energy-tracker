import { useQuery } from '@tanstack/react-query'
import { getAuthMe } from '../api/settingsApi'

export function useAuthMe() {
  return useQuery({
    queryKey: ['auth-me'],
    queryFn: getAuthMe,
    staleTime: 10 * 60 * 1_000,
    retry: false,
  })
}
