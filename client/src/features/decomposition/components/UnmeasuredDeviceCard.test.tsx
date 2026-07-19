import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { UnmeasuredDeviceCard } from '@/features/decomposition/components/UnmeasuredDeviceCard'
import type { DeviceDecomposition } from '@/features/decomposition/api/decompositionApi'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

function makeDevice(overrides: Partial<DeviceDecomposition> = {}): DeviceDecomposition {
  return {
    deviceId: 'device-1',
    powerPointId: 'pp-1',
    name: 'Old Lamp',
    kwh: 0,
    cost: 0,
    approach: 'None',
    isSmartStrip: false,
    subDevices: null,
    ...overrides,
  }
}

describe('UnmeasuredDeviceCard', () => {
  it('UnmeasuredDeviceCard_Rendered_ShowsNameAndHintText', () => {
    render(<UnmeasuredDeviceCard device={makeDevice({ name: 'Old Lamp' })} onConfigure={vi.fn()} />)

    expect(screen.getByText('Old Lamp')).toBeInTheDocument()
    expect(screen.getByText('roomCard.unmeasuredHint')).toBeInTheDocument()
  })

  it('UnmeasuredDeviceCard_Rendered_ShowsConfigureProfileButton', () => {
    render(<UnmeasuredDeviceCard device={makeDevice()} onConfigure={vi.fn()} />)

    expect(screen.getByText('roomCard.configureProfile')).toBeInTheDocument()
  })

  it('UnmeasuredDeviceCard_ConfigureButtonClicked_CallsOnConfigure', async () => {
    const user = userEvent.setup()
    const onConfigure = vi.fn()
    render(<UnmeasuredDeviceCard device={makeDevice()} onConfigure={onConfigure} />)

    await user.click(screen.getByText('roomCard.configureProfile'))

    expect(onConfigure).toHaveBeenCalled()
  })

  it('UnmeasuredDeviceCard_Rendered_DoesNotRenderKwhOrCostFigure', () => {
    const { container } = render(
      <UnmeasuredDeviceCard device={makeDevice({ kwh: 0, cost: 0 })} onConfigure={vi.fn()} />,
    )

    expect(screen.queryByText(/kWh/)).not.toBeInTheDocument()
    expect(container).not.toHaveTextContent('0')
  })

  it('UnmeasuredDeviceCard_Rendered_DimsOuterContainer', () => {
    const { container } = render(<UnmeasuredDeviceCard device={makeDevice()} onConfigure={vi.fn()} />)

    expect(container.firstChild).toHaveClass('opacity-[0.45]')
  })
})
