import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { FlatStructureEditor } from './FlatStructureEditor'
import type { FlatStructureResponse } from '@/features/flat-structure/api/flatStructureApi'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (k: string, opts?: Record<string, unknown>) => {
      if (opts?.count !== undefined) return `${k}:${opts.count}`
      if (opts?.roomCount !== undefined) return `${k}:${opts.roomCount}:${opts.plugCount}`
      return k
    },
  }),
}))

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom')
  return { ...actual, useNavigate: () => mockNavigate }
})
const mockNavigate = vi.fn()

vi.mock('@/features/flat-structure/hooks/useFlatStructure')
import { useFlatStructure } from '@/features/flat-structure/hooks/useFlatStructure'
const mockUseFlatStructure = vi.mocked(useFlatStructure)

vi.mock('@/features/flat-structure/hooks/useUpdateFlatStructure')
import { useUpdateFlatStructure } from '@/features/flat-structure/hooks/useUpdateFlatStructure'
const mockUseUpdateFlatStructure = vi.mocked(useUpdateFlatStructure)

const mockMutate = vi.fn()

function setupFlatStructure(options?: {
  isLoading?: boolean
  isError?: boolean
  data?: FlatStructureResponse
}) {
  const refetch = vi.fn()
  mockUseFlatStructure.mockReturnValue({
    data: options?.data,
    isLoading: options?.isLoading ?? false,
    isError: options?.isError ?? false,
    refetch,
  } as unknown as ReturnType<typeof useFlatStructure>)
  return { refetch }
}

function renderEditor(flatId: string | undefined = 'flat-1') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter>
        <FlatStructureEditor flatId={flatId} />
      </MemoryRouter>
    </QueryClientProvider>
  )
}

const defaultTemplateResponse: FlatStructureResponse = {
  flatId: 'flat-1',
  hasDefaultTemplate: true,
  rooms: [],
}

function seededResponse(overrides?: Partial<FlatStructureResponse>): FlatStructureResponse {
  return {
    flatId: 'flat-1',
    hasDefaultTemplate: false,
    rooms: [
      {
        roomId: 'room-1',
        name: 'Office',
        sortOrder: 0,
        powerPoints: [
          {
            powerPointId: 'pp-1',
            name: 'Desk Outlet',
            plugId: 'PLUG-1',
            devices: [],
          },
        ],
      },
      {
        roomId: 'room-2',
        name: 'Garage',
        sortOrder: 1,
        powerPoints: [
          {
            powerPointId: 'pp-2',
            name: 'Charger Outlet',
            plugId: 'PLUG-1',
            devices: [],
          },
        ],
      },
    ],
    ...overrides,
  }
}

describe('FlatStructureEditor', () => {
  beforeEach(() => {
    mockUseFlatStructure.mockReset()
    mockUseUpdateFlatStructure.mockReset()
    mockNavigate.mockReset()
    mockMutate.mockReset()
    mockUseUpdateFlatStructure.mockReturnValue({
      mutate: mockMutate,
      isPending: false,
    } as unknown as ReturnType<typeof useUpdateFlatStructure>)
  })

  it('FlatStructureEditor_DefaultTemplateWithNoRooms_RendersFiveDefaultRoomsAndFooterPrompt', () => {
    setupFlatStructure({ data: defaultTemplateResponse })

    renderEditor()

    expect(screen.getByDisplayValue('defaultRooms.livingRoom')).toBeInTheDocument()
    expect(screen.getByDisplayValue('defaultRooms.bedroom')).toBeInTheDocument()
    expect(screen.getByDisplayValue('defaultRooms.kitchen')).toBeInTheDocument()
    expect(screen.getByDisplayValue('defaultRooms.bathroom')).toBeInTheDocument()
    expect(screen.getByDisplayValue('defaultRooms.hallway')).toBeInTheDocument()
    expect(screen.getByText('editor.defaultTemplateNote')).toBeInTheDocument()
  })

  it('FlatStructureEditor_SeededRoomsNoDefaultTemplate_RendersSeededRoomsNoFooterPrompt', () => {
    setupFlatStructure({ data: seededResponse() })

    renderEditor()

    expect(screen.getByDisplayValue('Office')).toBeInTheDocument()
    expect(screen.getByDisplayValue('Garage')).toBeInTheDocument()
    expect(screen.queryByText('editor.defaultTemplateNote')).not.toBeInTheDocument()
  })

  it('FlatStructureEditor_RenamingRoomInline_UpdatesStateOnlyNoMutationCall', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    const input = screen.getByDisplayValue('Office')
    await user.clear(input)
    await user.type(input, 'Study')

    expect(screen.getByDisplayValue('Study')).toBeInTheDocument()
    expect(mockMutate).not.toHaveBeenCalled()
  })

  it('FlatStructureEditor_ClickRoomRow_TransitionsToRoomViewAndBackReturnsToList', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[0])

    expect(screen.getByText('Office')).toBeInTheDocument()
    expect(screen.getByDisplayValue('Desk Outlet')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: /editor\.back/ }))

    expect(screen.getByDisplayValue('Office')).toBeInTheDocument()
  })

  it('FlatStructureEditor_AddRoom_AppendsNewRoomRow', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getByRole('button', { name: 'editor.addRoom' }))

    expect(screen.getByDisplayValue('editor.newRoomName')).toBeInTheDocument()
  })

  it('FlatStructureEditor_AddPowerPointInRoomView_AppendsPowerPoint', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[0])
    await user.click(screen.getByRole('button', { name: 'room.addPowerPoint' }))

    expect(screen.getAllByLabelText('powerPoint.namePlaceholder')).toHaveLength(2)
  })

  it('FlatStructureEditor_AddDevice_OpensDeviceEditorForNewDevice', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[0])
    await user.click(screen.getByRole('button', { name: 'powerPoint.addDevice' }))

    expect(screen.getByText('device.title')).toBeInTheDocument()
    expect(screen.getByLabelText('device.namePlaceholder')).toHaveValue('')
  })

  it('FlatStructureEditor_TwoPowerPointsSameNonEmptyPlugId_SaveDisabledWithConflictText', () => {
    setupFlatStructure({ data: seededResponse() })

    renderEditor()

    expect(screen.getByText('editor.plugIdConflict')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'editor.save' })).toBeDisabled()
  })

  it('FlatStructureEditor_ClearingOnePlugId_ReEnablesSave', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[1])
    const plugInput = screen.getByLabelText('powerPoint.plugIdLabel')
    await user.clear(plugInput)
    await user.click(screen.getByRole('button', { name: /editor\.back/ }))

    expect(screen.queryByText('editor.plugIdConflict')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'editor.save' })).toBeEnabled()
  })

  it('FlatStructureEditor_TwoPowerPointsBothEmptyPlugId_NoConflictSaveStaysEnabled', () => {
    setupFlatStructure({
      data: seededResponse({
        rooms: [
          {
            roomId: 'room-1',
            name: 'Office',
            sortOrder: 0,
            powerPoints: [{ powerPointId: 'pp-1', name: 'Desk Outlet', plugId: '', devices: [] }],
          },
          {
            roomId: 'room-2',
            name: 'Garage',
            sortOrder: 1,
            powerPoints: [{ powerPointId: 'pp-2', name: 'Charger Outlet', plugId: null, devices: [] }],
          },
        ],
      }),
    })

    renderEditor()

    expect(screen.queryByText('editor.plugIdConflict')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'editor.save' })).toBeEnabled()
  })

  it('FlatStructureEditor_SaveClickedNoConflicts_CallsMutationWithCorrectlyShapedPayload', async () => {
    const user = userEvent.setup()
    setupFlatStructure({
      data: seededResponse({
        rooms: [
          {
            roomId: 'room-1',
            name: 'Office',
            sortOrder: 0,
            powerPoints: [
              {
                powerPointId: 'pp-1',
                name: 'Desk Outlet',
                plugId: 'PLUG-1',
                devices: [
                  {
                    deviceId: 'device-1',
                    name: 'Lamp',
                    type: null,
                    manufacturer: null,
                    model: null,
                    purchaseDate: null,
                    consumptionApproach: 'None',
                    euLabelClass: null,
                    euAnnualKwh: null,
                    selfMeasuredKwh: null,
                    selfMeasuredPeriod: null,
                  },
                ],
              },
            ],
          },
        ],
      }),
    })

    renderEditor()
    await user.click(screen.getByRole('button', { name: 'editor.save' }))

    expect(mockMutate).toHaveBeenCalledWith(
      {
        rooms: [
          {
            name: 'Office',
            sortOrder: 0,
            powerPoints: [
              {
                name: 'Desk Outlet',
                plugId: 'PLUG-1',
                devices: [
                  {
                    name: 'Lamp',
                    type: undefined,
                    manufacturer: undefined,
                    model: undefined,
                    consumptionApproach: 'None',
                  },
                ],
              },
            ],
          },
        ],
      },
      expect.any(Object)
    )
  })

  it('FlatStructureEditor_SaveSucceeds_ShowsSuccessConfirmation', async () => {
    const user = userEvent.setup()
    mockMutate.mockImplementation((_body, callbacks) => callbacks?.onSuccess?.())
    setupFlatStructure({
      data: seededResponse({
        rooms: [
          {
            roomId: 'room-1',
            name: 'Office',
            sortOrder: 0,
            powerPoints: [],
          },
        ],
      }),
    })

    renderEditor()
    await user.click(screen.getByRole('button', { name: 'editor.save' }))

    expect(screen.getByText('editor.saveSuccess')).toBeInTheDocument()
  })
})
