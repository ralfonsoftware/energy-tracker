import { render, screen, fireEvent } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { AccountSettings } from './AccountSettings'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

vi.mock('../hooks/useAuthMe')
import { useAuthMe } from '../hooks/useAuthMe'
const mockUseAuthMe = vi.mocked(useAuthMe)

const mockHref = vi.fn()

describe('AccountSettings', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockUseAuthMe.mockReturnValue({ data: { clientPrincipal: { userDetails: 'user@example.com' } } } as ReturnType<typeof useAuthMe>)

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
})
