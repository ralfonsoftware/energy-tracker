import { z } from 'zod'

export const tariffFormSchema = z.object({
  effectiveDate: z.string().min(1, 'Required'),
  pricePerKwh: z.string().min(1, 'Required'),
  monthlyBaseFee: z.string().min(1, 'Required'),
  providerName: z.string().optional(),
  contractStartDate: z.string().optional(),
  contractDurationMonths: z.number().nullable().optional(),
})

export type TariffFormValues = z.infer<typeof tariffFormSchema>
