import { useEffect, useRef, useState } from 'react'

export function useAnimatedNumber(
  target: number,
  armedRef: { current: boolean },
  durationMs = 700
) {
  const [value, setValue] = useState(target)
  const fromRef = useRef(target)

  useEffect(() => {
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
    const from = fromRef.current
    if (from === target) return

    // armedRef is set synchronously by the caller right when a submit succeeds — reading it
    // here (rather than diffing a "trigger" value against the previous render) means the
    // animation still fires correctly even when the query refetch that changes `target` lands
    // in an earlier render than the state update that would otherwise gate it. armedRef is
    // shared across every tile's call to this hook, so it is only ever read here, never
    // consumed — the caller resets it once, after all tiles have had a chance to read it.
    if (!armedRef.current || reduceMotion) {
      setValue(target)
      fromRef.current = target
      return
    }

    const start = performance.now()
    let raf = 0
    const step = (now: number) => {
      const progress = Math.min(1, (now - start) / durationMs)
      const next = from + (target - from) * progress
      setValue(next)
      fromRef.current = next
      if (progress < 1) raf = requestAnimationFrame(step)
      else fromRef.current = target
    }
    raf = requestAnimationFrame(step)
    return () => cancelAnimationFrame(raf)
  }, [target, durationMs, armedRef])

  return value
}
