import { apiClient } from '@/lib/apiClient'

export type UserSettings = {
  locale: string | null
  hasFlat: boolean
}

export const getUserSettings = () =>
  apiClient.get<UserSettings>('/user/settings')

export const updateUserSettings = (locale: string) =>
  apiClient.put<UserSettings>('/user/settings', { locale })
