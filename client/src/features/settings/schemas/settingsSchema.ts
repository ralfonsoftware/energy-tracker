import { z } from 'zod'

export const baselineEditSchema = z.object({
  annualKwhBaseline: z.string().min(1, 'Required'),
  plannedAnnualSpend: z.string().optional(),
})

export type BaselineEditFormValues = z.infer<typeof baselineEditSchema>
