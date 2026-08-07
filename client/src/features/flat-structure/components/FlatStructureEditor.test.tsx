import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { FlatStructureEditor } from './FlatStructureEditor'
import type { FlatStructureResponse, RoomResponse } from '@/features/flat-structure/api/flatStructureApi'

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

vi.mock('@/features/flat-structure/hooks/useSaveRoom')
import { useSaveRoom } from '@/features/flat-structure/hooks/useSaveRoom'
const mockUseSaveRoom = vi.mocked(useSaveRoom)

vi.mock('@/features/flat-structure/hooks/useDeleteRoom')
import { useDeleteRoom } from '@/features/flat-structure/hooks/useDeleteRoom'
const mockUseDeleteRoom = vi.mocked(useDeleteRoom)

vi.mock('@/features/flat-structure/hooks/useSaveDevice')
import { useSaveDevice } from '@/features/flat-structure/hooks/useSaveDevice'
const mockUseSaveDevice = vi.mocked(useSaveDevice)

vi.mock('@/features/flat-structure/hooks/useDeleteDevice')
import { useDeleteDevice } from '@/features/flat-structure/hooks/useDeleteDevice'
const mockUseDeleteDevice = vi.mocked(useDeleteDevice)

const mockSaveRoomMutate = vi.fn()
const mockDeleteRoomMutate = vi.fn()
const mockSaveDeviceMutate = vi.fn()
const mockDeleteDeviceMutate = vi.fn()

function setupFlatStructure(options?: {
  isLoading?: boolean
  isError?: boolean
  data?: FlatStructureResponse
}) {
  const refetch = vi.fn().mockResolvedValue({ data: options?.data })
  mockUseFlatStructure.mockReturnValue({
    data: options?.data,
    isLoading: options?.isLoading ?? false,
    isError: options?.isError ?? false,
    refetch,
  } as unknown as ReturnType<typeof useFlatStructure>)
  return { refetch }
}

function renderEditor(flatId: string | undefined = 'flat-1', initialEntries: string[] = ['/']) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={initialEntries}>
        <FlatStructureEditor flatId={flatId} />
      </MemoryRouter>
    </QueryClientProvider>
  )
}

const defaultTemplateResponse: FlatStructureResponse = {
  flatId: 'flat-1',
  hasDefaultTemplate: true,
  rooms: [],
  rowVersion: 'AQID',
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
        rowVersion: 'AQID',
        powerPoints: [
          {
            powerPointId: 'pp-1',
            name: 'Desk Outlet',
            plugId: 'PLUG-1',
            devices: [],
            rowVersion: 'AQID',
          },
        ],
      },
      {
        roomId: 'room-2',
        name: 'Garage',
        sortOrder: 1,
        rowVersion: 'AQID',
        powerPoints: [
          {
            powerPointId: 'pp-2',
            name: 'Charger Outlet',
            plugId: 'PLUG-2',
            devices: [],
            rowVersion: 'AQID',
          },
        ],
      },
    ],
    rowVersion: 'AQID',
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
        rowVersion: 'AQID',
        powerPoints: [
          {
            powerPointId: 'pp-1',
            name: 'Desk Outlet',
            plugId: 'PLUG-1',
            rowVersion: 'AQID',
            devices: [
              {
                deviceId: 'device-1',
                name: 'Lamp',
                type: null,
                manufacturer: null,
                model: null,
                purchaseDate: null,
                inUseSince: null,
                decommissionedDate: null,
                consumptionApproach: 'None',
                euLabelClass: null,
                euAnnualKwh: null,
                selfMeasuredKwh: null,
                selfMeasuredPeriod: null,
                rowVersion: 'AQID',
              },
            ],
          },
        ],
      },
    ],
  })
}

const officeRoomResponse: RoomResponse = {
  roomId: 'room-1',
  name: 'Office Renamed',
  sortOrder: 0,
  powerPoints: [{ powerPointId: 'pp-1', name: 'Desk Outlet', plugId: 'PLUG-1', devices: [], rowVersion: 'AQID' }],
  rowVersion: 'new-version',
}

describe('FlatStructureEditor', () => {
  beforeEach(() => {
    mockUseFlatStructure.mockReset()
    mockUseSaveRoom.mockReset()
    mockUseDeleteRoom.mockReset()
    mockUseSaveDevice.mockReset()
    mockUseDeleteDevice.mockReset()
    mockNavigate.mockReset()
    mockSaveRoomMutate.mockReset()
    mockDeleteRoomMutate.mockReset()
    mockSaveDeviceMutate.mockReset()
    mockDeleteDeviceMutate.mockReset()
    mockUseSaveRoom.mockReturnValue({
      mutate: mockSaveRoomMutate,
      isPending: false,
    } as unknown as ReturnType<typeof useSaveRoom>)
    mockUseDeleteRoom.mockReturnValue({
      mutate: mockDeleteRoomMutate,
      isPending: false,
    } as unknown as ReturnType<typeof useDeleteRoom>)
    mockUseSaveDevice.mockReturnValue({
      mutate: mockSaveDeviceMutate,
      isPending: false,
      isError: false,
      isSuccess: false,
      error: null,
      data: undefined,
    } as unknown as ReturnType<typeof useSaveDevice>)
    mockUseDeleteDevice.mockReturnValue({
      mutate: mockDeleteDeviceMutate,
      isPending: false,
      isError: false,
      error: null,
    } as unknown as ReturnType<typeof useDeleteDevice>)
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
    expect(mockSaveRoomMutate).not.toHaveBeenCalled()
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

  it('FlatStructureEditor_AddDeviceAndSave_CallsDeviceSaveMutationDirectlyNotRoomSaveMutation', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponseWithDevice() })

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[0])
    await user.click(screen.getByRole('button', { name: 'powerPoint.addDevice' }))
    await user.type(screen.getByLabelText('device.namePlaceholder'), 'Toaster')
    await user.click(screen.getByRole('button', { name: 'device.save' }))

    expect(mockSaveDeviceMutate).toHaveBeenCalledWith(
      expect.objectContaining({ deviceId: undefined, name: 'Toaster' })
    )
    expect(mockSaveRoomMutate).not.toHaveBeenCalled()
  })

  it('FlatStructureEditor_TwoPowerPointsSameNonEmptyPlugId_ShowsConflictTextOnBothRows', () => {
    setupFlatStructure({
      data: seededResponse({
        rooms: [
          {
            roomId: 'room-1',
            name: 'Office',
            sortOrder: 0,
            rowVersion: 'AQID',
            powerPoints: [{ powerPointId: 'pp-1', name: 'Desk Outlet', plugId: 'PLUG-1', devices: [], rowVersion: 'AQID' }],
          },
          {
            roomId: 'room-2',
            name: 'Garage',
            sortOrder: 1,
            rowVersion: 'AQID',
            powerPoints: [{ powerPointId: 'pp-2', name: 'Charger Outlet', plugId: 'PLUG-1', devices: [], rowVersion: 'AQID' }],
          },
        ],
      }),
    })

    renderEditor()

    expect(screen.getAllByText('editor.plugIdConflict')).toHaveLength(2)
  })

  it('FlatStructureEditor_ClearingOnePlugId_LeavesOnlyTheOtherRoomsInlineConflict', async () => {
    const user = userEvent.setup()
    setupFlatStructure({
      data: seededResponse({
        rooms: [
          {
            roomId: 'room-1',
            name: 'Office',
            sortOrder: 0,
            rowVersion: 'AQID',
            powerPoints: [{ powerPointId: 'pp-1', name: 'Desk Outlet', plugId: 'PLUG-1', devices: [], rowVersion: 'AQID' }],
          },
          {
            roomId: 'room-2',
            name: 'Garage',
            sortOrder: 1,
            rowVersion: 'AQID',
            powerPoints: [{ powerPointId: 'pp-2', name: 'Charger Outlet', plugId: 'PLUG-1', devices: [], rowVersion: 'AQID' }],
          },
        ],
      }),
    })

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[1])
    const plugInput = screen.getByLabelText('powerPoint.plugIdLabel')
    await user.clear(plugInput)
    await user.click(screen.getByRole('button', { name: /editor\.back/ }))

    // Office's row still shows its own inline conflict reason: its (unchanged) PLUG-1
    // still conflicts with Garage's *last-saved* PLUG-1 (Garage's clearing is draft-only,
    // not yet persisted) — this matches hasPlugIdConflictForRoomSave's existing semantics.
    expect(screen.getAllByText('editor.plugIdConflict')).toHaveLength(1)
  })

  it('FlatStructureEditor_TwoPowerPointsBothEmptyPlugId_NoConflictShown', () => {
    setupFlatStructure({
      data: seededResponse({
        rooms: [
          {
            roomId: 'room-1',
            name: 'Office',
            sortOrder: 0,
            rowVersion: 'AQID',
            powerPoints: [{ powerPointId: 'pp-1', name: 'Desk Outlet', plugId: '', devices: [], rowVersion: 'AQID' }],
          },
          {
            roomId: 'room-2',
            name: 'Garage',
            sortOrder: 1,
            rowVersion: 'AQID',
            powerPoints: [{ powerPointId: 'pp-2', name: 'Charger Outlet', plugId: null, devices: [], rowVersion: 'AQID' }],
          },
        ],
      }),
    })

    renderEditor()

    expect(screen.queryByText('editor.plugIdConflict')).not.toBeInTheDocument()
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

  it('FlatStructureEditor_DeleteDeviceArmThenConfirm_CallsDeleteDeviceMutationAndRemovesOnSuccess', async () => {
    const user = userEvent.setup()
    mockDeleteDeviceMutate.mockImplementation((_input, callbacks) => callbacks?.onSuccess?.())
    setupFlatStructure({ data: seededResponseWithDevice() })

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[0])

    expect(screen.getByText('Lamp')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'device.delete' }))

    expect(screen.getByText('device.deletePrompt')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'confirm.delete' }))

    expect(mockDeleteDeviceMutate).toHaveBeenCalledWith(
      { powerPointId: 'pp-1', deviceId: 'device-1', rowVersion: 'AQID' },
      expect.any(Object)
    )
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

  it('FlatStructureEditor_NeverSavedRoomWithPowerPointAddedBeforeFirstSave_CreateSaveIncludesThePowerPoint', async () => {
    // "Gap found" #1: RoomEditor.tsx's handleAddPowerPoint lets a user add power points to a
    // never-yet-persisted room (local draft only, no network call) before that room's own first Save.
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getByRole('button', { name: 'editor.addRoom' }))
    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[2])
    await user.click(screen.getByRole('button', { name: 'room.addPowerPoint' }))
    await user.type(screen.getByRole('textbox', { name: 'powerPoint.namePlaceholder' }), 'Wall Socket')
    await user.click(screen.getByRole('button', { name: /editor\.back/ }))
    await user.click(screen.getByRole('button', { name: 'editor.save: editor.newRoomName' }))

    expect(mockSaveRoomMutate).toHaveBeenCalledWith(
      {
        roomId: undefined,
        rowVersion: undefined,
        name: 'editor.newRoomName',
        sortOrder: 2,
        powerPoints: [{ powerPointId: undefined, name: 'Wall Socket', plugId: undefined }],
      },
      expect.any(Object)
    )
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

  it('FlatStructureEditor_ClickRoomSaveButton_CallsSaveRoomMutateWithOnlyThatRoomsData', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    const input = screen.getByDisplayValue('Office')
    await user.clear(input)
    await user.type(input, 'Office Renamed')
    await user.click(screen.getByRole('button', { name: 'editor.save: Office Renamed' }))

    expect(mockSaveRoomMutate).toHaveBeenCalledTimes(1)
    expect(mockSaveRoomMutate).toHaveBeenCalledWith(
      {
        roomId: 'room-1',
        rowVersion: 'AQID',
        name: 'Office Renamed',
        sortOrder: 0,
        powerPoints: [{ powerPointId: 'pp-1', name: 'Desk Outlet', plugId: 'PLUG-1' }],
      },
      expect.any(Object)
    )
  })

  it('FlatStructureEditor_SaveRoomWhileUnrelatedPowerPointNameIsBlank_PayloadHasNoTraceOfTheBlankPowerPoint', async () => {
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

    expect(mockSaveRoomMutate).toHaveBeenCalledWith(
      {
        roomId: 'room-2',
        rowVersion: 'AQID',
        name: 'Garage Renamed',
        sortOrder: 1,
        powerPoints: [{ powerPointId: 'pp-2', name: 'Charger Outlet', plugId: 'PLUG-2' }],
      },
      expect.any(Object)
    )
  })

  it('FlatStructureEditor_SaveRoomSucceeds_ShowsSuccessButtonDisabledAgainAndOriginalNameUpdated', async () => {
    const user = userEvent.setup()
    mockSaveRoomMutate.mockImplementation((_body, callbacks) => callbacks?.onSuccess?.(officeRoomResponse))
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    const input = screen.getByDisplayValue('Office')
    await user.clear(input)
    await user.type(input, 'Office Renamed')
    await user.click(screen.getByRole('button', { name: 'editor.save: Office Renamed' }))

    expect(screen.getByText('editor.saveSuccess')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'editor.save: Office Renamed' })).toBeDisabled()
  })

  it('FlatStructureEditor_SaveRoomFails_RevertsNameAndShowsSaveError', async () => {
    const user = userEvent.setup()
    mockSaveRoomMutate.mockImplementation((_body, callbacks) => callbacks?.onError?.())
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    const input = screen.getByDisplayValue('Office')
    await user.clear(input)
    await user.type(input, 'Study')
    await user.click(screen.getByRole('button', { name: 'editor.save: Study' }))

    expect(screen.getByDisplayValue('Office')).toBeInTheDocument()
    expect(screen.getByText('editor.saveError')).toBeInTheDocument()
  })

  it('FlatStructureEditor_DeleteRoomConfirm_CallsDeleteRoomMutateWithRoomIdAndRowVersion', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: 'room.delete' })[0])
    await user.click(screen.getByRole('button', { name: 'confirm.delete' }))

    expect(mockDeleteRoomMutate).toHaveBeenCalledWith(
      { roomId: 'room-1', rowVersion: 'AQID' },
      expect.any(Object)
    )
  })

  it('FlatStructureEditor_DeleteLastRemainingRoom_DoesNotCallMutation', async () => {
    const user = userEvent.setup()
    setupFlatStructure({
      data: seededResponse({
        rooms: [
          {
            roomId: 'room-1',
            name: 'Office',
            sortOrder: 0,
            rowVersion: 'AQID',
            powerPoints: [],
          },
        ],
      }),
    })

    renderEditor()
    await user.click(screen.getByRole('button', { name: 'room.delete' }))
    await user.click(screen.getByRole('button', { name: 'confirm.delete' }))

    expect(mockDeleteRoomMutate).not.toHaveBeenCalled()
  })

  it('FlatStructureEditor_DeleteRoomMutationPendingOnMount_DisablesDeleteAndAddRoomButtonsOnly', () => {
    setupFlatStructure({ data: seededResponse() })
    mockUseDeleteRoom.mockReturnValue({
      mutate: mockDeleteRoomMutate,
      isPending: true,
    } as unknown as ReturnType<typeof useDeleteRoom>)

    renderEditor()

    expect(screen.getAllByRole('button', { name: 'room.delete' })[0]).toBeDisabled()
    expect(screen.getAllByRole('button', { name: 'room.delete' })[1]).toBeDisabled()
    expect(screen.getByRole('button', { name: 'editor.addRoom' })).toBeDisabled()
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

    expect(mockSaveRoomMutate).toHaveBeenCalledWith(
      {
        roomId: 'room-1',
        rowVersion: 'AQID',
        name: 'Office Renamed',
        sortOrder: 0,
        powerPoints: [
          { powerPointId: 'pp-1', name: 'Desk Outlet', plugId: 'PLUG-1' },
          { powerPointId: undefined, name: 'Fridge Outlet', plugId: undefined },
        ],
      },
      expect.any(Object)
    )
  })

  it('FlatStructureEditor_CreatedRoomsRoomIdFlowsBackIntoDraft_SecondSaveIsRecognizedAsUpdate', async () => {
    const user = userEvent.setup()
    const createdResponse: RoomResponse = {
      roomId: 'new-room-id',
      name: 'NewB',
      sortOrder: 3,
      powerPoints: [],
      rowVersion: 'new-version',
    }
    mockSaveRoomMutate.mockImplementationOnce((_body, callbacks) => callbacks?.onSuccess?.(createdResponse))
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getByRole('button', { name: 'editor.addRoom' }))
    await user.click(screen.getByRole('button', { name: 'editor.addRoom' }))

    const newRoomInputs = screen.getAllByDisplayValue('editor.newRoomName')
    await user.clear(newRoomInputs[1])
    await user.type(newRoomInputs[1], 'NewB')
    await user.click(screen.getByRole('button', { name: 'editor.save: NewB' }))

    expect(mockSaveRoomMutate).toHaveBeenNthCalledWith(
      1,
      { roomId: undefined, rowVersion: undefined, name: 'NewB', sortOrder: 3, powerPoints: [] },
      expect.any(Object)
    )

    mockSaveRoomMutate.mockClear()

    const savedNewBInput = screen.getByDisplayValue('NewB')
    await user.clear(savedNewBInput)
    await user.type(savedNewBInput, 'NewB Renamed')
    await user.click(screen.getByRole('button', { name: 'editor.save: NewB Renamed' }))

    expect(mockSaveRoomMutate).toHaveBeenCalledWith(
      {
        roomId: 'new-room-id',
        rowVersion: 'new-version',
        name: 'NewB Renamed',
        sortOrder: 3,
        powerPoints: [],
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

    expect(mockDeleteRoomMutate).not.toHaveBeenCalled()
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

  it('FlatStructureEditor_ClickInRoomSaveButton_PersistsPowerPointEditScopedToJustThatRoom', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[0])
    const ppInput = screen.getByDisplayValue('Desk Outlet')
    await user.clear(ppInput)
    await user.type(ppInput, 'Desk Outlet Updated')
    await user.click(screen.getByRole('button', { name: 'editor.save' }))

    expect(mockSaveRoomMutate).toHaveBeenCalledWith(
      {
        roomId: 'room-1',
        rowVersion: 'AQID',
        name: 'Office',
        sortOrder: 0,
        powerPoints: [{ powerPointId: 'pp-1', name: 'Desk Outlet Updated', plugId: 'PLUG-1' }],
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
            rowVersion: 'AQID',
            powerPoints: [{ powerPointId: 'pp-1', name: 'Desk Outlet', plugId: 'PLUG-1', devices: [], rowVersion: 'AQID' }],
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

  it('FlatStructureEditor_SavingOneRoom_OtherDirtyRoomSaveButtonRemainsEnabledAndSavable', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    const officeInput = screen.getByDisplayValue('Office')
    await user.clear(officeInput)
    await user.type(officeInput, 'Office Renamed')
    await user.click(screen.getByRole('button', { name: 'editor.save: Office Renamed' }))

    expect(screen.getByRole('button', { name: 'editor.saving: Office Renamed' })).toBeDisabled()

    const garageInput = screen.getByDisplayValue('Garage')
    await user.clear(garageInput)
    await user.type(garageInput, 'Garage Renamed')

    expect(screen.getByRole('button', { name: 'editor.save: Garage Renamed' })).toBeEnabled()
    expect(mockSaveRoomMutate).toHaveBeenCalledTimes(1)
  })

  it('FlatStructureEditor_SavingOneRoomThenViewingUnrelatedRoomDetail_UnrelatedRoomSaveButtonNotDisabled', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    const officeInput = screen.getByDisplayValue('Office')
    await user.clear(officeInput)
    await user.type(officeInput, 'Office Renamed')
    await user.click(screen.getByRole('button', { name: 'editor.save: Office Renamed' }))

    await user.click(screen.getAllByRole('button', { name: /room\.powerPointsSummary/ })[1])
    const ppInput = screen.getByDisplayValue('Charger Outlet')
    await user.clear(ppInput)
    await user.type(ppInput, 'Charger Outlet Updated')

    const saveButton = screen.getByRole('button', { name: 'editor.save' })
    expect(saveButton).toBeEnabled()
  })

  it('FlatStructureEditor_RoomBlockedByPlugIdConflict_ShowsInlineConflictReasonNearThatRow', () => {
    setupFlatStructure({
      data: seededResponse({
        rooms: [
          {
            roomId: 'room-1',
            name: 'Office',
            sortOrder: 0,
            rowVersion: 'AQID',
            powerPoints: [{ powerPointId: 'pp-1', name: 'Desk Outlet', plugId: 'PLUG-1', devices: [], rowVersion: 'AQID' }],
          },
          {
            roomId: 'room-2',
            name: 'Garage',
            sortOrder: 1,
            rowVersion: 'AQID',
            powerPoints: [{ powerPointId: 'pp-2', name: 'Charger Outlet', plugId: 'PLUG-1', devices: [], rowVersion: 'AQID' }],
          },
        ],
      }),
    })

    renderEditor()

    const rows = screen.getAllByRole('listitem')
    expect(within(rows[0]).getByText('editor.plugIdConflict')).toBeInTheDocument()
    expect(within(rows[1]).getByText('editor.plugIdConflict')).toBeInTheDocument()
    expect(screen.getAllByText('editor.plugIdConflict')).toHaveLength(2)
  })

  it('FlatStructureEditor_RoomBlockedByBlankName_ShowsInlineBlankNameReasonNearThatRowOnly', async () => {
    const user = userEvent.setup()
    setupFlatStructure({ data: seededResponse() })

    renderEditor()
    const officeInput = screen.getByDisplayValue('Office')
    await user.clear(officeInput)

    const rows = screen.getAllByRole('listitem')
    expect(within(rows[0]).getByText('editor.blankNameError')).toBeInTheDocument()
    expect(within(rows[1]).queryByText('editor.blankNameError')).not.toBeInTheDocument()
    expect(screen.getAllByText('editor.blankNameError')).toHaveLength(1)
  })

  it('FlatStructureEditor_PowerPointIdQueryParamMatchesExistingPowerPoint_OpensThatRoomDirectly', () => {
    setupFlatStructure({ data: seededResponse() })

    renderEditor('flat-1', ['/settings/structure?powerPointId=pp-2'])

    expect(screen.getByText('Garage')).toBeInTheDocument()
    expect(screen.getByDisplayValue('Charger Outlet')).toBeInTheDocument()
  })

  it('FlatStructureEditor_PowerPointIdQueryParamStale_FallsBackToRoomListView', () => {
    setupFlatStructure({ data: seededResponse() })

    renderEditor('flat-1', ['/settings/structure?powerPointId=does-not-exist'])

    expect(screen.getByDisplayValue('Office')).toBeInTheDocument()
    expect(screen.getByDisplayValue('Garage')).toBeInTheDocument()
    expect(screen.queryByDisplayValue('Desk Outlet')).not.toBeInTheDocument()
  })
})
