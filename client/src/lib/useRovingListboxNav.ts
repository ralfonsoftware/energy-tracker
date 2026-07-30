import { useEffect, useRef, useState, type KeyboardEvent } from 'react'

export function useRovingListboxNav(itemCount: number, selectedIndex: number) {
  const itemRefs = useRef<(HTMLElement | null)[]>([])
  const [activeIndex, setActiveIndex] = useState(selectedIndex)
  const activeIndexRef = useRef(activeIndex)
  activeIndexRef.current = activeIndex
  const prevItemCountRef = useRef(itemCount)

  const focus = (index: number) => {
    setActiveIndex(index)
    itemRefs.current[index]?.focus()
  }

  // Resync while the popover is already open — e.g. itemCount going from 0 (still loading) to
  // populated, or the active item being removed from the list — since handleOpenAutoFocus only
  // fires once, at the moment the popover opens.
  useEffect(() => {
    const prevItemCount = prevItemCountRef.current
    prevItemCountRef.current = itemCount
    if (itemCount === 0) return
    const outOfRange = activeIndexRef.current >= itemCount
    const justBecameAvailable = prevItemCount === 0
    if (outOfRange || justBecameAvailable) {
      setActiveIndex(selectedIndex)
      itemRefs.current[selectedIndex]?.focus()
    }
  }, [itemCount, selectedIndex])

  const handleKeyDown = (event: KeyboardEvent<HTMLElement>) => {
    if (itemCount === 0) return
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault()
        focus(Math.min(activeIndex + 1, itemCount - 1))
        break
      case 'ArrowUp':
        event.preventDefault()
        focus(Math.max(activeIndex - 1, 0))
        break
      case 'Home':
        event.preventDefault()
        focus(0)
        break
      case 'End':
        event.preventDefault()
        focus(itemCount - 1)
        break
    }
  }

  const handleOpenAutoFocus = (event: Event) => {
    if (itemCount === 0) return
    event.preventDefault()
    focus(selectedIndex)
  }

  const getItemProps = (index: number) => ({
    ref: (el: HTMLElement | null) => {
      itemRefs.current[index] = el
    },
    tabIndex: index === activeIndex ? 0 : -1,
  })

  return { handleKeyDown, handleOpenAutoFocus, getItemProps }
}
