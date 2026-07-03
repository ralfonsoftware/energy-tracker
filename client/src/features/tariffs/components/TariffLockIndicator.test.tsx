import { render, screen } from '@testing-library/react'
import { describe, it, expect, vi, afterEach } from 'vitest'
import { TariffLockIndicator } from '@/features/tariffs/components/TariffLockIndicator'

vi.mock('react-i18next', () => ({
  useTranslation: () => ({
    t: (k: string, opts?: Record<string, unknown>) => (opts?.date ? `${k}:${opts.date}` : k),
    i18n: { language: 'en-US' },
  }),
}))

describe('TariffLockIndicator', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
  })

  it('TariffLockIndicator_ContractStartPlusDuration_RendersMonthYearLabel', () => {
    vi.stubEnv('TZ', 'UTC')

    render(<TariffLockIndicator contractStartDate="2026-01-15T00:00:00Z" contractDurationMonths={12} />)

    expect(screen.getByText(/January 2027/)).toBeInTheDocument()
  })

  it('TariffLockIndicator_StartDateNearMonthBoundaryWestOfUtc_UsesLocalDatePartsNotUtc', () => {
    // 2026-01-01T02:00:00Z is 2025-12-31 23:00 local time in UTC-3 (America/Sao_Paulo).
    // Naive UTC-part math (getUTCMonth) would compute Jan 2026 + 1 month = Feb 2026 — wrong.
    // Correct local-part math computes Dec 2025 + 1 month = Jan 2026.
    vi.stubEnv('TZ', 'America/Sao_Paulo')

    render(<TariffLockIndicator contractStartDate="2026-01-01T02:00:00Z" contractDurationMonths={1} />)

    expect(screen.getByText(/January 2026/)).toBeInTheDocument()
    expect(screen.queryByText(/February 2026/)).not.toBeInTheDocument()
  })

  it('TariffLockIndicator_NoDuration_RendersLockedSinceLabelUsingContractStartDate', () => {
    // Same local-date-parts computation as the duration-provided case — no getUTC*/toISOString.
    vi.stubEnv('TZ', 'America/Sao_Paulo')

    render(<TariffLockIndicator contractStartDate="2026-01-01T02:00:00Z" contractDurationMonths={null} />)

    expect(screen.getByText(/form.lockedSinceLabel/)).toBeInTheDocument()
    expect(screen.getByText(/December 2025/)).toBeInTheDocument()
  })
})
