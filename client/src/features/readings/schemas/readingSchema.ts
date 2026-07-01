import { z } from 'zod'

export const readingSheetSchema = z.object({
  kwhValue: z.string().min(1, 'Required'),
  readingDate: z.string().min(1, 'Required'),
})

export type ReadingSheetFormValues = z.infer<typeof readingSheetSchema>
