import { render, screen, act } from '@testing-library/react'
import { vi } from 'vitest'
import { createMemoryRouter, RouterProvider } from 'react-router-dom'
import NotFoundPage from './NotFoundPage'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}))

function renderNotFoundPage() {
  const router = createMemoryRouter(
    [
      { path: '/nonexistent', element: <NotFoundPage /> },
      { path: '/', element: <div>dashboard-stub</div> },
    ],
    { initialEntries: ['/nonexistent'] }
  )
  render(<RouterProvider router={router} />)
  return router
}

describe('NotFoundPage', () => {
  it('NotFoundPage_Rendered_ShowsHeadingAndBodyText', () => {
    renderNotFoundPage()

    expect(screen.getByText('notFound.heading')).toBeInTheDocument()
    expect(screen.getByText('notFound.body')).toBeInTheDocument()
  })

  it('NotFoundPage_CtaClicked_NavigatesToDashboard', async () => {
    renderNotFoundPage()

    await act(async () => {
      screen.getByText('notFound.cta').click()
    })

    expect(screen.getByText('dashboard-stub')).toBeInTheDocument()
  })
})
