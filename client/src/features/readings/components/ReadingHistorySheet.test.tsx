import { render, screen } from '@testing-library/react'
import { describe, it, expect, vi } from 'vitest'
import { ReadingHistorySheet } from '@/features/readings/components/ReadingHistorySheet'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

describe('ReadingHistorySheet', () => {
  it('ReadingHistorySheet_Rendered_ShowsTitleAndComingSoonText', () => {
    render(<ReadingHistorySheet flatId="flat-1" />)

    expect(screen.getByText('history.title')).toBeInTheDocument()
    expect(screen.getByText('history.comingSoon')).toBeInTheDocument()
  })

  it('ReadingHistorySheet_Rendered_RendersSynchronouslyWithNoLoadingOrErrorState', () => {
    render(<ReadingHistorySheet flatId={undefined} />)

    expect(screen.queryByRole('status')).not.toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })
})
