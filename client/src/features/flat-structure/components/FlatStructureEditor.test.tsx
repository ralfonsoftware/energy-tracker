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
            plugId: 'PLUG-2',
            devices: [],
          },
        ],
      },
    ],
    ...overrides,
  }
}

function seededResponseWithDevice(): FlatStructureResponse {
  return seededResponse({
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
  })
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
    setupFlatStructure({
      data: seededResponse({
        rooms: [
          {
            roomId: 'room-1',
            name: 'Office',
            sortOrder: 0,
            powerPoints: [{ powerPointId: 'pp-1', name: 'Desk Outlet', plugId: 'PLUG-1', devices: [] }],
          },
          {
            roomId: 'room-2',
            name: 'Garage',
            sortOrder: 1,
            powerPoints: [{ powerPointId: 'pp-2', name: 'Charger Outlet', plugId: 'PLUG-1', devices: [] }],
          },
        ],
      }),
    })

    renderEditor()

    expect(screen.getByText('editor.plugIdConflict')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'editor.save' })).toBeDisabled()
  })

  it('FlatStructureEditor_ClearingOnePlugId_ReEnablesSave', async () => {
    const user = userEvent.setup()
    setupFlatStructure({
      data: seededResponse({
        rooms: [
          {
            roomId: 'room-1',
            name: 'Office',
            sortOrder: 0,
            powerPoints: [{ powerPointId: 'pp-1', name: 'Desk Outlet', plugId: 'PLUG-1', devices: [] }],
          },
          {
            roomId: 'room-2',
            name: 'Garage',
            sortOrder: 1,
            powerPoints: [{ powerPointId: 'pp-2', name: 'Charger Outlet', plugId: 'PLUG-1', devices: [] }],
          },
        ],
      }),
    })

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

  it('FlatStructureEditor_DeleteRoomArmThenConfirm_RemovesOnlyThatRoom', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: 'room.delete' })[0])

    expect(screen.getByText('room.deletePrompt')).toBeInTheDocument()
    expect(screen.getByDisplayValue('Office')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'confirm.delete' }))

    expect(screen.queryByDisplayValue('Office')).not.toBeInTheDocument()
    expect(screen.getByDisplayValue('Garage')).toBeInTheDocument()
  })

  it('FlatStructureEditor_DeleteRoomArmThenCancel_RoomStillPresent', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: 'room.delete' })[0])
    await user.click(screen.getByRole('button', { name: 'confirm.cancel' }))

    expect(screen.getByDisplayValue('Office')).toBeInTheDocument()
    expect(screen.queryByText('room.deletePrompt')).not.toBeInTheDocument()
  })

  it('FlatStructureEditor_DeleteLastRemainingRoom_DisablesSaveAndShowsError', async () => {
    const user = userEvent.setup()
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
    await user.click(screen.getByRole('button', { name: 'room.delete' }))
    await user.click(screen.getByRole('button', { name: 'confirm.delete' }))

    expect(screen.getByText('editor.noRoomsError')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'editor.save' })).toBeDisabled()
  })

  it('FlatStructureEditor_DeletePowerPointArmThenConfirm_RemovesPowerPoint', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[0])
    await user.click(screen.getByRole('button', { name: 'powerPoint.delete' }))

    expect(screen.getByText('powerPoint.deletePrompt')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'confirm.delete' }))

    expect(screen.queryByDisplayValue('Desk Outlet')).not.toBeInTheDocument()
  })

  it('FlatStructureEditor_DeletePowerPointArmThenCancel_PowerPointStillPresent', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[0])
    await user.click(screen.getByRole('button', { name: 'powerPoint.delete' }))
    await user.click(screen.getByRole('button', { name: 'confirm.cancel' }))

    expect(screen.getByDisplayValue('Desk Outlet')).toBeInTheDocument()
    expect(screen.queryByText('powerPoint.deletePrompt')).not.toBeInTheDocument()
  })

  it('FlatStructureEditor_DeleteDeviceArmThenConfirm_RemovesDevice', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponseWithDevice() })

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[0])

    expect(screen.getByText('Lamp')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'device.delete' }))

    expect(screen.getByText('device.deletePrompt')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'confirm.delete' }))

    expect(screen.queryByText('Lamp')).not.toBeInTheDocument()
  })

  it('FlatStructureEditor_DeleteDeviceArmThenCancel_DeviceStillPresent', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponseWithDevice() })

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[0])
    await user.click(screen.getByRole('button', { name: 'device.delete' }))
    await user.click(screen.getByRole('button', { name: 'confirm.cancel' }))

    expect(screen.getByText('Lamp')).toBeInTheDocument()
    expect(screen.queryByText('device.deletePrompt')).not.toBeInTheDocument()
  })

  it('FlatStructureEditor_NewRoomAdded_SaveButtonEnabledImmediately', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getByRole('button', { name: 'editor.addRoom' }))

    expect(
      screen.getByRole('button', { name: 'editor.save: editor.newRoomName' })
    ).toBeEnabled()
  })

  it('FlatStructureEditor_ExistingRoomNameUnchanged_SaveButtonDisabled', () => {
    setupFlatStructure({ data: seededResponse() })

    renderEditor()

    expect(screen.getByRole('button', { name: 'editor.save: Office' })).toBeDisabled()
  })

  it('FlatStructureEditor_ExistingRoomRenamed_SaveButtonBecomesEnabled', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    const input = screen.getByDisplayValue('Office')
    await user.clear(input)
    await user.type(input, 'Study')

    expect(screen.getByRole('button', { name: 'editor.save: Study' })).toBeEnabled()
  })

  it('FlatStructureEditor_ExistingRoomRenamedThenRevertedToOriginal_SaveButtonDisabledAgain', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    const input = screen.getByDisplayValue('Office')
    await user.clear(input)
    await user.type(input, 'Study')
    await user.clear(input)
    await user.type(input, 'Office')

    expect(screen.getByRole('button', { name: 'editor.save: Office' })).toBeDisabled()
  })

  it('FlatStructureEditor_ClickRoomSaveButton_CallsMutationWithoutPageLevelSpeichernClick', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    const input = screen.getByDisplayValue('Office')
    await user.clear(input)
    await user.type(input, 'Office Renamed')
    await user.click(screen.getByRole('button', { name: 'editor.save: Office Renamed' }))

    expect(mockMutate).toHaveBeenCalledTimes(1)
    expect(mockMutate).toHaveBeenCalledWith(
      {
        rooms: [
          {
            name: 'Office Renamed',
            sortOrder: 0,
            powerPoints: [{ name: 'Desk Outlet', plugId: 'PLUG-1', devices: [] }],
          },
          {
            name: 'Garage',
            sortOrder: 1,
            powerPoints: [{ name: 'Charger Outlet', plugId: 'PLUG-2', devices: [] }],
          },
        ],
      },
      expect.any(Object)
    )
  })

  it('FlatStructureEditor_SaveRoomWhileUnrelatedPowerPointNameIsBlank_PayloadOmitsTheBlankPowerPoint', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[0])
    await user.click(screen.getByRole('button', { name: 'room.addPowerPoint' }))
    await user.click(screen.getByRole('button', { name: /editor\.back/ }))

    const input = screen.getByDisplayValue('Garage')
    await user.clear(input)
    await user.type(input, 'Garage Renamed')
    await user.click(screen.getByRole('button', { name: 'editor.save: Garage Renamed' }))

    expect(mockMutate).toHaveBeenCalledWith(
      {
        rooms: [
          {
            name: 'Office',
            sortOrder: 0,
            powerPoints: [{ name: 'Desk Outlet', plugId: 'PLUG-1', devices: [] }],
          },
          {
            name: 'Garage Renamed',
            sortOrder: 1,
            powerPoints: [{ name: 'Charger Outlet', plugId: 'PLUG-2', devices: [] }],
          },
        ],
      },
      expect.any(Object)
    )
  })

  it('FlatStructureEditor_SaveRoomSucceeds_ButtonDisabledAgainAndOriginalNameUpdated', async () => {
    const user = userEvent.setup()
    mockMutate.mockImplementation((_body, callbacks) => callbacks?.onSuccess?.())
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    const input = screen.getByDisplayValue('Office')
    await user.clear(input)
    await user.type(input, 'Study')
    await user.click(screen.getByRole('button', { name: 'editor.save: Study' }))

    expect(screen.getByRole('button', { name: 'editor.save: Study' })).toBeDisabled()
  })

  it('FlatStructureEditor_SaveRoomFails_RevertsNameAndShowsSaveError', async () => {
    const user = userEvent.setup()
    mockMutate.mockImplementation((_body, callbacks) => callbacks?.onError?.())
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    const input = screen.getByDisplayValue('Office')
    await user.clear(input)
    await user.type(input, 'Study')
    await user.click(screen.getByRole('button', { name: 'editor.save: Study' }))

    expect(screen.getByDisplayValue('Office')).toBeInTheDocument()
    expect(screen.getByText('editor.saveError')).toBeInTheDocument()
  })

  it('FlatStructureEditor_DeleteRoomConfirm_CallsMutationImmediatelyWithRoomRemoved', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: 'room.delete' })[0])
    await user.click(screen.getByRole('button', { name: 'confirm.delete' }))

    expect(mockMutate).toHaveBeenCalledWith(
      {
        rooms: [
          {
            name: 'Garage',
            sortOrder: 0,
            powerPoints: [{ name: 'Charger Outlet', plugId: 'PLUG-2', devices: [] }],
          },
        ],
      },
      expect.any(Object)
    )
  })

  it('FlatStructureEditor_DeleteLastRemainingRoom_DoesNotCallMutationShowsNoRoomsError', async () => {
    const user = userEvent.setup()
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
    await user.click(screen.getByRole('button', { name: 'room.delete' }))
    await user.click(screen.getByRole('button', { name: 'confirm.delete' }))

    expect(mockMutate).not.toHaveBeenCalled()
    expect(screen.getByText('editor.noRoomsError')).toBeInTheDocument()
  })

  it('FlatStructureEditor_AnySavePending_DisablesAllRoomSaveButtonsDeleteAndSpeichern', () => {
    setupFlatStructure({ data: seededResponse() })
    mockUseUpdateFlatStructure.mockReturnValue({
      mutate: mockMutate,
      isPending: true,
    } as unknown as ReturnType<typeof useUpdateFlatStructure>)

    renderEditor()

    expect(screen.getByRole('button', { name: 'editor.saving: Office' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'editor.saving: Garage' })).toBeDisabled()
    expect(screen.getAllByRole('button', { name: 'room.delete' })[0]).toBeDisabled()
    expect(screen.getAllByRole('button', { name: 'room.delete' })[1]).toBeDisabled()
    expect(screen.getByRole('button', { name: 'editor.addRoom' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'editor.saving' })).toBeDisabled()
  })

  it('FlatStructureEditor_SaveRoomWithOwnNewPowerPoint_PayloadIncludesTheNewPowerPoint', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[0])
    await user.click(screen.getByRole('button', { name: 'room.addPowerPoint' }))
    const nameInputs = screen.getAllByRole('textbox', { name: 'powerPoint.namePlaceholder' })
    await user.type(nameInputs[nameInputs.length - 1], 'Fridge Outlet')
    await user.click(screen.getByRole('button', { name: /editor\.back/ }))

    const input = screen.getByDisplayValue('Office')
    await user.clear(input)
    await user.type(input, 'Office Renamed')
    await user.click(screen.getByRole('button', { name: 'editor.save: Office Renamed' }))

    expect(mockMutate).toHaveBeenCalledWith(
      {
        rooms: [
          {
            name: 'Office Renamed',
            sortOrder: 0,
            powerPoints: [
              { name: 'Desk Outlet', plugId: 'PLUG-1', devices: [] },
              { name: 'Fridge Outlet', plugId: undefined, devices: [] },
            ],
          },
          {
            name: 'Garage',
            sortOrder: 1,
            powerPoints: [{ name: 'Charger Outlet', plugId: 'PLUG-2', devices: [] }],
          },
        ],
      },
      expect.any(Object)
    )
  })

  it('FlatStructureEditor_RenameAlreadySavedNewRoomWhileEarlierUnsavedRoomStillExists_PersistsTheRename', async () => {
    const user = userEvent.setup()
    mockMutate.mockImplementation((_body, callbacks) => callbacks?.onSuccess?.())
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getByRole('button', { name: 'editor.addRoom' }))
    await user.click(screen.getByRole('button', { name: 'editor.addRoom' }))

    const newRoomInputs = screen.getAllByDisplayValue('editor.newRoomName')
    await user.clear(newRoomInputs[1])
    await user.type(newRoomInputs[1], 'NewB')
    await user.click(screen.getByRole('button', { name: 'editor.save: NewB' }))

    mockMutate.mockClear()

    const savedNewBInput = screen.getByDisplayValue('NewB')
    await user.clear(savedNewBInput)
    await user.type(savedNewBInput, 'NewB Renamed')
    await user.click(screen.getByRole('button', { name: 'editor.save: NewB Renamed' }))

    expect(mockMutate).toHaveBeenCalledWith(
      {
        rooms: [
          {
            name: 'Office',
            sortOrder: 0,
            powerPoints: [{ name: 'Desk Outlet', plugId: 'PLUG-1', devices: [] }],
          },
          {
            name: 'Garage',
            sortOrder: 1,
            powerPoints: [{ name: 'Charger Outlet', plugId: 'PLUG-2', devices: [] }],
          },
          { name: 'NewB Renamed', sortOrder: 2, powerPoints: [] },
        ],
      },
      expect.any(Object)
    )
  })

  it('FlatStructureEditor_DeleteNeverSavedNewRoom_DoesNotCallMutation', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getByRole('button', { name: 'editor.addRoom' }))
    await user.click(screen.getAllByRole('button', { name: 'room.delete' })[2])
    await user.click(screen.getByRole('button', { name: 'confirm.delete' }))

    expect(mockMutate).not.toHaveBeenCalled()
  })

  it('FlatStructureEditor_EditPowerPointInRoomDetailNoRename_RoomListSaveButtonBecomesEnabled', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[0])
    const ppInput = screen.getByDisplayValue('Desk Outlet')
    await user.clear(ppInput)
    await user.type(ppInput, 'Desk Outlet Updated')
    await user.click(screen.getByRole('button', { name: /editor\.back/ }))

    expect(screen.getByRole('button', { name: 'editor.save: Office' })).toBeEnabled()
  })

  it('FlatStructureEditor_RoomDetailView_RendersStickySaveButtonEnabledWhenPowerPointEdited', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[0])
    const ppInput = screen.getByDisplayValue('Desk Outlet')
    await user.clear(ppInput)
    await user.type(ppInput, 'Desk Outlet Updated')

    expect(screen.getByRole('button', { name: 'editor.save' })).toBeEnabled()
  })

  it('FlatStructureEditor_ClickInRoomSaveButton_PersistsPowerPointEditAndLeavesOtherRoomsUnchanged', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[0])
    const ppInput = screen.getByDisplayValue('Desk Outlet')
    await user.clear(ppInput)
    await user.type(ppInput, 'Desk Outlet Updated')
    await user.click(screen.getByRole('button', { name: 'editor.save' }))

    expect(mockMutate).toHaveBeenCalledWith(
      {
        rooms: [
          {
            name: 'Office',
            sortOrder: 0,
            powerPoints: [{ name: 'Desk Outlet Updated', plugId: 'PLUG-1', devices: [] }],
          },
          {
            name: 'Garage',
            sortOrder: 1,
            powerPoints: [{ name: 'Charger Outlet', plugId: 'PLUG-2', devices: [] }],
          },
        ],
      },
      expect.any(Object)
    )
  })

  it('FlatStructureEditor_InRoomSaveWithBlankPowerPointNameInSameRoom_SaveButtonDisabledWithInlineError', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[0])
    await user.click(screen.getByRole('button', { name: 'room.addPowerPoint' }))

    expect(screen.getByText('editor.blankNameError')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'editor.save' })).toBeDisabled()
  })

  it('FlatStructureEditor_InRoomSaveWithPlugIdConflictAgainstAlreadySavedRoom_SaveButtonDisabledWithInlineError', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[1])
    const plugInput = screen.getByLabelText('powerPoint.plugIdLabel')
    await user.clear(plugInput)
    await user.type(plugInput, 'PLUG-1')

    expect(screen.getByText('editor.plugIdConflict')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'editor.save' })).toBeDisabled()
  })

  it('FlatStructureEditor_InRoomSaveWithUnrelatedDraftPlugIdConflictInAnotherUnsavedRoom_SaveButtonRemainsEnabled', async () => {
    const user = userEvent.setup()
    setupFlatStructure({
      data: seededResponse({
        rooms: [
          {
            roomId: 'room-1',
            name: 'Office',
            sortOrder: 0,
            powerPoints: [{ powerPointId: 'pp-1', name: 'Desk Outlet', plugId: 'PLUG-1', devices: [] }],
          },
        ],
      }),
    })

    renderEditor()
    await user.click(screen.getByRole('button', { name: 'editor.addRoom' }))
    await user.click(screen.getByRole('button', { name: 'editor.addRoom' }))

    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[1])
    await user.click(screen.getByRole('button', { name: 'room.addPowerPoint' }))
    await user.type(screen.getByRole('textbox', { name: 'powerPoint.namePlaceholder' }), 'New1 Outlet')
    await user.type(screen.getByLabelText('powerPoint.plugIdLabel'), 'DUPE')
    await user.click(screen.getByRole('button', { name: /editor\.back/ }))

    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[2])
    await user.click(screen.getByRole('button', { name: 'room.addPowerPoint' }))
    await user.type(screen.getByRole('textbox', { name: 'powerPoint.namePlaceholder' }), 'New2 Outlet')
    await user.type(screen.getByLabelText('powerPoint.plugIdLabel'), 'DUPE')
    await user.click(screen.getByRole('button', { name: /editor\.back/ }))

    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[0])
    const officePpInput = screen.getByDisplayValue('Desk Outlet')
    await user.type(officePpInput, ' Updated')

    expect(screen.getByRole('button', { name: 'editor.save' })).toBeEnabled()
  })

  it('FlatStructureEditor_AnySavePendingWithRoomDetailViewActive_DisablesInRoomSaveButton', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })
    mockUseUpdateFlatStructure.mockReturnValue({
      mutate: mockMutate,
      isPending: true,
    } as unknown as ReturnType<typeof useUpdateFlatStructure>)

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[0])

    expect(screen.getByRole('button', { name: 'editor.saving' })).toBeDisabled()
  })
})
