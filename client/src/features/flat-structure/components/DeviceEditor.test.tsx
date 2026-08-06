import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest'
import { toLocalDateString, toLocalMidnightIsoString } from '@/lib/localDate'
import { DeviceEditor } from './DeviceEditor'
import type { DraftDevice } from './draftModel'
import type { DeviceResponse } from '@/features/flat-structure/api/flatStructureApi'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (k: string, opts?: Record<string, unknown>) => (opts?.value ? `${k}:${opts.value}` : k),
  }),
}))

vi.mock('@/features/flat-structure/hooks/useSaveDevice')
import { useSaveDevice } from '@/features/flat-structure/hooks/useSaveDevice'
const mockUseSaveDevice = vi.mocked(useSaveDevice)

const mockMutate = vi.fn()

type MutationState = {
  mutate: typeof mockMutate
  isPending: boolean
  isError: boolean
  isSuccess: boolean
  error: (Error & { detail?: string }) | null
  data: DeviceResponse | undefined
}

let mockMutationState: MutationState

function defaultMutationState(): MutationState {
  return {
    mutate: mockMutate,
    isPending: false,
    isError: false,
    isSuccess: false,
    error: null,
    data: undefined,
  }
}

const sampleDevice: DraftDevice = {
  key: 'device-1',
  deviceId: 'server-device-1',
  rowVersion: 'AQID',
  name: 'Fridge',
  type: 'Kitchen appliance',
  manufacturer: 'Bosch',
  model: 'KGN36',
  consumptionApproach: 'None',
}

const sampleResponse: DeviceResponse = {
  deviceId: 'new-device-id',
  name: 'Toaster',
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
  rowVersion: 'AQIE',
}

function renderEditor(device: DraftDevice | undefined, onSaved = vi.fn(), onCancel = vi.fn()) {
  return render(
    <DeviceEditor
      device={device}
      flatId="flat-1"
      powerPointId="pp-1"
      onSaved={onSaved}
      onCancel={onCancel}
    />
  )
}

describe('DeviceEditor', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockMutationState = defaultMutationState()
    mockUseSaveDevice.mockReturnValue(
      mockMutationState as unknown as ReturnType<typeof useSaveDevice>
    )
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('DeviceEditor_EmptyName_SaveDisabled', () => {
    renderEditor(undefined)

    expect(screen.getByRole('button', { name: 'device.save' })).toBeDisabled()
  })

  it('DeviceEditor_NameEntered_SaveEnabled', async () => {
    const user = userEvent.setup()
    renderEditor(undefined)

    await user.type(screen.getByLabelText('device.namePlaceholder'), 'Toaster')

    expect(screen.getByRole('button', { name: 'device.save' })).toBeEnabled()
  })

  it('DeviceEditor_UnconfiguredDevice_RendersConsumptionNoteAndConfigureButton', () => {
    renderEditor(undefined)

    expect(screen.getByText('device.consumptionNote')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'device.configureProfile' })).toBeInTheDocument()
  })

  it('DeviceEditor_ConfigureProfileTapped_ShowsChoiceStepCards', async () => {
    const user = userEvent.setup()
    renderEditor(undefined)

    await user.click(screen.getByRole('button', { name: 'device.configureProfile' }))

    expect(screen.getByText('device.consumptionApproach.euLabelTitle')).toBeInTheDocument()
    expect(screen.getByText('device.consumptionApproach.selfMeasuredTitle')).toBeInTheDocument()
  })

  it('DeviceEditor_ExistingDevice_PrefillsFields', () => {
    renderEditor(sampleDevice)

    expect(screen.getByLabelText('device.namePlaceholder')).toHaveValue('Fridge')
    expect(screen.getByLabelText('device.manufacturerPlaceholder')).toHaveValue('Bosch')
  })

  it('DeviceEditor_SaveClicked_CallsMutateWithTrimmedNameAndNoDeviceIdForNewDevice', async () => {
    const user = userEvent.setup()
    renderEditor(undefined)

    await user.type(screen.getByLabelText('device.namePlaceholder'), '  Toaster  ')
    await user.click(screen.getByRole('button', { name: 'device.save' }))

    expect(mockMutate).toHaveBeenCalledWith(
      expect.objectContaining({
        name: 'Toaster',
        type: undefined,
        manufacturer: undefined,
        model: undefined,
        deviceId: undefined,
        rowVersion: undefined,
      })
    )
  })

  it('DeviceEditor_ExistingDeviceSaveClicked_CallsMutateWithDeviceIdAndRowVersion', async () => {
    const user = userEvent.setup()
    renderEditor(sampleDevice)

    await user.click(screen.getByRole('button', { name: 'device.save' }))

    expect(mockMutate).toHaveBeenCalledWith(
      expect.objectContaining({ deviceId: 'server-device-1', rowVersion: 'AQID', name: 'Fridge' })
    )
  })

  it('DeviceEditor_CancelClicked_CallsOnCancel', async () => {
    const user = userEvent.setup()
    const onCancel = vi.fn()
    renderEditor(undefined, vi.fn(), onCancel)

    await user.click(screen.getByRole('button', { name: 'device.cancel' }))

    expect(onCancel).toHaveBeenCalled()
  })

  it('DeviceEditor_NewDevice_DefaultsConsumptionApproachToNone', async () => {
    const user = userEvent.setup()
    renderEditor(undefined)

    await user.type(screen.getByLabelText('device.namePlaceholder'), 'Toaster')
    await user.click(screen.getByRole('button', { name: 'device.save' }))

    expect(mockMutate).toHaveBeenCalledWith(
      expect.objectContaining({ consumptionApproach: 'None', euLabelClass: undefined })
    )
  })

  it('DeviceEditor_EuLabelSelected_ShowsOnlyEuLabelFieldsHidesSelfMeasured', async () => {
    const user = userEvent.setup()
    renderEditor(undefined)

    await user.click(screen.getByRole('button', { name: 'device.configureProfile' }))
    await user.click(screen.getByRole('radio', { name: 'device.consumptionApproach.euLabelTitle' }))

    expect(screen.getByLabelText('device.euLabel.annualKwhLabel')).toBeInTheDocument()
    expect(screen.queryByText('device.selfMeasured.kwhLabelDaily')).not.toBeInTheDocument()
    expect(screen.queryByText('device.selfMeasured.kwhLabelWeekly')).not.toBeInTheDocument()
  })

  it('DeviceEditor_SelfMeasuredSelected_ShowsOnlySelfMeasuredFieldsHidesEuLabel', async () => {
    const user = userEvent.setup()
    renderEditor(undefined)

    await user.click(screen.getByRole('button', { name: 'device.configureProfile' }))
    await user.click(screen.getByRole('radio', { name: 'device.consumptionApproach.selfMeasuredTitle' }))

    expect(screen.getByText('device.selfMeasured.kwhLabelDaily')).toBeInTheDocument()
    expect(screen.queryByLabelText('device.euLabel.annualKwhLabel')).not.toBeInTheDocument()
  })

  it('DeviceEditor_EuAnnualKwhEntered_ShowsDerivedDailyEstimate', async () => {
    const user = userEvent.setup()
    renderEditor(undefined)

    await user.click(screen.getByRole('button', { name: 'device.configureProfile' }))
    await user.click(screen.getByRole('radio', { name: 'device.consumptionApproach.euLabelTitle' }))
    await user.type(screen.getByLabelText('device.euLabel.annualKwhLabel'), '365')

    expect(screen.getByText('device.euLabel.dailyEstimate:1 kWh')).toBeInTheDocument()
  })

  it('DeviceEditor_SelfMeasuredToggleSwitchedToWeekly_UpdatesKwhInputLabelInstantly', async () => {
    const user = userEvent.setup()
    renderEditor(undefined)

    await user.click(screen.getByRole('button', { name: 'device.configureProfile' }))
    await user.click(screen.getByRole('radio', { name: 'device.consumptionApproach.selfMeasuredTitle' }))

    expect(screen.getByText('device.selfMeasured.kwhLabelDaily')).toBeInTheDocument()

    await user.click(screen.getByRole('radio', { name: 'device.selfMeasured.periodWeekly' }))

    expect(screen.queryByText('device.selfMeasured.kwhLabelDaily')).not.toBeInTheDocument()
    expect(screen.getByText('device.selfMeasured.kwhLabelWeekly')).toBeInTheDocument()
  })

  it('DeviceEditor_EuLabelApproachMissingAnnualKwh_SaveDisabled', async () => {
    const user = userEvent.setup()
    renderEditor(undefined)

    await user.type(screen.getByLabelText('device.namePlaceholder'), 'Fridge')
    await user.click(screen.getByRole('button', { name: 'device.configureProfile' }))
    await user.click(screen.getByRole('radio', { name: 'device.consumptionApproach.euLabelTitle' }))

    expect(screen.getByRole('button', { name: 'device.save' })).toBeDisabled()
  })

  it('DeviceEditor_EuLabelApproachWithKwhOnly_SaveEnabledAndCallsMutateWithUndefinedClass', async () => {
    const user = userEvent.setup()
    renderEditor(undefined)

    await user.type(screen.getByLabelText('device.namePlaceholder'), 'Fridge')
    await user.click(screen.getByRole('button', { name: 'device.configureProfile' }))
    await user.click(screen.getByRole('radio', { name: 'device.consumptionApproach.euLabelTitle' }))
    await user.type(screen.getByLabelText('device.euLabel.annualKwhLabel'), '150')

    expect(screen.getByRole('button', { name: 'device.save' })).toBeEnabled()

    await user.click(screen.getByRole('button', { name: 'device.save' }))

    expect(mockMutate).toHaveBeenCalledWith(
      expect.objectContaining({ euLabelClass: undefined, euAnnualKwh: 150 })
    )
  })

  it('DeviceEditor_SelfMeasuredApproachWithValidKwh_CallsMutateWithApproachFields', async () => {
    const user = userEvent.setup()
    renderEditor(undefined)

    await user.type(screen.getByLabelText('device.namePlaceholder'), 'Fridge')
    await user.click(screen.getByRole('button', { name: 'device.configureProfile' }))
    await user.click(screen.getByRole('radio', { name: 'device.consumptionApproach.selfMeasuredTitle' }))
    await user.type(screen.getByLabelText('device.selfMeasured.kwhLabelDaily'), '5')
    await user.click(screen.getByRole('button', { name: 'device.save' }))

    expect(mockMutate).toHaveBeenCalledWith(
      expect.objectContaining({
        consumptionApproach: 'SelfMeasured',
        selfMeasuredKwh: 5,
        selfMeasuredPeriod: 'Daily',
      })
    )
  })

  it('DeviceEditor_ExistingDeviceWithConsumptionProfile_PreservesItUnchangedOnSave', async () => {
    const user = userEvent.setup()
    const deviceWithProfile: DraftDevice = {
      ...sampleDevice,
      consumptionApproach: 'EuLabel',
      euLabelClass: 'A+++',
      euAnnualKwh: 150,
    }
    renderEditor(deviceWithProfile)

    await user.click(screen.getByRole('button', { name: 'device.save' }))

    expect(mockMutate).toHaveBeenCalledWith(
      expect.objectContaining({ consumptionApproach: 'EuLabel', euLabelClass: 'A+++', euAnnualKwh: 150 })
    )
  })

  it('DeviceEditor_NewDevice_PrefillsInUseSinceWithTodayAndLeavesDecommissionedEmpty', () => {
    renderEditor(undefined)

    expect(screen.getByLabelText('device.inUseSinceLabel')).toHaveValue(toLocalDateString(new Date()))
    expect(screen.getByLabelText('device.decommissionedDateLabel')).toHaveValue('')
  })

  it('DeviceEditor_ExistingDeviceWithBothDatesSet_DisplaysThemCorrectly', () => {
    const deviceWithDates: DraftDevice = {
      ...sampleDevice,
      inUseSince: '2026-01-15T12:00:00.000Z',
      decommissionedDate: '2026-06-30T12:00:00.000Z',
    }
    renderEditor(deviceWithDates)

    expect(screen.getByLabelText('device.inUseSinceLabel')).toHaveValue('2026-01-15')
    expect(screen.getByLabelText('device.decommissionedDateLabel')).toHaveValue('2026-06-30')
  })

  it('DeviceEditor_ClearingEitherDateFieldAndSaving_PassesUndefinedNotEmptyString', async () => {
    const user = userEvent.setup()
    const deviceWithDates: DraftDevice = {
      ...sampleDevice,
      inUseSince: '2026-01-15T12:00:00.000Z',
      decommissionedDate: '2026-06-30T12:00:00.000Z',
    }
    renderEditor(deviceWithDates)

    await user.clear(screen.getByLabelText('device.inUseSinceLabel'))
    await user.clear(screen.getByLabelText('device.decommissionedDateLabel'))
    await user.click(screen.getByRole('button', { name: 'device.save' }))

    expect(mockMutate).toHaveBeenCalledWith(
      expect.objectContaining({ inUseSince: undefined, decommissionedDate: undefined })
    )
  })

  it('DeviceEditor_SaveWithBothDatesSet_ConvertsToLocalMidnightIsoString', async () => {
    const user = userEvent.setup()
    renderEditor(undefined)

    await user.type(screen.getByLabelText('device.namePlaceholder'), 'Fridge')
    await user.clear(screen.getByLabelText('device.inUseSinceLabel'))
    const decommissionedInput = screen.getByLabelText('device.decommissionedDateLabel')
    await user.type(decommissionedInput, '2026-06-30')
    await user.click(screen.getByRole('button', { name: 'device.save' }))

    expect(mockMutate).toHaveBeenCalledWith(
      expect.objectContaining({ decommissionedDate: toLocalMidnightIsoString('2026-06-30') })
    )
  })

  it('DeviceEditor_MutationPending_DisablesSaveAndCancelAndShowsSavingLabel', () => {
    mockMutationState.isPending = true
    renderEditor(sampleDevice)

    expect(screen.getByRole('button', { name: 'device.saving' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'device.cancel' })).toBeDisabled()
  })

  it('DeviceEditor_MutationError_ShowsDetailBannerAndKeepsFormOpenWithoutCallingOnCancel', () => {
    mockMutationState.isError = true
    mockMutationState.error = Object.assign(new Error('failed'), { detail: 'Could not save this device.' })
    const onCancel = vi.fn()
    renderEditor(sampleDevice, vi.fn(), onCancel)

    expect(screen.getByRole('alert')).toHaveTextContent('Could not save this device.')
    expect(onCancel).not.toHaveBeenCalled()
  })

  it('DeviceEditor_MutationErrorWithoutDetail_ShowsGenericSaveErrorMessage', () => {
    mockMutationState.isError = true
    mockMutationState.error = new Error('failed') as Error & { detail?: string }
    renderEditor(sampleDevice)

    expect(screen.getByRole('alert')).toHaveTextContent('device.saveError')
  })

  it('DeviceEditor_MutationSuccess_ShowsSuccessMessage', () => {
    mockMutationState.isSuccess = true
    mockMutationState.data = sampleResponse
    renderEditor(undefined)

    expect(screen.getByRole('status')).toHaveTextContent('device.saveSuccess')
  })

  it('DeviceEditor_MutationSucceeds_CallsOnSavedWithMappedDeviceAfterBriefDelay', async () => {
    vi.useFakeTimers()
    const onSaved = vi.fn()
    mockMutationState.isSuccess = true
    mockMutationState.data = sampleResponse
    renderEditor(undefined, onSaved)

    expect(onSaved).not.toHaveBeenCalled()

    await vi.advanceTimersByTimeAsync(1000)

    expect(onSaved).toHaveBeenCalledWith(
      expect.objectContaining({ deviceId: 'new-device-id', name: 'Toaster', rowVersion: 'AQIE' })
    )
  })
})
