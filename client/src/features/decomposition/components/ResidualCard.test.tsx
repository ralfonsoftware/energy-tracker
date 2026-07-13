import { render, screen } from '@testing-library/react'
import { ResidualCard } from '@/features/decomposition/components/ResidualCard'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

describe('ResidualCard', () => {
  it('ResidualCard_ZeroKwh_StillRendersCardWithTitleAndValue', () => {
    render(<ResidualCard kwh={0} cost={0} totalKwh={100} />)

    expect(screen.getByText('residual.title')).toBeInTheDocument()
    expect(screen.getByText(/0 kWh/)).toBeInTheDocument()
  })

  it('ResidualCard_PositiveKwh_RendersKwhAndCost', () => {
    render(<ResidualCard kwh={18.4} cost={4.26} totalKwh={100} />)

    expect(screen.getByText(/18.4/)).toBeInTheDocument()
    expect(screen.getByText(/4[.,]26/)).toBeInTheDocument()
  })

  it('ResidualCard_Rendered_HasNoButtonOrClickableAffordance', () => {
    render(<ResidualCard kwh={5} cost={1} totalKwh={100} />)

    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })
})
