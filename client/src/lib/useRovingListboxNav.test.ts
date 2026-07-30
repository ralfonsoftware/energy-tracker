import { act, renderHook } from '@testing-library/react'
import { useRovingListboxNav } from './useRovingListboxNav'

function key(key: string) {
  return { key, preventDefault: vi.fn() } as unknown as Parameters<
    ReturnType<typeof useRovingListboxNav>['handleKeyDown']
  >[0]
}

describe('useRovingListboxNav', () => {
  it('ArrowDown moves activeIndex forward and clamps at the last item', () => {
    const { result } = renderHook(() => useRovingListboxNav(3, 0))

    act(() => result.current.handleKeyDown(key('ArrowDown')))
    expect(result.current.getItemProps(1).tabIndex).toBe(0)

    act(() => result.current.handleKeyDown(key('ArrowDown')))
    act(() => result.current.handleKeyDown(key('ArrowDown')))
    expect(result.current.getItemProps(2).tabIndex).toBe(0)
    expect(result.current.getItemProps(0).tabIndex).toBe(-1)
  })

  it('ArrowUp moves activeIndex backward and clamps at the first item', () => {
    const { result } = renderHook(() => useRovingListboxNav(3, 2))

    act(() => result.current.handleKeyDown(key('ArrowUp')))
    expect(result.current.getItemProps(1).tabIndex).toBe(0)

    act(() => result.current.handleKeyDown(key('ArrowUp')))
    act(() => result.current.handleKeyDown(key('ArrowUp')))
    expect(result.current.getItemProps(0).tabIndex).toBe(0)
    expect(result.current.getItemProps(2).tabIndex).toBe(-1)
  })

  it('Home jumps to the first item and End jumps to the last item', () => {
    const { result } = renderHook(() => useRovingListboxNav(4, 1))

    act(() => result.current.handleKeyDown(key('End')))
    expect(result.current.getItemProps(3).tabIndex).toBe(0)

    act(() => result.current.handleKeyDown(key('Home')))
    expect(result.current.getItemProps(0).tabIndex).toBe(0)
  })

  it('calls preventDefault for handled keys', () => {
    const { result } = renderHook(() => useRovingListboxNav(3, 0))
    const event = key('ArrowDown')

    act(() => result.current.handleKeyDown(event))

    expect(event.preventDefault).toHaveBeenCalled()
  })

  it('does not throw when itemCount is 0', () => {
    const { result } = renderHook(() => useRovingListboxNav(0, 0))

    expect(() => act(() => result.current.handleKeyDown(key('ArrowDown')))).not.toThrow()
    expect(() =>
      act(() => result.current.handleOpenAutoFocus({ preventDefault: vi.fn() } as unknown as Event))
    ).not.toThrow()
  })

  it('getItemProps returns tabIndex 0 for the active index and -1 for others', () => {
    const { result } = renderHook(() => useRovingListboxNav(3, 1))

    expect(result.current.getItemProps(0).tabIndex).toBe(-1)
    expect(result.current.getItemProps(1).tabIndex).toBe(0)
    expect(result.current.getItemProps(2).tabIndex).toBe(-1)
  })

  it('resyncs activeIndex to selectedIndex when itemCount goes from 0 to populated while mounted', () => {
    const { result, rerender } = renderHook(
      ({ itemCount, selectedIndex }) => useRovingListboxNav(itemCount, selectedIndex),
      { initialProps: { itemCount: 0, selectedIndex: 1 } }
    )

    rerender({ itemCount: 3, selectedIndex: 1 })

    expect(result.current.getItemProps(1).tabIndex).toBe(0)
    expect(result.current.getItemProps(0).tabIndex).toBe(-1)
  })

  it('resyncs activeIndex to selectedIndex when it falls out of range after itemCount shrinks', () => {
    const { result, rerender } = renderHook(
      ({ itemCount, selectedIndex }) => useRovingListboxNav(itemCount, selectedIndex),
      { initialProps: { itemCount: 3, selectedIndex: 0 } }
    )

    act(() => result.current.handleKeyDown(key('End')))
    expect(result.current.getItemProps(2).tabIndex).toBe(0)

    rerender({ itemCount: 1, selectedIndex: 0 })

    expect(result.current.getItemProps(0).tabIndex).toBe(0)
  })
})
