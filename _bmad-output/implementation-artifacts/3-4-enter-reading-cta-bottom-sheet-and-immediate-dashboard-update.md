---
baseline_commit: 003d8a0
---

# Story 3.4: Enter Reading CTA, Bottom Sheet & Immediate Dashboard Update

Status: done

## Story

As a user,
I want to tap Enter Reading, type the meter value, tap Save, and immediately see the KPI tiles update,
so that the core meter-reading loop is fast, frictionless, and confirmatory.

## Acceptance Criteria

1. **Phone CTA** — Given the Dashboard on phone, when rendered, then the Enter Reading CTA is a full-width pill (`border-radius: 9999px`, `padding: 16px 24px`, `background: rgba(255,255,255,0.10)`, `backdrop-filter: blur(20px) saturate(180%)`, `border: 1.5px solid rgba(255,255,255,0.40)`) with label "Enter Reading" in `body` type role (16px/600/+0.01em).

2. **Tablet CTA** — Given the Dashboard on tablet (≥768px), when rendered, then the CTA is a 44×44px compact button (`border-radius: 14px`) in the content header top-right showing the Lucide `Zap` icon (20×20px) only — no text label.

3. **Sheet open state** — Given the Enter Reading CTA is tapped, when the bottom sheet opens, then it slides up from the bottom with a drag handle; the kWh input is auto-focused with `inputmode="numeric"`; the date/time field is pre-filled with the current timestamp; Save is inactive until `kwhValue > 0`; the hint "Date and time will be saved with your reading." appears below the date field.

4. **Successful submit** — Given a valid kWh value is entered and Save is tapped, when `useSubmitReading` calls `POST /api/v1/flats/{flatId}/readings` and succeeds, then (1) the sheet closes, (2) TanStack Query key `['dashboard', flatId]` is invalidated triggering an immediate refetch, (3) all four KPI tiles animate with a count animation to their new values; "Last read:" updates to the submitted reading's date/time. With `prefers-reduced-motion: reduce` active, values update immediately with no animation.

5. **Lower-than-last warning** — Given the entered kWh value is lower than the last recorded reading, when the value is typed, then the inline warning "Lower than your last reading ({lastKwhValue} kWh) — is this correct?" appears below the input; Save remains active.

6. **Submit failure** — Given the POST returns an error, when the mutation fails, then the sheet stays open; the typed value is preserved; "Couldn't save — try again." appears near the Save button.

7. **Accessibility** — Given the bottom sheet while open, when focus is managed, then focus is trapped within the sheet; closing returns focus to the CTA; all interactive elements meet 44×44pt minimum tap targets; validation messages use `aria-live="polite"`.

## Tasks / Subtasks

- [x] **Task 0: Backend — add `LastKwhValue` to the dashboard response** (AC: 5) — required because AC-5's comparison value does not exist in the current API contract; see Dev Notes "Blocking Gap"
  - [x] `api/Features/Dashboard/DashboardModels.cs` — add `decimal? LastKwhValue` as the final parameter of `DashboardSummary`
  - [x] `api/Features/Dashboard/KpiCalculator.cs` — populate `LastKwhValue` in all four `return new DashboardSummary(...)` branches (null when zero readings; `readings[^1].KwhValue` otherwise — note the single-reading branch uses `readings[0]`, which is the same element as `readings[^1]`)
  - [x] Update `api.Tests/Features/Dashboard/KpiCalculatorTests.cs` and `GetDashboardFunctionTests.cs` — these use named-argument construction so the compiler will flag every call site; add `LastKwhValue` assertions to existing test methods rather than new tests
  - [x] `dotnet test api.Tests` passes

- [x] **Task 1: `dashboardApi.ts` — add `lastKwhValue`** (AC: 5)
  - [x] `client/src/features/dashboard/api/dashboardApi.ts` — add `lastKwhValue: number | null` to `DashboardSummary` type (matches backend field, camelCase)

- [x] **Task 2: `readingApi.ts` — submit-reading API module** (AC: 4, 6)
  - [x] `client/src/features/readings/api/readingApi.ts` (NEW) — `SubmitReadingRequest` type `{ kwhValue: number; readingDate: string }`, `ReadingResponse` type, `submitReading(flatId: string, body: SubmitReadingRequest)` calling `apiClient.post<ReadingResponse>('/flats/${flatId}/readings', body)`

- [x] **Task 3: `readingSchema.ts` — zod schema for the sheet form** (AC: 3)
  - [x] `client/src/features/readings/schemas/readingSchema.ts` (NEW) — `kwhValue: z.string()`, validated/parsed the same way as the existing numeric-input pattern (see Dev Notes — this codebase parses locale-formatted numeric strings manually, it does not use `z.coerce.number()`)

- [x] **Task 4: `useSubmitReading.ts` — mutation hook** (AC: 4)
  - [x] `client/src/features/readings/hooks/useSubmitReading.ts` (NEW) — `useMutation`; `onSuccess`: `await queryClient.invalidateQueries({ queryKey: ['dashboard', flatId] })` (see Dev Notes for why `await` before closing the sheet)

- [x] **Task 5: Generate shadcn `Sheet` primitive** (AC: 3, 7)
  - [x] Run `npx shadcn@latest add sheet` from `client/`; verify output lands at `client/src/components/ui/sheet.tsx` (the CLI has previously misresolved the `@/` alias to a literal `client/@/...` directory — check for and remove any stray `@` folder)
  - [x] Do NOT hand-edit `ui/sheet.tsx` — apply project styling via `className` from the calling component only

- [x] **Task 6: `EnterReadingSheet.tsx` — bottom sheet content** (AC: 3, 4, 5, 6, 7)
  - [x] `client/src/features/readings/components/EnterReadingSheet.tsx` (NEW) — see Dev Notes Task 6 for full implementation guidance (auto-focus, low-value warning, error banner, focus trap via Radix defaults)

- [x] **Task 7: `EnterReadingCta.tsx` — phone pill / tablet icon button** (AC: 1, 2, 3)
  - [x] `client/src/features/readings/components/EnterReadingCta.tsx` (NEW) — wraps `Sheet` + two responsive `SheetTrigger` variants + `EnterReadingSheet`

- [x] **Task 8: `useAnimatedNumber.ts` — count animation hook** (AC: 4)
  - [x] `client/src/features/dashboard/hooks/useAnimatedNumber.ts` (NEW) — see Dev Notes Task 8

- [x] **Task 9: Wire into Dashboard** (AC: 1, 2, 4)
  - [x] `client/src/features/dashboard/DashboardPage.tsx` (MODIFY) — render `EnterReadingCta`, pass `flatId`, `lastKwhValue`; track a "just submitted" signal to scope the count animation to post-submit updates only (see Dev Notes Task 9)
  - [x] `client/src/features/dashboard/components/DashboardGrid.tsx` (MODIFY) — accept an `animate` flag/trigger and use `useAnimatedNumber` for the four headline values when set

- [x] **Task 10: Add missing `text-body` type-role utility** (AC: 1)
  - [x] `client/src/index.css` — add `@utility text-body { font-size: 16px; font-weight: 600; letter-spacing: 0.01em; }` next to the existing `text-body-sm` block (this role is specified in the UX design doc but was never added to the theme in prior stories)

- [x] **Task 11: Translation keys** (AC: 1, 3, 5, 6)
  - [x] `client/src/locales/en-US/readings.json` — currently `{}`; populate (see Dev Notes Task 11 for full key list)
  - [x] `client/src/locales/de-DE/readings.json` — currently `{}`; populate German equivalents

- [x] **Task 12: Tests** (AC: all)
  - [x] `client/src/features/readings/components/EnterReadingSheet.test.tsx` (NEW) — minimum 6 tests (see Dev Notes Task 12)
  - [x] `client/src/features/readings/hooks/useSubmitReading.test.ts` (NEW) — minimum 2 tests
  - [x] `client/src/features/dashboard/hooks/useAnimatedNumber.test.ts` (NEW) — minimum 2 tests (reduced-motion bypass; interpolation reaches target)

- [x] **Task 13: Final verification**
  - [x] `dotnet test api.Tests` exits 0 (Task 0 backend change)
  - [x] `cd client && npm run build` exits 0 with zero TypeScript errors
  - [x] `cd client && npm test` — all tests pass including all pre-existing tests
  - [x] `cd client && npm run lint` exits 0
  - [x] Update File List in this story

### Review Findings

**Patch (11):**
- [x] [Review][Patch] Count-up animation never plays on a real submit — trigger/target update race [client/src/features/dashboard/hooks/useAnimatedNumber.ts]
- [x] [Review][Patch] Dual-`Sheet`-root pattern in `EnterReadingCta` produces orphaned `aria-controls` on both trigger buttons [client/src/features/readings/components/EnterReadingCta.tsx]
- [x] [Review][Patch] AC-1: phone CTA pill missing `padding: 16px 24px` [client/src/features/readings/components/EnterReadingCta.tsx]
- [x] [Review][Patch] AC-3: date/time field not refreshed with current timestamp on sheet reopen [client/src/features/readings/components/EnterReadingSheet.tsx]
- [x] [Review][Patch] AC-7: shadcn stock close (X) button does not meet 44×44pt tap target [client/src/features/readings/components/EnterReadingSheet.tsx]
- [x] [Review][Patch] `new Date(readingDate).toISOString()` throws on an invalid/empty date instead of being guarded [client/src/features/readings/components/EnterReadingSheet.tsx]
- [x] [Review][Patch] Save can be tapped while `flatId` is still undefined (settings still loading) [client/src/features/readings/components/EnterReadingSheet.tsx]
- [x] [Review][Patch] Rapid double-click/tap on Save can fire two submissions before `isPending` propagates [client/src/features/readings/components/EnterReadingSheet.tsx]
- [x] [Review][Patch] `--radius-sheet` token defined but not consumed; hardcoded `rounded-t-[24px]` instead of `rounded-t-sheet` [client/src/features/readings/components/EnterReadingSheet.tsx]
- [x] [Review][Patch] `readingSchema.ts` missing `.min(1, 'Required')` per Dev Notes' cited FlatBaselineEdit/OnboardingContract convention [client/src/features/readings/schemas/readingSchema.ts]
- [x] [Review][Patch] Completion Notes overstate `class-variance-authority` as newly added by this story — it was already a pre-existing dependency; only `@radix-ui/react-dialog` is new [story Dev Agent Record]

**Defer (4):**
- [x] [Review][Defer] `parseLocaleNumber` mis-parses de-DE decimals typed with `.` and multi-comma input [client/src/lib/localeNumber.ts] — deferred, pre-existing shared utility, not introduced by this story
- [x] [Review][Defer] No upper-bound/sanity check on kWh value client-side [client/src/features/readings/components/EnterReadingSheet.tsx] — deferred, matches existing precedent (FlatBaselineEdit has no cap either)
- [x] [Review][Defer] No guard against future-dated readings [client/src/features/readings/components/EnterReadingSheet.tsx] — deferred, not spec-required, pre-existing gap pattern
- [x] [Review][Defer] `flatId` interpolated unencoded into API URL path [client/src/features/readings/api/readingApi.ts] — deferred, pre-existing repo-wide convention (same in dashboardApi.ts)



### Blocking Gap Discovered During Story Creation — Backend Field Missing

AC-5 requires comparing the typed value against "your last reading ({lastKwhValue} kWh)". `GetDashboardFunction` / `DashboardSummary` (`api/Features/Dashboard/DashboardModels.cs`) currently expose **no field carrying the last reading's kWh value** — only `LastReadingDate`. `KpiCalculator.cs` already holds this value in scope (`readings[^1].KwhValue`) when it builds every `DashboardSummary` branch, so surfacing it is a one-field addition, not a new query. This is Task 0. Do not skip it or invent a client-side workaround (e.g. caching the last submitted value in local state) — that would silently break on a fresh page load / different device, which is exactly the scenario the AC is protecting against.

### What Already Exists — Read Before Writing Any Code

- `client/src/features/readings/.gitkeep` and empty `api/`, `components/`, `hooks/`, `schemas/` subfolders do not exist yet under `readings/` — create the full VSA structure (`api/`, `components/`, `hooks/`, `schemas/`) per project convention; do not place files at the feature root.
- `client/src/locales/{en-US,de-DE}/readings.json` — already exist as empty `{}` stubs and the `readings` namespace is **already registered** in `client/src/lib/i18n.ts`'s `ns: [...]` array. Do NOT add it again — just populate the JSON files.
- `client/src/features/dashboard/DashboardPage.tsx` — fully implemented (not a stub). Current signature: `useUserSettings()` returns `{ settings, isLoading, isError }` (NOT `{ data }` — this tripped up Story 3.3's dev notes, which had the wrong shape). `useDashboard(settings?.flatId)` returns TanStack Query's full result (`data`, `isPending`, `isError`). Read the current file in full before editing — it already renders an inline error banner and passes `isColdOpen` to `EuroBurnGradient`.
- `client/src/features/dashboard/components/DashboardGrid.tsx` — fully implemented, takes `{ dashboard, annualKwhBaseline }` props and internally computes formatted strings via local `formatNumber`/`formatKwh`/`formatCurrency` helpers using `i18n.language`. Any animation must integrate with this existing formatting, not replace it.
- `client/src/features/dashboard/components/KpiTile.tsx` — pure presentational; `headline: ReactNode | undefined` (undefined → skeleton). No changes needed to this file — animation happens by feeding it a changing `headline` string over time from the parent.
- `client/src/components/ui/popover.tsx` — the ONLY shadcn primitive generated so far. It uses shadcn's default theme class names (`bg-popover`, etc.) which resolve to nothing because this project's Tailwind `@theme` never defines shadcn's CSS variables — the file is deliberately left with stock (non-functional-looking) classes, and all real styling is applied by the wrapping feature component (`CostGapBadge.tsx`) via `className` overrides. Follow the exact same pattern for `Sheet`: generate stock, style from `EnterReadingSheet.tsx`.
- `client/src/index.css` `@theme {}` already defines `--radius-sheet: 24px` (top corners only, per the design doc) and `--radius-pill: 9999px` — both unused until now. Use `rounded-t-sheet` (or the equivalent arbitrary-corner utility) on the sheet content, `rounded-pill` on the phone CTA.
- No animation library (`framer-motion`, etc.) is installed. The AC-4 count animation must be implemented with a small custom hook (`requestAnimationFrame`), not a new dependency.
- No shared "content header" component exists in `AppShell.tsx` (`client/src/components/AppShell.tsx`) — it is only a sidebar/outlet/tab-bar shell with no header row. AC-2's "content header top-right" placement is Dashboard-page-scoped for this story; do not add a cross-cutting header to `AppShell.tsx` (out of scope, and would violate VSA slice isolation — the compact CTA belongs to the dashboard/readings slices, not the shell).

### Task 2/3/4: Submit-Reading Flow

```typescript
// client/src/features/readings/api/readingApi.ts
import { apiClient } from '@/lib/apiClient'

export type SubmitReadingRequest = { kwhValue: number; readingDate: string }
export type ReadingResponse = {
  readingId: string
  kwhValue: number
  readingDate: string
  isCorrected: boolean
  originalKwhValue: number | null
}

export const submitReading = (flatId: string, body: SubmitReadingRequest) =>
  apiClient.post<ReadingResponse>(`/flats/${flatId}/readings`, body)
```

```typescript
// client/src/features/readings/hooks/useSubmitReading.ts
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { submitReading } from '@/features/readings/api/readingApi'

export function useSubmitReading(flatId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (body: { kwhValue: number; readingDate: string }) => {
      if (!flatId) throw new Error('flatId is required')
      return submitReading(flatId, body)
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['dashboard', flatId] })
    },
  })
}
```

- No optimistic update (`onMutate`) — the project rule is "never optimistically update the cache unless the story spec explicitly calls for it," and this story doesn't.
- `await` the invalidation in `onSuccess` before the caller closes the sheet (see Task 6) — matches the established project rule (`usePatchFlat.ts` does the same for its own invalidation, just via a different mechanism).
- This codebase does **not** use `z.coerce.number()` for numeric text inputs (see `FlatBaselineEdit.tsx`, `OnboardingContract.tsx`) — it keeps the field as `z.string()` in the form schema and parses with `parseLocaleNumber(value, i18n.language)` from `@/lib/localeNumber` at submit time, because inputs must accept locale-formatted decimals (e.g. `12,4` in `de-DE`). Follow this exact pattern for the kWh field — do not introduce `z.coerce.number()`, which would reject valid German decimal input and depart from an established, deliberate codebase convention.

### Task 6: `EnterReadingSheet.tsx`

```typescript
// client/src/features/readings/components/EnterReadingSheet.tsx
import { useRef, useState } from 'react'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import { parseLocaleNumber } from '@/lib/localeNumber'
import { Sheet, SheetContent } from '@/components/ui/sheet'
import { useSubmitReading } from '@/features/readings/hooks/useSubmitReading'
import { readingSheetSchema, type ReadingSheetFormValues } from '@/features/readings/schemas/readingSchema'

type Props = {
  flatId: string | undefined
  lastKwhValue: number | null
  open: boolean
  onOpenChange: (open: boolean) => void
}

function toDatetimeLocal(date: Date) {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`
}

export function EnterReadingSheet({ flatId, lastKwhValue, open, onOpenChange }: Props) {
  const { t } = useTranslation('readings')
  const { mutate, isPending, isError } = useSubmitReading(flatId)
  const [submitError, setSubmitError] = useState(false)
  const inputRef = useRef<HTMLInputElement | null>(null)

  const { control, handleSubmit, watch, reset } = useForm<ReadingSheetFormValues>({
    resolver: zodResolver(readingSheetSchema),
    defaultValues: { kwhValue: '', readingDate: toDatetimeLocal(new Date()) },
  })

  const kwhRaw = watch('kwhValue') ?? ''
  const kwhParsed = parseLocaleNumber(kwhRaw, i18n.language)
  const isLower = !isNaN(kwhParsed) && lastKwhValue !== null && kwhParsed < lastKwhValue
  const isSaveEnabled = !isNaN(kwhParsed) && kwhParsed > 0 && !isPending

  const onSubmit = (data: ReadingSheetFormValues) => {
    const parsed = parseLocaleNumber(data.kwhValue, i18n.language)
    if (isNaN(parsed) || parsed <= 0) return
    setSubmitError(false)
    mutate(
      { kwhValue: parsed, readingDate: new Date(data.readingDate).toISOString() },
      {
        onSuccess: () => {
          onOpenChange(false)
          reset({ kwhValue: '', readingDate: toDatetimeLocal(new Date()) })
        },
        onError: () => setSubmitError(true),
      }
    )
  }

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent
        side="bottom"
        onOpenAutoFocus={event => {
          event.preventDefault()
          inputRef.current?.focus()
        }}
        className="rounded-t-[24px] border-t border-white/[0.14] bg-[rgba(10,15,25,0.92)] backdrop-blur-[20px] backdrop-saturate-[1.8] px-6 pb-8 pt-3"
      >
        <div aria-hidden="true" className="mx-auto mb-4 h-1 w-9 rounded-full bg-white/25" />
        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col gap-3">
          <Controller
            control={control}
            name="kwhValue"
            render={({ field }) => (
              <input
                {...field}
                ref={el => {
                  field.ref(el)
                  inputRef.current = el
                }}
                type="text"
                inputMode="numeric"
                placeholder="0"
                className="h-14 w-full rounded-input border border-white/15 bg-white/[0.08] px-4 text-2xl text-text-primary outline-none focus:border-white/60"
              />
            )}
          />
          {isLower && (
            <p role="status" aria-live="polite" className="text-body-sm text-accent-over-budget">
              {t('sheet.lowerWarning', { value: lastKwhValue })}
            </p>
          )}
          <Controller
            control={control}
            name="readingDate"
            render={({ field }) => (
              <input
                {...field}
                type="datetime-local"
                style={{ colorScheme: 'dark' }}
                className="h-12 w-full rounded-input border border-white/15 bg-white/[0.08] px-4 text-text-primary outline-none"
              />
            )}
          />
          <p className="text-caption text-text-tertiary">{t('sheet.dateHint')}</p>
          {(submitError || isError) && (
            <p role="alert" aria-live="polite" className="text-body-sm text-accent-error">
              {t('sheet.saveError')}
            </p>
          )}
          <button
            type="submit"
            disabled={!isSaveEnabled}
            className="mt-2 h-14 w-full rounded-pill border border-white/40 bg-white/[0.12] text-body text-text-primary disabled:opacity-40"
          >
            {t('sheet.saveButton')}
          </button>
        </form>
      </SheetContent>
    </Sheet>
  )
}
```

- The `<div aria-hidden="true" ... />` bar at the top is the **visual** drag handle required by AC-3/EXPERIENCE.md. Actual swipe-to-dismiss physics require the `vaul` library (shadcn's separate "Drawer" component), which is not installed and is not required by any AC in this story — dismissal works via Radix's built-in tap-outside / Escape-key handling on the generated `Sheet`. Do not add `vaul` for this story; if true swipe gesture dismissal is wanted later, that's a separate scope decision.
- `onOpenAutoFocus` + manual `inputRef.current?.focus()` is necessary because Radix Dialog's default auto-focus behavior would otherwise focus the sheet's own close button, not the kWh input — AC-3 requires the kWh input specifically to be auto-focused.
- Focus trap and "closing returns focus to the CTA" (AC-7) are both handled automatically by Radix Dialog (which `Sheet` wraps) as long as the trigger button is rendered via `SheetTrigger asChild` — no extra code needed for that part.
- `role="status"`/`role="alert"` + `aria-live="polite"` on both the low-value warning and the save-error message satisfies AC-7's validation-message requirement.

### Task 7: `EnterReadingCta.tsx`

```typescript
// client/src/features/readings/components/EnterReadingCta.tsx
import { useState } from 'react'
import { Zap } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Sheet, SheetTrigger } from '@/components/ui/sheet'
import { EnterReadingSheet } from './EnterReadingSheet'

type Props = { flatId: string | undefined; lastKwhValue: number | null }

export function EnterReadingCta({ flatId, lastKwhValue }: Props) {
  const { t } = useTranslation('readings')
  const [open, setOpen] = useState(false)

  return (
    <>
      <Sheet open={open} onOpenChange={setOpen}>
        <SheetTrigger asChild>
          <button
            type="button"
            className="hidden md:flex h-11 w-11 items-center justify-center rounded-[14px] border border-white/40 bg-white/[0.10] backdrop-blur-[20px] backdrop-saturate-[1.8]"
            aria-label={t('sheet.ctaLabel')}
          >
            <Zap size={20} className="text-text-primary" />
          </button>
        </SheetTrigger>
      </Sheet>
      <Sheet open={open} onOpenChange={setOpen}>
        <SheetTrigger asChild>
          <button
            type="button"
            className="md:hidden w-full h-14 rounded-pill border-[1.5px] border-white/40 bg-white/[0.10] backdrop-blur-[20px] backdrop-saturate-[1.8] text-body text-text-primary"
          >
            {t('sheet.ctaLabel')}
          </button>
        </SheetTrigger>
      </Sheet>
      <EnterReadingSheet flatId={flatId} lastKwhValue={lastKwhValue} open={open} onOpenChange={setOpen} />
    </>
  )
}
```

- Two `<Sheet>` roots sharing the same lifted `open`/`setOpen` state is deliberate — Radix's `Dialog.Root` only renders one trigger; using two independent `Sheet` wrappers (each contributing only a responsive-hidden trigger) lets both the phone and tablet trigger markup exist in the DOM simultaneously (so CSS `hidden md:flex` / `md:hidden` can switch between them without JS media-query logic), while `EnterReadingSheet` renders the actual sheet content once, controlled by the same boolean.
- Verify this dual-root pattern doesn't cause duplicate `aria-label`/focus-trap issues in testing (Task 12) — if it does, an acceptable fallback is a single `Sheet` with one trigger whose inner button content switches via CSS (icon hidden on phone via `md:inline`/`hidden`, text hidden the opposite way), which avoids two Dialog roots entirely. Prefer that simpler single-root approach if the dual-root version causes any test flakiness or a11y-tree duplication — this is a case where the "good enough" version should be replaced if it complicates the accessible tree.

### Task 8: `useAnimatedNumber.ts`

```typescript
// client/src/features/dashboard/hooks/useAnimatedNumber.ts
import { useEffect, useRef, useState } from 'react'

export function useAnimatedNumber(target: number, trigger: unknown, durationMs = 700) {
  const [value, setValue] = useState(target)
  const fromRef = useRef(target)

  useEffect(() => {
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
    if (reduceMotion) {
      setValue(target)
      fromRef.current = target
      return
    }
    const from = fromRef.current
    if (from === target) return
    const start = performance.now()
    let raf = 0
    const step = (now: number) => {
      const progress = Math.min(1, (now - start) / durationMs)
      setValue(from + (target - from) * progress)
      if (progress < 1) raf = requestAnimationFrame(step)
      else fromRef.current = target
    }
    raf = requestAnimationFrame(step)
    return () => cancelAnimationFrame(raf)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trigger])

  return value
}
```

- `trigger` is a separate dependency (not `target`) so the animation only fires when the caller explicitly bumps it (e.g. an incrementing counter set in `useSubmitReading`'s `onSuccess`) — this is what scopes the animation to "just submitted a reading," not every background refetch (window refocus, etc.), matching AC-4's intent literally ("when the mutation succeeds ... tiles animate") rather than animating on every dashboard refetch.
- Reduced-motion check reads `window.matchMedia` directly at effect-run time (no stored media-query listener) — sufficient here since the preference doesn't need to be reactive mid-animation for this use case.

### Task 9: Wiring

`DashboardPage.tsx` needs an `animateTick` counter (e.g. `useState(0)`) incremented inside `useSubmitReading`'s success path (thread it in as a callback prop, or lift `useSubmitReading` itself up to `DashboardPage` and pass `mutate`/state down to `EnterReadingSheet` as props instead of calling the hook inside the sheet — pick whichever keeps `EnterReadingCta`/`EnterReadingSheet` free of extra plumbing; both are acceptable, prefer whichever produces less prop-drilling once the actual component tree is finalized). `DashboardGrid` then receives `animateTick` and internally calls `useAnimatedNumber(dashboard.dailyAvgKwh, animateTick)` etc. for each of the four headline numeric values feeding into the existing `formatKwh`/`formatCurrency` helpers — do not change what's displayed, only animate the numeric input to those existing formatters.

### Task 11: Translation Keys

**`client/src/locales/en-US/readings.json`:**
```json
{
  "sheet": {
    "ctaLabel": "Enter Reading",
    "dateHint": "Date and time will be saved with your reading.",
    "lowerWarning": "Lower than your last reading ({{value}} kWh) — is this correct?",
    "saveButton": "Save",
    "saveError": "Couldn't save — try again."
  }
}
```

**`client/src/locales/de-DE/readings.json`** (German equivalents, keep the em dash and punctuation style consistent with existing `dashboard.json`):
```json
{
  "sheet": {
    "ctaLabel": "Zählerstand erfassen",
    "dateHint": "Datum und Uhrzeit werden mit deiner Ablesung gespeichert.",
    "lowerWarning": "Niedriger als deine letzte Ablesung ({{value}} kWh) — ist das richtig?",
    "saveButton": "Speichern",
    "saveError": "Konnte nicht gespeichert werden — versuch's noch mal."
  }
}
```

### Task 12: Test Guidance

Follow the pattern from `client/src/features/dashboard/__tests__/DashboardGrid.test.tsx` (mock hooks, render component directly with prop fixtures; query by role/label/text).

**`EnterReadingSheet.test.tsx`** (minimum 6):
1. Renders with kWh input auto-focused and `readingDate` pre-filled with a current-looking timestamp
2. Save button disabled when kWh field is empty/zero
3. Save button enabled once a positive kWh value is typed
4. Typing a value below `lastKwhValue` shows the lower-than-last warning; Save remains enabled
5. On mutation success: `onOpenChange(false)` is called (mock `useSubmitReading`/the mutation)
6. On mutation error: sheet stays open (`onOpenChange` not called with `false`), typed value is still in the input, error text is shown

**`useSubmitReading.test.ts`** (minimum 2):
1. On success, invalidates `['dashboard', flatId]`
2. Mutation function throws/rejects when `flatId` is undefined (mirrors the `enabled`-guard pattern from `useDashboard`, but as a mutation there's no `enabled` flag — guard inside `mutationFn`)

**`useAnimatedNumber.test.ts`** (minimum 2):
1. With `prefers-reduced-motion: reduce` mocked true, value jumps straight to `target` with no intermediate frames
2. Advancing fake timers/`requestAnimationFrame` under normal conditions eventually settles on `target`

### Architecture Compliance Checklist

- [ ] `import type` for all type-only imports (TS6 strict module mode)
- [ ] No barrel files — import directly from the declaring file
- [ ] `@/` alias for all imports — never relative paths (this bit Story 3.3 in review; get it right the first time)
- [ ] TanStack Query v5: `isPending` not `isLoading` for mutation pending state
- [ ] No `!` non-null assertions in feature code
- [ ] `mode: 'onBlur'` is the project default for `useForm`, but this codebase's existing numeric-input forms (`FlatBaselineEdit`, `OnboardingContract`) don't set an explicit `mode` / use `onTouched` — match whichever existing convention keeps error display consistent with sibling forms; don't introduce a third variant
- [ ] All user-visible strings via `useTranslation('readings')` — no hardcoded English/German strings in JSX
- [ ] Never hand-edit `client/src/components/ui/sheet.tsx` after generation — style via wrapper component `className` only
- [ ] All currency/number formatting stays inside `DashboardGrid.tsx`'s existing `Intl.NumberFormat`-based helpers — do not introduce a second formatting path for the animated values
- [ ] `decimal`-equivalent care on the backend: `LastKwhValue` is `decimal?` in C#, never `double`/`float`

### Previous Story Intelligence (Story 3.3)

- `useUserSettings()` shape is `{ settings, isLoading, isError }`, not `{ data }` — confirmed again by reading the current `DashboardPage.tsx`.
- The project enforces `@/` alias imports strictly; Story 3.3's review pass had to fix multiple relative imports after the fact — get this right in the first pass this time.
- shadcn CLI (`npx shadcn@latest add <component>`) has previously misresolved the `@/` alias and written to a literal `client/@/components/ui/...` path — check for this and move the file if it happens again.
- Generated shadcn primitives in this project intentionally keep stock (non-visually-functional) default classes; all real theming happens in a feature-folder wrapper component. Don't try to "fix" the generated primitive to look right on its own.
- Story 3.3 explicitly deferred the Enter Reading CTA to this story — there is no partial implementation of it anywhere to reconcile with.

### Git Intelligence

Recent commits (`8b1c6c1` Story 3.1 backend, `8432120`/`75925fb`/`80604ce` Story 3.2 backend + amendment, `003d8a0` Story 3.3 frontend) show the established rhythm: backend-first stories add a `Function` + `Models` + `Validator` + `Configuration` under `api/Features/{Feature}/`; frontend stories add `api/`, `components/`, `hooks/` under `client/src/features/{feature}/`, plus locale JSON. This story is unusual in requiring one small backend change (Task 0) inside an otherwise frontend-only story — keep that change minimal and scoped to the one field, don't use it as an opportunity to refactor `KpiCalculator`.

### Project Structure — New/Modified Files

```
api/Features/Dashboard/
├── DashboardModels.cs               ← MODIFY (add LastKwhValue)
└── KpiCalculator.cs                 ← MODIFY (populate LastKwhValue ×4 branches)

api.Tests/Features/Dashboard/
├── KpiCalculatorTests.cs            ← MODIFY (assertions)
└── GetDashboardFunctionTests.cs     ← MODIFY (assertions)

client/src/features/readings/
├── api/
│   └── readingApi.ts                ← NEW
├── components/
│   ├── EnterReadingCta.tsx          ← NEW
│   ├── EnterReadingSheet.tsx        ← NEW
│   └── EnterReadingSheet.test.tsx   ← NEW
├── hooks/
│   ├── useSubmitReading.ts          ← NEW
│   └── useSubmitReading.test.ts     ← NEW
└── schemas/
    └── readingSchema.ts             ← NEW

client/src/features/dashboard/
├── api/dashboardApi.ts              ← MODIFY (add lastKwhValue)
├── components/DashboardGrid.tsx     ← MODIFY (animate flag/trigger)
├── hooks/
│   ├── useAnimatedNumber.ts         ← NEW
│   └── useAnimatedNumber.test.ts    ← NEW
└── DashboardPage.tsx                ← MODIFY (render EnterReadingCta, animate wiring)

client/src/components/ui/
└── sheet.tsx                        ← NEW (shadcn-generated, do not hand-edit)

client/src/index.css                 ← MODIFY (add text-body utility)

client/src/locales/en-US/readings.json  ← MODIFY (populate)
client/src/locales/de-DE/readings.json  ← MODIFY (populate)
```

### References

- Epic source: `_bmad-output/planning-artifacts/epics/epic-3-meter-reading-kpi-dashboard-reading-history.md#Story 3.4`
- Design tokens, glass card, Enter Reading Button spec: `_bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/DESIGN.md` (Enter Reading Button section, `radius.sheet`/`radius.pill`, type scale table)
- Interaction/motion spec: `_bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/EXPERIENCE.md` (bottom sheet behavior, reduced-motion note, tablet content-header CTA placement)
- Backend contract: `api/Features/Readings/{SubmitReadingFunction,ReadingModels,ReadingValidator}.cs` (already implemented in Story 3.1 — this story only consumes the endpoint, plus the one `DashboardSummary` field addition)
- Numeric locale parsing: `client/src/lib/localeNumber.ts`
- Form pattern precedent: `client/src/features/settings/components/FlatBaselineEdit.tsx`, `client/src/features/onboarding/components/OnboardingContract.tsx`
- Mutation invalidation precedent: `client/src/features/settings/hooks/usePatchFlat.ts`

## Change Log

- Story created: 2026-07-01 — Enter Reading CTA, bottom sheet, immediate dashboard update; Epic 3 fourth story
- Story implemented: 2026-07-01 — All 14 tasks (0–13) complete; 63 backend + 75 frontend tests pass; build clean; lint clean
- Code review (2026-07-01): 3-layer adversarial review (Blind Hunter, Edge Case Hunter, Acceptance Auditor) found 0 decision-needed, 11 patch, 4 defer. All 11 patches applied — 8 items resolved code issues (including a real AC-4 animation-never-plays bug and an AC-7 dual-Sheet-root aria-controls defect), 3 were doc/token-consistency fixes. 63 backend + 82 frontend tests pass; build/lint clean.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

None — no blocking failures encountered. One implementation detail deviated from the Dev Notes template and is recorded below.

### Completion Notes List

- Task 0: Added `decimal? LastKwhValue` as the final positional parameter of `DashboardSummary`; populated in all four `KpiCalculator.Compute` branches. Added assertions to existing `KpiCalculatorTests`/`GetDashboardFunctionTests` methods (no new test methods, per Dev Notes). `dotnet test api.Tests` — 63/63 pass.
- Task 1: Added `lastKwhValue: number | null` to the frontend `DashboardSummary` type; updated the two existing test fixtures (`DashboardGrid.test.tsx`, `useDashboard.test.ts`) that construct this type as object literals, since TypeScript now requires the field.
- Tasks 2–4: `readingApi.ts`, `readingSchema.ts`, `useSubmitReading.ts` created exactly per Dev Notes templates.
- Task 5: `npx shadcn@latest add sheet` again misresolved the `@/` alias, writing to a stray `client/@/components/ui/sheet.tsx` (as flagged by Story 3.3 intelligence). Moved the generated file to `client/src/components/ui/sheet.tsx` and removed the stray `client/@` directory. File is stock/unedited.
- Tasks 6–7: `EnterReadingSheet.tsx` and `EnterReadingCta.tsx` implemented per Dev Notes templates, with one addition: an optional `onSubmitSuccess` callback prop threaded through both components so `DashboardPage` can bump the animation trigger on successful submit (one of the two "acceptable" wiring approaches the Dev Notes explicitly allowed for Task 9). Verified via a throwaway smoke test (not kept) that the dual-`Sheet`-root pattern in `EnterReadingCta` does not duplicate the accessible `dialog` role or trigger labels — kept the dual-root implementation as specified, no fallback needed.
- Task 8: Implemented `useAnimatedNumber` with a deviation from the literal Dev Notes code sample. The sample's effect depended only on `[trigger]`, which means a `target` change without a `trigger` change (e.g., the very first dashboard load, or any background refetch) would never reach the returned `value` — the hook would display `0` (or whatever the mount-time target was) indefinitely. Fixed by depending on `[target, trigger, durationMs]` and tracking the previous `trigger` in a ref: when `trigger` hasn't changed since the last run, the value snaps directly to `target` (covers initial load and background refetch); when `trigger` has changed, it plays the count animation (covers "just submitted a reading"). Reduced-motion still forces an immediate snap in both cases. This preserves the exact behavior the two required tests check (reduced-motion bypass; settles on target) while fixing the initial-load/refetch staleness bug.
- Task 9: `DashboardPage.tsx` holds `animateTick` state, bumped via `EnterReadingCta`'s `onSubmitSuccess`; `DashboardGrid.tsx` accepts `animateTick` and calls `useAnimatedNumber` for the four headline numeric inputs (`dailyAvgKwh`, `weeklyAvgKwh`, `todayKwh`, `cost.projectedMonthlyCost`), feeding the animated numbers into the existing `formatKwh`/`formatCurrency`/`resolveCostDisplay` helpers unchanged. The Enter Reading CTA is rendered in a `px-4 pt-4 md:flex md:justify-end` wrapper above the grid — full-width on phone (CTA's own `w-full` class), right-aligned in that row on tablet (CTA's own `hidden md:flex` compact button), since no shared content-header component exists (out of scope per Dev Notes).
- Task 10: Added the `text-body` utility to `index.css` next to `text-body-sm`, exactly as specified.
- Task 11: Populated both locale JSON files with the exact key set from Dev Notes; the `readings` namespace was already registered in `i18n.ts`.
- Task 12: Added `EnterReadingSheet.test.tsx` (6 tests), `useSubmitReading.test.ts` (2 tests), `useAnimatedNumber.test.ts` (2 tests) — all passing. Discovered `SheetContent` renders through a Radix `Portal` into `document.body`, outside the RTL `render()` container — queries use `document.querySelector` rather than `container.querySelector` for the two form inputs (which have no accessible label/role in the current design, matching the stock-shadcn-primitive convention). Also discovered `window.matchMedia` is not polyfilled by jsdom in this project's Vitest setup; added a minimal stub to `test-setup.ts` (guarded by `if (!window.matchMedia)`) so any component using `useAnimatedNumber` works under test without every test file needing its own mock — the existing `DashboardGrid.test.tsx` suite depends on this since it now transitively uses the hook.
- Task 13: `dotnet test api.Tests` (63/63), `npm run build` (0 TS errors), `npm test` (75/75), `npm run lint` (exit 0, only pre-existing unrelated warnings in `router.tsx`) all pass.

**Review fix round (2026-07-01) — 11 patches applied:**
- The initial Task 8 fix (see above) was itself still broken: it correctly stopped snapping to a stale value on initial load, but introduced a *different* bug the three-layer review caught (two independent reviewers, corroborated by tracing TanStack Query's actual callback ordering) — the dashboard's refetched data lands in a render *before* the `animateTick` trigger bumps (hook-level `onSuccess` awaits `invalidateQueries` before the per-call `onSuccess` fires), so the old "snap when trigger hasn't changed" logic consumed the animation before the trigger ever arrived. **Real fix:** replaced the `trigger`-diffing design with a shared `armedRef` (`{ current: boolean }`) that the caller sets `true` synchronously the instant the mutation succeeds (via `useSubmitReading`'s new `onSuccessImmediate` parameter, called before the `await invalidateQueries`) and that `useAnimatedNumber` only *reads* (never mutates) when `target` changes. Also fixed a second-order bug this introduced: all four dashboard tiles share one `armedRef`, so if each hook instance consumed it internally, only the first of the four would ever see it as armed — moved consumption to a single `useEffect` in `DashboardGrid` that runs after all four hooks' own effects (same component, declared later, so it always runs last).
- Fixed the dual-`Sheet`-root pattern in `EnterReadingCta`/`EnterReadingSheet` (previously three independent Radix `Dialog.Root` instances sharing one `open` boolean) — confirmed via `@radix-ui/react-dialog`'s compiled source that `DialogTrigger` only sets `aria-controls` to its *own* Root's `contentId`, which never existed in the DOM for the two trigger-only Roots. Restructured to a single shared `<Sheet>`: `EnterReadingCta` now owns the one Root with one responsive trigger button (icon shown via `md:block`/hidden via `hidden`, label the inverse) instead of two separate buttons in two separate Roots; `EnterReadingSheet` no longer wraps its own `<Sheet>`, just returns `<SheetContent>` (relies on the ambient Root from its parent). Updated `EnterReadingSheet.test.tsx` to wrap the component under test in a `<Sheet>` accordingly.
- Fixed AC-1: added `px-6 py-4` (16px/24px) to the CTA button — previously had no horizontal/vertical padding classes at all.
- Fixed AC-3: `EnterReadingSheet` stays mounted across sheet opens/closes (its parent renders it unconditionally), so `useForm`'s `defaultValues` were only ever evaluated once — added a `useEffect` that resets the form (fresh timestamp, empty kWh) whenever `open` transitions to `true`.
- Fixed AC-7: the shadcn stock `Sheet`'s built-in close (X) button rendered at native icon size, well under 44×44pt — added `[&>button]:h-11 [&>button]:w-11 [&>button]:flex [&>button]:items-center [&>button]:justify-center` to `SheetContent`'s `className` (targets the Close button, which is the only bare `<button>` direct child, via styling from the wrapper component only — `ui/sheet.tsx` itself is still untouched).
- Fixed a crash: `new Date(data.readingDate).toISOString()` throws `RangeError` on an invalid/empty date; added an `isNaN(readingDate.getTime())` guard before calling `.toISOString()`.
- Fixed: Save could be tapped while `flatId` was still `undefined` (settings query still loading) — added `flatId !== undefined` to `isSaveEnabled`.
- Fixed: rapid double-click/tap on Save could fire two `mutate()` calls before `isPending` propagates back through a re-render — added a `submittingRef` guard set synchronously in `onSubmit`, cleared in both the per-call `onSuccess`/`onError`.
- Fixed: `EnterReadingSheet` hardcoded `rounded-t-[24px]` instead of consuming the `--radius-sheet` token via `rounded-t-sheet` (both numerically identical, but the token was defined specifically for this).
- Fixed: `readingSheetSchema` was missing `.min(1, 'Required')` on both fields, unlike the cited `FlatBaselineEdit`/`OnboardingContract` precedent.
- Corrected an inaccurate File List note: `class-variance-authority` was already a dependency (from Story 3.3's `popover.tsx`), not newly added by this story — only `@radix-ui/react-dialog` is new.
- Added 8 new/updated tests covering all of the above (reopen-resets-form, flatId-undefined-disables-save, empty-date-blocks-submit, double-click-guard, plus 2 new `useAnimatedNumber` tests for the armed-ref semantics). Full suite: `npm test` 82/82 pass, `npm run build` 0 errors, `npm run lint` exit 0, `dotnet test api.Tests` 63/63 (unaffected, backend not touched in this round).
- Deferred (4, pre-existing/out-of-scope, see `deferred-work.md`): `parseLocaleNumber`'s de-DE `.`-as-decimal and multi-comma mis-parsing; no upper-bound on kWh value; no future-dated-reading guard; unencoded `flatId` in API URL paths (matches existing `dashboardApi.ts` convention).

### File List

**Backend (new/modified):**
- `api/Features/Dashboard/DashboardModels.cs` (MODIFY)
- `api/Features/Dashboard/KpiCalculator.cs` (MODIFY)
- `api.Tests/Features/Dashboard/KpiCalculatorTests.cs` (MODIFY)
- `api.Tests/Features/Dashboard/GetDashboardFunctionTests.cs` (MODIFY)

**Frontend — readings feature (new):**
- `client/src/features/readings/api/readingApi.ts`
- `client/src/features/readings/schemas/readingSchema.ts`
- `client/src/features/readings/hooks/useSubmitReading.ts` (revised in review-fix round — added `onSuccessImmediate` param)
- `client/src/features/readings/hooks/useSubmitReading.test.ts` (revised — 3 tests)
- `client/src/features/readings/components/EnterReadingSheet.tsx` (revised in review-fix round — single-Sheet-root, reopen-reset, guards)
- `client/src/features/readings/components/EnterReadingSheet.test.tsx` (revised — 9 tests)
- `client/src/features/readings/components/EnterReadingCta.tsx` (revised in review-fix round — single shared `Sheet` root)

**Frontend — dashboard feature (new/modified):**
- `client/src/features/dashboard/hooks/useAnimatedNumber.ts` (NEW, revised in review-fix round)
- `client/src/features/dashboard/hooks/useAnimatedNumber.test.ts` (NEW, revised in review-fix round — 4 tests)
- `client/src/features/dashboard/api/dashboardApi.ts` (MODIFY)
- `client/src/features/dashboard/components/DashboardGrid.tsx` (MODIFY)
- `client/src/features/dashboard/DashboardPage.tsx` (MODIFY)
- `client/src/features/dashboard/__tests__/DashboardGrid.test.tsx` (MODIFY — added `lastKwhValue` to fixtures)
- `client/src/features/dashboard/__tests__/useDashboard.test.ts` (MODIFY — added `lastKwhValue` to fixture)

**Frontend — shared/config (new/modified):**
- `client/src/components/ui/sheet.tsx` (NEW — shadcn-generated, stock/unedited)
- `client/src/index.css` (MODIFY — added `text-body` utility)
- `client/src/locales/en-US/readings.json` (MODIFY — populated)
- `client/src/locales/de-DE/readings.json` (MODIFY — populated)
- `client/src/test-setup.ts` (MODIFY — added `window.matchMedia` stub)
- `client/package.json` / `client/package-lock.json` (MODIFY — `@radix-ui/react-dialog` added by shadcn CLI; `class-variance-authority` was already a dependency from Story 3.3's `popover.tsx`)
