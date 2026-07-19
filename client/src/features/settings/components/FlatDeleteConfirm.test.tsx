import { render, screen, fireEvent, act } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { FlatDeleteConfirm } from './FlatDeleteConfirm'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

vi.mock('../hooks/useDeleteFlat')
import { useDeleteFlat } from '../hooks/useDeleteFlat'
const mockUseDeleteFlat = vi.mocked(useDeleteFlat)

type MutateOptions = { onSuccess?: () => void; onError?: () => void }
type MutateArgs = { flatId: string; rowVersion: string }

function setup() {
  const mutate = vi.fn<(args: MutateArgs, opts?: MutateOptions) => void>()
  mockUseDeleteFlat.mockReturnValue({ mutate, isPending: false } as unknown as ReturnType<typeof useDeleteFlat>)

  const onCancel = vi.fn()
  render(
    <FlatDeleteConfirm flatId="flat-1" flatName="My Flat" flatRowVersion="AQID" onCancel={onCancel} />
  )

  const input = screen.getByRole('textbox')
  const deleteButton = screen.getByRole('button', { name: 'account.deleteFlat.deleteButton' })

  return { mutate, onCancel, input, deleteButton }
}

describe('FlatDeleteConfirm', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('FlatDeleteConfirm_Rendered_ShowsPromptText', () => {
    setup()
    expect(screen.getByText('account.deleteFlat.prompt')).toBeInTheDocument()
  })

  it('FlatDeleteConfirm_Rendered_InputIsLabelledByThePrompt', () => {
    const { input } = setup()
    expect(input).toHaveAccessibleName()
  })

  it('FlatDeleteConfirm_TypedValueEmpty_DeleteButtonDisabled', () => {
    const { deleteButton } = setup()
    expect(deleteButton).toBeDisabled()
  })

  it('FlatDeleteConfirm_TypedValuePartialMatch_DeleteButtonDisabled', async () => {
    const user = userEvent.setup()
    const { input, deleteButton } = setup()
    await user.type(input, 'My Fla')
    expect(deleteButton).toBeDisabled()
  })

  it('FlatDeleteConfirm_TypedValueCaseMismatch_DeleteButtonDisabled', async () => {
    const user = userEvent.setup()
    const { input, deleteButton } = setup()
    await user.type(input, 'my flat')
    expect(deleteButton).toBeDisabled()
  })

  it('FlatDeleteConfirm_TypedValueExactMatch_DeleteButtonEnabled', async () => {
    const user = userEvent.setup()
    const { input, deleteButton } = setup()
    await user.type(input, 'My Flat')
    expect(deleteButton).toBeEnabled()
  })

  it('FlatDeleteConfirm_DeleteClicked_CallsDeleteMutationWithFlatId', async () => {
    const user = userEvent.setup()
    const { mutate, input, deleteButton } = setup()
    await user.type(input, 'My Flat')
    await user.click(deleteButton)
    expect(mutate).toHaveBeenCalledWith({ flatId: 'flat-1', rowVersion: 'AQID' }, expect.any(Object))
  })

  it('FlatDeleteConfirm_DeleteSucceeds_CallsOnCancel', async () => {
    const user = userEvent.setup()
    const { mutate, onCancel, input, deleteButton } = setup()
    await user.type(input, 'My Flat')
    await user.click(deleteButton)

    const [, callbacks] = mutate.mock.calls[0] as [unknown, MutateOptions]
    act(() => callbacks.onSuccess?.())

    expect(onCancel).toHaveBeenCalled()
  })

  it('FlatDeleteConfirm_DeleteFails_ShowsErrorAndStaysOpen', async () => {
    const user = userEvent.setup()
    const { mutate, onCancel, input, deleteButton } = setup()
    await user.type(input, 'My Flat')
    await user.click(deleteButton)

    const [, callbacks] = mutate.mock.calls[0] as [unknown, MutateOptions]
    act(() => callbacks.onError?.())

    expect(screen.getByText('account.deleteFlat.error')).toBeInTheDocument()
    expect(onCancel).not.toHaveBeenCalled()
  })

  it('FlatDeleteConfirm_CancelClicked_CallsOnCancel', () => {
    const { onCancel } = setup()
    fireEvent.click(screen.getByRole('button', { name: 'account.deleteFlat.cancel' }))
    expect(onCancel).toHaveBeenCalled()
  })
})
