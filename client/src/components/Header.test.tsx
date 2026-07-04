import { render, screen } from '@testing-library/react'
import { vi, describe, it, expect } from 'vitest'
import { Header } from './Header'

vi.mock('./FlatSwitcher', () => ({
  FlatSwitcher: () => <div>flat-switcher</div>,
}))

describe('Header', () => {
  it('Header_Rendered_ContainsFlatSwitcher', () => {
    render(<Header />)
    expect(screen.getByText('flat-switcher')).toBeInTheDocument()
  })

  it('Header_Rendered_HasBannerRole', () => {
    render(<Header />)
    expect(screen.getByRole('banner')).toBeInTheDocument()
  })
})
