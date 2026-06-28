---
baseline_commit: "15697a12c9a1781bf2fd8eeb166c7252c910eab1"
---

# Story 1.5: App Shell — Design System Tokens, Routing & Navigation

Status: done

## Story

As an authenticated user,
I want to see the app shell with the Euro Burn design system applied globally and functioning tab-bar navigation between the four main sections,
So that the app has its correct visual identity and I can navigate to any section.

## Acceptance Criteria

1. **Gradient + tab bar on phone** — Given an authenticated user loading the app on phone (<768px), when the app shell renders, then the Euro Burn Gradient Background displays as a full-screen `linear-gradient(160deg, ...)` with all 5 color stops at their design-specified hex values behind all content; the Bottom Tab Bar is fixed at the bottom (72px height, `background: rgba(10,15,25,0.75)`, `backdrop-filter: blur(20px) saturate(180%)`, `border-top: 1px solid rgba(255,255,255,0.10)`).

2. **Bottom Tab Bar details** — Given the Bottom Tab Bar on phone, when rendered, then it shows 4 tabs (Dashboard · Insights · Decomposition · Settings) each with a 22×22px icon and micro-text label; the active tab icon is at opacity 1.0 with `text-primary` label; inactive tabs are at opacity 0.4 with `text-tertiary` label; each tab's tap target is minimum 44×44pt.

3. **Tablet sidebar nav** — Given the app shell on tablet (≥768px), when rendered, then the bottom tab bar is replaced by a 200px sidebar nav (`background: rgba(0,0,0,0.25)`, `backdrop-filter: blur(20px) saturate(180%)`, `border-right: 1px solid rgba(255,255,255,0.08)`); the active nav item has `background: rgba(255,255,255,0.12)` and `border-radius: 10px`.

4. **Route loading + lazy-loading** — Given any tab is tapped or clicked, when the route changes, then the corresponding route loads (`/` Dashboard, `/insights` Insights, `/decomposition` Decomposition, `/settings` Settings); each route is lazy-loaded via Vite dynamic import.

5. **CSS design system tokens** — Given the design system tokens, when the CSS is inspected, then all Euro Burn gradient tokens, glass surface tokens (`glass-surface`, `glass-border`, `glass-surface-light`, `glass-border-light`), all 7 semantic accent tokens, and type scale roles (display-kpi, body-sm, label-caps, caption, micro) are defined as Tailwind v4 / CSS custom property tokens globally.

6. **Zero web fonts** — Given the app rendering on any platform, when network requests are inspected, then zero web font files are loaded; the system font stack resolves natively.

7. **Screen reader accessibility** — Given any tab in the bottom tab bar or sidebar, when a screen reader focuses or activates it, then the surface name is announced on both focus and activation.

## Tasks / Subtasks

- [x] Task 1: Set up frontend testing framework (AC: foundational)
  - [x] Install vitest, jsdom, @testing-library/react, @testing-library/user-event as devDependencies in `client/`
  - [x] Add `"test": "vitest"` and `"test:ui": "vitest --ui"` scripts to `client/package.json`
  - [x] Add `test` config block to `client/vite.config.ts`: `{ environment: 'jsdom', globals: true, setupFiles: ['./src/test-setup.ts'] }`
  - [x] Create `client/src/test-setup.ts` with `import '@testing-library/jest-dom'`
  - [x] Install `@testing-library/jest-dom` as devDependency
  - [x] Verify `vitest` runs with zero tests and exits 0

- [x] Task 2: Add CSS design system tokens to `index.css` (AC: 5, 6)
  - [x] Add Tailwind v4 `@theme` block with all Euro Burn gradient color tokens (exact hex values from DESIGN.md)
  - [x] Add glass surface tokens (rgba values) to `@theme`
  - [x] Add all 7 semantic accent tokens + text tokens (text-primary, text-secondary, text-tertiary) to `@theme`
  - [x] Add `border-radius` tokens to `@theme` (pill, sheet, card, input, badge, sidebar-item)
  - [x] Add system font family override to `@theme` (no web fonts loaded — no `@import url()` for fonts)
  - [x] Define type scale roles as `@utility` classes (display-kpi, display-kpi-tablet, body-sm, label-caps, caption, micro) with font-size, font-weight, letter-spacing
  - [x] Verify `npm run build` exits 0 and no font files appear in `dist/assets/`

- [x] Task 3: Create `EuroBurnGradient` component (AC: 1)
  - [x] Create `client/src/components/EuroBurnGradient.tsx`
  - [x] Render a `div` with `position: fixed; inset: 0; z-index: -1` and `aria-hidden="true"`
  - [x] Apply `linear-gradient(160deg, #1a1f4e 0%, #0d4f5c 30%, #2d2018 60%, #4a2000 85%, #6b2d00 100%)` as static background (gradient position is dynamic in later stories when KPI data is available)
  - [x] Accept optional `consumptionPct?: number` prop (unused now; renders at neutral midpoint) for forward compatibility

- [x] Task 4: Create `BottomTabBar` component (AC: 1, 2, 7)
  - [x] Create `client/src/components/BottomTabBar.tsx`
  - [x] Fixed position, bottom 0, full width, height 72px
  - [x] Background: `rgba(10,15,25,0.75)`, `backdrop-filter: blur(20px) saturate(180%)`, `border-top: 1px solid rgba(255,255,255,0.10)`
  - [x] 4 tabs: Dashboard (`House` icon), Insights (`TrendingUp` icon), Decomposition (`BarChart2` icon), Settings (`Settings` icon) — all from `lucide-react`
  - [x] Each tab uses `<NavLink to="...">` from `react-router-dom` for active state detection
  - [x] Active state: icon + label at opacity 1.0 with `text-primary` color (`rgba(255,255,255,0.90)`)
  - [x] Inactive state: icon + label at opacity 0.4 with `text-tertiary` color (`rgba(255,255,255,0.35)`)
  - [x] Icon size: 22×22px (w-[22px] h-[22px])
  - [x] Label: `micro` type role (9px / 500 weight / +0.02em tracking / uppercase)
  - [x] Each tab `<button>` / link has minimum 44×44pt touch target (use `min-h-[44px] min-w-[44px]`)
  - [x] Each tab has `aria-label="Dashboard"` (etc.) for screen reader announcement
  - [x] `role="navigation"` on the nav container with `aria-label="Main navigation"`

- [x] Task 5: Create `SidebarNav` component (AC: 3, 7)
  - [x] Create `client/src/components/SidebarNav.tsx`
  - [x] Width: 200px, full-height flex column
  - [x] Background: `rgba(0,0,0,0.25)`, `backdrop-filter: blur(20px) saturate(180%)`, `border-right: 1px solid rgba(255,255,255,0.08)`
  - [x] Same 4 nav items as BottomTabBar (same icons, same labels) using `<NavLink>`
  - [x] Active item: `background: rgba(255,255,255,0.12)`, `border-radius: 10px` (sidebar-item token)
  - [x] Each nav item has `aria-label` with the surface name
  - [x] `role="navigation"` with `aria-label="Main navigation"` on the nav container
  - [x] Nav items show icon + text label side by side (horizontal layout)

- [x] Task 6: Create `AppShell` layout component (AC: 1, 3, 4)
  - [x] Create `client/src/components/AppShell.tsx`
  - [x] Renders: `<EuroBurnGradient />` + responsive nav + `<Outlet />` (from `react-router-dom`)
  - [x] Phone layout: `<Outlet />` content fills full width; `<BottomTabBar />` fixed bottom (content area has `pb-[84px]` bottom padding to clear the tab bar)
  - [x] Tablet layout (≥768px, `md:` breakpoint): `<SidebarNav />` renders as a fixed 200px left column; content area shifts right with `md:ml-[200px]`
  - [x] BottomTabBar visible only on phone (`md:hidden`)
  - [x] SidebarNav visible only on tablet+ (`hidden md:flex`)
  - [x] Default export `AppShell` (default export, not named, so lazy() import works)

- [x] Task 7: Update `router.tsx` to use AppShell as nested layout (AC: 4)
  - [x] Restructure `createBrowserRouter` to have AppShell as the parent layout route for all main routes
  - [x] Main routes nested under AppShell: `/` (Dashboard), `/insights`, `/decomposition`, `/settings/*`
  - [x] Onboarding route (`/onboarding`) stays flat (no AppShell) — the tab bar / sidebar must NOT be visible during onboarding
  - [x] Keep all existing lazy imports + `<Suspense fallback={null}>` pattern
  - [x] AppShell itself does NOT need lazy loading (it renders synchronously; child pages are lazy)

- [x] Task 8: Create `useAuth` hook (deferred from Story 1.4)
  - [x] Create `client/src/hooks/useAuth.ts`
  - [x] Use `useQuery` from `@tanstack/react-query` to call `getMe()` from `@/lib/authClient`
  - [x] Query key: `['auth', 'me']`; `staleTime: 5 * 60 * 1_000`; `retry: false`
  - [x] Return `{ user: SwaAuthUser | null, isLoading: boolean }`
  - [x] Hook is not yet wired into AppShell (no user display in this story); just create it for future use

- [x] Task 9: Write component tests (AC: 2, 7)
  - [x] Create `client/src/components/BottomTabBar.test.tsx`
  - [x] Test: renders 4 tabs with correct labels (Dashboard, Insights, Decomposition, Settings)
  - [x] Test: each tab link has an accessible name (aria-label present)
  - [x] Test: nav container has `role="navigation"` and `aria-label`
  - [x] Create `client/src/components/SidebarNav.test.tsx`
  - [x] Test: renders 4 nav items with correct accessible names
  - [x] Test: nav container has `role="navigation"` and `aria-label`
  - [x] Run `npm test` — all tests pass, 0 failures

- [x] Task 10: Final verification (AC: 1–7)
  - [x] `npm run build` in `client/` exits 0 with zero TypeScript errors
  - [x] `npm test` in `client/` passes all tests
  - [x] `npm run lint` passes (oxlint)
  - [x] Verify `dist/assets/` contains no `.woff`, `.woff2`, `.ttf`, `.eot` font files (AC: 6)
  - [x] Verify lazy-split JS chunks exist in `dist/assets/` for Dashboard, Insights, Decomposition, Settings, Onboarding pages (AC: 4)
  - [x] Update File List

## Dev Notes

### Current Frontend State (Critical — Read Before Implementing)

**What already exists and MUST NOT be broken:**

- `client/src/router.tsx` — Has `createBrowserRouter` with 5 routes (`/`, `/insights`, `/decomposition`, `/settings/*`, `/onboarding`) using `lazy()` + `Suspense`. Task 7 restructures this; the lazy-loading pattern MUST be preserved.
- `client/src/App.tsx` — Has `QueryClientProvider` + `RouterProvider` + ReactQueryDevtools in DEV. Do NOT add `MsalProvider` — it was removed per architecture correction (AD-9). Do NOT modify `App.tsx` in this story.
- `client/src/main.tsx` — Imports `./lib/i18n` before anything else. This MUST remain the first import. Do NOT modify `main.tsx` in this story.
- `client/src/index.css` — Currently only `@import "tailwindcss"` with a comment. Task 2 adds `@theme` block below that import.
- `client/src/lib/authClient.ts` — Updated in Story 1.4 with `getMe()`, `login()`, `logout()`, `SwaAuthUser` interface. Task 8 creates `useAuth.ts` that wraps it.
- All 5 page components (`DashboardPage`, `InsightsPage`, `DecompositionPage`, `SettingsPage`, `OnboardingPage`) are stub placeholders returning a single `<div>`. Do NOT modify them in this story.

**Package constraints:**
- Tailwind v4 is installed via `@tailwindcss/vite` plugin (NOT `tailwindcss` legacy CLI). No `tailwind.config.js` needed or wanted.
- `lucide-react` v1.21.0 is installed. Use `House`, `TrendingUp`, `BarChart2`, `Settings` icons for the 4 tabs.
- `react-router-dom` v7.18.0 is installed. Use `NavLink` (not `Link`) for tab/nav items to get active state detection.
- No new runtime npm packages required for this story. vitest + testing-library are devDependencies only.
- `shadcn/ui` (components.json exists) — do NOT run `npx shadcn add` for any component in this story. The only shadcn/ui components used are those already generated in `components/ui/`; there are none there yet.

### Tailwind v4 Token Syntax (CRITICAL)

Tailwind v4 uses `@theme` instead of `tailwind.config.js`. Add after the `@import "tailwindcss"` line in `index.css`:

```css
@import "tailwindcss";

@theme {
  /* Euro Burn gradient */
  --color-gradient-cool-start: #1a1f4e;
  --color-gradient-cool-end: #0d4f5c;
  --color-gradient-neutral: #2d2018;
  --color-gradient-warm-start: #4a2000;
  --color-gradient-warm-end: #6b2d00;

  /* Glass surfaces */
  --color-glass-surface: rgba(255, 255, 255, 0.08);
  --color-glass-border: rgba(255, 255, 255, 0.14);
  --color-glass-surface-light: rgba(255, 255, 255, 0.55);
  --color-glass-border-light: rgba(0, 0, 0, 0.08);

  /* Semantic accents */
  --color-accent-spike: #f59e0b;
  --color-accent-under-budget: #4ade80;
  --color-accent-over-budget: #f59e0b;
  --color-accent-info: #60a5fa;
  --color-accent-error: #f87171;
  --color-accent-tariff-locked: #d97706;
  --color-residual-tint: rgba(245, 158, 11, 0.10);

  /* Text palette (dark mode) */
  --color-text-primary: rgba(255, 255, 255, 0.90);
  --color-text-secondary: rgba(255, 255, 255, 0.65);
  --color-text-tertiary: rgba(255, 255, 255, 0.35);

  /* Border radii */
  --radius-pill: 9999px;
  --radius-sheet: 24px;
  --radius-card: 18px;
  --radius-input: 12px;
  --radius-badge: 20px;
  --radius-sidebar-item: 10px;

  /* System font — no web fonts */
  --font-sans: -apple-system, BlinkMacSystemFont, "SF Pro Display", "SF Pro Text",
    "Helvetica Neue", sans-serif;
}

/* Type scale roles */
@utility text-display-kpi {
  font-size: 22px;
  font-weight: 700;
  letter-spacing: -0.02em;
  line-height: 1.2;
}
@utility text-display-kpi-tablet {
  font-size: 20px;
  font-weight: 700;
  letter-spacing: -0.02em;
  line-height: 1.2;
}
@utility text-body-sm {
  font-size: 13px;
  font-weight: 500;
  letter-spacing: 0.01em;
}
@utility text-label-caps {
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.10em;
  text-transform: uppercase;
}
@utility text-caption {
  font-size: 10px;
  font-weight: 400;
}
@utility text-micro {
  font-size: 9px;
  font-weight: 500;
  letter-spacing: 0.02em;
}
```

**Important:** `--color-*` tokens become Tailwind utilities: `bg-gradient-cool-start`, `text-text-primary`, `border-glass-border`, etc. `--radius-*` become `rounded-card`, `rounded-sheet`, etc.

### Component Implementations

#### EuroBurnGradient.tsx

```tsx
// client/src/components/EuroBurnGradient.tsx
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
```

#### BottomTabBar.tsx — Key Implementation Points

- Use `NavLink` from `react-router-dom` for each tab. `NavLink` provides an `isActive` boolean via its `className` callback.
- Dashboard route: `to="/"` with `end` prop (prevents `/` matching all child routes).
- Active style: combine opacity-100 with text-text-primary color; inactive: opacity-40 with text-text-tertiary.
- The nav element: `<nav role="navigation" aria-label="Main navigation">`.
- Each `NavLink` gets `aria-label="Dashboard"` (etc.) so screen readers announce the name.

#### SidebarNav.tsx — Key Implementation Points

- Same 4 nav items, same icon + label layout.
- Active state uses inline style for `background: rgba(255,255,255,0.12)` + `borderRadius: '10px'` (matches `--radius-sidebar-item`).
- Width fixed at 200px, full height (`h-screen`), `sticky top-0`.
- `backdrop-filter` must be set via inline style (Tailwind v4 has `backdrop-blur-*` utilities but `saturate` requires the full value).

#### AppShell.tsx — Router Integration

`AppShell` uses `<Outlet />` from react-router-dom. It renders on every main route change. The gradient and nav are always present; only the `<Outlet>` content changes on navigation.

```tsx
// Simplified structure
import { Outlet } from 'react-router-dom'
import { EuroBurnGradient } from './EuroBurnGradient'
import { BottomTabBar } from './BottomTabBar'
import { SidebarNav } from './SidebarNav'

export default function AppShell() {
  return (
    <>
      <EuroBurnGradient />
      <div className="flex min-h-screen">
        <div className="hidden md:flex flex-col w-[200px] sticky top-0 h-screen shrink-0">
          <SidebarNav />
        </div>
        <main className="flex-1 overflow-y-auto pb-[84px] md:pb-0">
          <Outlet />
        </main>
      </div>
      <div className="md:hidden">
        <BottomTabBar />
      </div>
    </>
  )
}
```

#### router.tsx — Required Restructure

Current router has 5 flat routes. New structure uses AppShell as a layout route:

```tsx
import { lazy, Suspense } from 'react'
import { createBrowserRouter } from 'react-router-dom'
import AppShell from '@/components/AppShell'

const DashboardPage = lazy(() => import('@/features/dashboard/DashboardPage'))
const InsightsPage = lazy(() => import('@/features/insights/InsightsPage'))
const DecompositionPage = lazy(() => import('@/features/decomposition/DecompositionPage'))
const SettingsPage = lazy(() => import('@/features/settings/SettingsPage'))
const OnboardingPage = lazy(() => import('@/features/onboarding/OnboardingPage'))

function Wrap({ Page }: { Page: React.ComponentType }) {
  return (
    <Suspense fallback={null}>
      <Page />
    </Suspense>
  )
}

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

Note: AppShell is NOT lazy-loaded (it's the layout shell; lazy-loading it adds no benefit and complicates the Suspense boundary). The child pages remain lazy.

#### useAuth.ts

```ts
// client/src/hooks/useAuth.ts
import { useQuery } from '@tanstack/react-query'
import { getMe, type SwaAuthUser } from '@/lib/authClient'

export function useAuth(): { user: SwaAuthUser | null; isLoading: boolean } {
  const { data, isLoading } = useQuery({
    queryKey: ['auth', 'me'],
    queryFn: getMe,
    staleTime: 5 * 60 * 1_000,
    retry: false,
  })
  return { user: data ?? null, isLoading }
}
```

### Testing Framework Setup (Task 1)

Install exact commands:
```bash
cd client
npm install -D vitest @vitest/ui jsdom @testing-library/react @testing-library/user-event @testing-library/jest-dom
```

Add to `client/vite.config.ts`:
```ts
import { defineConfig } from 'vite'
// ... existing imports

export default defineConfig({
  // ... existing config
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test-setup.ts'],
  },
})
```

Add TypeScript types for vitest globals in `client/tsconfig.app.json` — add `"types": ["vitest/globals"]` to `compilerOptions` if not using `globals: true` type inference. With `globals: true`, vitest injects `describe`, `it`, `expect` globally and TypeScript should pick these up via `vitest/globals` type reference. Add to `tsconfig.app.json` if needed:
```json
{
  "compilerOptions": {
    "types": ["vitest/globals"]
  }
}
```

`client/src/test-setup.ts`:
```ts
import '@testing-library/jest-dom'
```

### Tab Icons (Lucide React)

From `lucide-react` (already installed v1.21.0):
- Dashboard: `House` 
- Insights: `TrendingUp`
- Decomposition: `BarChart2`
- Settings: `Settings`

Usage: `<House size={22} />` or `<House className="w-[22px] h-[22px]" />`

### What This Story Does NOT Implement

- **`OnboardingGate.tsx`** — Story 2.2. The onboarding route (`/onboarding`) is kept flat outside the AppShell so the tab bar is not visible, but there is no redirect logic yet. Users can still navigate to `/` directly after Story 1.5.
- **Dynamic gradient position** — The gradient renders at the static neutral midpoint. Budget-based gradient shifting requires KPI data, which comes in Story 3.x.
- **Flat name in header** — The header / app name display is Story 2.x (settings data required).
- **User display / account info** — `useAuth` is created but not wired to any UI in this story.
- **Light mode** — Light mode color tokens are defined (glass-surface-light, glass-border-light) but the light-mode gradient and full light-mode switching is out of scope for this story.
- **`OnboardingPage` content** — Still a stub. Story 2.2 adds the intro screen, locale dropdown, and step flow.
- **`apiClient.ts` hardening** (deferred: network error reshaping) — Do not touch `apiClient.ts` in this story.

### Deferred Work Notes (Do Not Fix in This Story)

From `deferred-work.md`, the following are pre-existing and should not be addressed here:
- i18n locale stubs (`resources: {}`) — Story 2.1
- No `pull_request` CI trigger — CI hardening pass
- No `catch-all` 404 route in React Router — future UX story (but do NOT add one in 1.5 either — out of scope)
- `fetch()` network error reshaping in `apiClient.ts` — future hardening

### Project Structure — New Files for This Story

```
client/src/
├── components/                ← NEW folder (global non-shadcn components)
│   ├── AppShell.tsx           ← NEW
│   ├── BottomTabBar.tsx       ← NEW
│   ├── EuroBurnGradient.tsx   ← NEW
│   ├── SidebarNav.tsx         ← NEW
│   ├── BottomTabBar.test.tsx  ← NEW
│   └── SidebarNav.test.tsx    ← NEW
├── hooks/                     ← NEW folder (global hooks)
│   └── useAuth.ts             ← NEW
├── test-setup.ts              ← NEW
├── index.css                  ← MODIFIED (add @theme + @utility blocks)
└── router.tsx                 ← MODIFIED (AppShell layout route)
```

Files NOT modified in this story: `App.tsx`, `main.tsx`, any feature page, `lib/`, `vite.config.ts` (except adding `test:` block).

### Patterns from Previous Stories

- Story 1.4 established: `authClient.ts` exports named functions (not object exports). `useAuth` should wrap `getMe` from `authClient.ts` — do not redefine the fetch.
- Story 1.1 established: the `Wrap` helper in `router.tsx` for lazy-loaded pages. Preserve this pattern.
- No new runtime dependencies without user approval — vitest + testing-library are devDependencies (build tools only, not shipped to users).

### References

- Design tokens: [Source: `_bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/DESIGN.md`]
- UX navigation IA: [Source: `_bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/EXPERIENCE.md` — Information Architecture table, Component Patterns table]
- Routing structure: [Source: `_bmad-output/planning-artifacts/architecture.md` — AD-19, Routing structure section]
- Frontend architecture: [Source: `_bmad-output/planning-artifacts/architecture.md` — Frontend Architecture, Structure Patterns]
- `useAuth` deferred note: [Source: `_bmad-output/implementation-artifacts/1-4-swa-easy-auth-and-tenantresolver-middleware.md` — Dev Notes, "What This Story Does NOT Implement"]
- Story ACs: [Source: `_bmad-output/planning-artifacts/epics.md` — Story 1.5]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

### Completion Notes List

- Task 1: Installed vitest 4.1.9, @vitest/ui, jsdom, @testing-library/react, @testing-library/user-event, @testing-library/jest-dom as devDependencies. Added `test` and `test:ui` scripts. Configured vite.config.ts with jsdom environment + `passWithNoTests: true`. Added `vitest/globals` to tsconfig types.
- Task 2: Added full `@theme` block to index.css with all Euro Burn gradient tokens, glass surface tokens, 7 semantic accent tokens, text palette, border radius tokens, and system font. Added 6 `@utility` type scale roles. Build exits 0, zero font files in dist.
- Task 3: Created `EuroBurnGradient.tsx` with fixed full-screen gradient div, `aria-hidden="true"`, and forward-compatible `consumptionPct` prop.
- Task 4: Created `BottomTabBar.tsx` with 4 NavLink tabs (Dashboard/Insights/Decomposition/Settings), 72px fixed bottom bar with glassmorphism styles, 22px icons from lucide-react, 44×44pt touch targets, aria-labels, and role="navigation".
- Task 5: Created `SidebarNav.tsx` with 200px fixed sidebar, glassmorphism styles, same 4 NavLink items, active state with rgba background + 10px border-radius, aria-labels, and role="navigation".
- Task 6: Created `AppShell.tsx` as default export layout component using Outlet, responsive nav (BottomTabBar phone / SidebarNav tablet+), pb-[84px] clearance for tab bar.
- Task 7: Restructured router.tsx to use AppShell as parent layout route for 4 main routes; onboarding stays flat outside AppShell.
- Task 8: Created `useAuth.ts` hook wrapping `getMe()` from authClient with TanStack Query (staleTime 5min, retry false).
- Task 9: Created BottomTabBar.test.tsx (3 tests) and SidebarNav.test.tsx (2 tests). All 5 pass with MemoryRouter wrapper.
- Task 10: All verifications pass — build clean (TypeScript + Vite), tests green, lint exits 0 (warnings only, pre-existing pattern), zero font files, 5 lazy chunks in dist.

### File List

| File | Action |
|------|--------|
| `client/src/index.css` | Modified |
| `client/src/router.tsx` | Modified |
| `client/src/components/AppShell.tsx` | Created |
| `client/src/components/BottomTabBar.tsx` | Created |
| `client/src/components/SidebarNav.tsx` | Created |
| `client/src/components/EuroBurnGradient.tsx` | Created |
| `client/src/components/BottomTabBar.test.tsx` | Created |
| `client/src/components/SidebarNav.test.tsx` | Created |
| `client/src/hooks/useAuth.ts` | Created |
| `client/src/test-setup.ts` | Created |
| `client/vite.config.ts` | Modified |
| `client/tsconfig.app.json` | Modified |
| `client/package.json` | Modified |

## Change Log

- Story created from epics.md, architecture.md, DESIGN.md, EXPERIENCE.md, and 1-4 learnings (Date: 2026-06-28)
- Implemented Story 1.5: vitest testing setup, CSS design system tokens, EuroBurnGradient, BottomTabBar, SidebarNav, AppShell layout, router restructure with AppShell as layout route, useAuth hook, component tests (5 passing) (Date: 2026-06-28)

### Review Findings

- [x] [Review][Defer] `accent-over-budget` and `accent-spike` share the same hex `#f59e0b` [index.css:20] — deferred, UX expert input needed on whether over-budget should be visually distinct from a spike
- [x] [Review][Patch] Duplicate `aria-label="Main navigation"` on two coexisting `<nav>` landmarks — fixed: BottomTabBar → "Bottom navigation", SidebarNav → "Sidebar navigation". [BottomTabBar.tsx:14, SidebarNav.tsx:13]
- [x] [Review][Patch] `@utility text-micro` missing `text-transform: uppercase` — fixed: added `text-transform: uppercase` to the utility. [index.css:72-76]
- [x] [Review][Patch] Active sidebar `borderRadius` hardcoded as `'10px'` — fixed: now uses `var(--radius-sidebar-item)`. [SidebarNav.tsx:36]
- [x] [Review][Patch] Missing `-webkit-backdrop-filter` vendor prefix — fixed: added `WebkitBackdropFilter` to both nav components. [BottomTabBar.tsx:20, SidebarNav.tsx:19]
- [x] [Review][Defer] `useAuth` doesn't expose `isError`/`error` field [useAuth.ts] — deferred, spec explicitly scopes return type to `{ user, isLoading }`; expand when hook is wired to UI
- [x] [Review][Defer] No 404/catch-all route — unmatched paths render blank AppShell [router.tsx] — deferred, pre-existing gap noted in deferred-work.md; out of scope for this story
- [x] [Review][Defer] No iOS safe-area-inset (`env(safe-area-inset-bottom)`) on BottomTabBar [BottomTabBar.tsx] — deferred, spec specifies exact dimensions; address in a UX-polish story
- [x] [Review][Defer] No `errorElement` / React error boundary around `<Outlet />` [router.tsx, AppShell.tsx] — deferred, out of scope for this story; add in a resilience pass
- [x] [Review][Defer] `queryKey: ['auth', 'me']` has no tenant/user scope — risks cross-account cache pollution in multi-tab logout/login scenarios [useAuth.ts] — deferred, single-account scope for now; revisit in multi-flat or multi-user story
