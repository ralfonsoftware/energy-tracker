import { render, screen, waitFor, act } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { ReadingHistorySheet } from '@/features/readings/components/ReadingHistorySheet'
import type { ReadingResponse } from '@/features/readings/api/readingApi'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

vi.mock('@/features/readings/hooks/useReadingHistory')
vi.mock('@/features/readings/hooks/usePatchReading')
import { useReadingHistory } from '@/features/readings/hooks/useReadingHistory'
import { usePatchReading } from '@/features/readings/hooks/usePatchReading'
const mockUseReadingHistory = vi.mocked(useReadingHistory)
const mockUsePatchReading = vi.mocked(usePatchReading)

type MutateOptions = { onSuccess?: () => void; onError?: () => void }

const sampleReadings: ReadingResponse[] = [
  {
    readingId: 'reading-1',
    kwhValue: 120,
    readingDate: '2026-06-30T08:00:00+02:00',
    isCorrected: false,
    originalKwhValue: null,
    rowVersion: 'AQID',
  },
  {
    readingId: 'reading-2',
    kwhValue: 150,
    readingDate: '2026-06-29T08:00:00+02:00',
    isCorrected: true,
    originalKwhValue: 100,
    rowVersion: 'AQID',
  },
]

function setupReadingHistory(options?: { isLoading?: boolean; isError?: boolean; data?: ReadingResponse[] }) {
  const refetch = vi.fn().mockResolvedValue({ data: options?.data })
  mockUseReadingHistory.mockReturnValue({
    data: options?.data,
    isLoading: options?.isLoading ?? false,
    isError: options?.isError ?? false,
    refetch,
  } as unknown as ReturnType<typeof useReadingHistory>)
  return { refetch }
}

function setupPatchReading(options?: { isPending?: boolean; isError?: boolean }) {
  const mutate = vi.fn<(variables: unknown, opts?: MutateOptions) => void>()
  mockUsePatchReading.mockReturnValue({
    mutate,
    isPending: options?.isPending ?? false,
    isError: options?.isError ?? false,
  } as unknown as ReturnType<typeof usePatchReading>)
  return { mutate }
}

describe('ReadingHistorySheet', () => {
  beforeEach(() => {
    mockUseReadingHistory.mockReset()
    mockUsePatchReading.mockReset()
  })

  it('ReadingHistorySheet_Loading_RendersSkeletonWithNoListItems', () => {
    setupReadingHistory({ isLoading: true })
    setupPatchReading()

    render(<ReadingHistorySheet flatId="flat-1" />)

    expect(screen.queryAllByRole('listitem')).toHaveLength(0)
  })

  it('ReadingHistorySheet_EmptyList_RendersEmptyStateTextAndNoListItems', () => {
    setupReadingHistory({ data: [] })
    setupPatchReading()

    render(<ReadingHistorySheet flatId="flat-1" />)

    expect(screen.getByText('history.empty')).toBeInTheDocument()
    expect(screen.queryAllByRole('listitem')).toHaveLength(0)
  })

  it('ReadingHistorySheet_PopulatedList_RendersFormattedDateAndKwhForEachReading', () => {
    setupReadingHistory({ data: sampleReadings })
    setupPatchReading()

    render(<ReadingHistorySheet flatId="flat-1" />)

    expect(screen.getAllByRole('listitem')).toHaveLength(2)
    expect(screen.getByText(/120/)).toBeInTheDocument()
    expect(screen.getByText(/150/)).toBeInTheDocument()
  })

  it('ReadingHistorySheet_CorrectedEntry_RendersCorrectedNoteText', () => {
    setupReadingHistory({ data: sampleReadings })
    setupPatchReading()

    render(<ReadingHistorySheet flatId="flat-1" />)

    expect(screen.getByText('history.correctedNote')).toBeInTheDocument()
  })

  it('ReadingHistorySheet_LoadError_ShowsAlertAndRetryCallsRefetch', async () => {
    const user = userEvent.setup()
    const { refetch } = setupReadingHistory({ isError: true })
    setupPatchReading()

    render(<ReadingHistorySheet flatId="flat-1" />)

    expect(screen.getByRole('alert')).toHaveTextContent('history.loadError')
    await user.click(screen.getByRole('button', { name: 'history.retry' }))

    expect(refetch).toHaveBeenCalled()
  })

  it('ReadingHistorySheet_TapEntry_SwitchesToEditViewWithPrefilledAutoFocusedInput', async () => {
    const user = userEvent.setup()
    setupReadingHistory({ data: sampleReadings })
    setupPatchReading()

    render(<ReadingHistorySheet flatId="flat-1" />)
    await user.click(screen.getAllByRole('button')[0])

    const kwhInput = screen.getByDisplayValue('120') as HTMLInputElement
    await waitFor(() => expect(document.activeElement).toBe(kwhInput))
  })

  it('ReadingHistorySheet_SaveInEditView_CallsMutateAndReturnsToListOnSuccess', async () => {
    const user = userEvent.setup()
    setupReadingHistory({ data: sampleReadings })
    const { mutate } = setupPatchReading()

    render(<ReadingHistorySheet flatId="flat-1" />)
    await user.click(screen.getAllByRole('button')[0])

    const kwhInput = screen.getByDisplayValue('120') as HTMLInputElement
    await user.clear(kwhInput)
    await user.type(kwhInput, '130')
    await user.click(screen.getByRole('button', { name: 'history.editSaveButton' }))

    expect(mutate).toHaveBeenCalledWith(
      { readingId: 'reading-1', kwhValue: 130, rowVersion: 'AQID' },
      expect.objectContaining({ onSuccess: expect.any(Function) })
    )

    const [, mutateOptions] = mutate.mock.calls[0] as [unknown, MutateOptions]
    act(() => {
      mutateOptions.onSuccess?.()
    })

    expect(screen.getAllByRole('listitem')).toHaveLength(2)
  })
})
