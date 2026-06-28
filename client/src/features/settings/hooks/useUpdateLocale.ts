import { useMutation, useQueryClient } from '@tanstack/react-query'
import { updateUserSettings } from '../api/settingsApi'
import i18n from '@/lib/i18n'

export function useUpdateLocale() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: updateUserSettings,
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ['settings'] })
      if (data.locale) i18n.changeLanguage(data.locale)
    },
  })
}
