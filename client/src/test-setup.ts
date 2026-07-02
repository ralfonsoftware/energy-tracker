import '@testing-library/jest-dom'
import '@/lib/i18n'

if (!window.matchMedia) {
  window.matchMedia = (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  })
}

if (!window.ResizeObserver) {
  window.ResizeObserver = class {
    private callback: ResizeObserverCallback
    constructor(callback: ResizeObserverCallback) {
      this.callback = callback
    }
    observe(target: Element) {
      this.callback(
        [{ target, contentRect: { width: 320, height: 90 } } as ResizeObserverEntry],
        this as unknown as ResizeObserver
      )
    }
    unobserve() {}
    disconnect() {}
  }
}
