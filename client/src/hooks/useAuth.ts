import { useQuery } from '@tanstack/react-query'
import { getMe, type SwaAuthUser } from '@/lib/authClient'

export function useAuth(): { user: SwaAuthUser | null; isLoading: boolean } {
  const { data, isLoading } = useQuery({
    queryKey: ['auth', 'me'],
    queryFn: getMe,
    staleTime: 5 * 60 * 1_000,
    retry: false,
  })
  return { user: data ?? null, isLoading }
}
