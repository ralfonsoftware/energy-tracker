import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { DeviceEditor } from './DeviceEditor'
import type { DraftDevice } from './draftModel'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (k: string, opts?: Record<string, unknown>) => (opts?.value ? `${k}:${opts.value}` : k),
  }),
}))

const sampleDevice: DraftDevice = {
  key: 'device-1',
  name: 'Fridge',
  type: 'Kitchen appliance',
  manufacturer: 'Bosch',
  model: 'KGN36',
  consumptionApproach: 'None',
}

describe('DeviceEditor', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('DeviceEditor_EmptyName_SaveDisabled', () => {
    render(<DeviceEditor device={undefined} onSave={vi.fn()} onCancel={vi.fn()} />)

    expect(screen.getByRole('button', { name: 'device.save' })).toBeDisabled()
  })

  it('DeviceEditor_NameEntered_SaveEnabled', async () => {
    const user = userEvent.setup()
    render(<DeviceEditor device={undefined} onSave={vi.fn()} onCancel={vi.fn()} />)

    await user.type(screen.getByLabelText('device.namePlaceholder'), 'Toaster')

    expect(screen.getByRole('button', { name: 'device.save' })).toBeEnabled()
  })

  it('DeviceEditor_UnconfiguredDevice_RendersConsumptionNoteAndConfigureButton', () => {
    render(<DeviceEditor device={undefined} onSave={vi.fn()} onCancel={vi.fn()} />)

    expect(screen.getByText('device.consumptionNote')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'device.configureProfile' })).toBeInTheDocument()
  })

  it('DeviceEditor_ConfigureProfileTapped_ShowsChoiceStepCards', async () => {
    const user = userEvent.setup()
    render(<DeviceEditor device={undefined} onSave={vi.fn()} onCancel={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'device.configureProfile' }))

    expect(screen.getByText('device.consumptionApproach.euLabelTitle')).toBeInTheDocument()
    expect(screen.getByText('device.consumptionApproach.selfMeasuredTitle')).toBeInTheDocument()
  })

  it('DeviceEditor_ExistingDevice_PrefillsFields', () => {
    render(<DeviceEditor device={sampleDevice} onSave={vi.fn()} onCancel={vi.fn()} />)

    expect(screen.getByLabelText('device.namePlaceholder')).toHaveValue('Fridge')
    expect(screen.getByLabelText('device.manufacturerPlaceholder')).toHaveValue('Bosch')
  })

  it('DeviceEditor_SaveClicked_CallsOnSaveWithTrimmedName', async () => {
    const user = userEvent.setup()
    const onSave = vi.fn()
    render(<DeviceEditor device={undefined} onSave={onSave} onCancel={vi.fn()} />)

    await user.type(screen.getByLabelText('device.namePlaceholder'), '  Toaster  ')
    await user.click(screen.getByRole('button', { name: 'device.save' }))

    expect(onSave).toHaveBeenCalledWith(
      expect.objectContaining({ name: 'Toaster', type: '', manufacturer: '', model: '' })
    )
  })

  it('DeviceEditor_CancelClicked_CallsOnCancel', async () => {
    const user = userEvent.setup()
    const onCancel = vi.fn()
    render(<DeviceEditor device={undefined} onSave={vi.fn()} onCancel={onCancel} />)

    await user.click(screen.getByRole('button', { name: 'device.cancel' }))

    expect(onCancel).toHaveBeenCalled()
  })

  it('DeviceEditor_NewDevice_DefaultsConsumptionApproachToNone', async () => {
    const user = userEvent.setup()
    const onSave = vi.fn()
    render(<DeviceEditor device={undefined} onSave={onSave} onCancel={vi.fn()} />)

    await user.type(screen.getByLabelText('device.namePlaceholder'), 'Toaster')
    await user.click(screen.getByRole('button', { name: 'device.save' }))

    expect(onSave).toHaveBeenCalledWith(
      expect.objectContaining({ consumptionApproach: 'None', euLabelClass: undefined })
    )
  })

  it('DeviceEditor_EuLabelSelected_ShowsOnlyEuLabelFieldsHidesSelfMeasured', async () => {
    const user = userEvent.setup()
    render(<DeviceEditor device={undefined} onSave={vi.fn()} onCancel={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'device.configureProfile' }))
    await user.click(screen.getByRole('radio', { name: 'device.consumptionApproach.euLabelTitle' }))

    expect(screen.getByLabelText('device.euLabel.annualKwhLabel')).toBeInTheDocument()
    expect(screen.queryByText('device.selfMeasured.kwhLabelDaily')).not.toBeInTheDocument()
    expect(screen.queryByText('device.selfMeasured.kwhLabelWeekly')).not.toBeInTheDocument()
  })

  it('DeviceEditor_SelfMeasuredSelected_ShowsOnlySelfMeasuredFieldsHidesEuLabel', async () => {
    const user = userEvent.setup()
    render(<DeviceEditor device={undefined} onSave={vi.fn()} onCancel={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'device.configureProfile' }))
    await user.click(screen.getByRole('radio', { name: 'device.consumptionApproach.selfMeasuredTitle' }))

    expect(screen.getByText('device.selfMeasured.kwhLabelDaily')).toBeInTheDocument()
    expect(screen.queryByLabelText('device.euLabel.annualKwhLabel')).not.toBeInTheDocument()
  })

  it('DeviceEditor_EuAnnualKwhEntered_ShowsDerivedDailyEstimate', async () => {
    const user = userEvent.setup()
    render(<DeviceEditor device={undefined} onSave={vi.fn()} onCancel={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'device.configureProfile' }))
    await user.click(screen.getByRole('radio', { name: 'device.consumptionApproach.euLabelTitle' }))
    await user.type(screen.getByLabelText('device.euLabel.annualKwhLabel'), '365')

    expect(screen.getByText('device.euLabel.dailyEstimate:1 kWh')).toBeInTheDocument()
  })

  it('DeviceEditor_SelfMeasuredToggleSwitchedToWeekly_UpdatesKwhInputLabelInstantly', async () => {
    const user = userEvent.setup()
    render(<DeviceEditor device={undefined} onSave={vi.fn()} onCancel={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'device.configureProfile' }))
    await user.click(screen.getByRole('radio', { name: 'device.consumptionApproach.selfMeasuredTitle' }))

    expect(screen.getByText('device.selfMeasured.kwhLabelDaily')).toBeInTheDocument()

    await user.click(screen.getByRole('radio', { name: 'device.selfMeasured.periodWeekly' }))

    expect(screen.queryByText('device.selfMeasured.kwhLabelDaily')).not.toBeInTheDocument()
    expect(screen.getByText('device.selfMeasured.kwhLabelWeekly')).toBeInTheDocument()
  })

  it('DeviceEditor_EuLabelApproachMissingAnnualKwh_SaveDisabled', async () => {
    const user = userEvent.setup()
    render(<DeviceEditor device={undefined} onSave={vi.fn()} onCancel={vi.fn()} />)

    await user.type(screen.getByLabelText('device.namePlaceholder'), 'Fridge')
    await user.click(screen.getByRole('button', { name: 'device.configureProfile' }))
    await user.click(screen.getByRole('radio', { name: 'device.consumptionApproach.euLabelTitle' }))

    expect(screen.getByRole('button', { name: 'device.save' })).toBeDisabled()
  })

  it('DeviceEditor_EuLabelApproachWithKwhOnly_SaveEnabledAndCallsOnSaveWithUndefinedClass', async () => {
    const user = userEvent.setup()
    const onSave = vi.fn()
    render(<DeviceEditor device={undefined} onSave={onSave} onCancel={vi.fn()} />)

    await user.type(screen.getByLabelText('device.namePlaceholder'), 'Fridge')
    await user.click(screen.getByRole('button', { name: 'device.configureProfile' }))
    await user.click(screen.getByRole('radio', { name: 'device.consumptionApproach.euLabelTitle' }))
    await user.type(screen.getByLabelText('device.euLabel.annualKwhLabel'), '150')

    expect(screen.getByRole('button', { name: 'device.save' })).toBeEnabled()

    await user.click(screen.getByRole('button', { name: 'device.save' }))

    expect(onSave).toHaveBeenCalledWith(
      expect.objectContaining({ euLabelClass: undefined, euAnnualKwh: 150 })
    )
  })

  it('DeviceEditor_SelfMeasuredApproachWithValidKwh_CallsOnSaveWithApproachFields', async () => {
    const user = userEvent.setup()
    const onSave = vi.fn()
    render(<DeviceEditor device={undefined} onSave={onSave} onCancel={vi.fn()} />)

    await user.type(screen.getByLabelText('device.namePlaceholder'), 'Fridge')
    await user.click(screen.getByRole('button', { name: 'device.configureProfile' }))
    await user.click(screen.getByRole('radio', { name: 'device.consumptionApproach.selfMeasuredTitle' }))
    await user.type(screen.getByLabelText('device.selfMeasured.kwhLabelDaily'), '5')
    await user.click(screen.getByRole('button', { name: 'device.save' }))

    expect(onSave).toHaveBeenCalledWith(
      expect.objectContaining({
        consumptionApproach: 'SelfMeasured',
        selfMeasuredKwh: 5,
        selfMeasuredPeriod: 'Daily',
      })
    )
  })

  it('DeviceEditor_ExistingDeviceWithConsumptionProfile_PreservesItUnchangedOnSave', async () => {
    const user = userEvent.setup()
    const onSave = vi.fn()
    const deviceWithProfile: DraftDevice = {
      ...sampleDevice,
      consumptionApproach: 'EuLabel',
      euLabelClass: 'A+++',
      euAnnualKwh: 150,
    }
    render(<DeviceEditor device={deviceWithProfile} onSave={onSave} onCancel={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'device.save' }))

    expect(onSave).toHaveBeenCalledWith(
      expect.objectContaining({ consumptionApproach: 'EuLabel', euLabelClass: 'A+++', euAnnualKwh: 150 })
    )
  })
})
