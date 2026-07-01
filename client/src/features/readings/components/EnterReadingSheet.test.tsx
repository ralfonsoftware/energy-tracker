import { render, screen, waitFor, act } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { Sheet } from '@/components/ui/sheet'
import { EnterReadingSheet } from '@/features/readings/components/EnterReadingSheet'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

vi.mock('@/features/readings/hooks/useSubmitReading')
import { useSubmitReading } from '@/features/readings/hooks/useSubmitReading'
const mockUseSubmitReading = vi.mocked(useSubmitReading)

type MutateOptions = { onSuccess?: () => void; onError?: () => void }

function setup(options?: {
  isPending?: boolean
  isError?: boolean
  lastKwhValue?: number | null
  flatId?: string | undefined
  open?: boolean
}) {
  const mutate = vi.fn<(variables: unknown, opts?: MutateOptions) => void>()
  mockUseSubmitReading.mockReturnValue({
    mutate,
    isPending: options?.isPending ?? false,
    isError: options?.isError ?? false,
  } as unknown as ReturnType<typeof useSubmitReading>)

  const onOpenChange = vi.fn()
  const open = options?.open ?? true
  const utils = render(
    <Sheet open={open} onOpenChange={onOpenChange}>
      <EnterReadingSheet
        flatId={'flatId' in (options ?? {}) ? options?.flatId : 'flat-1'}
        lastKwhValue={options?.lastKwhValue ?? 100}
        open={open}
        onOpenChange={onOpenChange}
      />
    </Sheet>
  )

  const kwhInput = document.querySelector('input[name="kwhValue"]') as HTMLInputElement
  const dateInput = document.querySelector('input[name="readingDate"]') as HTMLInputElement
  const saveButton = screen.getByRole('button', { name: 'sheet.saveButton' })

  return { mutate, onOpenChange, kwhInput, dateInput, saveButton, rerender: utils.rerender }
}

describe('EnterReadingSheet', () => {
  beforeEach(() => {
    mockUseSubmitReading.mockReset()
  })

  it('EnterReadingSheet_Open_KwhInputAutoFocusedAndDatePrefilled', async () => {
    const { kwhInput, dateInput } = setup()

    await waitFor(() => expect(document.activeElement).toBe(kwhInput))
    const currentYear = String(new Date().getFullYear())
    expect(dateInput.value).toContain(currentYear)
  })

  it('EnterReadingSheet_KwhFieldEmpty_SaveButtonDisabled', () => {
    const { saveButton } = setup()
    expect(saveButton).toBeDisabled()
  })

  it('EnterReadingSheet_PositiveKwhValueTyped_SaveButtonEnabled', async () => {
    const user = userEvent.setup()
    const { kwhInput, saveButton } = setup()

    await user.type(kwhInput, '150')

    expect(saveButton).toBeEnabled()
  })

  it('EnterReadingSheet_ValueBelowLastReading_ShowsLowerWarningAndSaveStaysEnabled', async () => {
    const user = userEvent.setup()
    const { kwhInput, saveButton } = setup({ lastKwhValue: 200 })

    await user.type(kwhInput, '50')

    expect(screen.getByRole('status')).toHaveTextContent('sheet.lowerWarning')
    expect(saveButton).toBeEnabled()
  })

  it('EnterReadingSheet_MutationSucceeds_ClosesSheet', async () => {
    const user = userEvent.setup()
    const { mutate, onOpenChange, kwhInput, saveButton } = setup()

    await user.type(kwhInput, '150')
    await user.click(saveButton)

    const [, mutateOptions] = mutate.mock.calls[0] as [unknown, MutateOptions]
    mutateOptions.onSuccess?.()

    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it('EnterReadingSheet_MutationFails_SheetStaysOpenWithValueAndErrorShown', async () => {
    const user = userEvent.setup()
    const { mutate, onOpenChange, kwhInput, saveButton } = setup()

    await user.type(kwhInput, '150')
    await user.click(saveButton)

    const [, mutateOptions] = mutate.mock.calls[0] as [unknown, MutateOptions]
    act(() => {
      mutateOptions.onError?.()
    })

    expect(onOpenChange).not.toHaveBeenCalledWith(false)
    expect(kwhInput.value).toBe('150')
    expect(screen.getByRole('alert')).toHaveTextContent('sheet.saveError')
  })

  it('EnterReadingSheet_FlatIdUndefined_SaveButtonDisabledEvenWithValidValue', async () => {
    const user = userEvent.setup()
    const { kwhInput, saveButton } = setup({ flatId: undefined })

    await user.type(kwhInput, '150')

    expect(saveButton).toBeDisabled()
  })

  it('EnterReadingSheet_EmptyReadingDate_DoesNotCallMutate', async () => {
    const user = userEvent.setup()
    const { mutate, kwhInput, dateInput, saveButton } = setup()

    await user.type(kwhInput, '150')
    await user.clear(dateInput)
    await user.click(saveButton)

    expect(mutate).not.toHaveBeenCalled()
  })

  it('EnterReadingSheet_RapidDoubleClickSave_OnlyCallsMutateOnce', async () => {
    const user = userEvent.setup()
    const { mutate, kwhInput, saveButton } = setup()

    await user.type(kwhInput, '150')
    await user.click(saveButton)
    await user.click(saveButton)

    expect(mutate).toHaveBeenCalledTimes(1)
  })

  it('EnterReadingSheet_ReopenedAfterClose_ResetsToFreshValues', async () => {
    const user = userEvent.setup()
    const { kwhInput, rerender, onOpenChange } = setup()

    await user.type(kwhInput, '999')
    expect(kwhInput.value).toBe('999')

    rerender(
      <Sheet open={false} onOpenChange={onOpenChange}>
        <EnterReadingSheet flatId="flat-1" lastKwhValue={100} open={false} onOpenChange={onOpenChange} />
      </Sheet>
    )
    rerender(
      <Sheet open={true} onOpenChange={onOpenChange}>
        <EnterReadingSheet flatId="flat-1" lastKwhValue={100} open={true} onOpenChange={onOpenChange} />
      </Sheet>
    )

    const reopenedKwhInput = document.querySelector('input[name="kwhValue"]') as HTMLInputElement
    expect(reopenedKwhInput.value).toBe('')
  })
})
