import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { DecompositionTab } from '@/features/decomposition/components/DecompositionTab'
import type { DecompositionResponse } from '@/features/decomposition/api/decompositionApi'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom')
  return { ...actual, useNavigate: () => mockNavigate }
})
const mockNavigate = vi.fn()

vi.mock('@/features/decomposition/hooks/useDecomposition')
import { useDecomposition } from '@/features/decomposition/hooks/useDecomposition'
const mockUseDecomposition = vi.mocked(useDecomposition)

function mockDecomposition(overrides: {
  data?: DecompositionResponse
  isPending?: boolean
  isError?: boolean
  refetch?: ReturnType<typeof vi.fn>
}) {
  mockUseDecomposition.mockReturnValue({
    data: overrides.data,
    isPending: overrides.isPending ?? false,
    isError: overrides.isError ?? false,
    refetch: overrides.refetch ?? vi.fn(),
  } as unknown as ReturnType<typeof useDecomposition>)
}

function makeResponse(overrides: Partial<DecompositionResponse> = {}): DecompositionResponse {
  return {
    period: { startDate: '2026-06-01', endDate: '2026-06-17' },
    totalKwh: 100,
    totalCost: 25,
    isUnavailable: false,
    hasInterpolatedData: false,
    residual: { kwh: 10, cost: 2.5 },
    rooms: [{ roomId: 'room-1', roomName: 'Living Room', kwh: 62.3, cost: 14.43, devices: [] }],
    ...overrides,
  }
}

describe('DecompositionTab', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('DecompositionTab_Loading_RendersThreeSkeletonBlocks', () => {
    mockDecomposition({ isPending: true })
    const { container } = render(<DecompositionTab flatId="flat-1" />)

    expect(container.querySelectorAll('.animate-pulse')).toHaveLength(3)
  })

  it('DecompositionTab_Error_RendersAlertAndRetryCallsRefetch', async () => {
    const user = userEvent.setup()
    const refetch = vi.fn()
    mockDecomposition({ isError: true, refetch })

    render(<DecompositionTab flatId="flat-1" />)

    expect(screen.getByRole('alert')).toHaveTextContent('loadError')
    await user.click(screen.getByRole('button', { name: 'retry' }))
    expect(refetch).toHaveBeenCalled()
  })

  it('DecompositionTab_Unavailable_RendersUnavailableStateNotResidualCard', () => {
    mockDecomposition({ data: makeResponse({ isUnavailable: true }) })

    render(<DecompositionTab flatId="flat-1" />)

    expect(screen.getByText('unavailable.heading')).toBeInTheDocument()
    expect(screen.queryByText('residual.title')).not.toBeInTheDocument()
  })

  it('DecompositionTab_UnavailableCtaClicked_NavigatesToImportRoute', async () => {
    const user = userEvent.setup()
    mockDecomposition({ data: makeResponse({ isUnavailable: true }) })

    render(<DecompositionTab flatId="flat-1" />)
    await user.click(screen.getByRole('button', { name: 'unavailable.cta' }))

    expect(mockNavigate).toHaveBeenCalledWith('/decomposition/import')
  })

  it('DecompositionTab_NormalResponse_RendersResidualCardBeforeAllRoomCardsInOrder', () => {
    mockDecomposition({
      data: makeResponse({
        rooms: [
          { roomId: 'room-1', roomName: 'Living Room', kwh: 62.3, cost: 14.43, devices: [] },
          { roomId: 'room-2', roomName: 'Bedroom', kwh: 18.1, cost: 4.2, devices: [] },
          { roomId: 'room-3', roomName: 'Kitchen', kwh: 9.4, cost: 2.18, devices: [] },
        ],
      }),
    })

    render(<DecompositionTab flatId="flat-1" />)

    const residual = screen.getByText('residual.title')
    const livingRoom = screen.getByText('Living Room')
    const bedroom = screen.getByText('Bedroom')
    const kitchen = screen.getByText('Kitchen')
    expect(residual.compareDocumentPosition(livingRoom) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(livingRoom.compareDocumentPosition(bedroom) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(bedroom.compareDocumentPosition(kitchen) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })

  it('DecompositionTab_CustomRangeSelectedWithoutDates_ShowsSelectRangePromptInsteadOfSkeleton', async () => {
    const user = userEvent.setup()
    mockDecomposition({ isPending: true })

    render(<DecompositionTab flatId="flat-1" />)
    await user.click(screen.getByRole('button', { name: /period.thisMonth/ }))
    await user.click(screen.getByRole('option', { name: 'period.custom' }))

    expect(screen.getByText('period.selectRange')).toBeInTheDocument()
    expect(document.querySelectorAll('.animate-pulse')).toHaveLength(0)
  })

  it('DecompositionTab_RoomsOutOfSortOrderInput_RendersRoomsSortedByDescendingKwh', () => {
    mockDecomposition({
      data: makeResponse({
        rooms: [
          { roomId: 'room-1', roomName: 'Bedroom', kwh: 5, cost: 1, devices: [] },
          { roomId: 'room-2', roomName: 'Living Room', kwh: 62.3, cost: 14.43, devices: [] },
          { roomId: 'room-3', roomName: 'Kitchen', kwh: 20, cost: 4.5, devices: [] },
        ],
      }),
    })

    render(<DecompositionTab flatId="flat-1" />)

    const livingRoom = screen.getByText('Living Room')
    const kitchen = screen.getByText('Kitchen')
    const bedroom = screen.getByText('Bedroom')
    expect(livingRoom.compareDocumentPosition(kitchen) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
    expect(kitchen.compareDocumentPosition(bedroom) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })

  it('DecompositionTab_HasInterpolatedDataTrue_RendersBanner', () => {
    mockDecomposition({ data: makeResponse({ hasInterpolatedData: true }) })

    render(<DecompositionTab flatId="flat-1" />)

    expect(screen.getByText('interpolatedBanner')).toBeInTheDocument()
  })

  it('DecompositionTab_HasInterpolatedDataFalse_DoesNotRenderBanner', () => {
    mockDecomposition({ data: makeResponse({ hasInterpolatedData: false }) })

    render(<DecompositionTab flatId="flat-1" />)

    expect(screen.queryByText('interpolatedBanner')).not.toBeInTheDocument()
  })

  it('DecompositionTab_SmartStripConfigureHintClicked_NavigatesToStructureWithPowerPointId', async () => {
    const user = userEvent.setup()
    mockDecomposition({
      data: makeResponse({
        rooms: [
          {
            roomId: 'room-1',
            roomName: 'Living Room',
            kwh: 62.3,
            cost: 14.43,
            devices: [
              {
                deviceId: 'pp-strip-1',
                powerPointId: 'pp-strip-1',
                name: 'Power Strip',
                kwh: 5,
                cost: 1.2,
                approach: 'Measured',
                isSmartStrip: true,
                subDevices: [
                  {
                    deviceId: 'sub-1',
                    name: 'Unknown Plug',
                    kwh: 1,
                    cost: 0.2,
                    isConfigured: false,
                    isUnconfigured: true,
                  },
                ],
              },
            ],
          },
        ],
      }),
    })

    render(<DecompositionTab flatId="flat-1" />)
    await user.click(screen.getByText('smartStripCard.configureHint'))

    expect(mockNavigate).toHaveBeenCalledWith('/settings/structure?powerPointId=pp-strip-1')
  })
})
