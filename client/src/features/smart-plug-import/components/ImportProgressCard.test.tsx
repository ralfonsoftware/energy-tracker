import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { ImportProgressCard } from './ImportProgressCard'
import type { ImportJobStatusResponse } from '@/features/smart-plug-import/api/importApi'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (k: string, opts?: Record<string, unknown>) => (opts?.range ? `${k}:${opts.range}` : k),
  }),
}))

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom')
  return { ...actual, useNavigate: () => mockNavigate }
})
const mockNavigate = vi.fn()

vi.mock('@/features/smart-plug-import/hooks/useImportJobStatus')
import { useImportJobStatus } from '@/features/smart-plug-import/hooks/useImportJobStatus'
const mockUseImportJobStatus = vi.mocked(useImportJobStatus)

const dismiss = vi.fn()

type Job = { importJobId: string; fileName: string; statusData?: ImportJobStatusResponse; isError?: boolean }

function mockJobs(jobs: Job[]) {
  mockUseImportJobStatus.mockReturnValue({ jobs, dismiss } as unknown as ReturnType<typeof useImportJobStatus>)
}

describe('ImportProgressCard', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('ImportProgressCard_NoActiveJobs_RendersNothing', () => {
    mockJobs([])
    const { container } = render(<ImportProgressCard flatId="flat-1" />)

    expect(container).toBeEmptyDOMElement()
  })

  it('ImportProgressCard_ProcessingJob_RendersProcessingCard', () => {
    mockJobs([{ importJobId: 'job-1', fileName: 'meross.csv', statusData: undefined }])

    render(<ImportProgressCard flatId="flat-1" />)

    expect(screen.getByText('progress.processingTitle')).toBeInTheDocument()
    expect(screen.getByText('meross.csv')).toBeInTheDocument()
  })

  it('ImportProgressCard_FailedDataUnreadable_RendersErrorRowWithRemoveButton', async () => {
    const user = userEvent.setup()
    mockJobs([
      {
        importJobId: 'job-1',
        fileName: 'bad.csv',
        statusData: {
          importJobId: 'job-1', status: 'Failed', createdAt: '2026-07-01T00:00:00Z', completedAt: '2026-07-01T00:01:00Z',
          errorCategory: 'DataUnreadable', gapNotifications: null,
        },
      },
    ])

    render(<ImportProgressCard flatId="flat-1" />)

    expect(screen.getByText('progress.errorDataUnreadable')).toBeInTheDocument()
    await user.click(screen.getByText('remove'))
    expect(dismiss).toHaveBeenCalledWith('job-1')
  })

  it('ImportProgressCard_FailedProcessingFailed_RendersRetryRowThatNavigates', async () => {
    const user = userEvent.setup()
    mockJobs([
      {
        importJobId: 'job-2',
        fileName: 'meross.csv',
        statusData: {
          importJobId: 'job-2', status: 'Failed', createdAt: '2026-07-01T00:00:00Z', completedAt: '2026-07-01T00:01:00Z',
          errorCategory: 'ProcessingFailed', gapNotifications: null,
        },
      },
    ])

    render(<ImportProgressCard flatId="flat-1" />)

    expect(screen.getByText('progress.errorProcessingFailed')).toBeInTheDocument()
    await user.click(screen.getByText('progress.retry'))
    expect(dismiss).toHaveBeenCalledWith('job-2')
    expect(mockNavigate).toHaveBeenCalledWith('/decomposition/import')
  })

  it('ImportProgressCard_PollingError_RendersServiceUnavailableRowThatNavigates', async () => {
    const user = userEvent.setup()
    mockJobs([{ importJobId: 'job-4', fileName: 'meross.csv', statusData: undefined, isError: true }])

    render(<ImportProgressCard flatId="flat-1" />)

    expect(screen.getByText('progress.errorServiceUnavailable')).toBeInTheDocument()
    await user.click(screen.getByText('progress.retry'))
    expect(dismiss).toHaveBeenCalledWith('job-4')
    expect(mockNavigate).toHaveBeenCalledWith('/decomposition/import')
  })

  it('ImportProgressCard_FailedWithoutErrorCategory_FallsBackToProcessingFailedMessage', () => {
    mockJobs([
      {
        importJobId: 'job-5',
        fileName: 'meross.csv',
        statusData: {
          importJobId: 'job-5', status: 'Failed', createdAt: '2026-07-01T00:00:00Z', completedAt: '2026-07-01T00:01:00Z',
          errorCategory: null, gapNotifications: null,
        },
      },
    ])

    render(<ImportProgressCard flatId="flat-1" />)

    expect(screen.getByText('progress.errorProcessingFailed')).toBeInTheDocument()
  })

  it('ImportProgressCard_CompleteWithGapNotifications_RendersGapNoticeUntilDismissed', async () => {
    const user = userEvent.setup()
    mockJobs([
      {
        importJobId: 'job-3',
        fileName: 'eve.xlsx',
        statusData: {
          importJobId: 'job-3', status: 'Complete', createdAt: '2026-07-01T00:00:00Z', completedAt: '2026-07-01T00:01:00Z',
          errorCategory: null, gapNotifications: JSON.stringify([{ plugId: 'plug-1', start: '2026-06-15', end: '2026-06-16' }]),
        },
      },
    ])

    render(<ImportProgressCard flatId="flat-1" />)

    expect(screen.getByText(/progress\.gapNotice:/)).toBeInTheDocument()
    await user.click(screen.getByText('progress.dismiss'))
    expect(dismiss).toHaveBeenCalledWith('job-3')
  })
})
