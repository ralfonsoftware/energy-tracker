import { useEffect, useRef } from 'react'

export function useSubmitGuard(isPending: boolean) {
  const submittingRef = useRef(false)

  useEffect(() => {
    if (!isPending) submittingRef.current = false
  }, [isPending])

  return function tryAcquire() {
    if (submittingRef.current) return false
    submittingRef.current = true
    return true
  }
}
