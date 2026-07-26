import { render, screen } from '@testing-library/react'
import { vi, describe, it, expect } from 'vitest'
import { InsightDiscoveryProgress } from '@/features/insights/components/InsightDiscoveryProgress'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

describe('InsightDiscoveryProgress', () => {
  it('InsightDiscoveryProgress_Rendered_ShowsProgressLabelAndSpinner', () => {
    const { container } = render(<InsightDiscoveryProgress />)

    expect(screen.getByRole('status')).toBeInTheDocument()
    expect(screen.getByText('progress.label')).toBeInTheDocument()
    expect(container.querySelector('.animate-spin')).toBeInTheDocument()
  })
})
