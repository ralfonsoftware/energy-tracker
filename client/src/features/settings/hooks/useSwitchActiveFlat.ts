import { useMutation, useQueryClient } from '@tanstack/react-query'
import { updateUserSettings } from '@/features/settings/api/settingsApi'

type SwitchActiveFlatVars = {
  flatId: string
  locale: string
  previousFlatId: string | undefined
}

export function useSwitchActiveFlat() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ flatId, locale }: SwitchActiveFlatVars) =>
      updateUserSettings({ locale, activeFlatId: flatId }),
    onSuccess: (_data, { previousFlatId }) => {
      queryClient.invalidateQueries({ queryKey: ['settings'] })
      queryClient.invalidateQueries({ queryKey: ['flats'] })
      if (previousFlatId) {
        queryClient.invalidateQueries({ queryKey: ['dashboard', previousFlatId] })
        queryClient.invalidateQueries({ queryKey: ['readings', previousFlatId] })
        queryClient.invalidateQueries({ queryKey: ['tariffs', previousFlatId] })
      }
    },
  })
}
