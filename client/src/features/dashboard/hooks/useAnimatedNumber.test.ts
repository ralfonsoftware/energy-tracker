import { renderHook, act } from '@testing-library/react'
import { vi, describe, it, expect, afterEach } from 'vitest'
import { useAnimatedNumber } from '@/features/dashboard/hooks/useAnimatedNumber'

function mockMatchMedia(matches: boolean) {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    configurable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches,
      media: query,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    })),
  })
}

describe('useAnimatedNumber', () => {
  afterEach(() => {
    vi.restoreAllMocks()
    vi.useRealTimers()
  })

  it('useAnimatedNumber_ReducedMotion_JumpsStraightToTargetWithNoIntermediateFrames', () => {
    mockMatchMedia(true)
    const armedRef = { current: false }
    const { result, rerender } = renderHook(
      ({ target }: { target: number }) => useAnimatedNumber(target, armedRef),
      { initialProps: { target: 0 } }
    )
    expect(result.current).toBe(0)

    armedRef.current = true
    rerender({ target: 100 })
    expect(result.current).toBe(100)
  })

  it('useAnimatedNumber_NormalConditions_AdvancingFramesEventuallySettlesOnTarget', () => {
    mockMatchMedia(false)
    vi.useFakeTimers()
    const armedRef = { current: true }

    const { result, rerender } = renderHook(
      ({ target }: { target: number }) => useAnimatedNumber(target, armedRef),
      { initialProps: { target: 0 } }
    )

    rerender({ target: 100 })

    act(() => {
      vi.advanceTimersByTime(1000)
    })

    expect(result.current).toBe(100)
  })

  it('useAnimatedNumber_TargetChangesWhileNotArmed_SnapsImmediatelyWithoutAnimating', () => {
    mockMatchMedia(false)
    vi.useFakeTimers()
    const armedRef = { current: false }

    const { result, rerender } = renderHook(
      ({ target }: { target: number }) => useAnimatedNumber(target, armedRef),
      { initialProps: { target: 0 } }
    )

    rerender({ target: 50 })
    expect(result.current).toBe(50)
  })

  it('useAnimatedNumber_ArmedBeforeTargetChangeArrivesInALaterRender_StillAnimates', () => {
    mockMatchMedia(false)
    vi.useFakeTimers()
    const armedRef = { current: false }

    const { result, rerender } = renderHook(
      ({ target }: { target: number }) => useAnimatedNumber(target, armedRef),
      { initialProps: { target: 0 } }
    )

    // Simulates the real submit sequence: the trigger is armed synchronously on success,
    // but an unrelated re-render (e.g. the sheet closing) can land before the refetched
    // dashboard value actually changes target.
    armedRef.current = true
    rerender({ target: 0 })
    expect(result.current).toBe(0)

    rerender({ target: 100 })
    act(() => {
      vi.advanceTimersByTime(1000)
    })
    expect(result.current).toBe(100)
  })
})
