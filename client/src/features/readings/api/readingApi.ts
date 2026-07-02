import { apiClient } from '@/lib/apiClient'

export type SubmitReadingRequest = { kwhValue: number; readingDate: string }
export type ReadingResponse = {
  readingId: string
  kwhValue: number
  readingDate: string
  isCorrected: boolean
  originalKwhValue: number | null
}

export const submitReading = (flatId: string, body: SubmitReadingRequest) =>
  apiClient.post<ReadingResponse>(`/flats/${flatId}/readings`, body)

export const getReadingHistory = (flatId: string) =>
  apiClient.get<ReadingResponse[]>(`/flats/${flatId}/readings`)

export type PatchReadingRequest = { kwhValue: number }

export const patchReading = (flatId: string, readingId: string, body: PatchReadingRequest) =>
  apiClient.patch<ReadingResponse>(`/flats/${flatId}/readings/${readingId}`, body)
