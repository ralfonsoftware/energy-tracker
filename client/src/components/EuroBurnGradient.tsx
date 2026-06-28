export function EuroBurnGradient({ consumptionPct = 0 }: { consumptionPct?: number }) {
  // consumptionPct is unused in 1.5 — gradient renders at neutral midpoint.
  // Dynamic stop interpolation added in Story 3.x when dashboard KPI data lands.
  void consumptionPct
  return (
    <div
      className="fixed inset-0 -z-10"
      style={{
        background:
          'linear-gradient(160deg, #1a1f4e 0%, #0d4f5c 30%, #2d2018 60%, #4a2000 85%, #6b2d00 100%)',
      }}
      aria-hidden="true"
    />
  )
}
