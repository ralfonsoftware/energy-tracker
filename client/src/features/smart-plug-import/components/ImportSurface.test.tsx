import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { ImportSurface } from './ImportSurface'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string, opts?: Record<string, unknown>) => (opts?.count !== undefined ? `${k}:${opts.count}` : k) }),
}))

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom')
  return { ...actual, useNavigate: () => mockNavigate }
})
const mockNavigate = vi.fn()

vi.mock('@/features/smart-plug-import/hooks/useUploadImport')
import { useUploadImport } from '@/features/smart-plug-import/hooks/useUploadImport'
const mockUseUploadImport = vi.mocked(useUploadImport)

const rooms = [
  {
    name: 'Kitchen',
    powerPoints: [{ plugId: 'plug-1', name: 'Fridge Outlet', devices: [{ name: 'Fridge' }] }],
  },
]

function selectUploadReturn(mutateAsync: (...args: unknown[]) => Promise<unknown>, isPending = false) {
  mockUseUploadImport.mockReturnValue({
    mutateAsync,
    isPending,
  } as unknown as ReturnType<typeof useUploadImport>)
}

describe('ImportSurface', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('ImportSurface_FileNameContainsKnownDevice_AutoSelectsPlugId', async () => {
    selectUploadReturn(vi.fn())
    const user = userEvent.setup()
    render(<ImportSurface flatId="flat-1" rooms={rooms} />)

    const input = screen.getByLabelText('uploadZone.chooseFiles') as HTMLInputElement
    const file = new File(['x'], 'fridge-export.csv')
    await user.upload(input, file)

    const select = await screen.findByLabelText('association.label')
    expect(select).toHaveValue('plug-1')
    expect(screen.getByText('association.auto')).toBeInTheDocument()
  })

  it('ImportSurface_FileNameContainsDeviceNameOnlyAsSubstring_DoesNotAutoMatch', async () => {
    selectUploadReturn(vi.fn())
    const user = userEvent.setup()
    render(<ImportSurface flatId="flat-1" rooms={rooms} />)

    const input = screen.getByLabelText('uploadZone.chooseFiles') as HTMLInputElement
    const file = new File(['x'], 'refridgerator-export.csv')
    await user.upload(input, file)

    const select = await screen.findByLabelText('association.label')
    expect(select).toHaveValue('')
    expect(screen.queryByText('association.auto')).not.toBeInTheDocument()
  })

  it('ImportSurface_PowerPointHasPlugIdButNoDevices_StillOfferedAsAssociationOption', async () => {
    const bareStripRooms = [
      {
        name: 'Office',
        powerPoints: [{ plugId: 'plug-strip', name: 'Office Strip', devices: [] }],
      },
    ]
    selectUploadReturn(vi.fn())
    const user = userEvent.setup()
    render(<ImportSurface flatId="flat-1" rooms={bareStripRooms} />)

    const input = screen.getByLabelText('uploadZone.chooseFiles') as HTMLInputElement
    await user.upload(input, new File(['x'], 'strip-export.csv'))

    expect(screen.queryByText('surface.noDevicesAvailable')).not.toBeInTheDocument()
    const select = await screen.findByLabelText('association.label')
    await user.selectOptions(select, 'plug-strip')
    expect(select).toHaveValue('plug-strip')
    expect(screen.getByText('Office Strip — Office')).toBeInTheDocument()
  })

  it('ImportSurface_PowerPointWithMultipleDevices_OffersOnePowerPointOptionNotOnePerDevice', async () => {
    const stripRooms = [
      {
        name: 'Office',
        powerPoints: [{ plugId: 'plug-strip', name: 'Office Strip', devices: [{ name: 'Monitor' }, { name: 'Lamp' }] }],
      },
    ]
    selectUploadReturn(vi.fn())
    const user = userEvent.setup()
    render(<ImportSurface flatId="flat-1" rooms={stripRooms} />)

    const input = screen.getByLabelText('uploadZone.chooseFiles') as HTMLInputElement
    await user.upload(input, new File(['x'], 'lamp-export.csv'))

    const select = await screen.findByLabelText('association.label')
    expect(select).toHaveValue('plug-strip')
    expect(screen.getAllByRole('option')).toHaveLength(2)
    expect(screen.getByText('Office Strip — Office')).toBeInTheDocument()
  })

  it('ImportSurface_NoDeviceOptionsAvailable_ShowsEmptyStateMessage', () => {
    selectUploadReturn(vi.fn())
    render(<ImportSurface flatId="flat-1" rooms={[]} />)

    expect(screen.getByText('surface.noDevicesAvailable')).toBeInTheDocument()
  })

  it('ImportSurface_FlatIdUndefined_UploadButtonDisabledEvenWhenAllAssociated', async () => {
    selectUploadReturn(vi.fn())
    const user = userEvent.setup()
    render(<ImportSurface flatId={undefined} rooms={rooms} />)

    const input = screen.getByLabelText('uploadZone.chooseFiles') as HTMLInputElement
    const file = new File(['x'], 'fridge-export.csv')
    await user.upload(input, file)

    expect(await screen.findByRole('button', { name: /surface.uploadButton/ })).toBeDisabled()
  })

  it('ImportSurface_NotAllFilesAssociated_UploadButtonDisabled', async () => {
    selectUploadReturn(vi.fn())
    const user = userEvent.setup()
    render(<ImportSurface flatId="flat-1" rooms={rooms} />)

    const input = screen.getByLabelText('uploadZone.chooseFiles') as HTMLInputElement
    const file = new File(['x'], 'unknown-device.csv')
    await user.upload(input, file)

    expect(await screen.findByRole('button', { name: /surface.uploadButton/ })).toBeDisabled()
  })

  it('ImportSurface_AllFilesUploadSucceed_NavigatesToDecomposition', async () => {
    const mutateAsync = vi.fn().mockResolvedValue({ importJobId: 'job-1' })
    selectUploadReturn(mutateAsync)
    const user = userEvent.setup()
    render(<ImportSurface flatId="flat-1" rooms={rooms} />)

    const input = screen.getByLabelText('uploadZone.chooseFiles') as HTMLInputElement
    const file = new File(['x'], 'fridge-export.csv')
    await user.upload(input, file)

    await user.click(await screen.findByRole('button', { name: /surface.uploadButton/ }))

    await waitFor(() => expect(mockNavigate).toHaveBeenCalledWith('/decomposition'))
  })

  it('ImportSurface_UploadIsPending_BackButtonIsDisabled', async () => {
    selectUploadReturn(vi.fn().mockReturnValue(new Promise(() => {})), true)
    render(<ImportSurface flatId="flat-1" rooms={rooms} />)

    expect(screen.getByRole('button', { name: /nav.decomposition/ })).toBeDisabled()
  })

  it('ImportSurface_RemoveClickedWhileUploadIsPending_FileIsNotRemoved', async () => {
    selectUploadReturn(vi.fn().mockReturnValue(new Promise(() => {})), true)
    const user = userEvent.setup()
    render(<ImportSurface flatId="flat-1" rooms={rooms} />)

    const input = screen.getByLabelText('uploadZone.chooseFiles') as HTMLInputElement
    await user.upload(input, new File(['x'], 'fridge-export.csv'))
    expect(screen.getByText('fridge-export.csv')).toBeInTheDocument()

    await user.click(screen.getByLabelText('remove'))

    expect(screen.getByText('fridge-export.csv')).toBeInTheDocument()
  })

  it('ImportSurface_PartialUploadFailure_FailedFileRemainsAndErrorBannerShown', async () => {
    const mutateAsync = vi.fn().mockRejectedValue(new Error('upload failed'))
    selectUploadReturn(mutateAsync)
    const user = userEvent.setup()
    render(<ImportSurface flatId="flat-1" rooms={rooms} />)

    const input = screen.getByLabelText('uploadZone.chooseFiles') as HTMLInputElement
    const file = new File(['x'], 'fridge-export.csv')
    await user.upload(input, file)

    await user.click(await screen.findByRole('button', { name: /surface.uploadButton/ }))

    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('surface.partialFailure'))
    expect(screen.getByText('fridge-export.csv')).toBeInTheDocument()
    expect(mockNavigate).not.toHaveBeenCalledWith('/decomposition')
  })
})
