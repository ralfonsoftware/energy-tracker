---
baseline_commit: b705962f2c34db48f7c4898f3bf20a8b9c14f05c
---

# Story 2.2: Onboarding Gate & Intro Screen

Status: done

## Story

As a first-time user,
I want the app to intercept my first visit and show an intro screen before I can reach any main feature,
So that I understand the app's purpose and can start the setup flow.

## Acceptance Criteria

1. **Gate blocks main routes on hasFlat=false** — Given `useUserSettings` (TanStack Query key: `['settings']`) fetches `GET /api/v1/user/settings` on app load, when `hasFlat === false`, then `OnboardingGate.tsx` intercepts navigation to any main route (`/`, `/insights`, `/decomposition`, `/settings`) and redirects to `/onboarding`; the main tab bar / sidebar is not visible during onboarding.

2. **Gate intercepts any direct main route access** — Given an authenticated user with no existing Flat (new user), when any main app route is accessed (`/`, `/insights`, `/decomposition`, `/settings`), then `OnboardingGate.tsx` intercepts the navigation and redirects to `/onboarding`; the main tab bar / sidebar is not visible during onboarding.

3. **Intro screen content** — Given the `/onboarding` route, when the Intro screen renders, then it shows: the app name (`t('intro.title')`), the value proposition copy "Know what your energy costs, every day." (`t('intro.valueProp')`), a locale dropdown in the top-right (`DE ▾` / `EN ▾`), and a "Get Started" CTA button (`t('intro.getStarted')`); no other navigation elements are shown.

4. **Locale dropdown fires immediately and stores server-side** — Given the locale dropdown on the Intro screen, when a locale is selected, then all text on the current screen immediately re-renders in the selected language and `PUT /api/v1/user/settings` stores the override server-side.

5. **No reload or flash on locale change** — Given a locale change is applied during onboarding, when the new locale renders, then all visible UI strings update in the same render cycle — no full-page reload, no flash of untranslated content, no scroll position reset.

6. **Field values preserved on locale switch** — Given the user has entered text in any form field when locale is switched, when locale is applied, then all previously entered field values are preserved exactly; only labels, placeholders, and error messages re-render in the new locale. (In this story there are no form fields on the Intro screen — this AC applies across the full onboarding flow and is preserved by keeping field state in `OnboardingPage.tsx`.)

7. **Layout holds with longer German strings** — Given the new locale introduces longer strings (e.g. German labels), when the layout reflows, then no CTA button is pushed off-screen and no input overlaps its label.

8. **Completed-onboarding user redirected from /onboarding** — Given a user who has already completed onboarding (`hasFlat === true`), when they navigate to `/onboarding`, then they are redirected to the Dashboard (`/`) — the gate does not re-trigger.

9. **Step indicator shows current position** — Given a step indicator component, when the onboarding flow is active, then the current step position (Intro / Step 1 / Step 2) is visible; the step indicator is hidden outside the onboarding flow.

## Tasks / Subtasks

- [x] Task 1: Create `OnboardingGate.tsx` layout route component (AC: 1, 2)
  - [x] `client/src/features/onboarding/components/OnboardingGate.tsx`: reads `useUserSettings()`, returns `null` while `isLoading`, returns `<Navigate to="/onboarding" replace />` when `!settings?.hasFlat`, returns `<Outlet />` when `hasFlat === true`
  - [x] Import `useUserSettings` from `@/features/settings/hooks/useUserSettings` (EXISTS from Story 2.1 — do NOT recreate)
  - [x] Import `Navigate` and `Outlet` from `react-router-dom`

- [x] Task 2: Update `router.tsx` to wrap AppShell routes with OnboardingGate (AC: 1, 2)
  - [x] Add `OnboardingGate` as a pathless layout route wrapping the existing `AppShell` route; the `/onboarding` route stays at top level unchanged
  - [x] See Dev Notes for exact router structure

- [x] Task 3: Add translation keys for new Intro screen strings (AC: 3, 7)
  - [x] `client/src/locales/en-US/onboarding.json`: add `"intro.tariffHint": "You need your current tariff information to complete setup."` and `"intro.duration": "Takes about 2 minutes"` inside the `"intro"` object
  - [x] `client/src/locales/de-DE/onboarding.json`: add `"intro.tariffHint": "Du benötigst deine aktuellen Tarifangaben für die Einrichtung."` and `"intro.duration": "Dauert etwa 2 Minuten"`
  - [x] Existing keys (`intro.title`, `intro.valueProp`, `intro.getStarted`, `locale.de`, `locale.en`, `locale.label`, `steps.step1`, `steps.step2`) are already present from Story 2.1 — do NOT remove or rename them

- [x] Task 4: Create `OnboardingIntro.tsx` (AC: 3, 4, 5, 7)
  - [x] `client/src/features/onboarding/components/OnboardingIntro.tsx`: renders the full intro screen UI (app icon, name, value prop, locale dropdown, info note, CTA)
  - [x] Use `useUpdateLocale` from `@/features/settings/hooks/useUpdateLocale` (EXISTS from Story 2.1) to call `mutate(localeValue)` on locale selection
  - [x] Use `useTranslation('onboarding')` for all strings
  - [x] Props: `{ onGetStarted: () => void }` — parent calls this when advancing to Step 1
  - [x] See Dev Notes for complete implementation pattern

- [x] Task 5: Update `OnboardingPage.tsx` (AC: 3, 8, 9)
  - [x] Replace the existing stub (`return <div>Onboarding</div>`) with the full implementation
  - [x] Add redirect guard: `if (!isLoading && settings?.hasFlat) return <Navigate to="/" replace />`
  - [x] Add step state: `const [step, setStep] = useState<OnboardingStep>('intro')` where type is `'intro' | 'flat-name' | 'contract'`
  - [x] Render step indicator (3 dots) above the current step component
  - [x] Render `<OnboardingIntro onGetStarted={() => setStep('flat-name')} />` for step `'intro'`
  - [x] Steps `'flat-name'` and `'contract'` render nothing yet (wired in Stories 2.3 and 2.4)
  - [x] See Dev Notes for complete implementation pattern including `StepIndicator` sub-component

- [x] Task 6: Write frontend tests (AC: 1, 2, 3, 4, 8)
  - [x] `client/src/features/onboarding/components/OnboardingGate.test.tsx`: (a) `isLoading=true` → renders null, no children; (b) `hasFlat=false` → redirects to `/onboarding` route; (c) `hasFlat=true` → renders children via Outlet
  - [x] `client/src/features/onboarding/components/OnboardingIntro.test.tsx`: (a) renders app name, value prop, CTA button; (b) clicking a locale option calls `mutate` with correct locale string; (c) clicking CTA calls `onGetStarted`

- [x] Task 7: Final verification (AC: 1–9)
  - [x] `npm run build` in `client/` exits 0 with zero TypeScript errors
  - [x] `npm test` in `client/` passes all tests including existing 5
  - [x] `npm run lint` exits 0 (pre-existing router.tsx fast-refresh warnings only)
  - [x] Update File List

## Dev Notes

### What Already Exists and MUST NOT Be Broken

- `client/src/features/settings/hooks/useUserSettings.ts` — EXISTS (Story 2.1). Returns `{ settings: UserSettings | undefined, isLoading: boolean, isError: boolean }`. `UserSettings = { locale: string | null, hasFlat: boolean }`. Import path: `@/features/settings/hooks/useUserSettings`. Do NOT recreate.
- `client/src/features/settings/hooks/useUpdateLocale.ts` — EXISTS (Story 2.1). `useMutation` calling `PUT /api/v1/user/settings`. On success calls `i18n.changeLanguage(data.locale)` (server-confirmed locale). `const { mutate } = useUpdateLocale(); mutate('de-DE')`. Do NOT recreate.
- `client/src/features/settings/api/settingsApi.ts` — EXISTS. `UserSettings` type exported from here. Do NOT touch.
- `client/src/locales/{de-DE,en-US}/onboarding.json` — Keys `intro.title`, `intro.valueProp`, `intro.getStarted`, `locale.de`, `locale.en`, `locale.label`, `steps.step1`, `steps.step2` ALREADY EXIST (Story 2.1). Task 3 ADDS `intro.tariffHint` and `intro.duration` inside the existing `"intro"` object — do NOT restructure the file.
- `client/src/features/onboarding/OnboardingPage.tsx` — EXISTS as a stub (`return <div>Onboarding</div>`). Task 5 replaces entirely.
- `client/src/App.tsx` — DO NOT modify. `LocaleSync` component already syncs server locale on load.
- `client/src/router.tsx` — EXISTS. Task 2 modifies structure. The `/onboarding` route (no AppShell wrapper) must stay outside the OnboardingGate wrapper.
- `client/src/components/AppShell.tsx` — DO NOT modify. `OnboardingGate` is placed in the router above it; AppShell itself is unchanged.
- All 5 existing tests (BottomTabBar + SidebarNav) must continue to pass — no changes to those components.

### OnboardingGate Implementation (Task 1)

```tsx
// client/src/features/onboarding/components/OnboardingGate.tsx
import { Navigate, Outlet } from 'react-router-dom'
import { useUserSettings } from '@/features/settings/hooks/useUserSettings'

export function OnboardingGate() {
  const { settings, isLoading } = useUserSettings()
  if (isLoading) return null
  if (!settings?.hasFlat) return <Navigate to="/onboarding" replace />
  return <Outlet />
}
```

**Why `null` while loading:** `useUserSettings` shares the TanStack Query cache with `LocaleSync` in `App.tsx` (same `['settings']` key). After the first network response, the cache is warm — `isLoading` will be `false` immediately on subsequent mounts. On a cold first render, `null` prevents a tab bar flash before the redirect fires. Never implement a separate `isLoading` boolean alongside TanStack Query state (architecture rule: "TanStack Query signals only").

**Why `replace` on Navigate:** Prevents the user from pressing browser Back to return to a main route after the gate blocks them — the blocked URL is replaced in history rather than stacked.

### Router Structure Change (Task 2)

**Before (current `router.tsx`):**
```tsx
export const router = createBrowserRouter([
  {
    element: <AppShell />,
    children: [
      { path: '/', element: <Wrap Page={DashboardPage} /> },
      { path: '/insights', element: <Wrap Page={InsightsPage} /> },
      { path: '/decomposition', element: <Wrap Page={DecompositionPage} /> },
      { path: '/settings/*', element: <Wrap Page={SettingsPage} /> },
    ],
  },
  { path: '/onboarding', element: <Wrap Page={OnboardingPage} /> },
])
```

**After (Task 2 changes):**
```tsx
import { OnboardingGate } from '@/features/onboarding/components/OnboardingGate'

export const router = createBrowserRouter([
  {
    element: <OnboardingGate />,        // NEW pathless layout route
    children: [
      {
        element: <AppShell />,          // unchanged, now nested one level deeper
        children: [
          { path: '/', element: <Wrap Page={DashboardPage} /> },
          { path: '/insights', element: <Wrap Page={InsightsPage} /> },
          { path: '/decomposition', element: <Wrap Page={DecompositionPage} /> },
          { path: '/settings/*', element: <Wrap Page={SettingsPage} /> },
        ],
      },
    ],
  },
  { path: '/onboarding', element: <Wrap Page={OnboardingPage} /> },  // unchanged
])
```

`OnboardingGate` has no `path` — it is a pathless layout route. It wraps only the `AppShell` subtree. The `/onboarding` route stays at the top level and is NOT gated (a gated `/onboarding` would cause an infinite redirect loop).

**The tab bar / sidebar is never rendered during onboarding:** Because `AppShell` is entirely inside `OnboardingGate`, and `OnboardingGate` returns `null` or `<Navigate>` before `<Outlet />` renders — the entire `AppShell` subtree (including `BottomTabBar` and `SidebarNav`) is never mounted when `hasFlat === false`.

### Translation File Format (Task 3)

`onboarding.json` after Task 3 (both locales, showing only the `"intro"` object; other keys unchanged):

```json
{
  "intro": {
    "title": "Energy Tracker",
    "valueProp": "Know what your energy costs, every day.",
    "getStarted": "Get Started",
    "tariffHint": "You need your current tariff information to complete setup.",
    "duration": "Takes about 2 minutes"
  },
  "locale": { "de": "DE", "en": "EN", "label": "Language" },
  "steps": { "step1": "Flat Name", "step2": "Energy Contract" }
}
```

German (`de-DE/onboarding.json`) `"intro"` object:
```json
"intro": {
  "title": "Energy Tracker",
  "valueProp": "Weißt du immer, was deine Energie kostet.",
  "getStarted": "Loslegen",
  "tariffHint": "Du benötigst deine aktuellen Tarifangaben für die Einrichtung.",
  "duration": "Dauert etwa 2 Minuten"
}
```

### OnboardingIntro Implementation (Task 4)

UX reference: `_bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/mockups/onboarding-flow.html` — "PHONE 1 — INTRO" section. Screen background: `#0f1235` (deep indigo-navy). All values below come from the mockup.

```tsx
// client/src/features/onboarding/components/OnboardingIntro.tsx
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import i18n from '@/lib/i18n'
import { useUpdateLocale } from '@/features/settings/hooks/useUpdateLocale'

interface OnboardingIntroProps {
  onGetStarted: () => void
}

export function OnboardingIntro({ onGetStarted }: OnboardingIntroProps) {
  const { t } = useTranslation('onboarding')
  const [isLocaleOpen, setIsLocaleOpen] = useState(false)
  const { mutate: updateLocale } = useUpdateLocale()

  const locales = [
    { value: 'de-DE', label: t('locale.de') },
    { value: 'en-US', label: t('locale.en') },
  ] as const

  const currentLabel = i18n.language === 'de-DE' ? t('locale.de') : t('locale.en')

  return (
    <div className="relative min-h-screen flex flex-col" style={{ background: '#0f1235' }}>

      {/* Locale pill dropdown — absolute top-right */}
      <div className="absolute top-4 right-4 z-20">
        <button
          className="px-3 py-1.5 rounded-full text-sm text-white/80 bg-white/10 border border-white/20"
          onClick={() => setIsLocaleOpen(v => !v)}
          aria-label={t('locale.label')}
          aria-expanded={isLocaleOpen}
        >
          {currentLabel} ▾
        </button>
        {isLocaleOpen && (
          <div className="absolute right-0 mt-1 min-w-[80px] bg-white/10 backdrop-blur border border-white/20 rounded-xl overflow-hidden">
            {locales.map(({ value, label }) => (
              <button
                key={value}
                className="block w-full px-4 py-2 text-sm text-left text-white/80 hover:bg-white/10"
                onClick={() => {
                  updateLocale(value)
                  setIsLocaleOpen(false)
                }}
              >
                {label}
              </button>
            ))}
          </div>
        )}
      </div>

      {/* Main content column */}
      <div className="flex-1 flex flex-col items-center px-6 pb-10">

        {/* App icon + name + tagline */}
        <div className="mt-20 flex flex-col items-center gap-6">
          <div
            className="w-20 h-20 rounded-full bg-white/10 border border-white/[0.18] flex items-center justify-center"
            style={{ boxShadow: '0 8px 32px rgba(99,102,241,0.30)' }}
          >
            <svg width="28" height="28" viewBox="0 0 24 24" fill="none"
                 stroke="white" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2" />
            </svg>
          </div>
          <div className="flex flex-col items-center gap-3">
            <span className="text-[28px] font-semibold text-white tracking-tight">
              {t('intro.title')}
            </span>
            <p className="text-lg text-white/70 text-center max-w-[280px] leading-snug">
              {t('intro.valueProp')}
            </p>
          </div>
        </div>

        {/* Push bottom content down */}
        <div className="flex-1" />

        {/* Info note card */}
        <div className="w-full bg-white/[0.08] border border-white/15 rounded-2xl p-4 flex gap-2.5 items-start mb-7">
          <span className="text-base flex-shrink-0 mt-px" aria-hidden="true">⚡</span>
          <span className="text-sm text-white/50 leading-relaxed">
            {t('intro.tariffHint')}
          </span>
        </div>

        {/* CTA button + duration hint */}
        <div className="w-full flex flex-col items-center gap-3">
          <button
            onClick={onGetStarted}
            className="w-full h-14 rounded-full bg-white/[0.12] border border-white/40 text-white text-[17px] font-semibold"
            style={{ boxShadow: '0 0 0 1px rgba(255,255,255,0.08) inset, 0 4px 24px rgba(99,102,241,0.25)' }}
          >
            {t('intro.getStarted')}
          </button>
          <span className="text-xs text-white/30">{t('intro.duration')}</span>
        </div>

      </div>
    </div>
  )
}
```

**Locale dropdown custom implementation (not native `<select>`):** The pill styling (`border-radius: 20px`, glass background, custom arrow) cannot be achieved with a native `<select>`. A custom `isLocaleOpen` boolean drives a small popover list. No external dropdown library needed — two options, simple state.

**`i18n.language` for current locale display:** `i18n` from `@/lib/i18n` (the same instance used throughout the app) exposes the currently active language. This is always up-to-date because `useUpdateLocale.onSuccess` calls `i18n.changeLanguage(data.locale)`, which triggers a re-render of any component reading `i18n.language`.

**No `useTranslation` re-render needed for locale display:** Because `useUpdateLocale` calls `i18n.changeLanguage()` on success, react-i18next re-renders all components using `useTranslation` automatically. The `currentLabel` line re-evaluates on re-render.

### OnboardingPage Implementation (Task 5)

```tsx
// client/src/features/onboarding/OnboardingPage.tsx
import { useState } from 'react'
import { Navigate } from 'react-router-dom'
import { useUserSettings } from '@/features/settings/hooks/useUserSettings'
import { OnboardingIntro } from './components/OnboardingIntro'

type OnboardingStep = 'intro' | 'flat-name' | 'contract'

const STEPS: OnboardingStep[] = ['intro', 'flat-name', 'contract']

function StepIndicator({ currentStep }: { currentStep: OnboardingStep }) {
  const currentIndex = STEPS.indexOf(currentStep)
  return (
    <div
      className="flex items-center justify-center gap-2 pt-4"
      role="status"
      aria-label={`Setup progress: step ${currentIndex + 1} of ${STEPS.length}`}
    >
      {STEPS.map((_, i) => (
        <span
          key={i}
          className={`w-2 h-2 rounded-full ${i <= currentIndex ? 'bg-white' : 'bg-white/25'}`}
        />
      ))}
    </div>
  )
}

export default function OnboardingPage() {
  const { settings, isLoading } = useUserSettings()
  const [step, setStep] = useState<OnboardingStep>('intro')

  // Redirect already-onboarded users away from /onboarding
  if (!isLoading && settings?.hasFlat) return <Navigate to="/" replace />

  return (
    <div style={{ background: '#0f1235', minHeight: '100vh' }}>
      <StepIndicator currentStep={step} />
      {step === 'intro' && (
        <OnboardingIntro onGetStarted={() => setStep('flat-name')} />
      )}
      {/* 'flat-name' rendered in Story 2.3; 'contract' rendered in Story 2.4 */}
    </div>
  )
}
```

**Why `OnboardingPage` also checks `hasFlat`:** `OnboardingGate` only wraps the main routes (`/`, `/insights`, etc.). A user who manually types `/onboarding` in the browser after completing onboarding would not be caught by `OnboardingGate`. The guard in `OnboardingPage` handles this case.

**Why `isLoading` check before redirect:** Without it, on first render (cache cold), `settings` is `undefined` — `settings?.hasFlat` would be `undefined` which is falsy, incorrectly leaving the page without a redirect. The `!isLoading &&` guard ensures the check runs only after data is confirmed.

**Step state and future stories:** Stories 2.3 and 2.4 will wire in `OnboardingFlatName` and `OnboardingContract` components for `'flat-name'` and `'contract'` steps. The `setStep` callback is passed down through `onGetStarted` → Step 1 "Continue" → Step 2 "Complete Setup". All onboarding wizard state lives in `OnboardingPage` (Story Note from 2.4: "do not rely on browser history state alone" — all values are held in component state). Stories 2.3/2.4 will add additional state fields (flat name string, kWh value, tariff fields) to `OnboardingPage` alongside `step`.

**`StepIndicator` is defined inline in `OnboardingPage.tsx`:** It is small (< 15 lines), used in only one place, and calling it from a separate file would add complexity without benefit. Stories 2.3 and 2.4 may keep it inline or extract it if needed.

**Dots logic:** `i <= currentIndex` means a dot is filled (white) if it represents the current or any completed step. At the Intro screen (index 0): dot 0 is filled, dots 1 and 2 are dim. At Step 1 (index 1): dots 0-1 filled, dot 2 dim. At Step 2 (index 2): all filled.

### Test Implementation Patterns (Task 6)

**`OnboardingGate.test.tsx`** — uses `MemoryRouter` + `Routes` to verify navigation behavior:

```tsx
// client/src/features/onboarding/components/OnboardingGate.test.tsx
import { render, screen } from '@testing-library/react'
import { MemoryRouter, Routes, Route, Outlet } from 'react-router-dom'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { OnboardingGate } from './OnboardingGate'

vi.mock('@/features/settings/hooks/useUserSettings')
import { useUserSettings } from '@/features/settings/hooks/useUserSettings'
const mockUseUserSettings = vi.mocked(useUserSettings)

function renderGate(initialPath = '/') {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route element={<OnboardingGate />}>
          <Route path="/" element={<div>Main content</div>} />
        </Route>
        <Route path="/onboarding" element={<div>Onboarding page</div>} />
      </Routes>
    </MemoryRouter>
  )
}

describe('OnboardingGate', () => {
  it('renders nothing while loading', () => {
    mockUseUserSettings.mockReturnValue({ settings: undefined, isLoading: true, isError: false })
    const { container } = renderGate()
    expect(container.firstChild).toBeNull()
  })

  it('redirects to /onboarding when hasFlat is false', () => {
    mockUseUserSettings.mockReturnValue({ settings: { locale: null, hasFlat: false }, isLoading: false, isError: false })
    renderGate()
    expect(screen.getByText('Onboarding page')).toBeInTheDocument()
    expect(screen.queryByText('Main content')).not.toBeInTheDocument()
  })

  it('renders children via Outlet when hasFlat is true', () => {
    mockUseUserSettings.mockReturnValue({ settings: { locale: null, hasFlat: true }, isLoading: false, isError: false })
    renderGate()
    expect(screen.getByText('Main content')).toBeInTheDocument()
  })
})
```

**`OnboardingIntro.test.tsx`** — mocks `useUpdateLocale` and verifies UI + interaction:

```tsx
// client/src/features/onboarding/components/OnboardingIntro.test.tsx
import { render, screen, fireEvent } from '@testing-library/react'
import { vi, describe, it, expect } from 'vitest'
import { OnboardingIntro } from './OnboardingIntro'

vi.mock('@/features/settings/hooks/useUpdateLocale')
import { useUpdateLocale } from '@/features/settings/hooks/useUpdateLocale'
const mockMutate = vi.fn()
vi.mocked(useUpdateLocale).mockReturnValue({ mutate: mockMutate } as any)

describe('OnboardingIntro', () => {
  it('renders app name, value prop, and CTA button', () => {
    render(<OnboardingIntro onGetStarted={() => {}} />)
    expect(screen.getByText('Energy Tracker')).toBeInTheDocument()
    expect(screen.getByText('Know what your energy costs, every day.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Get Started' })).toBeInTheDocument()
  })

  it('calls onGetStarted when CTA button is clicked', () => {
    const onGetStarted = vi.fn()
    render(<OnboardingIntro onGetStarted={onGetStarted} />)
    fireEvent.click(screen.getByRole('button', { name: 'Get Started' }))
    expect(onGetStarted).toHaveBeenCalledTimes(1)
  })

  it('opens locale dropdown and calls updateLocale mutation on selection', () => {
    render(<OnboardingIntro onGetStarted={() => {}} />)
    fireEvent.click(screen.getByRole('button', { name: 'Language' }))
    fireEvent.click(screen.getByText('DE'))
    expect(mockMutate).toHaveBeenCalledWith('de-DE')
  })
})
```

**Mocking pattern:** `vi.mock` with auto-mocking, then `vi.mocked()` for type-safe override. Same pattern used in backend tests from Story 2.1 (Moq). The frontend equivalent is `vi.mock` + `vi.fn()`.

**Note on `useUpdateLocale` mock shape:** `{ mutate: mockMutate } as any` — only `mutate` is used in `OnboardingIntro`; casting to `any` avoids mocking the full `UseMutationResult` type.

### Architecture Compliance Rules for This Story

From the project's 10 enforcement rules (relevant to this story):
1. **No `float`/`double` for energy/monetary values** — N/A (no numeric values in Story 2.2)
2. **TanStack Query signals only for loading states** — `OnboardingGate` uses `isLoading` from `useUserSettings`, not a manual boolean
3. **No Zustand or Redux** — step state lives in `OnboardingPage` via React `useState`
4. **react-hook-form + zod per slice** — N/A (no form in Story 2.2; Intro screen has no inputs)
5. **All UI text through i18n** — all visible strings use `t()` except the `▾` chevron character
6. **Camelcase JSON fields** — N/A (no new API endpoints)
7. **Co-locate zod schemas with feature** — N/A
8. **Locale-neutral storage** — N/A
9. **TanStack Query cache key `[resource, ...]` tuple** — existing `['settings']` key unchanged
10. **No Data Annotation on entities** — N/A (backend unchanged)

### No Backend Changes in This Story

Story 2.2 is **frontend-only**. No new API endpoints, no new EF Core entities, no C# changes. The `POST /api/v1/onboarding` endpoint (that creates the `Flat` + `Tariff` records) is Story 2.4's scope.

### Epic 1 + 2.1 Retrospective Lessons Applied

1. **Don't touch `main.tsx`** — i18n import is already there; do not modify import order
2. **Test before claiming done** — run `npm test` (all tests) before marking story done, not just the new test files
3. **`decimal` invariant** — no numeric DB fields in this story; lesson still noted for vigilance
4. **Review feedback awareness** — Story 2.1 review required injecting `LocaleResolver` into handlers; no equivalent pattern applies to pure frontend

### File Structure for This Story

```
client/src/
├── features/
│   └── onboarding/
│       ├── components/
│       │   ├── OnboardingGate.tsx      ← NEW
│       │   ├── OnboardingGate.test.tsx ← NEW
│       │   ├── OnboardingIntro.tsx     ← NEW
│       │   └── OnboardingIntro.test.tsx ← NEW
│       └── OnboardingPage.tsx          ← MODIFIED (full replacement)
├── locales/
│   ├── de-DE/
│   │   └── onboarding.json             ← MODIFIED (add tariffHint + duration)
│   └── en-US/
│       └── onboarding.json             ← MODIFIED (add tariffHint + duration)
└── router.tsx                          ← MODIFIED (wrap AppShell in OnboardingGate)
```

No backend files. No new npm packages required (React Router's `Navigate` + `Outlet` are already installed; `react-i18next` and `@tanstack/react-query` from Story 2.1).

### References

- Story ACs: [Source: `_bmad-output/planning-artifacts/epics.md` — Story 2.2, lines 429–471]
- UX mockup — Intro screen: [Source: `_bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/mockups/onboarding-flow.html` — PHONE 1 INTRO section]
- Architecture feature folder: [Source: `_bmad-output/planning-artifacts/architecture.md` — `features/onboarding/components/` section lines 571–576]
- Architecture routing: [Source: `_bmad-output/planning-artifacts/architecture.md` — Route tree, line 286–287]
- Architecture state management: [Source: `_bmad-output/planning-artifacts/architecture.md` — AD-16, AD-17]
- Previous story patterns: [Source: `_bmad-output/implementation-artifacts/2-1-i18n-infrastructure-and-locale-settings-api.md` — Dev Notes]
- `useUserSettings` hook: [Source: `client/src/features/settings/hooks/useUserSettings.ts`]
- `useUpdateLocale` hook: [Source: `client/src/features/settings/hooks/useUpdateLocale.ts`]
- `router.tsx` current state: [Source: `client/src/router.tsx`]
- `OnboardingPage.tsx` current stub: [Source: `client/src/features/onboarding/OnboardingPage.tsx`]
- `AppShell.tsx` current state: [Source: `client/src/components/AppShell.tsx`]
- Design tokens: [Source: `client/src/index.css`]
- UX design principles: [Source: `_bmad-output/planning-artifacts/epics.md` — UX-DR20]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

None — implementation followed story spec exactly; no debugging needed.

### Completion Notes List

- Created `OnboardingGate.tsx` as a pathless layout route wrapping the AppShell subtree; returns `null` while loading, redirects to `/onboarding` when `hasFlat === false`, renders `<Outlet />` when `hasFlat === true`. Reuses existing `useUserSettings` hook from Story 2.1.
- Updated `router.tsx` to wrap the AppShell route group with `OnboardingGate`; `/onboarding` remains at top level to avoid infinite redirect loop.
- Added `intro.tariffHint` and `intro.duration` keys to both `en-US/onboarding.json` and `de-DE/onboarding.json`; all pre-existing keys preserved intact.
- Created `OnboardingIntro.tsx` with full Intro screen UI: glass-style locale pill dropdown (custom, not native `<select>`), bolt-icon avatar, app name, value prop, tariff hint card, CTA button + duration hint. Reuses `useUpdateLocale` from Story 2.1 for immediate server-side locale persistence.
- Replaced `OnboardingPage.tsx` stub with full implementation: redirect guard for already-onboarded users, `OnboardingStep` state, inline `StepIndicator` (3-dot progress), renders `OnboardingIntro` for `'intro'` step; `'flat-name'` and `'contract'` steps are empty stubs for Stories 2.3/2.4.
- All 11 tests pass (3 new for OnboardingGate, 3 new for OnboardingIntro, 5 pre-existing BottomTabBar + SidebarNav). TypeScript build exits 0. Lint warnings are pre-existing (router.tsx lazy imports fast-refresh pattern — present before this story).

### File List

- `client/src/features/onboarding/components/OnboardingGate.tsx` (NEW)
- `client/src/features/onboarding/components/OnboardingGate.test.tsx` (NEW)
- `client/src/features/onboarding/components/OnboardingIntro.tsx` (NEW)
- `client/src/features/onboarding/components/OnboardingIntro.test.tsx` (NEW)
- `client/src/features/onboarding/OnboardingPage.tsx` (MODIFIED)
- `client/src/locales/en-US/onboarding.json` (MODIFIED)
- `client/src/locales/de-DE/onboarding.json` (MODIFIED)
- `client/src/router.tsx` (MODIFIED)

### Review Findings

- [x] [Review][Patch] Error state ignored — gate and page redirect/show intro incorrectly on API failure [OnboardingGate.tsx:5-7, OnboardingPage.tsx:32]
- [x] [Review][Patch] Locale change not immediate — `i18n.changeLanguage()` fires only in `onSuccess` after server round-trip, violating AC 4 ("immediately") and AC 5 ("same render cycle") [OnboardingIntro.tsx:41-44]
- [x] [Review][Patch] Locale dropdown stays open on click-outside — no dismiss handler; open dropdown can obscure CTA button [OnboardingIntro.tsx:26-51]
- [x] [Review][Patch] German locale variant detection brittle — `i18n.language === 'de-DE'` misidentifies `'de'`, `'de-AT'`, `'de-CH'`; pill shows EN while app renders German strings [OnboardingIntro.tsx:20]
- [x] [Review][Patch] Locale mutation error silently swallowed — `setIsLocaleOpen(false)` runs unconditionally even on PUT failure; dropdown closes appearing successful [OnboardingIntro.tsx:41-44]
- [x] [Review][Patch] StepIndicator + OnboardingIntro `min-h-screen` pushes CTA below viewport fold — outer page renders ~40px StepIndicator then OnboardingIntro (min-h-screen), total > 100vh; CTA at bottom of OnboardingIntro is off-screen [OnboardingPage.tsx:36-41]
- [x] [Review][Patch] Gate tests only cover `/` — three explicitly gated routes (`/insights`, `/decomposition`, `/settings`) have no redirect test [OnboardingGate.test.tsx]
- [x] [Review][Patch] No test for `hasFlat=true` redirect in OnboardingPage — AC 8 unverified by any test [OnboardingPage]

- [x] [Review][Defer] `Get Started` advances to blank `'flat-name'` step — intentional per spec; wired in Story 2.3 — deferred, pre-existing
- [x] [Review][Defer] No `aria-haspopup` on locale dropdown trigger — real a11y gap; address in UX-polish pass — deferred, pre-existing
- [x] [Review][Defer] `useUpdateLocale` mock set at module scope outside `beforeEach` in `OnboardingIntro.test.tsx` — test smell, low practical risk — deferred, pre-existing
- [x] [Review][Defer] `OnboardingIntro` tests assert against real i18n strings without explicit `react-i18next` mock — potential fragility if test env i18n is misconfigured — deferred, pre-existing

## Change Log

- Story created: 2026-06-29 — Onboarding gate + intro screen; Epic 2 second story
- Story implemented: 2026-06-29 — All 7 tasks complete; 11 tests passing; build clean
