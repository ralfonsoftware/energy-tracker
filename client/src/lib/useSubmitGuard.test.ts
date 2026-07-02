import { renderHook } from '@testing-library/react'
import { useSubmitGuard } from './useSubmitGuard'

describe('useSubmitGuard', () => {
  it('allows the first acquire and blocks a second one while still pending', () => {
    const { result } = renderHook(({ isPending }) => useSubmitGuard(isPending), {
      initialProps: { isPending: false },
    })

    expect(result.current()).toBe(true)
    expect(result.current()).toBe(false)
  })

  it('releases the guard once isPending transitions back to false', () => {
    const { result, rerender } = renderHook(({ isPending }) => useSubmitGuard(isPending), {
      initialProps: { isPending: false },
    })

    expect(result.current()).toBe(true)
    rerender({ isPending: true })
    rerender({ isPending: false })

    expect(result.current()).toBe(true)
  })
})
