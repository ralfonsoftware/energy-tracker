---
stepsCompleted: [1, 2, 3, 4]
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/DESIGN.md
  - _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/EXPERIENCE.md
---

# energy-tracker - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for energy-tracker, decomposing the requirements from the PRD, UX Design, and Architecture into implementable stories.

## Requirements Inventory

### Functional Requirements

**Release 1 (Core Tracking):**

FR-1: Any user can access the app only after completing authentication via the configured OIDC identity provider. Unauthenticated requests to any route redirect to OIDC login and return to the originally requested route after successful authentication.
FR-2: An authenticated session persists across browser restarts without requiring re-authentication until the session expires or the user signs out.
FR-3: The identity provider is specified via configuration (environment variables or config file). Changing the provider requires no code changes, only configuration changes.
FR-4: A new user (no existing Flat) cannot access any main app feature until Onboarding is complete. Onboarding collects: Flat name, Annual kWh Baseline (specific value or preset), initial Tariff (required: fixed monthly base fee + price per kWh; optional: provider name, contract start date, contract duration), and planned annual spend (auto-derived, editable).
FR-5: The user can enter the Annual kWh Baseline as a specific numeric value or by selecting one of four household-size presets: 1 person ≈ 1,500 kWh; 2 persons ≈ 2,500 kWh; 3 persons ≈ 3,500 kWh; 4 persons ≈ 4,250 kWh.
FR-6: The initial Tariff is captured during Onboarding with fixed monthly base fee and price per kWh as required fields; provider name, contract start date, and contract duration (1, 6, 12, or 24 months) are optional.
FR-7: All Onboarding fields (Flat name, Annual kWh Baseline, Tariff, planned annual spend) are accessible and editable from Settings after initial setup. Planned annual spend lives near the Tariff configuration in Settings.
FR-8: The user can submit a Meter Reading (numeric kWh value) for the active Flat. The Reading is stored with the date and time of submission. Server-side processing time within Tier 1 ≤2s.
FR-9: The user can enter a Meter Reading for a past date. Retroactive Readings are costed at the Tariff active on their entered date.
FR-10: The user can create a Tariff entry specifying: effective date (required), fixed monthly base fee (required), price per kWh (required), provider name (optional), contract start date (optional), contract duration in months (optional: 1, 6, 12, or 24).
FR-11: When a Tariff entry includes a contract start date and contract duration, the entry's prices cannot be modified once the Contract Period has started.
FR-12: The user can create a Tariff entry with a future effective date. This does not alter cost calculations for periods before that effective date.
FR-13: All cost calculations use the Tariff active on the date of the relevant consumption, not the current Tariff.
FR-14: The KPI Dashboard displays: daily average kWh, weekly average kWh, daily cost (€), weekly cost (€), and projected monthly cost (€).
FR-15: Dashboard figures update immediately after a new Meter Reading is saved, without a page refresh.
FR-16: The app displays a trend chart of historical daily consumption derived from Meter Readings.
FR-17: The app detects daily consumption spikes exceeding a configurable threshold above the 7-day rolling average (default: 2×). Spike days are rendered as amber bars in the trend chart. Threshold is user-configurable per Flat.
FR-40: The user can select their preferred Locale from Settings (de-DE, en-US). Selection takes effect immediately. Initial Locale derived from Accept-Language header; override stored server-side in user profile.
FR-41: All numbers, dates, times, and currency values are formatted according to the active Locale's conventions. No hardcoded locale-specific formatting anywhere in the codebase.
FR-42: All data is stored and transmitted locale-neutrally: ISO 8601 datetimes with timezone offset, decimal-point numbers, currency as fixed-decimal values. Locale formatting applied only at render time.

**Release 2 (Consumption Decomposition):**

FR-18: The user can create and manage more than one Flat. Each Flat has its own Meter Readings, Tariff history, Smart Plug Data, and Flat Structure.
FR-19: A header component visible across all surfaces allows the user to switch between Flats. The active Flat's name is displayed in the header.
FR-20: The last active Flat is remembered across browser sessions.
FR-21: For each Flat, the user can define a Flat Structure: Rooms → Power Points → Devices. Smart Plug assigned to exactly one Power Point; plug_id assigned at Power Point level, never derived from file metadata. Smart Power Strip supported with Strip Outlets.
FR-22: When the user initiates Flat Structure setup for the first time on a Flat, a default Room template is pre-populated: living room, bedroom, kitchen, bathroom, hallway.
FR-23: Deleting a Flat permanently removes all associated data (Readings, Tariff entries, Smart Plug Data, Flat Structure, Device registrations). No orphaned records remain.
FR-24: The app parses Eve Home .xlsx export files (single sheet "Gesamtverbrauch", reverse-chronological ~10-minute interval rows in Wh) into a daily kWh timeline per plug. Timestamps treated as local time (not converted to UTC). Multiple overlapping exports deduplicated before aggregation.
FR-25: The app parses Meross .csv export files (UTF-8 with optional BOM, tab-separated with per-value comma prefix) into a daily kWh timeline per plug. BOM, trailing whitespace, and empty rows stripped. Zero-value rows stored as valid zero-consumption days, not treated as gaps.
FR-26: If a plug's timeline contains missing dates within its covered period, gaps are detected, user notified with affected date ranges, and gaps filled by linear interpolation (capped at 7-day pre-gap average). Interpolated values marked internally; shown with hint wherever they appear.
FR-27: Total attributed kWh + Residual = Main Meter total for any period with Smart Plug Data (within ±0.1 kWh for clean periods; within ±1.0 kWh for periods containing interpolated values).
FR-28: Import failures surfaced to user as one of three categorized messages: (1) data cannot be read, (2) processing failed — retry, (3) service temporarily unavailable — retry later. Raw error detail never exposed.
FR-29: The user can register a Device with: type, manufacturer, and model (required); purchase date (optional). Device assigned to a Power Point.
FR-30: The user can configure a Device's consumption via EU energy label: energy class rating and annual kWh figure. App derives daily estimate. Device appears in Decomposition marked as estimated.
FR-31: The user can configure a Device's consumption as a self-measured average: daily or weekly kWh. Device appears in Decomposition marked as estimated.
FR-32: For any period with Smart Plug Data, the Decomposition view shows attributed consumption per Room and per Device. For Smart Power Strip: strip's measured total is authoritative; configured sub-devices get proportional estimated share; unconfigured sub-devices get equal share of remainder.
FR-33: The Residual is shown in the Decomposition view for every period with Smart Plug Data, including when Residual is zero. Never suppressed.
FR-34: Periods with no imported Smart Plug Data are shown as "decomposition unavailable" with a prompt to import data. Partial or zero figures never shown for unavailable periods.
FR-35: Based on Smart Plug interval data (Eve Home only — Meross excluded due to daily-aggregate format), the app identifies Devices drawing >2 W outside configured usage window and flags them as Insights with Device name and quantified monthly cost.
FR-36: The app identifies high-consumption Devices where replacement offers quantifiable payback, surfaced as an Insight with Device name, estimated current cost, and savings.
FR-37: The app computes a rolling monthly projection and generates a budget pressure alert Insight when projection exceeds planned annual spend.
FR-38: Insight discovery runs automatically daily at 02:00 UTC. The user can also trigger discovery manually. Prior insights remain visible during a new run.
FR-39: A visible progress indicator is displayed for the full duration of an insight discovery run.
FR-43: The app computes a rolling annual kWh figure and generates an invoice deviation Insight when consumption is trending ±10% or more above or below the Annual kWh Baseline. Insight shows projected annual kWh, baseline, and implied euro difference at current Tariff.

### NonFunctional Requirements

NFR-1 Performance: Three-tier model: Tier 1 (≤2s synchronous — standard server actions), Tier 2 (≤30s with UI hint — operations exceeding 2s), Tier 3 (fully background with notification on completion — smart plug import processing and insight discovery). KPI Dashboard load for up to 2 years of Readings: Tier 1 (≤2s).

NFR-2 Security and Data Isolation: Full tenant isolation by user ID at every data layer. All requests authenticated (FR-1); no unauthenticated endpoints except OIDC callback. Currency stored as fixed-decimal C# `decimal` — no floating-point monetary values at any layer.

NFR-3 Internationalization: All UI text through localization framework. No hardcoded locale-specific strings, number formats, date formats, or currency symbols. All data stored locale-neutrally: ISO 8601 datetimes with timezone offset, decimal-point numbers, fixed-decimal currency. Locale preference stored server-side; `Accept-Language` header provides default. Locale formatting applied only at render time via `Intl.NumberFormat` and `Intl.DateTimeFormat`.

NFR-4 Reliability and Async Processing: Smart Plug file uploads stored in Azure Blob Storage, processed asynchronously by blob-triggered Azure Function. Azure Storage Account queues for lightweight internal messaging. Azure SQL Basic DTU (~€5/month, 2 GB) as persistent data store.

### Additional Requirements

Architecture (cross-cutting requirements that affect implementation):

- **Project scaffold (Epic 1, Story 1):** Monorepo initialized with Vite + React + TypeScript frontend (`npm create vite@latest client -- --template react-ts`) and .NET 10 Azure Functions isolated worker backend (`func init --worker-runtime dotnet-isolated --target-framework net10.0`). All scaffold commands specified in Architecture must be executed in the first story.
- **Azure SQL + EF Core:** Azure SQL Basic DTU as persistent data store; EF Core code-first migrations; Fluent API configuration only (no Data Annotation attributes on entity classes).
- **SWA Easy Auth + TenantResolver:** Authentication handled by SWA Easy Auth at the edge. Functions app reads X-MS-CLIENT-PRINCIPAL header. TenantResolver middleware extracts UserId (OIDC `sub` claim). Every function verifies FlatId belongs to resolved UserId before touching data.
- **Managed Identity:** All Azure service-to-service connections (Functions → SQL, Blob Storage, Storage Queue, Key Vault) use DefaultAzureCredential. No connection strings with passwords.
- **Vertical Slice Architecture (VSA):** Backend: each feature is a self-contained slice (Function trigger, handler logic, DTOs, validators co-located). Frontend: feature-folder structure mirrors backend slices exactly.
- **DTO types:** C# `record` types for all request/response DTOs. EF Core entities are regular classes. Function entry classes: `{Feature}Function`; entry methods always `RunAsync`.
- **C# language:** `<LangVersion>latest</LangVersion>` (C# 13 on .NET 10). Primary constructors for DI, collection expressions, pattern matching, required members on records.
- **REST API versioning:** All routes prefixed `/api/v1/`. Path-based versioning only.
- **Problem Details RFC 9457:** All error responses use Problem Details. Import error categories map to HTTP 422/500/503 with `type` field for frontend category selection.
- **Internal messaging:** Azure Storage Queue for import/insight decoupled signals. JSON envelopes: `{ importJobId, flatId, blobPath, userId }`.
- **TanStack Query v5:** All frontend server state in TanStack Query cache. Cache invalidation after reading submission drives KPI update. No Zustand or Redux.
- **react-hook-form + zod:** One zod schema per form, co-located with the feature component. No shared schema files across features.
- **i18n:** react-i18next with i18next-browser-languagedetector. Translation files namespace-split by feature. Server-stored locale override fetched on app load.
- **Code splitting:** Route-level lazy loading via Vite dynamic imports.
- **CI/CD:** GitHub Actions single pipeline deploys client/ (Vite build) and api/ (.NET 10 publish, ReadyToRun, linux-x64).
- **Monitoring:** Application Insights attached to Functions app (free tier).
- **Azure Functions:** Flex Consumption plan (Linux), .NET 10 isolated worker, ReadyToRun enabled.
- **Two-tier smart plug data model:** SmartPlugDailyData (all plugs, daily aggregates, used for all computation) + SmartPlugIntervalData (Eve Home only, ~10-min interval rows, used exclusively for standby detection).
- **Decimal invariant:** `decimal` for all kWh, cost, tariff, and baseline values in entities, DTOs, and computation. `float` and `double` banned for energy and monetary values.
- **CancellationToken:** Every async method accepts and forwards `CancellationToken ct`.
- **Release staging:** R1 must deploy as a fully working product with no stubs. R2 must be addable without schema migrations that break R1 data or R1 API contracts.
- **Hard deletes:** Flat deletion cascades all child data. Meter Readings edited in-place with `IsCorrected = true` and `OriginalKwhValue` preserved.
- **FluentValidation:** Request-level validation via FluentValidation validators co-located per VSA slice.

### UX Design Requirements

UX-DR1: Euro Burn Gradient Background — full-screen CSS linear-gradient (5 stops, 160deg phone / 140deg tablet) that dynamically encodes current-day kWh vs daily budget. Stop positions shift based on consumption percentage (−50% clips to cool edge, +50% clips to warm edge). Separate light-mode gradient (sky-blue → warm-sand → warm-amber). Renders behind all other content; no midpoint marker.

UX-DR2: Glass Surface System — all content cards use `backdrop-filter: blur(20px) saturate(180%)` with semi-transparent backgrounds. Dark mode: rgba(255,255,255,0.08) bg, rgba(255,255,255,0.14) border, 18px border-radius. Light mode: rgba(255,255,255,0.55) bg, rgba(0,0,0,0.08) border. Cards must be translucent enough for gradient to bleed through.

UX-DR3: Semantic accent color palette — implemented as CSS custom properties/Tailwind tokens: accent-spike (#f59e0b), accent-under-budget (#4ade80 dark / #16a34a light), accent-over-budget (#f59e0b), accent-info (#60a5fa), accent-error (#f87171), accent-tariff-locked (#d97706), residual-tint (rgba(245,158,11,0.10)).

UX-DR4: Type scale — display-kpi (22px/700/−0.02em phone; 20px/700 tablet), body-sm (13px/500-600), label-caps (11px/600/+0.08-0.12em/uppercase), caption (10px/400), micro (9px/500). System font stack only: -apple-system, BlinkMacSystemFont, "SF Pro Display", "Helvetica Neue", sans-serif. Zero web font loading.

UX-DR5: Enter Reading CTA button — phone: full-width pill (9999px radius, 16px padding, glass treatment, 1.5px border rgba(255,255,255,0.40)). Tablet: 44×44px icon-only compact button (14px radius, Lucide Zap icon 20×20px) positioned in content header top-right. Light mode variants specified in DESIGN.md.

UX-DR6: Bottom Tab Bar (phone) / Sidebar Nav (tablet) — phone: fixed bottom, 72px height, glass treatment, 4 tabs (Dashboard · Insights · Decomposition · Settings), 22×22px icon + micro label, active/inactive opacity states. Tablet: 200px sidebar, row items with icon+label, active item background rgba(255,255,255,0.12), 10px radius.

UX-DR7: KPI Tile component — glass card with paired kWh headline (display-kpi) + € subline (body-sm). Daily tile adds budget delta line (label-caps size, accent-under-budget or accent-over-budget). Post-reading pulse animation (count animation counting up/down to new values). Reduce Motion: skip animation, show updated values immediately.

UX-DR8: Enter Reading bottom sheet — slides up from bottom with drag handle. kWh field auto-focused, inputmode="numeric" keyboard on open. Date/time field pre-filled with current timestamp, editable. Save failure: sheet stays open, value preserved, error toast appears near Save button: "Couldn't save — try again." Sheet must not close on save failure.

UX-DR9: Trend Chart component — recharts bar chart encoding daily consumption. Spike day bars: amber (accent-spike). Reading history clock/list icon in card header (20×20px, text-secondary). Period dropdown on Insights tab (This week / This month / Last month / This year / Custom).

UX-DR10: Progress Card component — amber-tinted glass card (residual-tint overlay on glass surface). Used for import processing and insight discovery progress states. Contains: status label (label-caps), progress description (body-sm), optional spinner/indeterminate bar. Persists until processing completes; app remains fully navigable during processing.

UX-DR11: Accessibility floor (WCAG 2.2 AA) — explicit aria-labels on all icon buttons, inputmode="numeric" for all kWh inputs, minimum 44×44pt tap targets for all interactive elements, focus trap in bottom sheets with return to trigger element on close, Reduce Motion support for KPI pulse, aria-live="polite" for validation messages, tab bar announces surface name on focus/activation, bottom sheet opening announces title and first interactive element.

UX-DR12: Responsive layout breakpoints — phone (<768px): bottom tab bar, 2×2 KPI grid, full-width CTA, single-column decomposition, file picker only (no drag-and-drop); tablet (768-1023px): sidebar nav, 4-across KPI grid, icon-only header CTA, drag-and-drop on upload zone; desktop (1024px+): expanded sidebar, multi-column decomposition, two-panel settings layout.

UX-DR13: Decomposition card system — Residual card (always first, amber tint overlay, kWh + % shown, never suppressed including when value is zero). Room cards grouping device cards. Device card variants: rich/measured (large card, "Measured" badge, sparkline detail for smart plug devices); compact/estimated (small card, "Estimated" badge, EU label or self-measured devices). Smart Power Strip card: "Smart strip" badge, measured total in header as authoritative, sub-device rows with two-tier opacity (configured: full opacity proportional share; unconfigured: 0.45 opacity equal share with configure hint).

UX-DR14: Import upload zone — file picker primary on all platforms (native file chooser, accepts .xlsx and .csv). Drag-and-drop additionally on desktop/tablet. File list below zone showing: filename, auto-detected type (Eve Home / Meross), device association dropdown per file (auto-pre-selected if filename matches device name case-insensitively). "Upload Files" button active only when all files have a device association.

UX-DR15: Tariff lock indicator — inline lock icon + "Locked — contract active until [month year]" label on price fields when contract period is active. Fields render as read-only (greyed). Non-price fields (provider name, contract dates) remain editable. Lock state immediately visible on form open — no dialog or tap-to-reveal.

UX-DR16: Flat deletion type-to-confirm dialog — text input: 'Type "[Flat name]" to delete'. Delete action enabled only when typed value matches exactly. Friction matches irreversibility.

UX-DR17: Choice Step and Toggle for Device energy approach — two mutually exclusive option cards (EU energy label / self-measured); selecting one reveals only that approach's fields (other fields hidden, not disabled). Toggle for Daily/Weekly self-measured period: "Daily" pre-selected; switching updates input label instantly.

UX-DR18: All state patterns per EXPERIENCE.md — cold open (dashes/empty KPI tiles, gradient at neutral midpoint), below-last-reading inline warning, post-reading-submit three-signal confirmation (sheet close + KPI pulse + "Last read:" timestamp update, no success toast), Reading History edit-with-log ("Original value was X kWh" note), Decomposition unavailable state (no zeros/partial figures), import error inline states (unreadable/failed/service unavailable), Insights insufficient data state.

UX-DR19: Voice and tone microcopy — arrow-first numeric deltas (↓/↑ symbol first, then quantity, then unit, then label), instrument register (factual, not coaching, no exclamation marks), factual lock labels with contract end date, no "Great job!" / "You're doing amazing!", error messages include retry action when possible. Specific strings specified in EXPERIENCE.md microcopy table must be used verbatim.

UX-DR20: Onboarding flow — Intro screen (app name + value prop, locale dropdown top-right, "Get Started" CTA) → Step 1 (flat name input) → Step 2 (Annual kWh Baseline with 4 household-size presets + custom value input; required Tariff fields + optional fields; auto-derived annual budget showing calculation derivation, editable). Onboarding gate component blocks all main routes until both steps complete. Step indicator shows current step position.

### FR Coverage Map

```
FR-1:  Epic 1 — OIDC authentication gate
FR-2:  Epic 1 — Session persistence across browser restarts
FR-3:  Epic 1 — Configurable identity provider (config only)
FR-4:  Epic 2 — First-use onboarding gate
FR-5:  Epic 2 — Annual kWh Baseline entry (presets + custom)
FR-6:  Epic 2 — Onboarding Tariff entry
FR-7:  Epic 2 — Onboarding fields editable from Settings
FR-8:  Epic 3 — Meter Reading submission
FR-9:  Epic 3 — Retroactive Reading entry
FR-10: Epic 4 — Tariff configuration
FR-11: Epic 4 — Period-locked Tariff prices
FR-12: Epic 4 — Future Tariff pre-entry
FR-13: Epic 4 — Period-accurate historical costing
FR-14: Epic 3 — KPI Dashboard display
FR-15: Epic 3 — Immediate Dashboard update
FR-16: Epic 3 — Consumption trend visualization
FR-17: Epic 3 — Spike detection
FR-18: Epic 5 — Multiple Flat support
FR-19: Epic 5 — Flat switcher
FR-20: Epic 5 — Last active Flat persistence
FR-21: Epic 5 — Flat Structure definition
FR-22: Epic 5 — Default room template
FR-23: Epic 5 — Flat deletion with cascade
FR-24: Epic 6 — Eve Home Excel import
FR-25: Epic 6 — Meross CSV import
FR-26: Epic 6 — Gap detection and linear interpolation
FR-27: Epic 6 — Smart Plug reconciliation against Main Meter
FR-28: Epic 6 — Import error categorization
FR-29: Epic 5 — Device metadata registration (Name, Type, Manufacturer, Model, PurchaseDate)
FR-30: Epic 6 — EU energy label consumption
FR-31: Epic 6 — Self-measured consumption
FR-32: Epic 7 — Decomposition view
FR-33: Epic 7 — Residual always shown
FR-34: Epic 7 — Decomposition unavailable state
FR-35: Epic 8 — Standby offender detection (Eve Home only)
FR-36: Epic 8 — Replacement candidate detection
FR-37: Epic 8 — Budget pressure alert
FR-38: Epic 8 — Scheduled and manual insight discovery
FR-39: Epic 8 — Discovery progress indicator
FR-40: Epic 2 — Locale selection
FR-41: Epic 2 — Locale-aware rendering
FR-42: Epic 2 — Locale-neutral storage
FR-43: Epic 8 — Invoice deviation hint
```

## Epic List

### Epic 1: Project Foundation & Authenticated App Shell
Users can authenticate via Azure Entra ID and reach a working app shell — routing, navigation, design system tokens, and CI/CD pipeline are all in place. This is the deployable skeleton that every subsequent epic builds on.
**FRs covered:** FR-1, FR-2, FR-3
**Architecture items:** Monorepo scaffold (Vite + React frontend, .NET 10 Azure Functions backend), EF Core baseline migration, Users table, SWA Easy Auth + TenantResolver, managed identity, Key Vault, Application Insights, GitHub Actions CI/CD, design system tokens (Euro Burn gradient tokens, glass surface tokens, type scale, semantic accent colors), app shell with bottom tab bar and routing.

### Epic 2: Onboarding & Locale Selection
A first-time user can complete the guided onboarding flow to configure their flat name, annual kWh baseline (preset or custom), and initial energy tariff — and select their preferred locale (de-DE / en-US). The onboarding gate ensures no main feature is accessible until setup is complete.
**FRs covered:** FR-4, FR-5, FR-6, FR-7, FR-40, FR-41, FR-42
**UX items:** UX-DR20 (3-screen onboarding flow + gate), UX-DR19 (microcopy conventions), locale dropdown, Intl.NumberFormat / Intl.DateTimeFormat render-time formatting.

### Epic 3: Meter Reading, KPI Dashboard & Reading History
The irreducible core of the product: a user standing in the basement can enter a meter reading in under 60 seconds and immediately see their daily, weekly, and projected monthly cost in euros on the Euro Burn Dashboard. Spike detection and trend chart are included. Reading history with correction is accessible from the chart.
**FRs covered:** FR-8, FR-9, FR-14, FR-15, FR-16, FR-17
**UX items:** UX-DR1 (Euro Burn Gradient Background), UX-DR2 (Glass Surface System), UX-DR5 (Enter Reading CTA), UX-DR7 (KPI Tile + pulse animation + Reduce Motion), UX-DR8 (Enter Reading bottom sheet), UX-DR9 (Trend Chart + amber spike bars + Reading History icon), UX-DR18 (cold open, post-submit 3-signal confirmation, below-last-reading warning, spike day, budget delta states), UX-DR11 (accessibility: focus trap, aria-live, 44pt targets, inputmode="numeric").

### Epic 4: Tariff Management
A user can maintain a full tariff history with effective dates, optional contract periods, and forward-dated tariff changes. All cost figures — dashboard, decomposition, insights — use the tariff active on the date of consumption, not the current tariff.
**FRs covered:** FR-10, FR-11, FR-12, FR-13
**UX items:** UX-DR15 (inline tariff lock indicator), tariff list and add/edit form in Settings.

### Epic 5: Multi-Flat Management & Flat Structure
A user can create and manage multiple flats, switch between them via the header, and define the four-level physical hierarchy (Flat → Rooms → Power Points → Devices) for each. Flat deletion is permanent and requires type-to-confirm.
**FRs covered:** FR-18, FR-19, FR-20, FR-21, FR-22, FR-23
**UX items:** UX-DR16 (flat deletion type-to-confirm), flat switcher dropdown in header, default 5-room template, Flat Structure editor.

### Epic 6: Smart Plug Import & Device Registry
A user can upload Eve Home Excel and Meross CSV exports, which are parsed into a unified daily kWh timeline, gap-interpolated, and reconciled against the main meter. Devices can be registered with EU label or self-measured consumption profiles.
**FRs covered:** FR-24, FR-25, FR-26, FR-27, FR-28, FR-29, FR-30, FR-31
**UX items:** UX-DR14 (import upload zone: file picker + drag-drop, auto-detection, device association dropdown), UX-DR10 (Progress Card for async import), UX-DR17 (Choice Step for EU label vs self-measured, Daily/Weekly toggle).

### Epic 7: Consumption Decomposition
A user can view their energy consumption broken down by room and device for any period with smart plug data. The Residual (unattributed kWh) is always shown and never suppressed. Periods without data show an explicit unavailable state.
**FRs covered:** FR-32, FR-33, FR-34
**UX items:** UX-DR13 (Residual card always first, Room cards, Device card variants — rich/measured vs compact/estimated, Smart Power Strip two-tier opacity sub-device rows).

### Epic 8: Actionable Insights
The app automatically discovers standby offenders, replacement candidates, budget pressure alerts, and invoice deviation hints on a daily schedule (02:00 UTC) and on-demand. Prior insights remain visible during discovery runs.
**FRs covered:** FR-35, FR-36, FR-37, FR-38, FR-39, FR-43
**UX items:** UX-DR10 (Progress Card for insight discovery), InsightCard variants (standby / replacement / budget / invoice types), insights insufficient-data state.

---

## Epic 1: Project Foundation & Authenticated App Shell

Users can authenticate via Azure Entra ID and reach a working app shell — routing, navigation, design system tokens, and CI/CD pipeline are all in place. This is the deployable skeleton that every subsequent epic builds on.

### Story 1.1: Monorepo Scaffold & CI/CD Pipeline

As a developer,
I want to initialize the energy-tracker monorepo with the prescribed scaffold (Vite + React + TypeScript frontend, .NET 10 Azure Functions isolated worker backend) and a working GitHub Actions CI/CD pipeline,
So that all subsequent development has a consistent, deployable foundation from day one.

**Acceptance Criteria:**

**Given** the monorepo root directory,
**When** the scaffold commands from Architecture are executed (`npm create vite@latest client`, shadcn/ui init, package installs, `func init` for api/, `dotnet add package` for all backend dependencies),
**Then** `client/` and `api/` are created and both build without errors (`npm run build` and `dotnet publish -c Release -r linux-x64 --no-self-contained /p:PublishReadyToRun=true`).

**Given** `client/` is running (`npm run dev`),
**When** a request is made to `/api/anything`,
**Then** the Vite dev proxy forwards it to `localhost:7071` as configured in `vite.config.ts`.

**Given** a push to the main branch,
**When** the GitHub Actions pipeline runs,
**Then** it builds the Vite frontend, publishes the .NET Functions app (ReadyToRun, linux-x64), and deploys both to Azure Static Web App without errors.

**Given** `staticwebapp.config.json`,
**When** any non-`/api` route is requested from the SWA,
**Then** the response is `index.html` (SPA fallback for client-side routing), and `/api/*` routes are forwarded to the linked Functions app.

---

### Story 1.2: Azure Infrastructure Provisioning

As a developer,
I want all required Azure resources provisioned and connected via managed identity,
So that the application has a secure, cost-appropriate cloud infrastructure with no hardcoded credentials.

**Acceptance Criteria:**

**Given** the Azure subscription,
**When** infrastructure provisioning is complete,
**Then** the following resources exist: Azure Static Web App (Free), Azure Functions app (Flex Consumption Linux, .NET 10), Azure Storage Account (Standard LRS) with blob container `smart-plug-imports/{userId}/{flatId}/` and a storage queue, Azure SQL Server + DB (Basic DTU ~€5/mo), Azure Key Vault (Standard), Application Insights + Log Analytics workspace, and a user-assigned Managed Identity assigned to the Functions app.

**Given** the Managed Identity assigned to the Functions app,
**When** `DefaultAzureCredential` is used in any Function,
**Then** connections to Azure SQL, Blob Storage, Storage Queue, and Key Vault succeed without password-based connection strings in any config file or source code.

**Given** Application Insights attached to the Functions app,
**When** a Function executes,
**Then** the invocation trace, any dependencies (SQL, Blob), and any failures are visible in Application Insights within 5 minutes.

**Given** `local.settings.json` (gitignored),
**When** the developer runs the Functions host locally after `az login`,
**Then** all service connections use the developer's Azure CLI credentials via `DefaultAzureCredential` — no secrets in source code.

---

### Story 1.3: Database Schema — Users Table & EF Core Migration Baseline

As a developer,
I want EF Core configured with code-first Fluent API migrations and the Users table created in Azure SQL,
So that the schema management foundation is in place and the first entity exists for authentication context.

**Acceptance Criteria:**

**Given** the `api/` project,
**When** EF Core is configured in `Program.cs`,
**Then** `AppDbContext` is registered using `DefaultAzureCredential` for SQL auth, and the `UserConfiguration : IEntityTypeConfiguration<User>` class defines the `Users` table (`UserId` nvarchar PK, `LocaleOverride` nvarchar nullable) using Fluent API only — no Data Annotation attributes on the `User` entity class.

**Given** the EF Core configuration,
**When** `dotnet ef migrations add InitialCreate` is run and `dotnet ef database update` is applied,
**Then** the `Users` table exists in Azure SQL with the correct columns and no errors.

**Given** any entity class in `api/Data/Entities/`,
**When** the code is reviewed,
**Then** zero Data Annotation attributes (`[Key]`, `[MaxLength]`, `[Required]`, etc.) appear — all schema rules are in `api/Data/Configurations/` classes.

**Given** all DTOs in `api/Features/`,
**When** the code is reviewed,
**Then** all request and response DTOs are C# `record` types; all EF Core entities are regular `class` types; all async methods are suffixed `Async` and accept `CancellationToken ct`.

---

### Story 1.4: SWA Easy Auth & TenantResolver Middleware

As an authenticated user,
I want all app routes to require authentication via Azure Entra ID,
So that my energy data is protected and I am returned to the originally requested route after signing in.

**Acceptance Criteria:**

**Given** an unauthenticated browser session,
**When** any app route is accessed (including deep links to `/settings`, `/insights`, etc.),
**Then** SWA Easy Auth intercepts the request and redirects to the Azure Entra ID OIDC login flow.

**Given** a successful OIDC login,
**When** the auth callback completes,
**Then** the user lands on the originally requested route, not the app root.

**Given** an authenticated session,
**When** the browser is closed and reopened,
**Then** the session persists without requiring re-authentication (until natural session expiry or sign-out).

**Given** any HTTP Function in the Functions app,
**When** a request arrives with the `X-MS-CLIENT-PRINCIPAL` header injected by SWA Easy Auth,
**Then** `TenantResolver` middleware registered in `Program.cs` extracts the OIDC `sub` claim and makes the resolved `UserId` available to the Function's execution context; a missing or malformed header returns HTTP 403 Problem Details.

**Given** a change to the OIDC provider configuration (environment variable / config file swap),
**When** the app is redeployed,
**Then** all auth flows route through the new OIDC provider with zero code changes.

---

### Story 1.5: App Shell — Design System Tokens, Routing & Navigation

As an authenticated user,
I want to see the app shell with the Euro Burn design system applied globally and functioning tab-bar navigation between the four main sections,
So that the app has its correct visual identity and I can navigate to any section.

**Acceptance Criteria:**

**Given** an authenticated user loading the app on phone (<768px),
**When** the app shell renders,
**Then** the Euro Burn Gradient Background displays as a full-screen `linear-gradient(160deg, ...)` with all 5 color stops at their design-specified hex values behind all content; the Bottom Tab Bar is fixed at the bottom (72px height, `background: rgba(10,15,25,0.75)`, `backdrop-filter: blur(20px) saturate(180%)`, `border-top: 1px solid rgba(255,255,255,0.10)`).

**Given** the Bottom Tab Bar on phone,
**When** rendered,
**Then** it shows 4 tabs (Dashboard · Insights · Decomposition · Settings) each with a 22×22px icon and micro-text label; the active tab icon is at opacity 1.0 with `text-primary` label; inactive tabs are at opacity 0.4 with `text-tertiary` label; each tab's tap target is minimum 44×44pt.

**Given** the app shell on tablet (≥768px),
**When** rendered,
**Then** the bottom tab bar is replaced by a 200px sidebar nav (`background: rgba(0,0,0,0.25)`, `backdrop-filter: blur(20px) saturate(180%)`, `border-right: 1px solid rgba(255,255,255,0.08)`); the active nav item has `background: rgba(255,255,255,0.12)` and `border-radius: 10px`.

**Given** any tab is tapped or clicked,
**When** the route changes,
**Then** the corresponding route loads (`/` Dashboard, `/insights` Insights, `/decomposition` Decomposition, `/settings` Settings); each route is lazy-loaded via Vite dynamic import.

**Given** the design system tokens,
**When** the CSS is inspected,
**Then** all Euro Burn gradient tokens, glass surface tokens (`glass-surface`, `glass-border`, `glass-surface-light`, `glass-border-light`), all 7 semantic accent tokens, and type scale roles (display-kpi, body-sm, label-caps, caption, micro) are defined as Tailwind v4 / CSS custom property tokens globally.

**Given** the app rendering on any platform,
**When** network requests are inspected,
**Then** zero web font files are loaded; the system font stack resolves natively.

**Given** any tab in the bottom tab bar or sidebar,
**When** a screen reader focuses or activates it,
**Then** the surface name is announced on both focus and activation.

---

## Epic 2: Onboarding & Locale Selection

A first-time user can complete the guided onboarding flow to configure their flat name, annual kWh baseline (preset or custom), and initial energy tariff — and select their preferred locale (de-DE / en-US). The onboarding gate ensures no main feature is accessible until setup is complete.

### Story 2.1: i18n Infrastructure & Locale Settings API

As a user,
I want the app to detect my preferred locale from my browser and display all text, numbers, dates, and currency in my locale's format,
So that the app feels native to my region from the very first screen.

**Acceptance Criteria:**

**Given** the `client/` project,
**When** the i18n infrastructure is set up,
**Then** react-i18next is initialized in `lib/i18n.ts` with `i18next-browser-languagedetector`, namespace-split translation files exist for both `de-DE` and `en-US` under `locales/{locale}/` (at minimum: `common.json`, `onboarding.json`, `settings.json`), and all UI strings are rendered via `useTranslation` hooks — zero hardcoded locale-specific strings in any component.

**Given** a new browser session with `Accept-Language: de-DE`,
**When** the app loads with no server-stored locale override,
**Then** all UI text renders in German, numbers use comma-decimal (e.g. `1,27 €`), dates use `dd.mm.yyyy`, and times use `HH:mm`.

**Given** a new browser session with `Accept-Language: en-US`,
**When** the app loads with no server-stored locale override,
**Then** all UI text renders in English, numbers use period-decimal (e.g. `$1.27`), dates use `mm/dd/yyyy`, and times use `h:mm AM/PM`.

**Given** the `GET /api/v1/user/settings` and `PUT /api/v1/user/settings` endpoints,
**When** a locale override is stored via PUT,
**Then** a subsequent GET returns the stored locale; `LocaleResolver` in `api/Shared/LocaleResolver.cs` applies the stored override over the `Accept-Language` header; the `Users.LocaleOverride` column stores the value; all currency, number, and date values in the database remain locale-neutral (ISO 8601 with offset, decimal-point numbers, fixed-decimal currency).

**Given** `GET /api/v1/user/settings`,
**When** called by an authenticated user,
**Then** the response includes `hasFlat: bool` — `true` when at least one `Flat` record exists for the resolved `UserId`, `false` otherwise; this field is derived at query time (no stored flag); it requires no additional DB writes.

**Given** a currency amount stored during a `de-DE` session,
**When** the locale is subsequently changed to `en-US`,
**Then** the stored value renders correctly with `$` symbol and period decimal — no re-storage required.

---

### Story 2.2: Onboarding Gate & Intro Screen

As a first-time user,
I want the app to intercept my first visit and show an intro screen before I can reach any main feature,
So that I understand the app's purpose and can start the setup flow.

**Acceptance Criteria:**

**Given** `useUserSettings` (TanStack Query key: `['settings']`) fetches `GET /api/v1/user/settings` on app load,
**When** `hasFlat === false`,
**Then** `OnboardingGate.tsx` intercepts navigation to any main route (`/`, `/insights`, `/decomposition`, `/settings`) and redirects to `/onboarding`; the main tab bar / sidebar is not visible during onboarding.

**Given** an authenticated user with no existing Flat (new user),
**When** any main app route is accessed (`/`, `/insights`, `/decomposition`, `/settings`),
**Then** `OnboardingGate.tsx` intercepts the navigation and redirects to `/onboarding`; the main tab bar / sidebar is not visible during onboarding.

**Given** the `/onboarding` route,
**When** the Intro screen renders,
**Then** it shows: the app name, the value proposition copy "Know what your energy costs, every day.", a locale dropdown in the top-right (`DE ▾` / `EN ▾`), and a "Get Started" CTA button; no other navigation elements are shown.

**Given** the locale dropdown on the Intro screen,
**When** a locale is selected,
**Then** all text on the current screen immediately re-renders in the selected language and `PUT /api/v1/user/settings` stores the override server-side.

**Given** a locale change is applied during onboarding,
**When** the new locale renders,
**Then** all visible UI strings update in the same render cycle — no full-page reload, no flash of untranslated content, no scroll position reset.

**Given** the user has entered text in any form field when locale is switched,
**When** locale is applied,
**Then** all previously entered field values are preserved exactly; only labels, placeholders, and error messages re-render in the new locale.

**Given** the new locale introduces longer strings (e.g. German labels),
**When** the layout reflows,
**Then** no CTA button is pushed off-screen and no input overlaps its label.

**Given** a user who has already completed onboarding (`hasFlat === true`),
**When** they navigate to `/onboarding`,
**Then** they are redirected to the Dashboard (`/`) — the gate does not re-trigger.

**Given** a step indicator component,
**When** the onboarding flow is active,
**Then** the current step position (Intro / Step 1 / Step 2) is visible; the step indicator is hidden outside the onboarding flow.

---

### Story 2.3: Onboarding Step 1 — Flat Name

As a first-time user,
I want to name my flat in Step 1 of onboarding,
So that my energy data is associated with a meaningful label I recognize.

**Acceptance Criteria:**

**Given** the user taps "Get Started" on the Intro screen,
**When** Step 1 renders,
**Then** a text input labelled for flat name entry is auto-focused; the "Continue" button is inactive until a non-empty name is entered; `input.value.trim()` is used for the empty check — whitespace-only values do not enable "Continue".

**Given** the user has typed only whitespace characters into the name field,
**When** the component evaluates the field value,
**Then** "Continue" remains disabled; no validation error is shown until the user blurs the field.

**Given** a flat name is entered and "Continue" is tapped,
**When** the step advances,
**Then** the entered name is held in client state and Step 2 renders; no backend call is made yet (all data is submitted together at Step 2 completion).

**Given** Step 1 is active and the user navigates back,
**When** returning to the Intro screen,
**Then** no data is lost and the user can re-enter Step 1 with the previously typed value still present.

**Given** the flat name input on a mobile device and the soft keyboard opens,
**When** the keyboard is fully raised,
**Then** the "Continue" button is still fully visible within the visible viewport without requiring a scroll.

**Story Note (2.3):** Implement keyboard-aware CTA using `position: sticky; bottom: 0` on the CTA container inside a scrollable parent, or listen to `visualViewport.resize` and adjust padding.

**Given** the flat name input,
**When** rendered,
**Then** it uses `border-radius: 12px` (input token), standard text keyboard, and `body-sm` label styling.

---

### Story 2.4: Onboarding Step 2 — Energy Contract & Completion

As a first-time user,
I want to configure my annual energy baseline and initial tariff in Step 2 and submit the complete setup,
So that the app can calculate my costs and budget from the moment I enter my first meter reading.

**Acceptance Criteria:**

**Given** Step 2 renders,
**When** the Annual kWh Baseline section is shown,
**Then** four household-size preset buttons appear (1 person ≈ 1,500 kWh; 2 persons ≈ 2,500 kWh; 3 persons ≈ 3,500 kWh; 4 persons ≈ 4,250 kWh) and a custom numeric input.

**Given** the user taps a preset tile,
**When** tapped,
**Then** the tile enters a selected visual state (highlighted border + checkmark) AND the kWh input is prefilled with the preset value AND focus moves to the kWh input field.

**Given** a preset tile is selected and the user modifies the kWh input (any keystroke that changes the value),
**When** the value changes,
**Then** the tile deselects (returns to default visual state) and the input retains the user-typed value.

**Given** the user manually types a value into the kWh field that exactly matches a preset value,
**When** typed,
**Then** the corresponding tile does NOT auto-select — manual entry is not equivalent to tile selection.

**Given** the Tariff section in Step 2,
**When** rendered,
**Then** fixed monthly base fee and price per kWh are required fields; provider name, contract start date, and contract duration (1 / 6 / 12 / 24 months) are optional.

**Given** Annual kWh Baseline and price per kWh are both entered,
**When** either value changes,
**Then** the planned annual spend field auto-calculates as `(annual_kwh × price_per_kwh) + (monthly_base_fee × 12)`, shows the derivation formula below the field, and remains manually editable by the user.

**Given** the user enters a value in the planned spend field,
**When** entered,
**Then** the field displays an "override active" indicator (e.g. small tag "Custom budget") signalling it is decoupled from the auto-calculation.

**Given** a spend override is active AND the user changes the kWh or tariff values,
**When** the other fields change,
**Then** the spend field is NOT recalculated — the override persists; the user must clear the spend field manually to return to auto-calculation.

**Given** the user clears the planned spend field,
**When** the field loses focus,
**Then** the field returns to showing a computed placeholder (e.g. "~€420 / yr based on current tariff") and the "override active" indicator is removed.

**Given** the user is on Step 2 and taps "Back" to Step 1, then "Continue" again to return,
**When** Step 2 re-renders,
**Then** all previously entered kWh, preset tile selection, tariff fields, and planned spend values are restored exactly as left.

**Story Note (2.4):** Preserve all onboarding wizard state in component state or a lightweight store slice — do not rely on browser history state alone.

**Given** all required fields are valid and "Complete Setup" is tapped,
**When** `POST /api/v1/onboarding` is called,
**Then** the backend creates a `Flats` record (`AnnualKwhBaseline` as `decimal`, `SpikeThreshold` defaulting to `2.0`, `PlannedAnnualSpend` as nullable `decimal` — stored from the user's Step 2 input, null if not provided) and a `Tariffs` record (`EffectiveDate` = today as `datetimeoffset`, all monetary values as `decimal`, locale-neutral); HTTP 201 is returned; `['settings']` TanStack Query key is invalidated (causing `hasFlat` to return `true`); the onboarding gate clears; the user is redirected to `/`.
**And** `OnboardingValidator` (FluentValidation) enforces: flat name non-empty, `AnnualKwhBaseline > 0`, `PricePerKwh > 0`, `MonthlyBaseFee >= 0`; failures return HTTP 400 Problem Details; zero Data Annotation attributes on entity classes.

**Given** the `Flats` EF Core entity and `FlatConfiguration`,
**When** reviewed,
**Then** `FlatConfiguration` defines: `FlatId` (guid PK), `UserId` (FK to `Users`, cascade delete), `Name` (nvarchar), `AnnualKwhBaseline` (decimal), `SpikeThreshold` (decimal, default `2.0`), `PlannedAnnualSpend` (nullable decimal); all mappings via Fluent API only; zero Data Annotation attributes on the `Flat` entity class.

**Given** the `Tariffs` EF Core entity and `TariffConfiguration`,
**When** reviewed,
**Then** `TariffConfiguration` defines the complete schema: `TariffId` (guid PK), `FlatId` (FK to `Flats`, cascade delete), `EffectiveDate` (datetimeoffset), `PricePerKwh` (decimal), `MonthlyBaseFee` (decimal), `ProviderName` (nullable nvarchar), `ContractStartDate` (nullable datetimeoffset), `ContractDurationMonths` (nullable int); index `IX_Tariffs_FlatId_EffectiveDate`; all mappings via Fluent API only; zero Data Annotation attributes on the `Tariff` entity class. This migration creates the full Tariffs schema — Story 4.1 adds application logic only, no further schema changes to this table.

**Given** the tariff price/kWh or monthly base fee fields,
**When** rendered with locale `de-DE` (or any locale using comma as decimal separator),
**Then** the field accepts a comma as the decimal separator (e.g. "0,28") and correctly parses the value; a period is treated as a thousands separator.

**Given** locale is `en-US`,
**When** the user types "3,500" into a numeric field,
**Then** it is accepted as 3500 (thousands separator); a period is the decimal separator.

**Given** the user submits a value that cannot be parsed in the active locale (e.g. "3.5.0"),
**When** the field loses focus,
**Then** an inline validation error reads "Please enter a valid number" in the active locale language.

**Story Note (2.4):** Do not rely on browser default locale for number parsing — use the locale resolved by the i18n context from Story 2.1.

**Given** the "Complete Setup" API call is in-flight,
**When** pending,
**Then** the button displays a loading spinner, is disabled, and shows the label "Saving…".

**Given** the API call fails (network error or 5xx),
**When** the error response is received,
**Then** the inline error "Something went wrong. Your data wasn't saved — please try again." appears below the CTA; the button reverts to "Complete Setup" (enabled); all entered values are preserved — the user can retry without re-entering any data.

---

### Story 2.5: Settings — Flat Name, Annual kWh Baseline & Locale

As a returning user,
I want to update my flat name, annual kWh baseline, and locale from Settings,
So that I can refine my setup at any time without restarting onboarding.

**Acceptance Criteria:**

**Given** the Settings root screen,
**When** rendered,
**Then** a Flat card shows the current flat name with a "kWh Baseline" quick link; a "Language & Region" section is present; an Account section shows a Sign Out action.

**Given** the user taps the flat name on the Flat card,
**When** tapped,
**Then** the name text transforms into an inline editable input pre-filled with the current value; a "Save" action appears adjacent (or via keyboard "Done").

**Given** the user confirms the inline name edit,
**When** "Save" / "Done" is tapped,
**Then** the UI immediately shows the new name (optimistic update) AND `PATCH /api/v1/flats/{flatId}` is sent in the background with body `{ "name": string }`.

**Given** the PATCH request fails,
**When** the error response is received,
**Then** the name reverts to the previous value and the inline error "Couldn't save changes — please try again." appears; the input re-opens with the failed new value so the user does not need to retype.

**Given** the "kWh Baseline" quick link,
**When** tapped,
**Then** the user is navigated to a full edit screen reusing the consumption form from Story 2.4 (preset tiles + kWh input + planned spend) with current values pre-populated; a "Save changes" CTA and a back/cancel affordance are present.

**Given** the user saves baseline changes and `PATCH /api/v1/flats/{flatId}` succeeds,
**When** the response is received,
**Then** the user is returned to the Settings screen with updated values shown and the change takes effect immediately on future budget pressure evaluations.

**Given** the PATCH request for baseline changes fails,
**When** the error response is received,
**Then** the user remains on the edit screen with an error banner; no data is lost and all entered values remain in the form.

**Story Note (2.5 — PATCH endpoint contract):** `PATCH /api/v1/flats/{flatId}` accepts a partial body `{ "name"?: string, "annualKwhBaseline"?: number, "plannedAnnualSpend"?: number | null }`. Omitted fields are not updated. Explicit `null` for `plannedAnnualSpend` clears the override. Returns HTTP 200 with the updated Flat resource (not 204 — client needs the persisted values to confirm the optimistic update).

**Given** the "Language & Region" locale dropdown,
**When** changed to a different locale,
**Then** `PUT /api/v1/user/settings` stores the override server-side; all UI text, numbers, dates, times, and currency immediately re-render in the new locale without a page reload; accessing the app from any other browser subsequently restores the stored locale automatically.

**Given** the user taps "Sign Out" in the Account section,
**When** tapped,
**Then** a confirmation dialog appears with title "Sign out?", body "You'll need to sign in again to access your data.", and two actions: "Cancel" (dismisses, no action) and "Sign out" (destructive styling, proceeds).

**Given** the user confirms sign-out,
**When** confirmed,
**Then** the browser is redirected to `/.auth/logout` (SWA Easy Auth built-in endpoint); no backend code is required — the Sign Out action is a link or `window.location.href` assignment; the user lands on the app sign-in screen after the SWA session is cleared.

**Given** the `/.auth/logout` redirect fails,
**When** failure occurs,
**Then** local session state is cleared anyway and the user is still redirected to the sign-in screen — retaining a broken session is worse than a silent failure.

---

## Epic 3: Meter Reading, KPI Dashboard & Reading History

The irreducible core of the product: a user can enter a meter reading in under 60 seconds and immediately see daily, weekly, and projected monthly cost in euros on the Euro Burn Dashboard. Spike detection and trend chart are included. Reading history with correction is accessible from the chart.

### Story 3.1: Meter Reading Submission — Backend

As a user,
I want to submit a meter reading from my phone and have it persisted immediately,
So that I have a timestamped record of my consumption that the app can use for cost calculations.

**Acceptance Criteria:**

**Given** a `POST /api/v1/flats/{flatId}/readings` request with a valid `kwhValue` (decimal > 0) and `readingDate` (datetimeoffset),
**When** `SubmitReadingFunction.RunAsync` executes,
**Then** `TenantResolver` verifies `flatId` belongs to the resolved `UserId` (HTTP 403 otherwise); a `MeterReading` record is created with `ReadingId` (guid), `FlatId`, `KwhValue` (decimal), `ReadingDate` (datetimeoffset), `IsCorrected = false`, `OriginalKwhValue = null`; HTTP 201 is returned with a `ReadingResponse` record and `Location` header.
**And** server-side processing time is ≤ 2 seconds (NFR-1 Tier 1).

**Given** a reading with a past `readingDate` (retroactive entry per FR-9),
**When** submitted,
**Then** it is stored with the provided past date; cost calculations use the Tariff active on that past date via `TariffResolver.ResolveAsync(flatId, readingDate, ct)`, not the current Tariff.

**Given** `ReadingValidator` (FluentValidation),
**When** `kwhValue ≤ 0` or `readingDate` is missing,
**Then** HTTP 400 Problem Details is returned; no record is created.

**Given** the `MeterReadings` EF Core entity and `MeterReadingConfiguration`,
**When** reviewed,
**Then** all column mappings use Fluent API; `KwhValue` and `OriginalKwhValue` are `decimal`; `ReadingDate` is `datetimeoffset`; zero Data Annotation attributes appear on the entity class.

---

### Story 3.2: KPI Dashboard — Backend Computation

As a user,
I want the server to compute my daily average, weekly average, and projected monthly cost from my meter readings,
So that the dashboard always reflects period-accurate figures in euros.

**Acceptance Criteria:**

**Given** a `GET /api/v1/flats/{flatId}/dashboard` request,
**When** `GetDashboardFunction.RunAsync` executes,
**Then** `KpiCalculator` returns a `DashboardSummary` record with: `DailyAvgKwh`, `WeeklyAvgKwh`, `DailyAvgCost`, `WeeklyAvgCost`, `ProjectedMonthlyCost` (all decimal), `LastReadingDate` (datetimeoffset nullable), `TodayKwh` (decimal), `DailyBudgetKwh` (decimal — `AnnualKwhBaseline ÷ 365`), `SpikeDays` (array of date strings); all cost figures computed via `TariffResolver` using the Tariff active on each period's date.
**And** response time ≤ 2 seconds for a Flat with up to 2 years of Readings.

**Given** a Flat with no Meter Readings yet,
**When** `GET /api/v1/flats/{flatId}/dashboard` is called,
**Then** HTTP 200 is returned with all KPI values as `0` and `LastReadingDate` as `null` — no error.

**Given** a Flat with readings spanning two different Tariff periods,
**When** the dashboard is computed,
**Then** each reading's cost uses the Tariff active on its `ReadingDate`, not the current Tariff.

**Given** all JSON responses from this endpoint,
**When** inspected,
**Then** all field names are camelCase; datetimes include explicit timezone offset; decimal values are JSON numbers.

---

### Story 3.3: KPI Dashboard Frontend — Euro Burn Design & Grid

As a user,
I want to see my KPI figures on the Euro Burn Dashboard with the gradient background encoding my consumption against my daily budget,
So that my energy cost is visible at a glance and the ambient background tells me how I am doing before I read a single number.

**Acceptance Criteria:**

**Given** an authenticated user on the Dashboard with readings present,
**When** the page loads,
**Then** `useDashboard` (TanStack Query key: `['dashboard', flatId]`) fetches the dashboard; four KPI tiles render in a 2×2 grid on phone / 4-across on tablet, each as a glass card (`backdrop-filter: blur(20px) saturate(180%)`, `background: rgba(255,255,255,0.08)`, `border: 1px solid rgba(255,255,255,0.14)`, `border-radius: 18px`, `padding: 16px 18px`).

**Given** the four KPI tiles,
**When** rendered,
**Then** Tile 1 (Daily avg): headline `{DailyAvgKwh} kWh` at display-kpi (22px/700/−0.02em), subline `€{DailyAvgCost}` at body-sm; budget delta line reads `↓ X kWh under budget` in `accent-under-budget` / `↑ X kWh over budget` in `accent-over-budget` / `— at daily budget` at label-caps size; tertiary caption reads "based on {AnnualKwhBaseline} kWh/yr". Tiles 2–4 follow the same paired kWh+€ structure for weekly and projected monthly figures.

**Given** `TodayKwh` and `DailyBudgetKwh` from the dashboard response,
**When** the Euro Burn Gradient Background renders,
**Then** gradient stop positions shift based on the consumption-to-budget ratio: ≤ −50% clips to the cool edge; ≥ +50% clips to the warm edge; angle is 160deg on phone, 140deg on tablet; no midpoint marker is added.

**Given** the Dashboard with no readings (cold open),
**When** rendered,
**Then** all KPI tiles show `—` (dashes); gradient renders at the neutral midpoint; "Last read:" shows "never"; the Enter Reading CTA is prominently visible.

**Given** `isLoading === true` on first fetch,
**When** the dashboard renders,
**Then** skeleton placeholders appear in the KPI tile positions — no content flash.

---

### Story 3.4: Enter Reading CTA, Bottom Sheet & Immediate Dashboard Update

As a user,
I want to tap Enter Reading, type the meter value, tap Save, and immediately see the KPI tiles update,
So that the core meter-reading loop is fast, frictionless, and confirmatory.

**Acceptance Criteria:**

**Given** the Dashboard on phone,
**When** rendered,
**Then** the Enter Reading CTA is a full-width pill (`border-radius: 9999px`, `padding: 16px 24px`, `background: rgba(255,255,255,0.10)`, `backdrop-filter: blur(20px) saturate(180%)`, `border: 1.5px solid rgba(255,255,255,0.40)`) with label "Enter Reading" in `body` type role.

**Given** the Dashboard on tablet (≥768px),
**When** rendered,
**Then** the CTA is a 44×44px compact button (`border-radius: 14px`) in the content header top-right showing the Lucide `Zap` icon (20×20px) only — no text label.

**Given** the Enter Reading CTA is tapped,
**When** the bottom sheet opens,
**Then** it slides up from the bottom with a drag handle; the kWh input is auto-focused with `inputmode="numeric"`; the date/time field is pre-filled with the current timestamp; Save is inactive until `kwhValue > 0`; the hint "Date and time will be saved with your reading." appears below the date field.

**Given** a valid kWh value is entered and Save is tapped,
**When** `useSubmitReading` mutation calls `POST /api/v1/flats/{flatId}/readings` and succeeds,
**Then** (1) the sheet closes, (2) TanStack Query key `['dashboard', flatId]` is invalidated triggering an immediate refetch, (3) all four KPI tiles animate with a count animation to their new values; "Last read:" updates to the submitted reading's date/time. With `prefers-reduced-motion: reduce` active, values update immediately with no animation.

**Given** the entered kWh value is lower than the last recorded reading,
**When** the value is typed,
**Then** the inline warning "Lower than your last reading ({lastKwhValue} kWh) — is this correct?" appears below the input; Save remains active.

**Given** the POST returns an error,
**When** the mutation fails,
**Then** the sheet stays open; the typed value is preserved; "Couldn't save — try again." appears near the Save button.

**Given** the bottom sheet while open,
**When** focus is managed,
**Then** focus is trapped within the sheet; closing returns focus to the CTA; all interactive elements meet 44×44pt minimum tap targets; validation messages use `aria-live="polite"`.

---

### Story 3.5: Trend Chart & Spike Detection

As a user,
I want to see a bar chart of my daily consumption history with spike days highlighted in amber,
So that I can immediately spot unusual consumption and know to check the Insights tab for context.

**Acceptance Criteria:**

**Given** `KpiCalculator` computing the dashboard,
**When** spike detection runs,
**Then** a day is flagged as a spike when its consumption exceeds `Flat.SpikeThreshold × 7-day rolling average` (default `2.0`); flagged dates are returned in `SpikeDays`; `SpikeThreshold` is stored as `decimal` on the `Flats` entity and is user-configurable per Flat.

**Given** `TrendChart.tsx`,
**When** rendered with dashboard data,
**Then** a recharts bar chart displays daily consumption bars; spike-day bars render with `fill: #f59e0b` (accent-spike amber); non-spike bars use the standard subdued color; the chart card has standard glass card treatment (`border-radius: 18px`, `backdrop-filter`).

**Given** a spike bar is tapped,
**When** the interaction occurs,
**Then** no banner or notification is generated on the Dashboard — full spike context is accessible only from the Insights tab.

**Given** the trend chart card header,
**When** rendered,
**Then** a 20×20px clock/list icon in `text-secondary` is positioned top-right with a minimum 44×44pt tap target and explicit `aria-label`; tapping it opens the Reading History bottom sheet.

---

### Story 3.6: Reading History — View & Correction

As a user,
I want to view all my past meter readings and correct any that were entered incorrectly,
So that my consumption history is accurate and the original value is preserved for reference.

**Acceptance Criteria:**

**Given** `GET /api/v1/flats/{flatId}/readings`,
**When** called,
**Then** `GetReadingHistoryFunction` returns all Readings in reverse-chronological order as `ReadingResponse` records (`ReadingId`, `KwhValue` decimal, `ReadingDate` datetimeoffset, `IsCorrected` bool, `OriginalKwhValue` nullable decimal); HTTP 200; ≤ 2s response time.

**Given** tapping the clock/list icon in the trend chart card header,
**When** the Reading History bottom sheet opens,
**Then** `useReadingHistory` (key: `['readings', flatId]`) fetches the list; entries render in reverse-chronological order with date/time and kWh value; entries with `IsCorrected = true` show a "corrected" note inline.

**Given** a reading entry is tapped,
**When** the edit form opens,
**Then** kWh value and date/time fields are pre-populated with current values; the same numeric input pattern applies (`inputmode="numeric"`, 44pt targets, focus trap).

**Given** the edit form is saved with a new kWh value,
**When** `PATCH /api/v1/flats/{flatId}/readings/{readingId}` is called,
**Then** `KwhValue` is updated; `IsCorrected = true`; `OriginalKwhValue` is set to the previous value (not overwritten on subsequent corrections); HTTP 200; TanStack Query keys `['readings', flatId]` and `['dashboard', flatId]` are invalidated.

**Given** the Reading History sheet fails to load,
**When** the fetch errors,
**Then** the sheet shows "Couldn't load reading history." with a Retry link; the sheet stays open.

---

## Epic 4: Tariff Management

A user can maintain a full tariff history with effective dates, optional contract periods, and forward-dated tariff changes. All cost figures use the tariff active on the date of consumption, not the current tariff.

### Story 4.1: Tariff CRUD Backend — List, Create & Contract Lock Enforcement

As a user,
I want to add new tariff entries, list my tariff history, and have the app prevent me from editing price fields on active contracts,
So that my tariff history is accurate and locked rates cannot be accidentally changed.

**Acceptance Criteria:**

**Given** `GET /api/v1/flats/{flatId}/tariffs`,
**When** called,
**Then** `GetTariffsFunction` returns all Tariff entries in descending effective-date order as `TariffResponse` records (`TariffId`, `EffectiveDate` datetimeoffset, `PricePerKwh` decimal, `MonthlyBaseFee` decimal, `ProviderName` nullable string, `ContractStartDate` nullable datetimeoffset, `ContractDurationMonths` nullable int, `IsLocked` bool derived from: ContractStartDate is not null AND in the past AND ContractDurationMonths is not null); HTTP 200; ≤ 2s response time.

**Given** `POST /api/v1/flats/{flatId}/tariffs` with a valid request body,
**When** `CreateTariffFunction.RunAsync` executes,
**Then** a new `Tariff` record is created with all fields stored locale-neutrally (`EffectiveDate` as datetimeoffset, `PricePerKwh` and `MonthlyBaseFee` as `decimal`); HTTP 201 with `Location` header; ≤ 2s response time.
**And** a future `EffectiveDate` is accepted without affecting any past cost calculations (FR-12).

**Given** `TariffValidator` (FluentValidation),
**When** `PricePerKwh ≤ 0`, `MonthlyBaseFee < 0`, or `EffectiveDate` is missing,
**Then** HTTP 400 Problem Details is returned; no record is created.

**Given** `PATCH /api/v1/flats/{flatId}/tariffs/{tariffId}` attempting to update `PricePerKwh` or `MonthlyBaseFee`,
**When** the Tariff entry has `ContractStartDate` in the past AND `ContractDurationMonths` is not null,
**Then** HTTP 422 Problem Details with `type: "tariff-locked"` is returned; the price fields are not modified.
**And** non-price fields (`ProviderName`, `ContractStartDate`, `ContractDurationMonths`) are updated successfully regardless of lock status.

**Given** the `Tariffs` EF Core entity and `TariffConfiguration`,
**When** reviewed,
**Then** all column mappings use Fluent API; `PricePerKwh` and `MonthlyBaseFee` are `decimal`; `EffectiveDate` and `ContractStartDate` are `datetimeoffset`; index `IX_Tariffs_FlatId_EffectiveDate` exists; zero Data Annotation attributes on the entity class.

---

### Story 4.2: Tariff Management UI — List & Add Form in Settings

As a user,
I want to see all my tariff entries in Settings and add new ones including future-dated changes,
So that I can track my full contract history and pre-enter upcoming rate changes.

**Acceptance Criteria:**

**Given** the user navigates to Settings → Flat card → Tariff quick link,
**When** the Tariff settings screen renders,
**Then** `useTariffs` (TanStack Query key: `['tariffs', flatId]`) fetches the tariff list; all entries render in descending effective-date order showing effective date, price per kWh, monthly base fee, and provider name (if present); an "Add Tariff" button is visible.

**Given** a tariff entry in the list,
**When** rendered,
**Then** the effective date is formatted via `Intl.DateTimeFormat` for the active locale; all currency values are formatted via `Intl.NumberFormat` with the correct symbol for the active locale.

**Given** the "Add Tariff" button is tapped,
**When** `TariffForm.tsx` renders,
**Then** it shows: effective date (required, pre-filled today), price per kWh (required, `inputmode="numeric"`), monthly base fee (required, numeric), provider name (optional, text), contract start date (optional), contract duration dropdown (optional: 1 / 6 / 12 / 24 months); Save is inactive until all required fields are valid.

**Given** the form is submitted with valid data,
**When** `useCreateTariff` mutation calls `POST /api/v1/flats/{flatId}/tariffs`,
**Then** on success: the form closes; TanStack Query keys `['tariffs', flatId]` and `['dashboard', flatId]` are invalidated; the new entry appears in the list.

**Given** a future-dated tariff entry in the list,
**When** displayed,
**Then** it is clearly labelled as upcoming (e.g., "From {date}") and appears at the top of the list.

---

### Story 4.3: Tariff Lock Indicator & Planned Annual Spend Settings

As a user,
I want price fields on active contracts to be visibly locked and uneditable, and I want to update my planned annual spend near the tariff configuration,
So that locked rates cannot be accidentally modified and my budget target is easy to find.

**Acceptance Criteria:**

**Given** a tariff entry with an active contract period,
**When** its edit form opens,
**Then** `PricePerKwh` and `MonthlyBaseFee` render as read-only and visually greyed out, each with an inline lock icon (`accent-tariff-locked` #d97706) and the label "Locked — contract active until {month year}"; non-price fields remain fully editable; the lock state is immediately visible on form open — no dialog or tap-to-reveal.

**Given** a PATCH request with modified price fields on a locked tariff submitted directly to the API,
**When** the backend receives it,
**Then** HTTP 422 Problem Details with `type: "tariff-locked"` is returned — lock enforced server-side regardless of UI state.

**Given** the Tariff settings screen,
**When** rendered below the tariff list,
**Then** a "Planned Annual Spend" field shows the current value (`Flats.PlannedAnnualSpend` decimal); an edit control allows the user to update it; saving updates the value and takes effect immediately on future budget pressure alert evaluations (FR-7, FR-37).

**Given** the Annual kWh Baseline or tariff price per kWh values,
**When** shown alongside the Planned Annual Spend field,
**Then** helper text displays the auto-derived value: `({AnnualKwhBaseline} kWh × {PricePerKwh} €/kWh) + ({MonthlyBaseFee} × 12)` so the user can compare it against their manually set target.

**Given** all monetary values in the tariff forms,
**When** displayed,
**Then** they are formatted via `Intl.NumberFormat` for the active locale; stored values in the database remain locale-neutral fixed-decimal.

---

## Epic 5: Multi-Flat Management & Flat Structure

A user can create and manage multiple flats, switch between them via the header, and define the four-level physical hierarchy (Flat → Rooms → Power Points → Devices) for each. Flat deletion is permanent and requires type-to-confirm. This epic is the R2 foundation — the Flat Structure it produces is consumed by Smart Plug Import (Epic 6) and Decomposition (Epic 7).

### Story 5.1: Multi-Flat Backend — Create, List & Cascade Delete

As a user,
I want to create additional flats, list all my flats, and permanently delete a flat along with all its data,
So that I can manage multiple dwellings independently with complete data isolation between them.

**Acceptance Criteria:**

**Given** `GET /api/v1/flats`,
**When** called by an authenticated user,
**Then** `GetFlatsFunction` returns all Flats belonging to the resolved `UserId` as `FlatSummary` records (`FlatId`, `Name`, `AnnualKwhBaseline` decimal, `SpikeThreshold` decimal, `PlannedAnnualSpend` nullable decimal); HTTP 200; ≤ 2s; Flats belonging to other users are never returned.

**Given** `POST /api/v1/flats` with `{ name, annualKwhBaseline, plannedAnnualSpend }`,
**When** `CreateFlatFunction.RunAsync` executes,
**Then** a new `Flat` record is created scoped to the resolved `UserId`; `SpikeThreshold` defaults to `2.0`; HTTP 201 with `Location: /api/v1/flats/{flatId}`; no Tariff entries are created — the caller must add one via the Tariff endpoint.

**Given** `DELETE /api/v1/flats/{flatId}`,
**When** `DeleteFlatFunction.RunAsync` executes,
**Then** `TenantResolver` verifies `flatId` belongs to the resolved `UserId` (HTTP 403 otherwise); the Flat and all associated data are permanently deleted: all `MeterReadings`, `Tariffs`, `SmartPlugDailyData`, `SmartPlugIntervalData`, `ImportJobs`, `Rooms` (and their `PowerPoints`, `Devices`), `InsightRuns`, `Insights`; HTTP 204; no orphaned records remain.
**And** cascade delete is enforced at the database level via `OnDelete(DeleteBehavior.Cascade)` in Fluent API on all FK relationships from `Flats` — not application-side loops.

**Given** `GET /api/v1/user/settings` and `PUT /api/v1/user/settings`,
**When** called,
**Then** the response includes `activeFlatId` (nullable guid); PUT accepts an `activeFlatId` field and persists it to `Users.ActiveFlatId` (new nullable column added via EF Core migration).

---

### Story 5.2: Flat Switcher, Add Flat & Deletion UI

As a user,
I want to switch between my flats from the app header, create additional flats, and delete a flat by typing its name to confirm,
So that I can move between dwellings quickly and remove a flat with friction appropriate to an irreversible action.

**Acceptance Criteria:**

**Given** the app header on any surface,
**When** rendered,
**Then** the active Flat's name is displayed as a tappable element; tapping it opens the flat switcher dropdown listing all Flats plus an "Add flat" option at the bottom; the active Flat is visually distinguished.

**Given** a different Flat is selected from the dropdown,
**When** the selection is made,
**Then** `PUT /api/v1/user/settings` is called with the new `activeFlatId`; all TanStack Query keys scoped to the previous `flatId` are invalidated; all surfaces reload data for the newly selected Flat; the header Flat name updates immediately.

**Given** the browser is closed and reopened,
**When** the app loads,
**Then** `GET /api/v1/user/settings` returns the stored `activeFlatId`; the app initialises with that Flat active without requiring re-selection (FR-20).

**Given** "Add flat" is tapped in the switcher,
**When** the add flat form opens,
**Then** the user can enter a flat name (required) and Annual kWh Baseline using the same preset + custom pattern from onboarding; submitting calls `POST /api/v1/flats` and on success switches to the new Flat; a prompt guides the user to Settings → Tariff to add an initial tariff.

**Given** the user navigates to Settings → Account and taps "Delete Flat",
**When** `FlatDeleteConfirm.tsx` opens,
**Then** a text input shows the prompt `Type "{flatName}" to delete`; the Delete button is disabled until the typed value matches the Flat name exactly (case-sensitive); tapping Delete calls `DELETE /api/v1/flats/{flatId}` and on 204: switches to another available Flat or redirects to onboarding if no Flats remain.

---

### Story 5.3: Flat Structure Backend — Rooms, Power Points & Devices

As a user,
I want the server to store and return the four-level physical hierarchy of my flat including Smart Plug and Smart Power Strip assignments,
So that imported smart plug data can be correctly attributed and Decomposition can group consumption by room and device.

**Acceptance Criteria:**

**Given** EF Core migrations for `Rooms`, `PowerPoints`, and `Devices`,
**When** reviewed,
**Then** `RoomConfiguration` defines `RoomId` (guid PK), `FlatId` (FK, cascade delete), `Name`, `SortOrder` (int). `PowerPointConfiguration` defines `PowerPointId` (guid PK), `RoomId` (FK, cascade delete), `Name`, `PlugId` (nullable nvarchar — assigned smart plug identifier; never derived from file metadata). `DeviceConfiguration` defines `DeviceId` (guid PK), `PowerPointId` (FK, cascade delete), `Name`, `Type`, `Manufacturer`, `Model`, `PurchaseDate` (nullable datetimeoffset), `ConsumptionApproach` (enum: None/EuLabel/SelfMeasured), `EuLabelClass` (nullable), `EuAnnualKwh` (nullable decimal), `SelfMeasuredKwh` (nullable decimal), `SelfMeasuredPeriod` (nullable enum: Daily/Weekly). Zero Data Annotation attributes on any entity class.

**Given** `GET /api/v1/flats/{flatId}/structure`,
**When** called,
**Then** `GetFlatStructureFunction` returns the full nested hierarchy as a `FlatStructureResponse` record (Flat → Rooms → PowerPoints → Devices); each PowerPoint includes `plugId` (nullable) and a `hasDefaultTemplate` flag (true when no Rooms exist); HTTP 200; ≤ 2s; tenant-scoped.

**Given** `PUT /api/v1/flats/{flatId}/structure` with a complete structure payload,
**When** `UpdateFlatStructureFunction.RunAsync` executes,
**Then** the full structure is replaced atomically within a transaction (delete-and-reinsert); a `plugId` on a PowerPoint is stored as provided — never derived from file metadata; HTTP 200 with the updated structure; ≤ 2s.

**Given** a PUT payload where a `plugId` is assigned to more than one PowerPoint in the same Flat,
**When** validated,
**Then** HTTP 422 Problem Details is returned — each Smart Plug may be assigned to exactly one PowerPoint.

---

### Story 5.4: Flat Structure Editor Frontend

As a user,
I want to define and edit the rooms, power points, and devices in my flat using a structured editor pre-populated with a default room template,
So that I have the physical hierarchy ready before importing smart plug data.

**Acceptance Criteria:**

**Given** the Flat Structure editor opens for a Flat with no existing structure (`hasDefaultTemplate: true`),
**When** rendered,
**Then** `FlatStructureEditor.tsx` pre-populates five default Room entries: living room, bedroom, kitchen, bathroom, hallway; a prompt reads "These rooms were pre-filled — edit names or add your own."; no database write occurs until the user saves.

**Given** the editor with rooms present,
**When** rendered,
**Then** each Room shows its name (editable inline) and expands to reveal its PowerPoints; each PowerPoint shows its name, an optional Smart Plug / Smart Power Strip `plugId` assignment field, and its Devices; "Add Room", "Add Power Point", and "Add Device" controls are available at the appropriate hierarchy levels.

**Given** the user assigns a Smart Power Strip to a PowerPoint,
**When** saved,
**Then** Strip Outlet rows (one per device slot) appear beneath the PowerPoint for Device assignment; the PowerPoint's `plugId` is set at the strip level (FR-21).

**Given** a Device row in the editor at this stage,
**When** rendered,
**Then** `Name`, `Type`, `Manufacturer`, `Model` fields are editable; `ConsumptionApproach` defaults to `None`; an inline note reads "Configure consumption profile to include this device in Decomposition" — the EU label / self-measured entry UI is added in Epic 6 Story 6.5.

**Given** the user saves the structure,
**When** `useUpdateFlatStructure` calls `PUT /api/v1/flats/{flatId}/structure`,
**Then** on success: TanStack Query key `['flat-structure', flatId]` is invalidated; a success confirmation is shown.

**Given** a `plugId` conflict (same plug assigned to two PowerPoints),
**When** the user attempts to save,
**Then** an inline validation error appears: "This plug is already assigned to another power point"; Save is disabled until resolved.

---

## Epic 6: Smart Plug Import & Device Registry

A user can upload Eve Home Excel and Meross CSV exports, parsed into a unified daily kWh timeline, gap-interpolated, and reconciled against the main meter. Devices can be registered with EU label or self-measured consumption profiles.

### Story 6.1: Import Pipeline Infrastructure — Upload, Job Tracking & Blob Trigger

As a user,
I want to upload a smart plug file and get immediate confirmation it was received with a job ID to track processing,
So that I am not blocked waiting for the file to process and can navigate freely while it runs in the background.

**Acceptance Criteria:**

**Given** EF Core migrations for `ImportJobs`, `SmartPlugDailyData`, and `SmartPlugIntervalData`,
**When** reviewed,
**Then** `ImportJobConfiguration` defines `ImportJobId` (guid PK), `FlatId` (FK, cascade delete), `Status` (enum: Pending/Processing/Complete/Failed), `CreatedAt` (datetimeoffset), `CompletedAt` (nullable datetimeoffset), `ErrorCategory` (nullable enum: DataUnreadable/ProcessingFailed/ServiceUnavailable). `SmartPlugDailyDataConfiguration` defines `Id` (guid PK), `PlugId` (nvarchar), `FlatId` (FK, cascade delete), `Date` (date), `KwhValue` (decimal), `IsInterpolated` (bool); unique index on `(FlatId, PlugId, Date)`. `SmartPlugIntervalDataConfiguration` defines `Id` (guid PK), `PlugId` (nvarchar), `FlatId` (FK, cascade delete), `Timestamp` (datetimeoffset), `WhValue` (decimal); index on `(FlatId, PlugId, Timestamp)`. Zero Data Annotation attributes on any entity class.

**Given** `POST /api/v1/flats/{flatId}/imports` with a multipart file upload,
**When** `UploadFunction.RunAsync` executes,
**Then** an `ImportJob` record is created with `Status = Pending`; the file is written to Azure Blob Storage at `smart-plug-imports/{userId}/{flatId}/{importJobId}.{ext}` using Managed Identity; HTTP 202 is returned with `{ importJobId }`; server-side time to 202 response is ≤ 2s.

**Given** `GET /api/v1/flats/{flatId}/imports/{jobId}`,
**When** called,
**Then** `GetImportStatusFunction` returns `{ importJobId, status, createdAt, completedAt, errorCategory, gapNotifications? }`; HTTP 200; tenant-scoped.

**Given** the blob trigger fires on `ProcessImportFunction`,
**When** executed,
**Then** it sets `Status = Processing`; reads the blob; detects file type from extension (`.xlsx` → Eve Home, `.csv` → Meross); routes to the appropriate parser; on unhandled exception: `Status = Failed`, `ErrorCategory = ProcessingFailed`; on storage outage: `ErrorCategory = ServiceUnavailable`; on unreadable/corrupt file: `ErrorCategory = DataUnreadable`; raw exception messages are logged to Application Insights only — never returned to the user.

**Given** import error categorization (FR-28),
**When** `ImportJob.ErrorCategory` is set,
**Then** the frontend maps it to exactly one user-facing message: `DataUnreadable` → "Data cannot be read."; `ProcessingFailed` → "Processing failed — try again."; `ServiceUnavailable` → "Service temporarily unavailable — try again later."

---

### Story 6.2: Eve Home Excel Parser

As a user,
I want to upload an Eve Home `.xlsx` export and have it parsed correctly into a daily kWh timeline with raw interval rows retained,
So that the app can compute daily consumption and later detect standby offenders using the high-resolution interval data.

**Acceptance Criteria:**

**Given** a valid Eve Home `.xlsx` file (single sheet `Gesamtverbrauch`, reverse-chronological ~10-minute interval rows in Wh),
**When** `EveHomeParser.cs` processes it,
**Then** device name is extracted from cell A1; Room name from cell A2 (informational only, not used for structure matching); rows with empty values are skipped; all remaining rows are parsed as `(Timestamp: datetimeoffset, WhValue: decimal)`; timestamps are treated as local time and are **not** converted to UTC — conversion would corrupt daily aggregation boundaries by shifting interval assignments across midnight.

**Given** two overlapping Eve Home exports for the same `plugId`,
**When** both are imported,
**Then** rows are deduplicated by timestamp before daily aggregation — duplicate timestamps produce a single daily total, not a doubled value (FR-24).

**Given** the parsed interval rows,
**When** written to the database,
**Then** all rows are inserted into `SmartPlugIntervalData` (`PlugId`, `FlatId`, `Timestamp`, `WhValue`); daily totals are aggregated and upserted into `SmartPlugDailyData` (`KwhValue` decimal, `IsInterpolated = false`); existing rows for the same `(FlatId, PlugId, Date)` are updated, not duplicated.

**Given** `EveHomeParserTests.cs` in `api.Tests/Features/SmartPlugImport/`,
**When** run,
**Then** tests cover: valid file produces correct daily totals; UTC conversion is absent (local-time daily boundaries preserved); overlapping exports produce deduplicated totals; rows with empty values are skipped; device name extracted from A1.

---

### Story 6.3: Meross CSV Parser

As a user,
I want to upload a Meross `.csv` export and have it parsed correctly, with zero-consumption days stored as valid data rather than treated as gaps,
So that my Meross plug timeline is accurate and days with no consumption are not incorrectly interpolated.

**Acceptance Criteria:**

**Given** a valid Meross `.csv` file (UTF-8 with optional BOM, tab-separated with per-value comma prefix, filename `Power Monitor Day Data - {device_name} - {YYYYMMDD}.csv`),
**When** `MerossParser.cs` processes it,
**Then** device name is extracted from the filename using the specified pattern; UTF-8 BOM is stripped before parsing; trailing whitespace and empty rows are removed; each remaining row is parsed as `(Date: date, KwhValue: decimal)`.

**Given** a Meross row with `Power Consumption-(kWh)` value of `0.000`,
**When** parsed,
**Then** it is stored in `SmartPlugDailyData` with `KwhValue = 0` and `IsInterpolated = false` — it is **not** treated as a missing date subject to gap detection or interpolation (FR-25).

**Given** the parsed Meross rows,
**When** written to the database,
**Then** rows are upserted into `SmartPlugDailyData` only — no `SmartPlugIntervalData` rows are created for Meross imports; standby detection is not available for this format.

**Given** `MerossParserTests.cs` in `api.Tests/Features/SmartPlugImport/`,
**When** run,
**Then** tests cover: valid file produces correct daily totals; UTF-8 BOM file parses correctly; zero-value row stored as valid data not flagged as a gap; device name extracted from filename; empty rows ignored.

---

### Story 6.4: Gap Detection, Interpolation & Main Meter Reconciliation

As a user,
I want gaps in my smart plug timeline to be automatically filled by interpolation with a notification, and total attributed consumption to always be reconciled against my main meter,
So that my decomposition data is complete and never over-counts my actual usage.

**Acceptance Criteria:**

**Given** `InterpolationEngine.cs` processing a plug's daily timeline after parsing,
**When** missing dates are detected within the plug's covered period (first to last date in the import),
**Then** gap date ranges are recorded for notification; missing days are filled by linear interpolation between surrounding anchor values, capped at the 7-day per-day average preceding the gap; filled rows are inserted into `SmartPlugDailyData` with `IsInterpolated = true`; zero-valued rows are **not** treated as gaps — only absent dates trigger interpolation (FR-26).

**Given** dates outside the plug's covered period (before the first export date or after the last),
**When** gap detection runs,
**Then** those periods are not treated as gaps — gap detection applies only within the covered range of the export.

**Given** `InterpolationEngine` detects one or more gaps,
**When** `ImportJob` is completed,
**Then** a `gapNotifications` payload is stored on the `ImportJob` listing affected `plugId` and date ranges; `GET /api/v1/flats/{flatId}/imports/{jobId}` returns this payload for frontend display: "Gap detected: {date range}. Missing days have been interpolated."

**Given** `ReconciliationEngine.cs` running after all plugs for a period are processed,
**When** it computes the period total,
**Then** `Residual = MainMeterTotal − sum(SmartPlugDailyData.KwhValue for all plugs in the period)`; the invariant `attributed + Residual = MainMeterTotal` holds within ±0.1 kWh for clean periods and within ±1.0 kWh for periods containing any interpolated values (FR-27); if attributed kWh exceeds the main meter total, `ImportJob.Status = Failed` with `ErrorCategory = ProcessingFailed`.

**Given** `InterpolationEngineTests.cs` and `ReconciliationEngineTests.cs`,
**When** run,
**Then** interpolation tests cover: gap within covered period is filled; 7-day cap is applied; zero-value days not flagged; periods outside covered range unaffected. Reconciliation tests cover: attributed + residual = meter total within tolerance for clean and interpolated periods; over-attribution triggers failure.

---

### Story 6.5: Device Registry — EU Label & Self-Measured Consumption

As a user,
I want to configure a consumption profile for each device using either its EU energy label or a self-measured average,
So that devices without a direct smart plug contribute an estimated baseline to the Decomposition view.

**Acceptance Criteria:**

**Given** a Device with `ConsumptionApproach = None` in the Flat Structure editor,
**When** the user taps "Configure consumption profile",
**Then** a Choice Step presents two mutually exclusive selector cards: "EU energy label" and "Self-measured average"; selecting one reveals only that approach's fields; the other approach's fields are hidden, not disabled (UX-DR17).

**Given** "EU energy label" is selected,
**When** the EU label fields render,
**Then** an energy class rating field (text) and an annual kWh field (`inputmode="numeric"`) are shown; saving updates `Device.ConsumptionApproach = EuLabel`, `EuLabelClass`, and `EuAnnualKwh` (decimal); the derived daily estimate (`EuAnnualKwh ÷ 365`) is displayed below the field for confirmation (FR-30).

**Given** "Self-measured average" is selected,
**When** the self-measured fields render,
**Then** a Daily/Weekly toggle is shown with "Daily" pre-selected; the kWh input label updates instantly on toggle switch ("kWh per day" / "kWh per week"); saving updates `Device.ConsumptionApproach = SelfMeasured`, `SelfMeasuredKwh` (decimal), and `SelfMeasuredPeriod` (Daily/Weekly) (FR-31).

**Given** saving a consumption profile,
**When** `PUT /api/v1/flats/{flatId}/structure` is called,
**Then** HTTP 200 is returned; TanStack Query key `['flat-structure', flatId]` is invalidated; all decimal values are stored as `decimal` in the database — no float or double.

---

### Story 6.6: Import UI — Upload Zone, File List & Progress Card

As a user,
I want to select or drag smart plug files, see auto-detected file types with device associations, and track processing progress from the Decomposition tab,
So that I can upload exports in a few taps and continue using the app while they process in the background.

**Acceptance Criteria:**

**Given** the Import surface on phone,
**When** rendered,
**Then** `FileUploadZone.tsx` shows a file picker button; tapping it opens the native file chooser accepting `.xlsx` and `.csv`; drag-and-drop is not available on phone.

**Given** the Import surface on desktop or tablet,
**When** rendered,
**Then** the upload zone also accepts drag-and-drop; dragging valid files onto the zone triggers the same file-selection flow as the file picker.

**Given** one or more files are selected,
**When** the file list renders,
**Then** each `FileListItem.tsx` shows: filename, auto-detected type label ("Eve Home" for `.xlsx`, "Meross" for `.csv`), and a device association dropdown populated from the Flat Structure PowerPoints with assigned `plugId` values; if the filename contains a known device name (case-insensitive match), that device is auto-pre-selected; "Upload Files" is active only when all files have an association.

**Given** "Upload Files" is tapped,
**When** `useUploadImport` calls `POST /api/v1/flats/{flatId}/imports` for each file,
**Then** on 202: a Progress Card (`ImportProgressCard.tsx`) appears on the Decomposition tab — amber-tinted glass card (`residual-tint` overlay) with status label and description; the app remains fully navigable.

**Given** `useImportJobStatus` polling `GET .../imports/{jobId}` every 3 seconds,
**When** status reaches `Complete`,
**Then** the Progress Card disappears and TanStack Query key `['decomposition', flatId, ...]` is invalidated; when status reaches `Failed`: the Progress Card shows the categorized error message with a Retry action.

**Given** a gap notification in the completed job response,
**When** displayed,
**Then** the message reads "Gap detected: {date range}. Missing days have been interpolated." — shown as a non-blocking notification.

**Given** `ErrorCategory = DataUnreadable` on a file row,
**When** displayed,
**Then** the file row shows an `accent-error` left border and "Data cannot be read." inline; the user can remove the file and try another.

---

## Epic 7: Consumption Decomposition

A user can view their energy consumption broken down by room and device for any selectable period with smart plug data. The Residual (unattributed kWh) is always shown and never suppressed, including when zero. Periods without smart plug data show an explicit unavailable state with an import CTA.

### Story 7.1: Decomposition Backend — Engine, API & Cost Attribution

As a user,
I want the app to compute my consumption breakdown by room and device for any date range I choose, with attributed costs using the tariff that was active during each day,
So that I get accurate per-room and per-device kWh and cost figures that are consistent with my billing history.

**Acceptance Criteria:**

**Given** `DecompositionModels.cs` in `api/Features/Decomposition/`,
**When** reviewed,
**Then** `DecompositionResponse` (C# record) contains: `Period` (startDate, endDate); `TotalKwh` (decimal); `TotalCost` (decimal); `IsUnavailable` (bool); `HasInterpolatedData` (bool); `Residual` (`ResidualItem`: kWh, cost, always present); `Rooms` (list of `RoomDecomposition`: roomId, roomName, kWh, cost, devices); each `DeviceDecomposition` has: deviceId, name, kWh, cost, `approach` (Measured/EuLabel/SelfMeasured), `isSmartStrip` (bool), `subDevices?` (list with deviceId, name, kWh, cost, isConfigured, isUnconfigured). No Data Annotation attributes on any class.

**Given** `DecompositionEngine.cs` processes a period with SmartPlugDailyData present,
**When** `ComputeAsync(flatId, startDate, endDate, ct)` is called,
**Then** for each Device with a linked `plugId`: kWh = sum of `SmartPlugDailyData.KwhValue` for that plugId in the period, `approach = Measured`; for each Device with `ConsumptionApproach = EuLabel`: daily estimate = `EuAnnualKwh ÷ 365`, projected across period days, `approach = EuLabel`; for each Device with `ConsumptionApproach = SelfMeasured`: daily kWh from `SelfMeasuredKwh`/`SelfMeasuredPeriod`, `approach = SelfMeasured`; devices with no approach and no plug contribute 0; devices are grouped by room; `TotalKwh` = sum of all MainMeter daily readings for period.

**Given** a Smart Power Strip device,
**When** its sub-devices are attributed,
**Then** the Strip's measured total kWh (from SmartPlugDailyData for the strip's plugId) is split among sub-devices: configured sub-devices receive proportional shares based on their own smart plug measurements where available, otherwise equal-share of the unattributed strip remainder; unconfigured sub-devices receive an equal-share portion of the remaining unattributed kWh; `isConfigured` / `isUnconfigured` set accordingly; the strip's total exactly equals the sum of all sub-device shares within ±0.01 kWh rounding tolerance.

**Given** `TariffResolver` is called for cost attribution,
**When** computing costs,
**Then** each day's kWh is multiplied by the import tariff active on that calendar date (FR-13 period-accurate costing); summed into device/room/total cost fields; all cost fields are `decimal` — no float or double.

**Given** `Residual` computation,
**When** included in the response,
**Then** `Residual.Kwh = TotalKwh − sum(all device kWh)`; `Residual.Cost` uses the same period-accurate TariffResolver; the invariant `Residual.Kwh + attributed kWh = TotalKwh` holds within ±0.1 kWh; `Residual.Kwh` may be zero but is always included in the response (FR-33).

**Given** a period with no SmartPlugDailyData for any plug in the flat,
**When** `ComputeAsync` is called,
**Then** `DecompositionResponse.IsUnavailable = true`; `Rooms` is empty; `Residual` is still present with `Kwh = 0`; HTTP 200 is returned (not 404).

**Given** any `SmartPlugDailyData` row in the period has `IsInterpolated = true`,
**When** building the response,
**Then** `HasInterpolatedData = true` on the response object.

**Given** `GET /api/v1/flats/{flatId}/decomposition?startDate={date}&endDate={date}`,
**When** `GetDecompositionFunction.RunAsync` executes,
**Then** `startDate` and `endDate` are required query params; missing or invalid dates return HTTP 400 Problem Details; `endDate < startDate` returns HTTP 400; tenant check enforces flatId belongs to authenticated userId (HTTP 403 on mismatch); valid request returns HTTP 200 with `DecompositionResponse`.

**Given** `DecompositionEngineTests.cs` in `api.Tests/Features/Decomposition/`,
**When** run,
**Then** tests cover: measured device attribution; EU label daily estimate; SelfMeasured daily estimate; Smart Power Strip proportional split; unconfigured sub-devices get equal share; Residual = TotalKwh − attributed within tolerance; period with no SmartPlugData sets `IsUnavailable = true`; `HasInterpolatedData = true` when any row is interpolated; `Residual.Kwh` is 0 but present when all kWh is attributed.

---

### Story 7.2: Decomposition Tab — Period Selector, Residual Card & Unavailable State

As a user,
I want to select a date range from a dropdown and immediately see my Residual (unattributed kWh) as the first card in a glass surface — even when it is zero — so I always know how much of my consumption is unaccounted for,
And when no smart plug data exists for the period, I want a clear unavailable state with a direct link to import data.

**Acceptance Criteria:**

**Given** the Decomposition tab renders,
**When** it mounts,
**Then** `DecompositionTab.tsx` renders a period selector dropdown with options: "This week", "This month" (default), "Last month", "This year", "Custom range"; Custom range reveals a start/end date picker pair; the selected period's `startDate` and `endDate` drive the TanStack Query key `['decomposition', flatId, { startDate, endDate }]`; a new selection triggers an immediate refetch with loading skeleton.

**Given** `useDecomposition(flatId, startDate, endDate)` via TanStack Query,
**When** data loads successfully and `IsUnavailable = false`,
**Then** the Residual card renders first before all Room cards regardless of the Residual value; it is never suppressed even when `Residual.Kwh === 0`.

**Given** the Residual card renders,
**When** `Residual.Kwh > 0` or `Residual.Kwh === 0`,
**Then** in both cases: card uses the glass surface system with a `residual-tint` amber overlay; the card header shows "Residual" (i18n key); kWh value and attributed cost are shown; no collapse or hide control is present; card is visually first in the list.

**Given** `DecompositionResponse.HasInterpolatedData = true`,
**When** the tab renders,
**Then** a non-blocking info banner appears above the cards: "Some values have been interpolated from incomplete import data." (i18n key); it does not block interaction with any card.

**Given** `DecompositionResponse.IsUnavailable = true`,
**When** the tab renders,
**Then** instead of cards, an unavailable state renders with: an informational icon, heading "No smart plug data for this period" (i18n key), body copy "Upload a smart plug export to see your breakdown", and a primary CTA button "Import Data" that navigates to the Import surface; the Residual card is not shown in this state (FR-34).

**Given** the query is in a loading state,
**When** the tab renders,
**Then** skeleton placeholders match the height of the Residual card and two Room cards; no layout shift when real data arrives.

**Given** the query returns an error,
**When** the tab renders,
**Then** an error state with "Couldn't load decomposition" (i18n key) and a Retry button is shown.

---

### Story 7.3: Room Cards, Device Card Variants & Smart Power Strip Card

As a user,
I want to see my consumption grouped by room with device cards that visually distinguish between directly measured devices and estimated ones, and Smart Power Strip sub-devices that clearly show which are configured and which are not,
So that I understand both what I know precisely and where estimates are being used.

**Acceptance Criteria:**

**Given** `RoomCard.tsx` renders for a room in the decomposition response,
**When** displayed,
**Then** room name, total room kWh, and total room cost are shown in the card header; child DeviceCards are rendered in a vertical list within the room; rooms are ordered by descending kWh.

**Given** a device with `approach = Measured` (smart plug device),
**When** its `DeviceCard` renders,
**Then** it uses the **rich/measured variant**: large card layout; device name prominent; kWh and cost values; a "Measured" badge (`accent-success` colour token); no estimate disclaimer; ordered before estimated devices within the room.

**Given** a device with `approach = EuLabel` or `SelfMeasured`,
**When** its `DeviceCard` renders,
**Then** it uses the **compact/estimated variant**: smaller card; device name; kWh and cost values; an "Estimated" badge (`accent-warning` colour token); a one-line disclaimer beneath: "Based on EU label" or "Based on self-measured average" depending on approach (i18n keys).

**Given** a device with `isSmartStrip = true`,
**When** `SmartStripCard.tsx` renders,
**Then** the card header shows the strip name, total measured kWh, and cost; sub-device rows are listed beneath the header in a compact list.

**Given** a sub-device row where `isConfigured = true`,
**When** rendered,
**Then** the row shows at full opacity (1.0); device name; attributed kWh share and cost; no configure hint.

**Given** a sub-device row where `isUnconfigured = true`,
**When** rendered,
**Then** the row renders at `opacity: 0.45`; device name shows as "Unassigned outlet {n}" or the stored name; an inline chip or hint reading "Configure" navigates to the Flat Structure editor at the Smart Power Strip's PowerPoint; the row's kWh shows its equal-share portion.

**Given** device cards within a room,
**When** ordered,
**Then** Measured devices appear before Estimated devices; within each group, descending kWh order; the Smart Power Strip card is treated as Measured if it has a plugId.

**Given** a room with zero devices having any approach or plug configured,
**When** it appears in the response with a non-zero kWh,
**Then** it renders one compact card reading "Direct consumption" with the room's kWh (unattributed room total); no badge is shown.

---

## Epic 8: Actionable Insights

The Insights tab automatically surfaces four categories of findings — standby offenders, replacement candidates, budget pressure alerts, and invoice deviation hints — via a daily scheduled job and a manual trigger. Prior insights remain visible while a new run is in progress.

### Story 8.1: Insights Infrastructure — Data Model, Run Tracking, Schedule & API

As a user,
I want the app to automatically discover insights every night and let me trigger a refresh manually, with prior insights staying visible while a new run completes,
So that I always see the most recent findings and never land on an empty screen while a run is in progress.

**Acceptance Criteria:**

**Given** EF Core migrations for `InsightRuns` and `Insights`,
**When** reviewed,
**Then** `InsightRunConfiguration` defines `RunId` (guid PK), `FlatId` (FK, cascade delete), `Status` (enum: Pending/Processing/Complete/Failed), `StartedAt` (datetimeoffset), `CompletedAt` (nullable datetimeoffset). `InsightConfiguration` defines `InsightId` (guid PK), `FlatId` (FK, cascade delete), `RunId` (FK, set-null on run delete), `Type` (enum: Standby/Replacement/Budget/InvoiceDeviation), `DeviceId` (nullable guid FK), `Data` (nvarchar(max) JSON column), `CreatedAt` (datetimeoffset); index on `(FlatId, Type, CreatedAt desc)`. Zero Data Annotation attributes on any entity class.

**Given** `ScheduledInsightsFunction.cs` with `[TimerTrigger("0 0 2 * * *")]`,
**When** it fires at 02:00 UTC,
**Then** it queries all `FlatId` values for active users; for each flat it enqueues a discovery message containing `{ flatId, runId }` onto the insights Azure Storage queue using Managed Identity; no HTTP response — fire-and-forget.

**Given** `POST /api/v1/flats/{flatId}/insights/trigger`,
**When** `TriggerInsightsFunction.RunAsync` executes,
**Then** a new `InsightRun` with `Status = Pending` is created and saved; a discovery message `{ flatId, runId }` is enqueued; HTTP 202 is returned with `{ runId }`; tenant check enforces flatId belongs to authenticated userId (HTTP 403 on mismatch); if a run with `Status = Pending` or `Processing` already exists for this flatId, the existing `runId` is returned with HTTP 202 (no duplicate runs).

**Given** `ProcessInsightsFunction.cs` with `[QueueTrigger("insights-discovery")]`,
**When** a discovery message is dequeued,
**Then** `InsightRun.Status` is set to `Processing`; all four detectors are called in sequence: `StandbyDetector`, `ReplacementDetector`, `BudgetAlertDetector`, `InvoiceDeviationDetector`; each detector's findings are written as `Insight` rows; on successful completion, `InsightRun.Status = Complete` and `CompletedAt` is set; on unhandled exception, `InsightRun.Status = Failed`; detector errors are logged to Application Insights but do not suppress other detectors — each runs independently.

**Given** `GET /api/v1/flats/{flatId}/insights`,
**When** `GetInsightsFunction.RunAsync` executes,
**Then** HTTP 200 returns `{ runStatus: { status, startedAt, completedAt? }, insights: [...] }` where `insights` is all `Insight` rows for the flat sorted by `CreatedAt desc`; the most recent run's status is included regardless of whether it is still running; tenant check applied; TanStack Query cache key: `['insights', flatId]`.

**Given** `InsightModels.cs`,
**When** reviewed,
**Then** `InsightsResponse` (C# record) has `RunStatus` (`RunStatusDto`: status, startedAt, completedAt) and `Insights` (list of `InsightDto`). `InsightDto` has: `insightId` (guid), `type` (string enum), `deviceId` (nullable guid), `data` (raw JSON passthrough — serialized as-is to client), `createdAt` (datetimeoffset). No Data Annotation attributes.

---

### Story 8.2: Standby Offender & Replacement Candidate Detectors

As a user,
I want to be told when a specific device is drawing power outside its normal hours of use and when a high-consumption device could be replaced at a known payback, with exact device names and quantified euro figures,
So that I can take targeted action rather than guessing where to investigate.

**Acceptance Criteria:**

**Given** `StandbyDetector.cs` invoked during a discovery run for a flat,
**When** the flat has Devices linked to Eve Home plugs with interval data in `SmartPlugIntervalData`,
**Then** for each such device: the detector queries the last 30 days of `SmartPlugIntervalData` rows for the device's `plugId`; rows outside the flat's configured usage window (default 22:00–08:00 local time) are filtered as "out-of-use hours"; a device is flagged as a standby offender when its mean out-of-use watt draw exceeds 2 W across at least 7 days of interval data; for each offender, one `Insight` row is written with `Type = Standby`, the `DeviceId` set, and `Data` JSON: `{ "deviceName": string, "meanStandbyWatts": decimal, "estimatedMonthlyKwh": decimal, "estimatedMonthlyCost": decimal }`; cost uses the current active tariff via `TariffResolver`.

**Given** `StandbyDetector.cs` processing a flat that has Devices linked only to Meross plugs,
**When** detecting standby offenders,
**Then** Meross-linked devices are excluded entirely — no Insight is created and no error is surfaced; this is an explicit format limitation, not a failure condition (FR-35).

**Given** `StandbyDetector.cs` processing a flat with fewer than 7 days of interval data,
**When** invoked,
**Then** no standby `Insight` rows are written; the detector exits cleanly; `InsightRun` proceeds to the next detector.

**Given** `ReplacementDetector.cs` invoked during a discovery run,
**When** the flat has Devices with measurable annual consumption (via SmartPlugDailyData or EU label or SelfMeasured approach),
**Then** for each device with computable annual cost: devices in the top 20% of consumption whose EU label class is C or below are flagged as replacement candidates; one `Insight` row is written per candidate with `Type = Replacement` and `Data` JSON: `{ "deviceName": string, "estimatedAnnualKwh": decimal, "estimatedAnnualCost": decimal, "suggestedClass": string, "estimatedSavingsEur": decimal }`; savings are estimated from the delta between current consumption and a next-class-up EU label target; all decimal fields are `decimal` — no float.

**Given** `StandbyDetectorTests.cs` and `ReplacementDetectorTests.cs` in `api.Tests/Features/Insights/`,
**When** run,
**Then** standby tests cover: Eve Home device above 2W threshold over 7+ days → Insight written; device below threshold → no Insight; Meross device → excluded with no error; <7 days data → no Insight. Replacement tests cover: high-consumption C-rated device → Insight with savings; A-class device → no Insight; no devices with approach → no Insights.

---

### Story 8.3: Budget Pressure & Invoice Deviation Detectors

As a user,
I want to be warned when my projected annual spend is tracking over budget and when my rolling annual consumption is diverging significantly from my baseline, with exact euro and kWh figures,
So that I can act before the invoice arrives rather than after.

**Acceptance Criteria:**

**Given** `BudgetAlertDetector.cs` invoked during a discovery run,
**When** the flat has `PlannedAnnualSpend` configured and at least 30 days of `MeterReadings`,
**Then** the detector computes `dailyAverageCost` over the 30-day rolling window using period-accurate TariffResolver; annualised projection = `dailyAverageCost × 365`; when `projectedAnnualCost > PlannedAnnualSpend`: one `Insight` row is written with `Type = Budget` and `Data` JSON: `{ "projectedAnnualCost": decimal, "plannedAnnualSpend": decimal, "overspendEur": decimal }`; all fields are `decimal` — no float.

**Given** `BudgetAlertDetector.cs` when `projectedAnnualCost ≤ PlannedAnnualSpend`,
**When** the run completes,
**Then** no Budget `Insight` row is written; no error generated.

**Given** `BudgetAlertDetector.cs` when `PlannedAnnualSpend` is null,
**When** invoked,
**Then** the detector skips execution for the flat and exits cleanly.

**Given** `InvoiceDeviationDetector.cs` invoked during a discovery run,
**When** the flat has `AnnualKwhBaseline` configured and at least 60 days of `MeterReadings`,
**Then** the detector computes `dailyAverageKwh` over 60 days; projected annual kWh = `dailyAverageKwh × 365`; deviation = `|projected − baseline| / baseline`; when `deviation ≥ 0.10` (≥10%): one `Insight` row is written with `Type = InvoiceDeviation` and `Data` JSON: `{ "projectedAnnualKwh": decimal, "baselineKwh": decimal, "deviationPct": decimal, "impliedDeltaEur": decimal, "direction": "above" | "below" }`; `impliedDeltaEur = (projectedAnnualKwh − baselineKwh) × currentTariffKwhRate`; all decimal fields are `decimal` — no float.

**Given** `InvoiceDeviationDetector.cs` when deviation is below the ±10% threshold,
**When** the run completes,
**Then** no InvoiceDeviation `Insight` row is written; no error generated.

**Given** `InvoiceDeviationDetector.cs` when `AnnualKwhBaseline` is null or fewer than 60 days of readings exist,
**When** invoked,
**Then** the detector skips execution for the flat and exits cleanly.

**Given** `BudgetAlertDetectorTests.cs` and `InvoiceDeviationDetectorTests.cs` in `api.Tests/Features/Insights/`,
**When** run,
**Then** budget tests cover: projected > planned → Insight with correct amounts; projected ≤ planned → no Insight; null PlannedAnnualSpend → skip. Invoice deviation tests cover: +15% deviation → Insight with direction=above; −12% → Insight with direction=below; +8% → no Insight; null baseline → skip; <60 days data → skip.

---

### Story 8.4: Insights Tab — Trend Chart, Insight Cards & Discovery Progress

As a user,
I want to open the Insights tab and see my 30-day trend chart alongside auto-discovered insight cards, with a visible progress indicator during a discovery run and prior cards staying visible underneath,
And when I tap "Refresh insights" it immediately shows the run in progress rather than waiting.

**Acceptance Criteria:**

**Given** `InsightsTab.tsx` mounts,
**When** the Insights tab is activated,
**Then** the 30-day `TrendChart` component (from Epic 3) renders at the top at full width; spike bars are amber-coloured (already implemented in Epic 3); below the chart, `useInsights` queries `['insights', flatId]` via `GET /api/v1/flats/{flatId}/insights`; a "Refresh insights" button is present.

**Given** `useInsights(flatId)` returns `runStatus.status = Processing` or `Pending`,
**When** the Insights section renders,
**Then** `InsightDiscoveryProgress.tsx` renders above the insight cards with an animated progress indicator and label "Discovering insights…" (i18n key); any prior insight cards from the previous run remain visible and interactive below the progress component; `useInsights` polls every 5 seconds until `status = Complete` or `Failed`, then issues a final refetch (FR-39).

**Given** `runStatus.status = Complete` and insights are available,
**When** the Insights section renders,
**Then** `InsightDiscoveryProgress.tsx` is hidden; `InsightCard.tsx` renders one card per insight in a scrollable vertical list (2-column grid on tablet); cards are ordered by `createdAt desc`.

**Given** an `InsightCard` with `type = Standby`,
**When** rendered,
**Then** the card shows: standby icon; `data.deviceName`; "Drawing {meanStandbyWatts} W outside usage hours" (i18n key); "Estimated monthly cost: {estimatedMonthlyCost}" formatted via `Intl.NumberFormat` with the active locale's currency.

**Given** an `InsightCard` with `type = Replacement`,
**When** rendered,
**Then** the card shows: replacement icon; device name; "Annual cost: {estimatedAnnualCost}" and "Potential savings: {estimatedSavingsEur}/year" (i18n keys); suggested EU class label shown.

**Given** an `InsightCard` with `type = Budget`,
**When** rendered,
**Then** the card shows: `accent-error` left border; budget warning icon; "Projected annual spend: {projectedAnnualCost}" and "Planned: {plannedAnnualSpend}" and "Over by: {overspendEur}" (i18n keys); all amounts formatted via `Intl.NumberFormat`.

**Given** an `InsightCard` with `type = InvoiceDeviation`,
**When** rendered,
**Then** the card shows: invoice icon; "Projected annual usage: {projectedAnnualKwh} kWh" and "Your baseline: {baselineKwh} kWh" and "Implied difference: {impliedDeltaEur}" with direction indicator (above/below); all figures locale-formatted.

**Given** `useInsights` returns an empty `insights` array and `runStatus.status = Complete`,
**When** the Insights section renders,
**Then** empty state: "No findings this run. Everything looks within normal range." (i18n key); trend chart remains visible above.

**Given** no completed run exists and fewer than 30 days of readings (insufficient data),
**When** the Insights section renders,
**Then** empty state: "Not enough data for insights. Add readings and import smart plug data to generate insight cards." (i18n key); trend chart still renders if readings exist.

**Given** `useTriggerInsights` fires when "Refresh insights" is tapped,
**When** `POST /api/v1/flats/{flatId}/insights/trigger` returns 202,
**Then** TanStack Query key `['insights', flatId]` is immediately refetched; UI transitions to the "Processing" state; the "Refresh insights" button is disabled while `runStatus.status` is Pending or Processing.
