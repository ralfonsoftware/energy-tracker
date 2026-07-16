import type { ReactNode } from 'react'

type Props = {
  children: ReactNode
}

export function StickyActionBar({ children }: Props) {
  return (
    <div
      className="sticky bottom-[calc(84px_+_env(safe-area-inset-bottom,0px))] md:bottom-0 px-6 py-3 flex flex-col gap-2"
      style={{
        background: '#111827',
        borderTop: '1px solid rgba(255,255,255,0.12)',
      }}
    >
      {children}
    </div>
  )
}
