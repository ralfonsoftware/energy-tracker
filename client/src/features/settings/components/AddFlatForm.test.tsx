import { render, screen, fireEvent, act } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { Sheet } from '@/components/ui/sheet'
import { AddFlatForm } from './AddFlatForm'
import type { UserSettings, FlatSummary } from '../api/settingsApi'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

vi.mock('../hooks/useUserSettings')
import { useUserSettings } from '../hooks/useUserSettings'
const mockUseUserSettings = vi.mocked(useUserSettings)

vi.mock('../hooks/useCreateFlat')
import { useCreateFlat } from '../hooks/useCreateFlat'
const mockUseCreateFlat = vi.mocked(useCreateFlat)

vi.mock('../hooks/useSwitchActiveFlat')
import { useSwitchActiveFlat } from '../hooks/useSwitchActiveFlat'
const mockUseSwitchActiveFlat = vi.mocked(useSwitchActiveFlat)

const defaultSettings: UserSettings = {
  locale: 'en-US',
  hasFlat: true,
  flatId: 'flat-1',
  flatName: 'Home',
  annualKwhBaseline: 2500,
  plannedAnnualSpend: null,
}

const newFlat: FlatSummary = {
  flatId: 'flat-2',
  name: 'Cabin',
  annualKwhBaseline: 1500,
  spikeThreshold: 2,
  plannedAnnualSpend: null,
}

type MutateOptions<T> = { onSuccess?: (data: T) => void; onError?: () => void }

function setup(options?: { isPending?: boolean }) {
  mockUseUserSettings.mockReturnValue({
    settings: defaultSettings,
    isLoading: false,
    isError: false,
  } as unknown as ReturnType<typeof useUserSettings>)

  const createMutate = vi.fn<(variables: unknown, opts?: MutateOptions<FlatSummary>) => void>()
  mockUseCreateFlat.mockReturnValue({
    mutate: createMutate,
    isPending: options?.isPending ?? false,
  } as unknown as ReturnType<typeof useCreateFlat>)

  const switchMutate = vi.fn<(variables: unknown, opts?: MutateOptions<unknown>) => void>()
  mockUseSwitchActiveFlat.mockReturnValue({
    mutate: switchMutate,
  } as unknown as ReturnType<typeof useSwitchActiveFlat>)

  const onOpenChange = vi.fn()
  render(
    <Sheet open={true} onOpenChange={onOpenChange}>
      <AddFlatForm open={true} onOpenChange={onOpenChange} />
    </Sheet>
  )

  const nameInput = screen.getByLabelText('addFlat.nameLabel')
  const submitButton = screen.getByRole('button', { name: 'addFlat.submit' })

  return { createMutate, switchMutate, onOpenChange, nameInput, submitButton }
}

describe('AddFlatForm', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('AddFlatForm_PresetClicked_FillsBaselineInput', () => {
    setup()
    fireEvent.click(screen.getByRole('button', { name: /2,500/ }))
    const baselineInput = document.querySelector('input[name="annualKwhBaseline"]') as HTMLInputElement
    expect(baselineInput.value).toBe('2500')
  })

  it('AddFlatForm_CustomValueTyped_ClearsPresetSelection', async () => {
    const user = userEvent.setup()
    setup()
    const presetButton = screen.getByRole('button', { name: /2,500/ })
    fireEvent.click(presetButton)
    expect(presetButton).toHaveStyle({ background: '#ffffff' })

    const baselineInput = document.querySelector('input[name="annualKwhBaseline"]') as HTMLInputElement
    await user.clear(baselineInput)
    await user.type(baselineInput, '1800')

    expect(presetButton).not.toHaveStyle({ background: '#ffffff' })
  })

  it('AddFlatForm_ValidSubmit_CallsCreateThenSwitchInOrder', async () => {
    const user = userEvent.setup()
    const { createMutate, switchMutate, nameInput, submitButton } = setup()

    await user.type(nameInput, 'Cabin')
    fireEvent.click(screen.getByRole('button', { name: /1,500/ }))
    await user.click(submitButton)

    expect(createMutate).toHaveBeenCalledWith(
      { name: 'Cabin', annualKwhBaseline: 1500, plannedAnnualSpend: null },
      expect.any(Object)
    )

    const [, callbacks] = createMutate.mock.calls[0] as [unknown, MutateOptions<FlatSummary>]
    act(() => {
      callbacks.onSuccess?.(newFlat)
    })

    expect(switchMutate).toHaveBeenCalledWith(
      { flatId: 'flat-2', locale: 'en-US', previousFlatId: 'flat-1' },
      expect.any(Object)
    )
  })

  it('AddFlatForm_CreateSucceeds_ShowsTariffPromptOnlyAfterSwitchSucceeds', async () => {
    const user = userEvent.setup()
    const { createMutate, switchMutate, onOpenChange, nameInput, submitButton } = setup()

    await user.type(nameInput, 'Cabin')
    fireEvent.click(screen.getByRole('button', { name: /1,500/ }))
    await user.click(submitButton)

    const [, createCallbacks] = createMutate.mock.calls[0] as [unknown, MutateOptions<FlatSummary>]
    act(() => {
      createCallbacks.onSuccess?.(newFlat)
    })

    expect(screen.queryByText('addFlat.tariffPrompt')).not.toBeInTheDocument()

    const [, switchCallbacks] = switchMutate.mock.calls[0] as [unknown, MutateOptions<unknown>]
    act(() => {
      switchCallbacks.onSuccess?.(undefined)
    })

    expect(screen.getByText('addFlat.tariffPrompt')).toBeInTheDocument()
    expect(onOpenChange).not.toHaveBeenCalledWith(false)
  })

  it('AddFlatForm_CreateSucceedsButSwitchFails_ShowsErrorInsteadOfTariffPrompt', async () => {
    const user = userEvent.setup()
    const { createMutate, switchMutate, nameInput, submitButton } = setup()

    await user.type(nameInput, 'Cabin')
    fireEvent.click(screen.getByRole('button', { name: /1,500/ }))
    await user.click(submitButton)

    const [, createCallbacks] = createMutate.mock.calls[0] as [unknown, MutateOptions<FlatSummary>]
    act(() => {
      createCallbacks.onSuccess?.(newFlat)
    })

    const [, switchCallbacks] = switchMutate.mock.calls[0] as [unknown, MutateOptions<unknown>]
    act(() => {
      switchCallbacks.onError?.()
    })

    expect(screen.queryByText('addFlat.tariffPrompt')).not.toBeInTheDocument()
    expect(screen.getByText('addFlat.error')).toBeInTheDocument()
  })

  it('AddFlatForm_CreateFails_SheetStaysOpenWithInlineError', async () => {
    const user = userEvent.setup()
    const { createMutate, onOpenChange, nameInput, submitButton } = setup()

    await user.type(nameInput, 'Cabin')
    fireEvent.click(screen.getByRole('button', { name: /1,500/ }))
    await user.click(submitButton)

    const [, callbacks] = createMutate.mock.calls[0] as [unknown, MutateOptions<FlatSummary>]
    act(() => {
      callbacks.onError?.()
    })

    expect(screen.getByText('addFlat.error')).toBeInTheDocument()
    expect(onOpenChange).not.toHaveBeenCalledWith(false)
  })

  it('AddFlatForm_NonFiniteBaseline_ShowsValidationErrorAndDoesNotCreate', async () => {
    const user = userEvent.setup()
    const { createMutate, nameInput, submitButton } = setup()

    await user.type(nameInput, 'Cabin')
    const baselineInput = document.querySelector('input[name="annualKwhBaseline"]') as HTMLInputElement
    await user.type(baselineInput, 'Infinity')
    await user.click(submitButton)

    expect(createMutate).not.toHaveBeenCalled()
    expect(screen.getByText('common:errors.validationNumber')).toBeInTheDocument()
  })

  it('AddFlatForm_NameEmpty_SubmitDisabled', () => {
    const { submitButton } = setup()
    expect(submitButton).toBeDisabled()
  })
})
