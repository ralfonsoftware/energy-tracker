import { render, screen, fireEvent } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { FileUploadZone } from './FileUploadZone'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

describe('FileUploadZone', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('FileUploadZone_ChooseFilesClicked_OpensNativeFileChooser', async () => {
    const user = userEvent.setup()
    render(<FileUploadZone onFilesSelected={vi.fn()} />)

    const input = screen.getByLabelText('uploadZone.chooseFiles') as HTMLInputElement
    const clickSpy = vi.spyOn(input, 'click')

    await user.click(screen.getByRole('button', { name: 'uploadZone.chooseFiles' }))

    expect(clickSpy).toHaveBeenCalled()
  })

  it('FileUploadZone_ValidFilesSelectedViaInput_CallsOnFilesSelectedFilteringInvalidExtensions', async () => {
    const onFilesSelected = vi.fn()
    const user = userEvent.setup()
    render(<FileUploadZone onFilesSelected={onFilesSelected} />)

    const csvFile = new File(['x'], 'export.csv', { type: 'text/csv' })
    const xlsxFile = new File(['x'], 'export.xlsx')
    const txtFile = new File(['x'], 'notes.txt')

    const input = screen.getByLabelText('uploadZone.chooseFiles') as HTMLInputElement
    await user.upload(input, [csvFile, xlsxFile, txtFile])

    expect(onFilesSelected).toHaveBeenCalledWith([csvFile, xlsxFile])
  })

  it('FileUploadZone_FilesDropped_CallsOnFilesSelected', () => {
    const onFilesSelected = vi.fn()
    const { container } = render(<FileUploadZone onFilesSelected={onFilesSelected} />)

    const csvFile = new File(['x'], 'export.csv')
    const dropZone = container.firstChild as HTMLElement

    fireEvent.drop(dropZone, { dataTransfer: { files: [csvFile] } })

    expect(onFilesSelected).toHaveBeenCalledWith([csvFile])
  })

  it('FileUploadZone_NoValidFilesDropped_DoesNotCallOnFilesSelected', () => {
    const onFilesSelected = vi.fn()
    const { container } = render(<FileUploadZone onFilesSelected={onFilesSelected} />)

    const txtFile = new File(['x'], 'notes.txt')
    const dropZone = container.firstChild as HTMLElement

    fireEvent.drop(dropZone, { dataTransfer: { files: [txtFile] } })

    expect(onFilesSelected).not.toHaveBeenCalled()
  })
})
