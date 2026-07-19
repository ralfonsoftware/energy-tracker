import { useQuery } from '@tanstack/react-query'
import { getUserSettings } from '../api/settingsApi'

export function useUserSettings() {
  const { data: settings, isLoading, isError, refetch } = useQuery({
    queryKey: ['settings'],
    queryFn: getUserSettings,
    staleTime: 5 * 60 * 1_000,
    retry: false,
  })
  return { settings, isLoading, isError, refetch }
}
