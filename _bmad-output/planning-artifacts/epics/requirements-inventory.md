# Requirements Inventory

## Functional Requirements

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

## NonFunctional Requirements

NFR-1 Performance: Three-tier model: Tier 1 (≤2s synchronous — standard server actions), Tier 2 (≤30s with UI hint — operations exceeding 2s), Tier 3 (fully background with notification on completion — smart plug import processing and insight discovery). KPI Dashboard load for up to 2 years of Readings: Tier 1 (≤2s).

NFR-2 Security and Data Isolation: Full tenant isolation by user ID at every data layer. All requests authenticated (FR-1); no unauthenticated endpoints except OIDC callback. Currency stored as fixed-decimal C# `decimal` — no floating-point monetary values at any layer.

NFR-3 Internationalization: All UI text through localization framework. No hardcoded locale-specific strings, number formats, date formats, or currency symbols. All data stored locale-neutrally: ISO 8601 datetimes with timezone offset, decimal-point numbers, fixed-decimal currency. Locale preference stored server-side; `Accept-Language` header provides default. Locale formatting applied only at render time via `Intl.NumberFormat` and `Intl.DateTimeFormat`.

NFR-4 Reliability and Async Processing: Smart Plug file uploads stored in Azure Blob Storage, processed asynchronously by blob-triggered Azure Function. Azure Storage Account queues for lightweight internal messaging. Azure SQL Basic DTU (~€5/month, 2 GB) as persistent data store.

## Additional Requirements

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

## UX Design Requirements

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

## FR Coverage Map

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
