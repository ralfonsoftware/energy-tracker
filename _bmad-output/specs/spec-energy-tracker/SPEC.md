---
id: SPEC-energy-tracker
companions:
  - smart-plug-formats.md
  - locale-formats.md
sources:
  - ../../planning-artifacts/briefs/brief-energy-tracker-2026-06-20/brief.md
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability only — consult them only if you need narrative rationale or prose color this contract intentionally omits.

# energy-tracker

## Why

Working from home makes domestic energy a material monthly cost, yet the only available tool is a spreadsheet: friction-heavy on mobile, incapable of trend analysis or anomaly detection, and blind to which rooms or devices are responsible. The result is an annual invoice that surprises, and a standing suspicion that standby draw or specific appliances are wasting money — with no way to confirm or quantify it. energy-tracker replaces the spreadsheet with a purpose-built, mobile-first web app that translates raw meter readings into cost figures, decomposes total consumption down to individual rooms and devices using file exports from smart plugs and EU energy label estimates, and surfaces actionable savings signals — all without requiring a hub, cloud subscription, or always-on hardware.

## Capabilities

- id: CAP-1
  intent: User can log a main meter reading from a mobile browser in under 60 seconds.
  success: A reading is submitted and persisted with date/time; server-side processing time (from request received to response dispatched, client network excluded) is within the time budget; confirmed across three test entries. Client-to-server network latency is outside the test boundary.

- id: CAP-2
  intent: User can view a KPI dashboard showing daily average kWh, weekly average kWh, daily cost, weekly cost, and projected monthly cost in euros.
  success: Dashboard figures update immediately after a new reading is saved and match independently calculated values for the same period.

- id: CAP-3
  intent: User can see consumption trend visualizations and receive spike detection alerts when daily usage deviates unusually from recent baseline.
  success: A spike exceeding the configured threshold (default: 2× the 7-day rolling average) triggers a visible alert; the threshold is user-configurable; trend chart correctly reflects historical readings.

- id: CAP-4
  intent: User can configure tariff entries (fixed monthly base fee + €/kWh; optionally provider name, contract start date, contract duration) with an effective date. Tariff prices are locked for their contract period. Future tariff changes — including provider switches — can be pre-entered. Historical meter readings can be entered retroactively and are costed at the tariff active for each period.
  success: A future tariff change entered today does not alter cost figures for any past period; a meter reading entered for a past date is costed at the tariff that was active on that date; a tariff entry with a contract duration prevents modification of that entry's prices once its period has started.

- id: CAP-5
  intent: Access to the app is restricted to users authenticated via an OpenID Connect-compatible identity provider, with Azure Entra ID as the initial provider.
  success: Unauthenticated requests to any app route redirect to the OIDC login flow; authenticated session persists across browser restarts; swapping the identity provider requires only configuration changes, not code changes.

- id: CAP-6
  intent: User can upload smart plug exports (Eve Home Excel files, Meross CSV files) — including exports covering past periods — and the app parses them into a unified consumption timeline reconciled against main meter totals for periods where both data sources are present. Mid-period gaps within an export (individual missing days inside an otherwise covered range) are detected, the user is notified with the affected date ranges, and the gaps are filled by linear interpolation between the surrounding anchor values; interpolated values are internally marked as attributed consumption and a hint is shown to the user whenever a viewed period contains interpolated data. Import failures are logged internally and surfaced to the user as one of three categorized messages: data cannot be read, processing failed (user should retry), or service temporarily unavailable (user should retry later).
  success: An Eve Home and a Meross export covering the same calendar period load without error; attributed kWh summed across plugs does not exceed the main meter total for that period; an export with mid-period gaps completes with a notification listing affected date ranges and those gaps are recorded as linearly interpolated; a corrupt or unreadable file surfaces a user-friendly categorized error message; periods with no smart plug data at all are shown as "decomposition unavailable" in CAP-9 with a prompt to import data.

- id: CAP-7
  intent: User can create and manage multiple flats via settings and switch between them using a header component; the last active flat is remembered across sessions. For each flat, the user can define its physical structure (rooms → power points → devices) and assign smart plugs to power points. Flat structure setup is only required when using smart plug import or consumption decomposition; a default room template (living room, bedroom, kitchen, bathroom, hallway) is pre-populated for customization when structure setup is initiated. Deleting a flat permanently removes all data associated with it.
  success: A flat switcher in the app header allows selecting any of the user's flats; the app reopens on the last active flat; a second flat can be created and deleted from settings; deleting a flat removes all its meter readings, tariff entries, smart plug data, flat structure, and device registrations with no orphaned records; a flat's room structure starts from the default template and can be customized; each smart plug can be assigned to exactly one power point; meter reading entry and the KPI dashboard are accessible without any flat structure defined.

- id: CAP-8
  intent: User can register a device with general metadata (type, manufacturer, model; optionally purchase date) and configure its energy consumption via one of two approaches: (a) EU energy label — energy class rating and the label's stated annual kWh figure; (b) self-measured average — a daily or weekly kWh value entered by the user, useful when no permanent smart plug is installed. Both approaches contribute an estimated baseline to the decomposition.
  success: A device registered via EU label displays its annual kWh and a derived daily estimate in the decomposition view, marked as estimated; a device registered via self-measured average displays its configured daily or weekly consumption, marked as estimated; both device types appear in CAP-9's decomposition accounting; devices without a consumption approach entered still appear in the flat structure but contribute zero to decomposition with a prompt to configure consumption.

- id: CAP-9
  intent: User can view a consumption decomposition by room and device for any period where smart plug data has been imported; periods without smart plug data are shown as explicitly unavailable with a prompt to import data rather than displaying zeros or partial figures.
  success: Attributed totals (including interpolated values) + residual = main meter total for any period with smart plug data (within ±0.1 kWh when no interpolation is present; tolerance relaxed when interpolation is used); a hint is displayed whenever the viewed period contains interpolated data; residual is always shown, never suppressed, even when zero; a time range with no imported smart plug data renders as "decomposition unavailable" with an import prompt.

- id: CAP-10
  intent: The app automatically discovers and surfaces actionable insights on a dedicated insights page: high-standby offenders, high-consumption device replacement candidates (with quantified payback), and budget pressure alerts when rolling projections exceed the user's planned annual spend (a signal to review tariffs). Insight discovery runs on a daily schedule (02:00) and can also be manually triggered; the user receives visible feedback while discovery is running; insights are presented as soon as they are discovered.
  success: After a manual trigger or scheduled run, the insights page updates with at least one named device and quantified figure (kWh or €) once sufficient smart plug data exists; a budget alert appears when the rolling monthly projection exceeds the planned annual spend set in the dedicated budget settings; a progress indicator is visible for the duration of the discovery run; insights from prior runs remain visible while a new run is in progress.

- id: CAP-11
  intent: On first use, the app guides the user through a one-time onboarding setup for their first flat: flat name, estimated current annual power consumption (kWh, used as a baseline for invoice deviation hints), and current tariff details (fixed monthly base fee and price per kWh required; provider name, contract start date, and contract duration [1, 6, 12, or 24 months] optional). The annual kWh estimate can be entered as a specific value or selected from household-size presets (1 person ≈ 1,500 kWh; 2 persons ≈ 2,500 kWh; 3 persons ≈ 3,500 kWh; 4 persons ≈ 4,250 kWh). Onboarding settings are accessible from settings at any time to update.
  success: A new user cannot reach any main app feature until onboarding is complete; both the specific-value entry and each household-size preset correctly populate the annual kWh baseline; the entered tariff is immediately reflected in all cost calculations; the annual kWh baseline is used in CAP-10 invoice deviation logic; all onboarding fields can be updated from settings after initial setup.

- id: CAP-12
  intent: User can select their preferred language and locale; all UI text is displayed in the selected language and all numbers, dates, times, and currency values are formatted according to the user's locale conventions. Currency follows the user's locale (e.g. € for de-DE, $ for en-US). English (en-US) and German (de-DE) are supported from launch; exact format specifications are in `locale-formats.md`.
  success: Switching between en-US and de-DE updates all UI text, number separators, date format, time format, and currency symbol and format throughout the app; raw data is stored and transmitted locale-neutrally (ISO 8601 with timezone, decimal-point numbers, fixed-decimal currency values) and is formatted only at render time; a currency amount stored as a fixed-decimal value renders with the correct symbol, separators, and precision for the active locale.

## Constraints

- Hosted on Azure: Static Web App frontend, .NET Azure Functions backend. No other hosting targets for v1/v2.
- Authentication and authorization use OpenID Connect and OAuth 2.0. The identity provider is swappable via configuration; Azure Entra ID is the initial provider.
- Responsive web app only; mobile browser is the primary form factor. No native app for v1/v2.
- Smart plug file imports are limited to Eve Home (Excel) and Meross (CSV); no other brands or direct API integrations in v1/v2.
- A tenant is automatically provisioned per authenticated user (keyed by user-id). Each user can track multiple flats; all energy data, tariff history, and flat structure are scoped to an individual flat. No cross-tenant data access. No multi-user management UI.
- Tariff prices are locked for their contract period and cannot be changed retroactively. Future tariff changes (including provider switches / new contracts) can be pre-entered with an effective date. Historical meter readings and smart plug imports can be entered or uploaded for past periods and are costed at the tariff active for each period.
- No hub, cloud subscription, or always-on hardware may be required; the app works entirely from manually uploaded files and manually entered readings.
- Flat structure follows a strict four-level hierarchy: flat → rooms → power points → devices.
- Uploaded smart plug files are stored in Azure Blob Storage and processed asynchronously by a blob-triggered Azure Function. Azure Storage Account queues handle lightweight internal messaging. Persistent data store is TBD (Azure SQL or Cosmos DB — decision based on schema complexity and cost).
- The app is fully internationalized (i18n) at the architecture level: all UI text goes through a localization framework, all numbers, dates, times, and currency values are rendered via locale-aware formatters, and all data is stored and transmitted locale-neutrally (ISO 8601 datetimes with timezone offset, decimal-point numbers, currency as fixed-decimal). No hardcoded locale-specific formatting anywhere in the codebase. Exact format specifications per locale are in `locale-formats.md`.
- All datetime values are stored and transferred with explicit timezone information (ISO 8601 with offset). Scheduled jobs (e.g. CAP-10 daily insight run) execute in UTC.
- Currency amounts are stored as fixed-decimal values (equivalent to C# `decimal`) throughout the data and API layers to ensure precision; no floating-point representation of monetary values.

## Non-goals

- Native iOS app (any release in this spec).
- Multi-user management UI (each user manages only their own flats; no admin or cross-user views).
- Real-time or near-real-time energy monitoring.
- Export or reporting features (CSV/PDF output).
- Direct smart plug API integration (Eve, Meross, Home Assistant, or any other).
- Tariff comparison wizard.
- Multi-tenant hosted version.
- Support for smart plug brands other than Eve Home and Meross.

## Success signal

A user reads the basement meter, enters it on their phone, and sees updated daily and weekly cost figures within 60 seconds. After one month of real use with smart plug exports uploaded, the app has identified at least one named device as a standby or cost offender, and the rolling monthly projection is within 10% of the eventual invoice.

## Assumptions

- "Flat" means a single European-style dwelling unit (apartment or house); the four-level hierarchy (flat → rooms → power points → devices) covers the realistic physical layout. A user may own or rent multiple such units.


