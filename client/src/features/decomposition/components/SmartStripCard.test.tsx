import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { SmartStripCard } from '@/features/decomposition/components/SmartStripCard'
import type {
  DeviceDecomposition,
  SubDeviceDecomposition,
} from '@/features/decomposition/api/decompositionApi'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

function makeSubDevice(overrides: Partial<SubDeviceDecomposition> = {}): SubDeviceDecomposition {
  return {
    deviceId: 'sub-1',
    name: 'Lamp',
    kwh: 1,
    cost: 0.2,
    isConfigured: true,
    isUnconfigured: false,
    ...overrides,
  }
}

function makeStrip(subDevices: SubDeviceDecomposition[] | null): DeviceDecomposition {
  return {
    deviceId: 'strip-1',
    name: 'Power Strip',
    kwh: 5,
    cost: 1.2,
    approach: 'Measured',
    isSmartStrip: true,
    subDevices,
  }
}

describe('SmartStripCard', () => {
  it('SmartStripCard_HeaderRendered_ShowsNameKwhCostAndMeasuredBadge', () => {
    render(<SmartStripCard device={makeStrip([])} onConfigure={vi.fn()} />)

    expect(screen.getByText('Power Strip')).toBeInTheDocument()
    expect(screen.getByText(/5/)).toBeInTheDocument()
    expect(screen.getByText('deviceCard.badgeMeasured')).toBeInTheDocument()
  })

  it('SmartStripCard_EmptySubDevices_RendersHeaderOnlyWithoutError', () => {
    render(<SmartStripCard device={makeStrip([])} onConfigure={vi.fn()} />)

    expect(screen.queryByText('smartStripCard.configureHint')).not.toBeInTheDocument()
  })

  it('SmartStripCard_ConfiguredSubDevice_RendersFullOpacityWithoutHint', () => {
    render(
      <SmartStripCard
        device={makeStrip([makeSubDevice({ name: 'Lamp', isConfigured: true, isUnconfigured: false })])}
        onConfigure={vi.fn()}
      />
    )

    const row = screen.getByText('Lamp').closest('div[class*="opacity"]') as HTMLElement
    expect(row).not.toHaveClass('opacity-[0.45]')
    expect(screen.queryByText('smartStripCard.configureHint')).not.toBeInTheDocument()
  })

  it('SmartStripCard_UnconfiguredSubDevice_RendersDimmedWithConfigureHintChip', () => {
    render(
      <SmartStripCard
        device={makeStrip([
          makeSubDevice({ name: 'Unknown Plug', isConfigured: false, isUnconfigured: true }),
        ])}
        onConfigure={vi.fn()}
      />
    )

    const row = screen.getByText('Unknown Plug').closest('div[class*="opacity"]') as HTMLElement
    expect(row).toHaveClass('opacity-[0.45]')
    expect(screen.getByText('smartStripCard.configureHint')).toBeInTheDocument()
  })

  it('SmartStripCard_ConfigureHintChipClicked_CallsOnConfigure', async () => {
    const user = userEvent.setup()
    const onConfigure = vi.fn()
    render(
      <SmartStripCard
        device={makeStrip([makeSubDevice({ isConfigured: false, isUnconfigured: true })])}
        onConfigure={onConfigure}
      />
    )

    await user.click(screen.getByText('smartStripCard.configureHint'))

    expect(onConfigure).toHaveBeenCalled()
  })

  it('SmartStripCard_MixedSubDevices_OrdersConfiguredFirstThenByDescendingKwhWithinGroup', () => {
    render(
      <SmartStripCard
        device={makeStrip([
          makeSubDevice({ name: 'UnconfiguredLow', kwh: 1, isConfigured: false, isUnconfigured: true }),
          makeSubDevice({ name: 'ConfiguredLow', kwh: 2, isConfigured: true, isUnconfigured: false }),
          makeSubDevice({ name: 'UnconfiguredHigh', kwh: 9, isConfigured: false, isUnconfigured: true }),
          makeSubDevice({ name: 'ConfiguredHigh', kwh: 8, isConfigured: true, isUnconfigured: false }),
        ])}
        onConfigure={vi.fn()}
      />
    )

    const configuredHigh = screen.getByText('ConfiguredHigh')
    const configuredLow = screen.getByText('ConfiguredLow')
    const unconfiguredHigh = screen.getByText('UnconfiguredHigh')
    const unconfiguredLow = screen.getByText('UnconfiguredLow')

    expect(
      configuredHigh.compareDocumentPosition(configuredLow) & Node.DOCUMENT_POSITION_FOLLOWING
    ).toBeTruthy()
    expect(
      configuredLow.compareDocumentPosition(unconfiguredHigh) & Node.DOCUMENT_POSITION_FOLLOWING
    ).toBeTruthy()
    expect(
      unconfiguredHigh.compareDocumentPosition(unconfiguredLow) & Node.DOCUMENT_POSITION_FOLLOWING
    ).toBeTruthy()
  })
})
