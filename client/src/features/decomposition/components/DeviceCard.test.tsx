import { render, screen } from '@testing-library/react'
import { DeviceCard } from '@/features/decomposition/components/DeviceCard'
import type { DeviceDecomposition } from '@/features/decomposition/api/decompositionApi'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

function makeDevice(overrides: Partial<DeviceDecomposition> = {}): DeviceDecomposition {
  return {
    deviceId: 'device-1',
    powerPointId: 'pp-1',
    name: 'Fridge',
    kwh: 12.5,
    cost: 3.1,
    approach: 'Measured',
    isSmartStrip: false,
    subDevices: null,
    ...overrides,
  }
}

describe('DeviceCard', () => {
  it('DeviceCard_MeasuredApproach_RendersMeasuredBadgeAndNoDisclaimer', () => {
    render(<DeviceCard device={makeDevice({ approach: 'Measured' })} />)

    expect(screen.getByText('deviceCard.badgeMeasured')).toBeInTheDocument()
    expect(screen.queryByText('deviceCard.disclaimerEuLabel')).not.toBeInTheDocument()
    expect(screen.queryByText('deviceCard.disclaimerSelfMeasured')).not.toBeInTheDocument()
  })

  it('DeviceCard_MeasuredApproach_RendersNameKwhAndCost', () => {
    render(<DeviceCard device={makeDevice({ name: 'Fridge', kwh: 12.5, cost: 3.1 })} />)

    expect(screen.getByText('Fridge')).toBeInTheDocument()
    expect(screen.getByText(/12.5/)).toBeInTheDocument()
    expect(screen.getByText(/3[.,]10|3[.,]1\b/)).toBeInTheDocument()
  })

  it('DeviceCard_EuLabelApproach_RendersEstimatedBadgeAndEuLabelDisclaimer', () => {
    render(<DeviceCard device={makeDevice({ approach: 'EuLabel' })} />)

    expect(screen.getByText('deviceCard.badgeEstimated')).toBeInTheDocument()
    expect(screen.getByText('deviceCard.disclaimerEuLabel')).toBeInTheDocument()
  })

  it('DeviceCard_SelfMeasuredApproach_RendersEstimatedBadgeAndSelfMeasuredDisclaimer', () => {
    render(<DeviceCard device={makeDevice({ approach: 'SelfMeasured' })} />)

    expect(screen.getByText('deviceCard.badgeEstimated')).toBeInTheDocument()
    expect(screen.getByText('deviceCard.disclaimerSelfMeasured')).toBeInTheDocument()
  })
})
