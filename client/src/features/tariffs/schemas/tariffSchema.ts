import { z } from 'zod'

export const tariffFormSchema = z.object({
  pricePerKwh: z.string().min(1, 'Required'),
  monthlyBaseFee: z.string().min(1, 'Required'),
  providerName: z.string().optional(),
  contractStartDate: z.string().min(1, 'Required').regex(/^\d{4}-\d{2}-\d{2}$/, 'Invalid date'),
  contractDurationMonths: z.number().nullable().optional(),
})

export type TariffFormValues = z.infer<typeof tariffFormSchema>
