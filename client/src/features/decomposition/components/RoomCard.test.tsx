import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { RoomCard } from '@/features/decomposition/components/RoomCard'
import type { DeviceDecomposition, RoomDecomposition } from '@/features/decomposition/api/decompositionApi'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

function makeDevice(overrides: Partial<DeviceDecomposition> = {}): DeviceDecomposition {
  return {
    deviceId: 'device-1',
    name: 'Device',
    kwh: 1,
    cost: 0.2,
    approach: 'Measured',
    isSmartStrip: false,
    subDevices: null,
    ...overrides,
  }
}

function makeRoom(overrides: Partial<RoomDecomposition> = {}): RoomDecomposition {
  return {
    roomId: 'room-1',
    roomName: 'Living Room',
    kwh: 10,
    cost: 2.5,
    devices: [],
    ...overrides,
  }
}

describe('RoomCard', () => {
  it('RoomCard_MixedDevices_RendersMeasuredGroupBeforeEstimatedGroup', () => {
    const room = makeRoom({
      devices: [
        makeDevice({ deviceId: 'd1', name: 'Estimated Low', approach: 'EuLabel', kwh: 3 }),
        makeDevice({ deviceId: 'd2', name: 'Measured Low', approach: 'Measured', kwh: 2 }),
        makeDevice({ deviceId: 'd3', name: 'Estimated High', approach: 'SelfMeasured', kwh: 9 }),
        makeDevice({ deviceId: 'd4', name: 'Measured High', approach: 'Measured', kwh: 8 }),
      ],
    })

    render(<RoomCard room={room} onConfigureDevice={vi.fn()} />)

    const measuredHigh = screen.getByText('Measured High')
    const measuredLow = screen.getByText('Measured Low')
    const estimatedHigh = screen.getByText('Estimated High')
    const estimatedLow = screen.getByText('Estimated Low')

    expect(
      measuredHigh.compareDocumentPosition(measuredLow) & Node.DOCUMENT_POSITION_FOLLOWING
    ).toBeTruthy()
    expect(
      measuredLow.compareDocumentPosition(estimatedHigh) & Node.DOCUMENT_POSITION_FOLLOWING
    ).toBeTruthy()
    expect(
      estimatedHigh.compareDocumentPosition(estimatedLow) & Node.DOCUMENT_POSITION_FOLLOWING
    ).toBeTruthy()
  })

  it('RoomCard_SmartStripInMeasuredGroup_InterleavedByKwhWithDeviceCards', () => {
    const room = makeRoom({
      devices: [
        makeDevice({ deviceId: 'd1', name: 'Measured Device', approach: 'Measured', kwh: 3 }),
        makeDevice({
          deviceId: 'd2',
          name: 'Power Strip',
          approach: 'Measured',
          isSmartStrip: true,
          kwh: 9,
          subDevices: [],
        }),
      ],
    })

    render(<RoomCard room={room} onConfigureDevice={vi.fn()} />)

    const strip = screen.getByText('Power Strip')
    const device = screen.getByText('Measured Device')
    expect(strip.compareDocumentPosition(device) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy()
  })

  it('RoomCard_NoneApproachNonStripDevice_IsExcludedFromList', () => {
    const room = makeRoom({
      devices: [
        makeDevice({ deviceId: 'd1', name: 'Real Device', approach: 'Measured', kwh: 5 }),
        makeDevice({ deviceId: 'd2', name: 'Zero Device', approach: 'None', isSmartStrip: false, kwh: 0 }),
      ],
    })

    render(<RoomCard room={room} onConfigureDevice={vi.fn()} />)

    expect(screen.getByText('Real Device')).toBeInTheDocument()
    expect(screen.queryByText('Zero Device')).not.toBeInTheDocument()
  })

  it('RoomCard_EmptyDevices_RendersDirectConsumptionFallback', () => {
    const room = makeRoom({ devices: [] })

    render(<RoomCard room={room} onConfigureDevice={vi.fn()} />)

    expect(screen.getByText('roomCard.directConsumption')).toBeInTheDocument()
  })

  it('RoomCard_AllDevicesNoneApproachNonStrip_RendersDirectConsumptionFallback', () => {
    const room = makeRoom({
      devices: [
        makeDevice({ deviceId: 'd1', name: 'Zero A', approach: 'None', isSmartStrip: false, kwh: 0 }),
        makeDevice({ deviceId: 'd2', name: 'Zero B', approach: 'None', isSmartStrip: false, kwh: 0 }),
      ],
    })

    render(<RoomCard room={room} onConfigureDevice={vi.fn()} />)

    expect(screen.getByText('roomCard.directConsumption')).toBeInTheDocument()
    expect(screen.queryByText('Zero A')).not.toBeInTheDocument()
  })

  it('RoomCard_MultipleDevices_UsesResponsiveGridContainer', () => {
    const room = makeRoom({
      devices: [
        makeDevice({ deviceId: 'd1', name: 'Device A', approach: 'Measured', kwh: 5 }),
        makeDevice({ deviceId: 'd2', name: 'Device B', approach: 'Measured', kwh: 3 }),
      ],
    })

    const { container } = render(<RoomCard room={room} onConfigureDevice={vi.fn()} />)
    const grids = container.querySelectorAll('.grid')

    expect(grids).toHaveLength(1)
    const grid = grids[0]
    expect(grid).toHaveClass('grid-cols-1', 'md:grid-cols-2', 'lg:grid-cols-3')
    expect(grid.children).toHaveLength(2)
    expect(grid).toContainElement(screen.getByText('Device A'))
    expect(grid).toContainElement(screen.getByText('Device B'))
  })

  it('RoomCard_RegularDevice_DoesNotGetFullWidthSpanClass', () => {
    const room = makeRoom({
      devices: [makeDevice({ deviceId: 'd1', name: 'Device A', approach: 'Measured', kwh: 5 })],
    })

    render(<RoomCard room={room} onConfigureDevice={vi.fn()} />)
    const device = screen.getByText('Device A')

    expect(device.closest('.md\\:col-span-full')).not.toBeInTheDocument()
  })

  it('RoomCard_SmartStripDevice_WrapperSpansFullGridWidth', () => {
    const room = makeRoom({
      devices: [
        makeDevice({
          deviceId: 'd1',
          name: 'Power Strip',
          approach: 'Measured',
          isSmartStrip: true,
          kwh: 5,
          subDevices: [],
        }),
      ],
    })

    render(<RoomCard room={room} onConfigureDevice={vi.fn()} />)
    const strip = screen.getByText('Power Strip')
    const wrapper = strip.closest('.md\\:col-span-full')

    expect(wrapper).toBeInTheDocument()
  })

  it('RoomCard_SmartStripConfigureHintClicked_CallsOnConfigureDevice', async () => {
    const user = userEvent.setup()
    const onConfigureDevice = vi.fn()
    const room = makeRoom({
      devices: [
        makeDevice({
          deviceId: 'd1',
          name: 'Power Strip',
          approach: 'Measured',
          isSmartStrip: true,
          kwh: 5,
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
        }),
      ],
    })

    render(<RoomCard room={room} onConfigureDevice={onConfigureDevice} />)
    await user.click(screen.getByText('smartStripCard.configureHint'))

    expect(onConfigureDevice).toHaveBeenCalled()
  })
})
