import { useMutation } from '@tanstack/react-query'
import { createFlat, type CreateFlatBody } from '@/features/settings/api/settingsApi'

export function useCreateFlat() {
  return useMutation({
    mutationFn: (body: CreateFlatBody) => createFlat(body),
  })
}
