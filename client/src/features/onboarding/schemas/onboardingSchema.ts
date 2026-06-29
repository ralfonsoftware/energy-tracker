import { z } from 'zod'

export const flatNameSchema = z.object({
  name: z.string().trim().min(1, 'Flat name is required'),
})

export type FlatNameFormValues = z.infer<typeof flatNameSchema>
