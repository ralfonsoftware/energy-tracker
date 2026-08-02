import { apiClient } from '@/lib/apiClient'

export type SubmitReadingRequest = { kwhValue: number; readingDate: string }
export type ReadingResponse = {
  readingId: string
  kwhValue: number
  readingDate: string
  isCorrected: boolean
  originalKwhValue: number | null
  rowVersion: string
}

export const submitReading = (flatId: string, body: SubmitReadingRequest) =>
  apiClient.post<ReadingResponse>(`/flats/${flatId}/readings`, body)

export type ReadingHistoryPage = { items: ReadingResponse[]; totalCount: number }

export const getReadingHistory = (flatId: string, params: { skip: number; take: number }) =>
  apiClient.get<ReadingHistoryPage>(`/flats/${flatId}/readings?skip=${params.skip}&take=${params.take}`)

export type PatchReadingRequest = { kwhValue: number; rowVersion: string }

export const patchReading = (flatId: string, readingId: string, body: PatchReadingRequest) =>
  apiClient.patch<ReadingResponse>(`/flats/${flatId}/readings/${readingId}`, body)
