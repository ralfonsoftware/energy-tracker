import { useMutation, useQueryClient } from '@tanstack/react-query'
import { patchFlat, type UserSettings } from '../api/settingsApi'

export function usePatchFlat() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ flatId, body }: { flatId: string; body: Parameters<typeof patchFlat>[1] }) =>
      patchFlat(flatId, body),
    onMutate: async ({ body }) => {
      await queryClient.cancelQueries({ queryKey: ['settings'] })
      const previous = queryClient.getQueryData<UserSettings>(['settings'])
      queryClient.setQueryData<UserSettings>(['settings'], old => {
        if (!old) return old
        const update: Partial<UserSettings> = {}
        if (body.name !== undefined) update.flatName = body.name
        if (body.annualKwhBaseline !== undefined) update.annualKwhBaseline = body.annualKwhBaseline
        if ('plannedAnnualSpend' in body) update.plannedAnnualSpend = body.plannedAnnualSpend
        return { ...old, ...update }
      })
      return { previous }
    },
    onError: (_err, _vars, ctx) => {
      if (ctx?.previous) queryClient.setQueryData(['settings'], ctx.previous)
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['settings'] })
    },
  })
}
