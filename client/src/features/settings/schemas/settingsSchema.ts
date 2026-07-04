import { z } from 'zod'

export const baselineEditSchema = z.object({
  annualKwhBaseline: z.string().min(1, 'Required'),
  plannedAnnualSpend: z.string().optional(),
})

export type BaselineEditFormValues = z.infer<typeof baselineEditSchema>

export const addFlatSchema = z.object({
  name: z.string().trim().min(1, 'Required'),
  annualKwhBaseline: z.string().min(1, 'Required'),
})

export type AddFlatFormValues = z.infer<typeof addFlatSchema>
