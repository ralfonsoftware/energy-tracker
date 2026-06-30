# Epic List

## Epic 1: Project Foundation & Authenticated App Shell
Users can authenticate via Azure Entra ID and reach a working app shell — routing, navigation, design system tokens, and CI/CD pipeline are all in place. This is the deployable skeleton that every subsequent epic builds on.
**FRs covered:** FR-1, FR-2, FR-3
**Architecture items:** Monorepo scaffold (Vite + React frontend, .NET 10 Azure Functions backend), EF Core baseline migration, Users table, SWA Easy Auth + TenantResolver, managed identity, Key Vault, Application Insights, GitHub Actions CI/CD, design system tokens (Euro Burn gradient tokens, glass surface tokens, type scale, semantic accent colors), app shell with bottom tab bar and routing.

## Epic 2: Onboarding & Locale Selection
A first-time user can complete the guided onboarding flow to configure their flat name, annual kWh baseline (preset or custom), and initial energy tariff — and select their preferred locale (de-DE / en-US). The onboarding gate ensures no main feature is accessible until setup is complete.
**FRs covered:** FR-4, FR-5, FR-6, FR-7, FR-40, FR-41, FR-42
**UX items:** UX-DR20 (3-screen onboarding flow + gate), UX-DR19 (microcopy conventions), locale dropdown, Intl.NumberFormat / Intl.DateTimeFormat render-time formatting.

## Epic 3: Meter Reading, KPI Dashboard & Reading History
The irreducible core of the product: a user standing in the basement can enter a meter reading in under 60 seconds and immediately see their daily, weekly, and projected monthly cost in euros on the Euro Burn Dashboard. Spike detection and trend chart are included. Reading history with correction is accessible from the chart.
**FRs covered:** FR-8, FR-9, FR-14, FR-15, FR-16, FR-17
**UX items:** UX-DR1 (Euro Burn Gradient Background), UX-DR2 (Glass Surface System), UX-DR5 (Enter Reading CTA), UX-DR7 (KPI Tile + pulse animation + Reduce Motion), UX-DR8 (Enter Reading bottom sheet), UX-DR9 (Trend Chart + amber spike bars + Reading History icon), UX-DR18 (cold open, post-submit 3-signal confirmation, below-last-reading warning, spike day, budget delta states), UX-DR11 (accessibility: focus trap, aria-live, 44pt targets, inputmode="numeric").

## Epic 4: Tariff Management
A user can maintain a full tariff history with effective dates, optional contract periods, and forward-dated tariff changes. All cost figures — dashboard, decomposition, insights — use the tariff active on the date of consumption, not the current tariff.
**FRs covered:** FR-10, FR-11, FR-12, FR-13
**UX items:** UX-DR15 (inline tariff lock indicator), tariff list and add/edit form in Settings.

## Epic 5: Multi-Flat Management & Flat Structure
A user can create and manage multiple flats, switch between them via the header, and define the four-level physical hierarchy (Flat → Rooms → Power Points → Devices) for each. Flat deletion is permanent and requires type-to-confirm.
**FRs covered:** FR-18, FR-19, FR-20, FR-21, FR-22, FR-23
**UX items:** UX-DR16 (flat deletion type-to-confirm), flat switcher dropdown in header, default 5-room template, Flat Structure editor.

## Epic 6: Smart Plug Import & Device Registry
A user can upload Eve Home Excel and Meross CSV exports, which are parsed into a unified daily kWh timeline, gap-interpolated, and reconciled against the main meter. Devices can be registered with EU label or self-measured consumption profiles.
**FRs covered:** FR-24, FR-25, FR-26, FR-27, FR-28, FR-29, FR-30, FR-31
**UX items:** UX-DR14 (import upload zone: file picker + drag-drop, auto-detection, device association dropdown), UX-DR10 (Progress Card for async import), UX-DR17 (Choice Step for EU label vs self-measured, Daily/Weekly toggle).

## Epic 7: Consumption Decomposition
A user can view their energy consumption broken down by room and device for any period with smart plug data. The Residual (unattributed kWh) is always shown and never suppressed. Periods without data show an explicit unavailable state.
**FRs covered:** FR-32, FR-33, FR-34
**UX items:** UX-DR13 (Residual card always first, Room cards, Device card variants — rich/measured vs compact/estimated, Smart Power Strip two-tier opacity sub-device rows).

## Epic 8: Actionable Insights
The app automatically discovers standby offenders, replacement candidates, budget pressure alerts, and invoice deviation hints on a daily schedule (02:00 UTC) and on-demand. Prior insights remain visible during discovery runs.
**FRs covered:** FR-35, FR-36, FR-37, FR-38, FR-39, FR-43
**UX items:** UX-DR10 (Progress Card for insight discovery), InsightCard variants (standby / replacement / budget / invoice types), insights insufficient-data state.

---
