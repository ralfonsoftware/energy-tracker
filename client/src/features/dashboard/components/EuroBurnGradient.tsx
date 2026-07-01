type Props = { todayKwh: number; dailyBudgetKwh: number; isColdOpen?: boolean }

const BASE_STOPS = [0, 25, 50, 75, 100]

export function EuroBurnGradient({ todayKwh, dailyBudgetKwh, isColdOpen = false }: Props) {
  // ratio: 0 = at budget, positive = over budget, negative = under budget
  // Cold open (no readings yet) always renders the neutral midpoint, regardless of
  // the budget derived from the flat's annual baseline.
  const ratio = isColdOpen || dailyBudgetKwh <= 0 ? 0 : (todayKwh - dailyBudgetKwh) / dailyBudgetKwh
  const clamped = Math.max(-0.5, Math.min(0.5, ratio))
  // Shifting all stops by -clamped*100 lets over-budget push warm stops into view while
  // cool stops slide past 0% (clipped), and vice versa for under-budget — no midpoint marker needed.
  const shift = -clamped * 100
  const [coolStart, coolEnd, neutral, warmStart, warmEnd] = BASE_STOPS.map(s => s + shift)

  return (
    <div
      className="fixed inset-0 -z-10 [--gradient-angle:160deg] md:[--gradient-angle:140deg]"
      style={{
        background: `linear-gradient(var(--gradient-angle), var(--color-gradient-cool-start) ${coolStart}%, var(--color-gradient-cool-end) ${coolEnd}%, var(--color-gradient-neutral) ${neutral}%, var(--color-gradient-warm-start) ${warmStart}%, var(--color-gradient-warm-end) ${warmEnd}%)`,
      }}
      aria-hidden="true"
    />
  )
}
