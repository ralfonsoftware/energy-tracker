import { z } from 'zod'

export const flatNameSchema = z.object({
  name: z.string().trim().min(1, 'Flat name is required'),
})
export type FlatNameFormValues = z.infer<typeof flatNameSchema>

export const contractSchema = z.object({
  annualKwhBaseline: z.string().min(1, 'Required'),
  pricePerKwh: z.string().min(1, 'Required'),
  monthlyBaseFee: z.string().min(1, 'Required'),
  providerName: z.string().optional(),
  contractStartDate: z.string().optional(),
  contractDurationMonths: z.number().nullable().optional(),
  plannedAnnualSpend: z.string().optional(),
})

export type ContractFormValues = z.infer<typeof contractSchema>
