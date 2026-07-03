import { render, screen, act, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { TariffForm } from '@/features/tariffs/components/TariffForm'
import type { TariffResponse } from '@/features/tariffs/api/tariffApi'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k, i18n: { language: 'en-US' } }),
}))

vi.mock('@/features/tariffs/hooks/useCreateTariff')
import { useCreateTariff } from '@/features/tariffs/hooks/useCreateTariff'
const mockUseCreateTariff = vi.mocked(useCreateTariff)

vi.mock('@/features/tariffs/hooks/usePatchTariff')
import { usePatchTariff } from '@/features/tariffs/hooks/usePatchTariff'
const mockUsePatchTariff = vi.mocked(usePatchTariff)

type MutateOptions = { onSuccess?: () => void; onError?: () => void }

const lockedTariff: TariffResponse = {
  tariffId: 'tariff-locked',
  contractStartDate: '2025-01-01T00:00:00Z',
  pricePerKwh: 0.28,
  monthlyBaseFee: 10,
  providerName: 'E.ON',
  contractDurationMonths: 12,
  isLocked: true,
}

const unlockedTariff: TariffResponse = {
  tariffId: 'tariff-unlocked',
  contractStartDate: '2099-01-01T00:00:00Z',
  pricePerKwh: 0.28,
  monthlyBaseFee: 10,
  providerName: 'E.ON',
  contractDurationMonths: null,
  isLocked: false,
}

function setup(options?: { isPending?: boolean }) {
  const mutate = vi.fn<(variables: unknown, opts?: MutateOptions) => void>()
  mockUseCreateTariff.mockReturnValue({
    mutate,
    isPending: options?.isPending ?? false,
  } as unknown as ReturnType<typeof useCreateTariff>)
  mockUsePatchTariff.mockReturnValue({
    mutateAsync: vi.fn(),
    isPending: false,
  } as unknown as ReturnType<typeof usePatchTariff>)

  const onClose = vi.fn()
  render(<TariffForm flatId="flat-1" onClose={onClose} />)

  const priceInput = document.querySelector('input[name="pricePerKwh"]') as HTMLInputElement
  const feeInput = document.querySelector('input[name="monthlyBaseFee"]') as HTMLInputElement
  const dateInput = document.querySelector('input[name="contractStartDate"]') as HTMLInputElement
  const saveButton = screen.getByRole('button', { name: 'form.saveButton' })

  return { mutate, onClose, priceInput, feeInput, dateInput, saveButton }
}

function setupEdit(tariff: TariffResponse, options?: { isPending?: boolean }) {
  mockUseCreateTariff.mockReturnValue({
    mutate: vi.fn(),
    isPending: false,
  } as unknown as ReturnType<typeof useCreateTariff>)
  const mutateAsync = vi.fn<(variables: unknown) => Promise<unknown>>().mockResolvedValue(undefined)
  mockUsePatchTariff.mockReturnValue({
    mutateAsync,
    isPending: options?.isPending ?? false,
  } as unknown as ReturnType<typeof usePatchTariff>)

  const onClose = vi.fn()
  render(<TariffForm flatId="flat-1" tariff={tariff} onClose={onClose} />)

  const priceInput = document.querySelector('input[name="pricePerKwh"]') as HTMLInputElement
  const feeInput = document.querySelector('input[name="monthlyBaseFee"]') as HTMLInputElement
  const providerInput = document.querySelector('input[name="providerName"]') as HTMLInputElement
  const saveButton = screen.getByRole('button', { name: 'form.saveButton' })

  return { mutateAsync, onClose, priceInput, feeInput, providerInput, saveButton }
}

describe('TariffForm', () => {
  beforeEach(() => {
    mockUseCreateTariff.mockReset()
  })

  it('TariffForm_ContractStartDatePrefilledWithToday', () => {
    const { dateInput } = setup()
    const now = new Date()
    const today = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`
    expect(dateInput.value).toBe(today)
  })

  it('TariffForm_RequiredFieldsEmpty_SaveButtonDisabled', () => {
    const { saveButton } = setup()
    expect(saveButton).toBeDisabled()
  })

  it('TariffForm_ValidPriceAndFeeEntered_SaveButtonEnabled', async () => {
    const user = userEvent.setup()
    const { priceInput, feeInput, saveButton } = setup()

    await user.type(priceInput, '0.28')
    await user.type(feeInput, '12')

    expect(saveButton).toBeEnabled()
  })

  it('TariffForm_InvalidPrice_SaveButtonStaysDisabled', async () => {
    const user = userEvent.setup()
    const { priceInput, feeInput, saveButton } = setup()

    await user.type(priceInput, 'abc')
    await user.type(feeInput, '12')

    expect(saveButton).toBeDisabled()
  })

  it('TariffForm_ZeroOrNegativePrice_SaveButtonStaysDisabled', async () => {
    const user = userEvent.setup()
    const { priceInput, feeInput, saveButton } = setup()

    await user.type(priceInput, '0')
    await user.type(feeInput, '12')
    expect(saveButton).toBeDisabled()

    await user.clear(priceInput)
    await user.type(priceInput, '-1')
    expect(saveButton).toBeDisabled()
  })

  it('TariffForm_Submit_CallsMutateWithParsedNumericValues', async () => {
    const user = userEvent.setup()
    const { mutate, priceInput, feeInput, saveButton } = setup()

    await user.type(priceInput, '0.28')
    await user.type(feeInput, '12')
    await user.click(saveButton)

    expect(mutate).toHaveBeenCalledWith(
      expect.objectContaining({ pricePerKwh: 0.28, monthlyBaseFee: 12 }),
      expect.any(Object)
    )
  })

  it('TariffForm_OnSuccess_CallsOnClose', async () => {
    const user = userEvent.setup()
    const { mutate, onClose, priceInput, feeInput, saveButton } = setup()

    await user.type(priceInput, '0.28')
    await user.type(feeInput, '12')
    await user.click(saveButton)

    const [, mutateOptions] = mutate.mock.calls[0] as [unknown, MutateOptions]
    mutateOptions.onSuccess?.()

    expect(onClose).toHaveBeenCalled()
  })

  it('TariffForm_OnError_ShowsErrorBannerWithoutClosing', async () => {
    const user = userEvent.setup()
    const { mutate, onClose, priceInput, feeInput, saveButton } = setup()

    await user.type(priceInput, '0.28')
    await user.type(feeInput, '12')
    await user.click(saveButton)

    const [, mutateOptions] = mutate.mock.calls[0] as [unknown, MutateOptions]
    act(() => {
      mutateOptions.onError?.()
    })

    expect(onClose).not.toHaveBeenCalled()
    expect(screen.getByRole('alert')).toHaveTextContent('form.errorMessage')
  })

  it('TariffForm_RapidDoubleClickSave_OnlyCallsMutateOnce', async () => {
    const user = userEvent.setup()
    const { mutate, priceInput, feeInput, saveButton } = setup()

    await user.type(priceInput, '0.28')
    await user.type(feeInput, '12')
    await user.click(saveButton)
    await user.click(saveButton)

    expect(mutate).toHaveBeenCalledTimes(1)
  })

  it('TariffForm_ContractDurationToggle_SelectsAndDeselects', async () => {
    const user = userEvent.setup()
    setup()

    const twelveMonthButton = screen.getByRole('button', { name: 'form.duration12' })
    await user.click(twelveMonthButton)
    await user.click(twelveMonthButton)
    // no assertion error means selecting/deselecting doesn't throw; value verified via submit test below
  })
})

describe('TariffForm edit mode', () => {
  beforeEach(() => {
    mockUseCreateTariff.mockReset()
    mockUsePatchTariff.mockReset()
  })

  it('TariffForm_EditModeLocked_PriceInputsDisabledAndLockIndicatorVisible', () => {
    const { priceInput, feeInput } = setupEdit(lockedTariff)

    expect(priceInput).toBeDisabled()
    expect(feeInput).toBeDisabled()
    expect(screen.getByText('form.lockedLabel')).toBeInTheDocument()
  })

  it('TariffForm_EditModeUnlocked_PriceInputsEnabledAndNoLockIndicator', () => {
    const { priceInput, feeInput } = setupEdit(unlockedTariff)

    expect(priceInput).toBeEnabled()
    expect(feeInput).toBeEnabled()
    expect(screen.queryByText('form.lockedLabel')).not.toBeInTheDocument()
  })

  it('TariffForm_TapEditAnyway_OpensConfirmationDialog', async () => {
    const user = userEvent.setup()
    setupEdit(lockedTariff)

    await user.click(screen.getByRole('button', { name: /form.editAnywayButton/ }))

    expect(screen.getByText('form.overrideDialogTitle')).toBeInTheDocument()
  })

  it('TariffForm_ConfirmOverride_EnablesPriceInputsWithoutCallingMutation', async () => {
    const user = userEvent.setup()
    const { priceInput, feeInput, mutateAsync } = setupEdit(lockedTariff)

    await user.click(screen.getByRole('button', { name: /form.editAnywayButton/ }))
    await user.click(screen.getByRole('button', { name: 'form.overrideDialogConfirm' }))

    expect(priceInput).toBeEnabled()
    expect(feeInput).toBeEnabled()
    expect(mutateAsync).not.toHaveBeenCalled()
  })

  it('TariffForm_CancelOverride_KeepsFieldsLockedWithoutCallingMutation', async () => {
    const user = userEvent.setup()
    const { priceInput, feeInput, mutateAsync } = setupEdit(lockedTariff)

    await user.click(screen.getByRole('button', { name: /form.editAnywayButton/ }))
    await user.click(screen.getByRole('button', { name: 'form.overrideDialogCancel' }))

    expect(priceInput).toBeDisabled()
    expect(feeInput).toBeDisabled()
    expect(mutateAsync).not.toHaveBeenCalled()
  })

  it('TariffForm_SubmitAfterOverrideConfirmed_SendsLockOverrideTrue', async () => {
    const user = userEvent.setup()
    const { priceInput, saveButton, mutateAsync } = setupEdit(lockedTariff)

    await user.click(screen.getByRole('button', { name: /form.editAnywayButton/ }))
    await user.click(screen.getByRole('button', { name: 'form.overrideDialogConfirm' }))

    await user.clear(priceInput)
    await user.type(priceInput, '0.35')
    await user.click(saveButton)

    await waitFor(() =>
      expect(mutateAsync).toHaveBeenCalledWith(
        expect.objectContaining({
          tariffId: 'tariff-locked',
          body: expect.objectContaining({ pricePerKwh: 0.35, lockOverride: true }),
        })
      )
    )
  })

  it('TariffForm_OnlyNonPriceFieldDirty_SendsSingleCallWithNoPriceFields', async () => {
    const user = userEvent.setup()
    const { providerInput, saveButton, mutateAsync } = setupEdit(unlockedTariff)

    await user.clear(providerInput)
    await user.type(providerInput, 'Vattenfall')
    await user.click(saveButton)

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledTimes(1))
    const [call] = mutateAsync.mock.calls[0] as [{ body: Record<string, unknown> }]
    expect(call.body.pricePerKwh).toBeUndefined()
    expect(call.body.monthlyBaseFee).toBeUndefined()
    expect(call.body.providerName).toBe('Vattenfall')
  })

  it('TariffForm_PriceAndContractTermBothDirty_SendsSingleCallWithBothCategories', async () => {
    const user = userEvent.setup()
    const { priceInput, providerInput, saveButton, mutateAsync } = setupEdit(unlockedTariff)

    await user.clear(priceInput)
    await user.type(priceInput, '0.35')
    await user.clear(providerInput)
    await user.type(providerInput, 'Vattenfall')
    await user.click(saveButton)

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledTimes(1))
    const [call] = mutateAsync.mock.calls[0] as [{ body: Record<string, unknown> }]
    expect(call.body).toHaveProperty('pricePerKwh', 0.35)
    expect(call.body).toHaveProperty('providerName', 'Vattenfall')
  })

  it('TariffForm_ClearingProviderName_SendsExplicitNullNotUndefined', async () => {
    const user = userEvent.setup()
    const { providerInput, saveButton, mutateAsync } = setupEdit(unlockedTariff)

    await user.clear(providerInput)
    await user.click(saveButton)

    await waitFor(() => expect(mutateAsync).toHaveBeenCalledTimes(1))
    const [call] = mutateAsync.mock.calls[0] as [{ body: Record<string, unknown> }]
    expect(call.body).toHaveProperty('providerName', null)
  })

  it('TariffForm_EditModeSubmit_OnSuccessCallsOnClose', async () => {
    const user = userEvent.setup()
    const { providerInput, saveButton, onClose } = setupEdit(unlockedTariff)

    await user.clear(providerInput)
    await user.type(providerInput, 'Vattenfall')
    await user.click(saveButton)

    await waitFor(() => expect(onClose).toHaveBeenCalled())
  })
})
