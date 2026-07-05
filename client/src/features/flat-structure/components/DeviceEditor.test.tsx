import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { DeviceEditor } from './DeviceEditor'
import type { DraftDevice } from './draftModel'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
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

  it('DeviceEditor_AlwaysRendersConsumptionNote', () => {
    render(<DeviceEditor device={undefined} onSave={vi.fn()} onCancel={vi.fn()} />)

    expect(screen.getByText('device.consumptionNote')).toBeInTheDocument()
  })

  it('DeviceEditor_NoConsumptionApproachChoiceUIRendered', () => {
    render(<DeviceEditor device={undefined} onSave={vi.fn()} onCancel={vi.fn()} />)

    expect(screen.queryByText(/EuLabel/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/SelfMeasured/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/smart plug connected/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/estimated usage/i)).not.toBeInTheDocument()
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

  it('DeviceEditor_ExistingDeviceWithConsumptionProfile_PreservesItUnchangedOnSave', async () => {
    const user = userEvent.setup()
    const onSave = vi.fn()
    const deviceWithProfile: DraftDevice = {
      ...sampleDevice,
      consumptionApproach: 'EuLabel',
      euLabelClass: 'A+++',
    }
    render(<DeviceEditor device={deviceWithProfile} onSave={onSave} onCancel={vi.fn()} />)

    await user.click(screen.getByRole('button', { name: 'device.save' }))

    expect(onSave).toHaveBeenCalledWith(
      expect.objectContaining({ consumptionApproach: 'EuLabel', euLabelClass: 'A+++' })
    )
  })
})
