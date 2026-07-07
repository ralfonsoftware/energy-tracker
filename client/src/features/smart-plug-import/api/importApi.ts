import { apiClient } from '@/lib/apiClient'

export type ImportStatus = 'Pending' | 'Processing' | 'Complete' | 'Failed'
export type ImportErrorCategory = 'DataUnreadable' | 'ProcessingFailed' | 'ServiceUnavailable'
export type DetectedFileType = 'EveHome' | 'Meross'

export type UploadImportResponse = { importJobId: string }

export type ImportJobStatusResponse = {
  importJobId: string
  status: ImportStatus
  createdAt: string
  completedAt: string | null
  errorCategory: ImportErrorCategory | null
  gapNotifications: string | null
}

export type GapNotification = { plugId: string; start: string; end: string }

export const uploadImport = (flatId: string, file: File, plugId: string) => {
  const formData = new FormData()
  formData.append('file', file)
  formData.append('plugId', plugId)
  return apiClient.postForm<UploadImportResponse>(`/flats/${flatId}/imports`, formData)
}

export const getImportStatus = (flatId: string, jobId: string) =>
  apiClient.get<ImportJobStatusResponse>(`/flats/${flatId}/imports/${jobId}`)

export const parseGapNotifications = (raw: string | null): GapNotification[] => {
  if (!raw) return []
  try {
    const parsed: unknown = JSON.parse(raw)
    return Array.isArray(parsed) ? (parsed as GapNotification[]) : []
  } catch {
    return []
  }
}

export const detectFileType = (fileName: string): DetectedFileType | null => {
  const lower = fileName.toLowerCase()
  if (lower.endsWith('.xlsx')) return 'EveHome'
  if (lower.endsWith('.csv')) return 'Meross'
  return null
}
