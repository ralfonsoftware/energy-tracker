import { apiClient } from '@/lib/apiClient'

export type UserSettings = {
  locale: string | null
  hasFlat: boolean
  flatId?: string
  flatName?: string
  annualKwhBaseline?: number
  plannedAnnualSpend?: number | null
}

export const getUserSettings = () =>
  apiClient.get<UserSettings>('/user/settings')

export const updateUserSettings = (locale: string) =>
  apiClient.put<UserSettings>('/user/settings', { locale })

export type PatchFlatBody = {
  name?: string
  annualKwhBaseline?: number
  plannedAnnualSpend?: number | null
}

export type FlatData = {
  flatId: string
  name: string
  annualKwhBaseline: number
  plannedAnnualSpend: number | null
}

export const patchFlat = (flatId: string, body: PatchFlatBody) =>
  apiClient.patch<FlatData>(`/flats/${flatId}`, body)

export type AuthMe = { clientPrincipal: { userDetails: string } | null }
export const getAuthMe = () =>
  fetch('/.auth/me').then(r => {
    if (!r.ok) throw new Error(`/.auth/me returned ${r.status}`)
    return r.json() as Promise<AuthMe>
  })
