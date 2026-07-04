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

export const updateUserSettings = (body: { locale: string; activeFlatId?: string | null }) =>
  apiClient.put<UserSettings>('/user/settings', body)

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

export type FlatSummary = {
  flatId: string
  name: string
  annualKwhBaseline: number
  spikeThreshold: number
  plannedAnnualSpend: number | null
}

export const getFlats = () => apiClient.get<FlatSummary[]>('/flats')

export type CreateFlatBody = {
  name: string
  annualKwhBaseline: number
  plannedAnnualSpend: number | null
}

export const createFlat = (body: CreateFlatBody) =>
  apiClient.post<FlatSummary>('/flats', body)

export const deleteFlat = (flatId: string) =>
  apiClient.delete<void>(`/flats/${flatId}`)

export type AuthMe = { clientPrincipal: { userDetails: string } | null }
export const getAuthMe = () =>
  fetch('/.auth/me').then(r => {
    if (!r.ok) throw new Error(`/.auth/me returned ${r.status}`)
    return r.json() as Promise<AuthMe>
  })
