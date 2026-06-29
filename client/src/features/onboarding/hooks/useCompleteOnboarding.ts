import { useMutation, useQueryClient } from '@tanstack/react-query'
import { completeOnboarding } from '../api/onboardingApi'

export function useCompleteOnboarding() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: completeOnboarding,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['settings'] })
    },
  })
}
