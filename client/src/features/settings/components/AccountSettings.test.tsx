import { render, screen, fireEvent } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { AccountSettings } from './AccountSettings'
import type { UserSettings } from '../api/settingsApi'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

vi.mock('../hooks/useAuthMe')
import { useAuthMe } from '../hooks/useAuthMe'
const mockUseAuthMe = vi.mocked(useAuthMe)

vi.mock('../hooks/useUserSettings')
import { useUserSettings } from '../hooks/useUserSettings'
const mockUseUserSettings = vi.mocked(useUserSettings)

vi.mock('./FlatDeleteConfirm', () => ({
  FlatDeleteConfirm: ({ flatName, onCancel }: { flatName: string; onCancel: () => void }) => (
    <div>
      flat-delete-confirm:{flatName}
      <button onClick={onCancel}>mock-cancel</button>
    </div>
  ),
}))

const mockHref = vi.fn()

const defaultSettings: UserSettings = {
  locale: 'en-US',
  hasFlat: true,
  flatId: 'flat-1',
  flatName: 'My Flat',
  annualKwhBaseline: 2500,
  plannedAnnualSpend: null,
  flatRowVersion: 'AQID',
}

describe('AccountSettings', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockUseAuthMe.mockReturnValue({ data: { clientPrincipal: { userDetails: 'user@example.com' } } } as ReturnType<typeof useAuthMe>)
    mockUseUserSettings.mockReturnValue({
      settings: defaultSettings,
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof useUserSettings>)

    Object.defineProperty(window, 'location', {
      value: { get href() { return '' }, set href(v) { mockHref(v) } },
      writable: true,
      configurable: true,
    })
  })

  it('renders Sign Out button', () => {
    render(<AccountSettings />)
    expect(screen.getByText('account.signOut')).toBeInTheDocument()
  })

  it('clicking Sign Out shows confirmation dialog', () => {
    render(<AccountSettings />)
    fireEvent.click(screen.getByText('account.signOut'))
    expect(screen.getByText('account.signOutConfirm.title')).toBeInTheDocument()
    expect(screen.getByText('account.signOutConfirm.body')).toBeInTheDocument()
  })

  it('clicking Cancel hides confirmation dialog', () => {
    render(<AccountSettings />)
    fireEvent.click(screen.getByText('account.signOut'))
    fireEvent.click(screen.getByText('account.signOutConfirm.cancel'))
    expect(screen.queryByText('account.signOutConfirm.title')).not.toBeInTheDocument()
    expect(screen.getByText('account.signOut')).toBeInTheDocument()
  })

  it('clicking confirm sign-out navigates to /.auth/logout', () => {
    render(<AccountSettings />)
    fireEvent.click(screen.getByText('account.signOut'))
    fireEvent.click(screen.getByText('account.signOutConfirm.confirm'))
    expect(mockHref).toHaveBeenCalledWith('/.auth/logout')
  })

  it('renders Delete Flat button when a flat is active', () => {
    render(<AccountSettings />)
    expect(screen.getByText('account.deleteFlat.button')).toBeInTheDocument()
  })

  it('does not render Delete Flat button when no flat is active', () => {
    mockUseUserSettings.mockReturnValue({
      settings: { ...defaultSettings, hasFlat: false, flatId: undefined, flatName: undefined },
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof useUserSettings>)
    render(<AccountSettings />)
    expect(screen.queryByText('account.deleteFlat.button')).not.toBeInTheDocument()
  })

  it('clicking Delete Flat shows FlatDeleteConfirm with the active flat name', () => {
    render(<AccountSettings />)
    fireEvent.click(screen.getByText('account.deleteFlat.button'))
    expect(screen.getByText('flat-delete-confirm:My Flat')).toBeInTheDocument()
  })

  it('FlatDeleteConfirm onCancel hides it and restores the normal row', () => {
    render(<AccountSettings />)
    fireEvent.click(screen.getByText('account.deleteFlat.button'))
    fireEvent.click(screen.getByText('mock-cancel'))
    expect(screen.queryByText(/flat-delete-confirm/)).not.toBeInTheDocument()
    expect(screen.getByText('account.deleteFlat.button')).toBeInTheDocument()
  })

  it('FlatDeleteConfirm auto-closes if the active flat disappears while open', () => {
    const { rerender } = render(<AccountSettings />)
    fireEvent.click(screen.getByText('account.deleteFlat.button'))
    expect(screen.getByText(/flat-delete-confirm/)).toBeInTheDocument()

    mockUseUserSettings.mockReturnValue({
      settings: { ...defaultSettings, hasFlat: false, flatId: undefined, flatName: undefined },
      isLoading: false,
      isError: false,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof useUserSettings>)
    rerender(<AccountSettings />)

    expect(screen.queryByText(/flat-delete-confirm/)).not.toBeInTheDocument()
  })

  it('Delete Flat and sign-out confirms are mutually exclusive', () => {
    render(<AccountSettings />)
    fireEvent.click(screen.getByText('account.signOut'))
    expect(screen.getByText('account.signOutConfirm.title')).toBeInTheDocument()
    expect(screen.queryByText('account.deleteFlat.button')).not.toBeInTheDocument()
  })
})
