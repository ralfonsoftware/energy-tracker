import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

type Props = {
  label: string
  headline: ReactNode | undefined // undefined → skeleton
  subline?: ReactNode
  delta?: string
  deltaVariant?: 'under' | 'over' | 'neutral'
  caption?: string
}

export function KpiTile({ label, headline, subline, delta, deltaVariant, caption }: Props) {
  return (
    <div className="flex flex-col gap-1 rounded-card border border-glass-border bg-glass-surface py-4 px-[18px] backdrop-blur-[20px] backdrop-saturate-[1.8]">
      <p className="text-label-caps text-text-tertiary">{label}</p>
      {headline === undefined ? (
        <div role="status" aria-label="loading" className="h-[22px] w-16 animate-pulse rounded bg-white/10" />
      ) : (
        <p className="text-display-kpi text-text-primary">{headline}</p>
      )}
      {subline !== undefined && <p className="text-body-sm text-text-secondary">{subline}</p>}
      {delta !== undefined && (
        <p
          className={cn(
            'text-label-caps',
            deltaVariant === 'under' && 'text-accent-under-budget',
            deltaVariant === 'over' && 'text-accent-over-budget',
            (deltaVariant === undefined || deltaVariant === 'neutral') && 'text-text-tertiary'
          )}
        >
          {delta}
        </p>
      )}
      {caption !== undefined && <p className="text-caption text-text-tertiary">{caption}</p>}
    </div>
  )
}
