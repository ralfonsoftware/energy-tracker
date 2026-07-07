import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { FileListItem, type DeviceOption } from './FileListItem'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (k: string, opts?: Record<string, unknown>) => (opts?.value ? `${k}:${opts.value}` : k),
  }),
}))

const deviceOptions: DeviceOption[] = [
  { plugId: 'plug-1', label: 'Fridge Outlet', roomName: 'Kitchen' },
  { plugId: 'plug-2', label: 'TV Outlet', roomName: 'Living Room' },
]

describe('FileListItem', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('FileListItem_EveHomeFile_ShowsEveHomeBadge', () => {
    render(
      <FileListItem
        fileName="export.xlsx"
        detectedType="EveHome"
        deviceOptions={deviceOptions}
        selectedPlugId={null}
        isAutoMatched={false}
        onSelectPlugId={vi.fn()}
        onRemove={vi.fn()}
      />
    )

    expect(screen.getByText('export.xlsx')).toBeInTheDocument()
    expect(screen.getByText('fileType.eveHome')).toBeInTheDocument()
  })

  it('FileListItem_MerossFile_ShowsMerossBadge', () => {
    render(
      <FileListItem
        fileName="export.csv"
        detectedType="Meross"
        deviceOptions={deviceOptions}
        selectedPlugId={null}
        isAutoMatched={false}
        onSelectPlugId={vi.fn()}
        onRemove={vi.fn()}
      />
    )

    expect(screen.getByText('fileType.meross')).toBeInTheDocument()
  })

  it('FileListItem_AutoMatchedWithSelection_ShowsAutoIndicator', () => {
    render(
      <FileListItem
        fileName="fridge-export.csv"
        detectedType="Meross"
        deviceOptions={deviceOptions}
        selectedPlugId="plug-1"
        isAutoMatched={true}
        onSelectPlugId={vi.fn()}
        onRemove={vi.fn()}
      />
    )

    expect(screen.getByText('association.auto')).toBeInTheDocument()
  })

  it('FileListItem_NotAutoMatched_DoesNotShowAutoIndicator', () => {
    render(
      <FileListItem
        fileName="export.csv"
        detectedType="Meross"
        deviceOptions={deviceOptions}
        selectedPlugId="plug-1"
        isAutoMatched={false}
        onSelectPlugId={vi.fn()}
        onRemove={vi.fn()}
      />
    )

    expect(screen.queryByText('association.auto')).not.toBeInTheDocument()
  })

  it('FileListItem_SelectingDifferentPowerPoint_CallsOnSelectPlugId', async () => {
    const user = userEvent.setup()
    const onSelectPlugId = vi.fn()
    render(
      <FileListItem
        fileName="export.csv"
        detectedType="Meross"
        deviceOptions={deviceOptions}
        selectedPlugId="plug-1"
        isAutoMatched={false}
        onSelectPlugId={onSelectPlugId}
        onRemove={vi.fn()}
      />
    )

    await user.selectOptions(screen.getByLabelText('association.label'), 'plug-2')

    expect(onSelectPlugId).toHaveBeenCalledWith('plug-2')
  })

  it('FileListItem_RemoveClicked_CallsOnRemove', async () => {
    const user = userEvent.setup()
    const onRemove = vi.fn()
    render(
      <FileListItem
        fileName="export.csv"
        detectedType="Meross"
        deviceOptions={deviceOptions}
        selectedPlugId={null}
        isAutoMatched={false}
        onSelectPlugId={vi.fn()}
        onRemove={onRemove}
      />
    )

    await user.click(screen.getByLabelText('remove'))

    expect(onRemove).toHaveBeenCalled()
  })
})
