---
stepsCompleted: [1, 2, 3, 4, 5, 6, 7, 8]
lastStep: 8
status: 'complete'
completedAt: '2026-06-21'
inputDocuments:
  - _bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md
  - _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/DESIGN.md
  - _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/EXPERIENCE.md
  - _bmad-output/planning-artifacts/briefs/brief-energy-tracker-2026-06-20/brief.md
  - _bmad-output/specs/spec-energy-tracker/SPEC.md
  - _bmad-output/specs/spec-energy-tracker/smart-plug-formats.md
workflowType: 'architecture'
project_name: 'energy-tracker'
user_name: 'Ralf'
date: '2026-06-21'
---

# Architecture Decision Document — energy-tracker

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

---

## Project Context Analysis

### Requirements Overview

**Functional Requirements:**
43 FRs across 12 feature areas. Release 1 covers authentication, onboarding, meter reading, tariff management, KPI dashboard, trends/spike detection, and localization. Release 2 layers multi-flat management, smart plug import (Eve Home Excel + Meross CSV), device registry, consumption decomposition, and actionable insights on top of R1.

The central domain invariant is **period-accurate tariff costing**: every cost figure must use the tariff active on the date of consumption, not the current tariff. This rule applies at every layer — API, computation, display.

**Non-Functional Requirements:**
- **Performance (NFR-1):** Three-tier model — Tier 1 (≤2s synchronous), Tier 2 (≤30s with UI hint), Tier 3 (fully background with progress notification). Smart plug import and insight discovery are Tier 3. KPI dashboard load is Tier 1.
- **Security (NFR-2):** Full tenant isolation by user-ID at every data layer; C# `decimal` throughout for currency (no floating-point monetary values); OIDC gate on all routes; managed identities for all Azure service-to-service connections; Key Vault for secrets that cannot use managed identity; no private endpoints (cost trade-off accepted).
- **Internationalization (NFR-3):** All text through l10n framework; all storage in locale-neutral form (ISO 8601 with offset, decimal-point numbers, fixed-decimal currency). `Accept-Language` header drives locale selection by default; user can override via Settings — override stored server-side in user profile (not browser local storage as originally specified in PRD/UX).
- **Reliability (NFR-4):** Azure Blob Storage for smart plug uploads; blob-triggered Azure Function for async processing; Azure Storage queues for internal messaging; **Azure SQL (Basic DTU, ~€5/month)** as the persistent data store (decided).

**Scale & Complexity:**
- **Primary domain:** Full-stack web — Azure Static Web App (frontend) + .NET Azure Functions (backend), mobile-first responsive (375px primary form factor)
- **Complexity level:** Medium-high — driven by domain rule correctness (tariff costing invariant, interpolation + reconciliation, async pipeline) and R1/R2 release staging, not by data volume (single user, O(thousands) of data points)
- **Estimated architectural components:** 8–10 — auth layer, onboarding gate, reading service, tariff service, KPI computation, trend/spike engine, smart plug import pipeline, decomposition engine, insights discovery engine, localization layer

### Technical Constraints & Dependencies

- **Hosting:** Azure Static Web App (frontend) + .NET Azure Functions (backend) — fixed
- **Auth:** OIDC/OAuth 2.0; Azure Entra ID as initial provider; provider-swappable via configuration only (no code changes to switch)
- **File storage:** Azure Blob Storage for smart plug uploads (blob-trigger pattern)
- **Messaging:** Azure Storage Account queues (internal lightweight messaging)
- **Persistent store:** Azure SQL Basic DTU (~€5/month, 2 GB, 5 DTUs) — decided in this session
- **Security posture:** Managed identities for all service-to-service; Key Vault for remaining secrets; no private endpoints
- **UI system:** shadcn/ui; WCAG 2.2 AA accessibility floor; Reduce Motion support required
- **Currency:** C# `decimal` throughout (fixed-decimal); no floating-point monetary values at any layer

### Decisions Made in This Step

**AD-1: Persistent Data Store — Azure SQL Basic DTU**
Selected over Cosmos DB. The data model is a normalized relational graph (User → Flat → Rooms → Power Points → Devices) with FK constraints and cascade deletes. The decomposition query — the most domain-critical computation in the application — requires a multi-table join with a correlated date-range subquery for period-accurate tariff costing. This is SQL's native territory; implementing equivalent logic application-side in Cosmos DB adds maintenance burden with no benefit at this data volume. Azure SQL Basic DTU (~€5/month) is cost-appropriate for a personal single-user project.

**AD-2: Smart Plug Interval Data Retention**
Raw Eve Home ~10-minute interval Wh rows are retained in a separate `SmartPlugIntervalData` table (Eve Home only). All plugs additionally write to a `SmartPlugDailyData` table (daily kWh aggregates), which is used for all decomposition and KPI computation. The interval table is used exclusively for standby detection (FR-35). Meross exports provide daily aggregates only — standby offender detection (FR-35) is **not available for Meross plugs** due to format limitations; this is a refined feature requirement, not an architecture gap.

### Cross-Cutting Concerns Identified

1. **Tenant isolation** — All data scoped user-ID → flat. Every API endpoint and every data query must enforce this boundary.
2. **Period-accurate tariff costing** — The domain's primary invariant. Tariff history must be queryable by effective date. Affects every cost calculation: KPI figures, decomposition costs, budget projections, insights.
3. **Locale-neutral storage** — ISO 8601 datetimes with explicit timezone offset, decimal-point numbers, C# `decimal` for currency. Formatting is render-time only. Locale preference stored server-side in user profile; `Accept-Language` header is the default resolution path.
4. **Async processing pipeline** — Smart plug import (blob-triggered) and insight discovery (scheduled + manual) both follow the Tier 3 pattern: background execution, progress visible in UI, prior results remain visible during a new run.
5. **Retroactive data entry** — Both meter readings and smart plug files can cover past periods. Data model and computation layer must support out-of-order ingestion without data corruption.
6. **Release staging (R1/R2 boundary)** — R1 must deploy as a fully working product with no stubs. R2 must be addable without schema migrations that break R1 data or R1 API contracts.
7. **Two-tier smart plug data model** — `SmartPlugDailyData` (all plugs, daily aggregates, all computation) + `SmartPlugIntervalData` (Eve Home only, ~10-min rows, standby detection). These are separate concerns with separate consumers.

---

## Starter Template Evaluation

### Primary Technology Domain

Full-stack web: React SPA (Azure Static Web App) + C# .NET API (standalone Azure Functions app linked to SWA). Mobile-first responsive; shadcn/ui as the component system.

### Backend Hosting Decision

**AD-3: Azure Functions — Flex Consumption plan (Linux) with .NET 10 isolated worker**

Selected over Windows Consumption (legacy, marked for retirement) and Flex Consumption was confirmed to support .NET 10. Flex Consumption provides improved cold start behaviour relative to legacy Consumption while remaining cost-appropriate for a personal project. ReadyToRun (R2R) compilation enabled at publish time to further reduce cold start JIT overhead.

`.csproj` flag:
```xml
<PublishReadyToRun>true</PublishReadyToRun>
```

Publish pipeline flag:
```bash
dotnet publish -c Release -r linux-x64 --no-self-contained /p:PublishReadyToRun=true
```

**AD-4: Standalone Azure Functions app linked to SWA (not SWA managed functions)**

SWA managed functions do not support blob triggers. The smart plug import pipeline (FR-24–28) requires a blob-triggered Function; nightly insight discovery (FR-38) requires a timer trigger. All backend logic — HTTP API, blob-triggered import processing, timer-triggered insight discovery — lives in one standalone Functions app. SWA routes `/api/*` to the linked Functions app; no CORS configuration needed.

### Selected Stack

| Layer | Technology | Version |
|---|---|---|
| Frontend framework | React + TypeScript | Latest (via `create vite@latest`) |
| Build tool | Vite | Latest |
| UI system | shadcn/ui | v4 |
| CSS | Tailwind CSS | v4 (`@tailwindcss/vite` plugin) |
| Backend language | C# | .NET 10 LTS (support until Nov 2028) |
| Backend runtime | Azure Functions isolated worker | v4 runtime |
| Backend hosting | Flex Consumption (Linux) | — |
| ORM | EF Core | Latest .NET 10 compatible |
| Auth (frontend) | SWA Easy Auth (`/.auth/*`) | — (no frontend library; see AD-9) |

### Monorepo Structure

```
energy-tracker/
├── client/                        # Vite + React 18 + TypeScript + shadcn/ui
│   ├── src/
│   │   ├── components/ui/         # shadcn/ui generated components
│   │   ├── features/              # feature-grouped modules (mirrors backend slices)
│   │   ├── lib/                   # API client, i18n setup, query client
│   │   └── main.tsx
│   ├── vite.config.ts             # Tailwind v4 plugin + /api proxy → localhost:7071
│   └── package.json
├── api/                           # .NET 10 Azure Functions isolated worker
│   ├── Features/                  # vertical slices (see below)
│   ├── Data/                      # EF Core DbContext + Migrations
│   ├── Shared/                    # TenantResolver, TariffResolver, LocaleResolver
│   ├── Program.cs
│   └── energy-tracker-api.csproj
├── staticwebapp.config.json       # SWA routing rules + linked backend reference
└── .github/
    └── workflows/
        └── azure-static-web-apps.yml
```

### Backend: Vertical Slice Architecture

Each feature is a self-contained slice: Function trigger, handler logic, DTOs, and feature-specific services co-located. No top-level `Services/` or `Repositories/` folders.

```
api/Features/
├── Readings/           SubmitReadingFunction, GetReadingHistoryFunction, ReadingModels
├── Dashboard/          GetDashboardFunction (KPI computation)
├── Tariffs/            CreateTariffFunction, UpdateTariffFunction, GetTariffsFunction, TariffModels
├── SmartPlugImport/    UploadFunction (HTTP), ProcessImportFunction (BlobTrigger),
│                       EveHomeParser, MerossParser, InterpolationEngine, ImportModels
├── Decomposition/      GetDecompositionFunction, DecompositionEngine, DecompositionModels
├── Insights/           TriggerInsightsFunction (HTTP), ScheduledInsightsFunction (TimerTrigger),
│                       StandbyDetector, BudgetAlertDetector, InvoiceDeviationDetector, InsightModels
├── FlatStructure/      GetFlatStructureFunction, UpdateFlatStructureFunction, FlatStructureModels
├── Onboarding/         CompleteOnboardingFunction
└── Settings/           GetUserSettingsFunction, UpdateLocaleFunction

api/Shared/
├── TenantResolver.cs   # extracts user-id from OIDC token — used by every function
├── TariffResolver.cs   # period-accurate tariff lookup — the central domain invariant
└── LocaleResolver.cs   # Accept-Language resolution + server-stored override
```

### Scaffold Commands

**Frontend:**
```bash
npm create vite@latest client -- --template react-ts
cd client && npm install
npm install -D @tailwindcss/vite
npx shadcn@latest init
npm install react-router-dom @tanstack/react-query react-i18next recharts
```

**Backend:**
```bash
mkdir api && cd api
func init --worker-runtime dotnet-isolated --target-framework net10.0
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Azure.Identity
dotnet add package Microsoft.Data.SqlClient
dotnet add package Azure.Storage.Blobs
dotnet add package ExcelDataReader
```

**Note:** Project initialisation using these commands is the first implementation story.

---

## Core Architectural Decisions

### Data Architecture

**AD-5: EF Core code-first migrations, Fluent API configuration only**
All entity configuration uses EF Core Fluent API in `IEntityTypeConfiguration<T>` classes — no Data Annotation attributes on entity classes for database concerns. Keeps entity classes clean and centralises all schema rules in one discoverable location per entity.

**AD-6: No caching layer**
No Redis or in-memory cache. Azure SQL Basic DTU is sufficient for personal-scale data volumes. A KPI aggregation over a few hundred readings is well within the Tier 1 ≤2s budget. First lever if performance degrades: computed columns or a materialised daily-aggregate view in Azure SQL, not an external cache.

**AD-7: FluentValidation for request-level validation; EF Core Fluent API for entity constraints**
FluentValidation validators live inside each VSA slice alongside the Function. No cross-cutting validation base classes.

**AD-8: Hard deletes throughout; correction-in-place for readings**
Flat deletion cascades all child data (FR-23). Meter Readings are edited in-place with `IsCorrected = true` and `OriginalKwhValue` preserved on the row (not a separate audit table).

**Entity model:**

| Table | Key columns |
|---|---|
| `Users` | `UserId` (string, OIDC `sub` claim PK), `LocaleOverride` (nullable) |
| `Flats` | `FlatId` (guid), `UserId` FK, `Name`, `AnnualKwhBaseline` (decimal), `SpikeThreshold` (decimal), `PlannedAnnualSpend` (nullable decimal) |
| `Tariffs` | `TariffId` (guid), `FlatId` FK, `EffectiveDate` (datetimeoffset), `PricePerKwh` (decimal), `MonthlyBaseFee` (decimal), `ContractStartDate` (nullable), `ContractDurationMonths` (nullable int), `ProviderName` (nullable) |
| `MeterReadings` | `ReadingId` (guid), `FlatId` FK, `ReadingDate` (datetimeoffset), `KwhValue` (decimal), `IsCorrected` (bool), `OriginalKwhValue` (nullable decimal) |
| `Rooms` | `RoomId` (guid), `FlatId` FK, `Name`, `SortOrder` (int) |
| `PowerPoints` | `PowerPointId` (guid), `RoomId` FK, `Name`, `PlugId` (string, nullable — assigned smart plug identifier) |
| `Devices` | `DeviceId` (guid), `PowerPointId` FK, `Name`, `Type`, `Manufacturer`, `Model`, `PurchaseDate` (nullable), `ConsumptionApproach` (enum: None/EuLabel/SelfMeasured), `EuLabelClass` (nullable), `EuAnnualKwh` (nullable decimal), `SelfMeasuredKwh` (nullable decimal), `SelfMeasuredPeriod` (nullable enum: Daily/Weekly) |
| `SmartPlugDailyData` | `Id` (guid), `PlugId` (string), `FlatId` FK, `Date` (date), `KwhValue` (decimal), `IsInterpolated` (bool) |
| `SmartPlugIntervalData` | `Id` (guid), `PlugId` (string), `FlatId` FK, `Timestamp` (datetimeoffset), `WhValue` (decimal) — Eve Home only |
| `ImportJobs` | `ImportJobId` (guid), `FlatId` FK, `Status` (enum), `CreatedAt`, `CompletedAt` (nullable), `ErrorCategory` (nullable enum) |
| `InsightRuns` | `RunId` (guid), `FlatId` FK, `Status` (enum), `StartedAt`, `CompletedAt` (nullable) |
| `Insights` | `InsightId` (guid), `FlatId` FK, `RunId` FK, `Type` (enum: Standby/Replacement/Budget/InvoiceDeviation), `DeviceId` (nullable guid FK), `Data` (JSON column), `CreatedAt` |

`Insights.Data` is a JSON column. The `Type` enum determines the shape of the JSON payload; no separate per-insight-type tables. Azure SQL supports JSON natively; insight payloads are never queried relationally.

### Authentication & Security

**AD-9: SWA Easy Auth → Functions trust `X-MS-CLIENT-PRINCIPAL` header**
SWA validates the OIDC token at the edge. The linked Functions app reads the `X-MS-CLIENT-PRINCIPAL` header (Base64 JSON injected by SWA) and extracts the `sub` claim as `UserId` via `TenantResolver` middleware. No JWT validation code in the Functions app.

**AD-10: Managed identity for all Azure service connections**
Functions → Azure SQL, Blob Storage, Storage Queue, Key Vault all use `DefaultAzureCredential`. No connection strings with passwords anywhere in configuration. `Microsoft.Data.SqlClient` with Azure AD authentication for SQL.

**AD-11: Key Vault provisioned now, used for future secrets**
With managed identity covering all current service connections, no secrets require Key Vault in v1. Key Vault is provisioned as infrastructure now so any future secret (third-party API key, etc.) has a home without a deployment change.

**AD-12: Tenant isolation via middleware**
`TenantResolver` runs in Azure Functions middleware registered in `Program.cs`. Every function receives a resolved `UserId` context. All EF Core queries filter by `UserId` (via `Flat`) — never raw unscoped queries.

### API & Communication

**AD-13: REST with path-based API versioning**
All routes prefixed `/api/v1/`. Simple path-based versioning — no header or query-string versioning. Prepared for a future `/api/v2/` when breaking changes are needed without requiring client header changes.

```
POST   /api/v1/flats/{flatId}/readings
GET    /api/v1/flats/{flatId}/readings
GET    /api/v1/flats/{flatId}/dashboard
GET    /api/v1/flats/{flatId}/tariffs
POST   /api/v1/flats/{flatId}/tariffs
PATCH  /api/v1/flats/{flatId}/tariffs/{tariffId}
POST   /api/v1/flats/{flatId}/imports
GET    /api/v1/flats/{flatId}/imports/{jobId}
GET    /api/v1/flats/{flatId}/decomposition
GET    /api/v1/flats/{flatId}/insights
POST   /api/v1/flats/{flatId}/insights/trigger
GET    /api/v1/flats/{flatId}/structure
PUT    /api/v1/flats/{flatId}/structure
GET    /api/v1/user/settings
PUT    /api/v1/user/settings
POST   /api/v1/onboarding
```

**AD-14: Problem Details (RFC 9457) for all error responses**
Consistent error envelope across all endpoints. The three import error categories (FR-28) map to: HTTP 422 `data-unreadable`, HTTP 500 `processing-failed`, HTTP 503 `service-unavailable`. Frontend reads the `type` field to select the categorised user-facing message.

**AD-15: Azure Storage Queue for import/insight internal messaging**
JSON envelopes: `{ importJobId, flatId, blobPath, userId }`. Blob-triggered Function reads blob path directly; queue handles any decoupled internal signals.

### Frontend Architecture

**AD-16: TanStack Query v5 for all server state; no global store**
Server state (readings, KPI, decomposition, insights, tariffs) lives entirely in TanStack Query cache. Cache invalidation after reading submission drives the KPI tile update without a page reload. Local UI state (sheet open/closed, form values, selected period) uses React `useState`. No Zustand or Redux — not needed at this complexity level.

**AD-17: react-hook-form + zod per slice**
One zod schema per form, co-located with the feature component. Mirrors the VSA principle on the frontend — no shared form/validation base classes.

**AD-18: i18n — Accept-Language detection + server-stored override**
`react-i18next` with `i18next-browser-languagedetector` for initial locale resolution from `Accept-Language`. Server-stored locale override fetched as part of the user settings query on app load; overrides the detected locale when present. Translation files namespace-split by feature (`reading.json`, `dashboard.json`, `tariff.json`, etc.) for route-level code splitting alignment.

**AD-19: Route-level code splitting**
Each tab (Dashboard, Insights, Decomposition, Settings) lazy-loaded via Vite's built-in dynamic import. No additional bundler configuration required.

**Routing structure:**
```
/                  → Dashboard (redirect to /onboarding if first use)
/onboarding        → Onboarding gate
/insights          → Insights tab
/decomposition     → Decomposition tab (R2)
/settings          → Settings root
/settings/flat     → Flat configuration
/settings/tariff   → Tariff management
/settings/locale   → Language & region
/settings/account  → Account (sign out, flat deletion)
```

### Infrastructure & Deployment

**AD-20: GitHub Actions CI/CD — single pipeline**
SWA-generated workflow deploys `client/` (Vite build) and `api/` (.NET 10 publish, ReadyToRun, `linux-x64`) in one pipeline. No separate pipelines for frontend and backend.

**AD-21: Local dev + Production only (no staging)**
Personal project — one production environment. Local dev: `local.settings.json` (gitignored) for Functions, Vite `/api` proxy to `localhost:7071`.

**AD-22: Application Insights for monitoring**
Attached to the Functions app. Free tier sufficient for personal-project invocation volumes. Built-in failure alerting, invocation traces, dependency tracking.

**Complete Azure resource set:**

| Resource | Tier | Notes |
|---|---|---|
| Azure Static Web App | Free | Frontend hosting + SWA Easy Auth |
| Azure Functions app | Flex Consumption (Linux) | .NET 10 isolated, ReadyToRun |
| Azure Storage Account | Standard LRS | Blob (imports) + Queues (messaging) |
| Azure SQL Server + DB | Basic DTU (~€5/mo) | EF Core, managed identity auth |
| Azure Key Vault | Standard | Provisioned for future secrets |
| Application Insights + Log Analytics | Free tier | Monitoring and traces |
| Azure Entra ID app registration | — | OIDC provider for SWA Easy Auth |
| User-assigned Managed Identity | — | Functions → SQL, Blob, Queue, Key Vault |

---

## Implementation Patterns & Consistency Rules

### Naming Patterns

**Database (all via EF Core Fluent API — no Data Annotation attributes on entities):**
- Tables: PascalCase singular (`User`, `Flat`, `Tariff`, `MeterReading`, `SmartPlugDailyData`)
- Columns: PascalCase (`UserId`, `FlatId`, `KwhValue`, `IsInterpolated`)
- Primary keys: `{Entity}Id` pattern (`FlatId`, `ReadingId`, `TariffId`)
- Foreign keys: `{Referenced Entity}Id` on the dependent table (`FlatId` on `Tariff`)
- Indexes: `IX_{Table}_{Column(s)}` (e.g., `IX_Tariff_FlatId_EffectiveDate`)
- JSON columns: `nvarchar(max)` in Fluent API, named naturally (`Data` on `Insight`)

**API endpoints:**
- Plural resource names throughout: `/flats`, `/readings`, `/tariffs`, `/insights`
- Route parameters: camelCase in .NET route template — `{flatId}`, `{tariffId}`
- Query parameters: camelCase — `?startDate=`, `?endDate=`, `?period=`
- No trailing slashes

**Backend code (.NET):**
- Classes, methods, properties: PascalCase
- Private fields: `_camelCase`
- Local variables, parameters: camelCase
- Async methods always suffix `Async` — `SubmitReadingAsync`, `ResolveCurrentTariffAsync`
- Function entry class: `{Feature}Function` → `SubmitReadingFunction`
- Function entry method: always `RunAsync`
- Request DTOs: `{Action}{Entity}Request` — `SubmitReadingRequest`, `CreateTariffRequest`
- Response DTOs: `{Entity}Response` or `{Entity}Summary` — `ReadingResponse`, `DashboardSummary`
- EF Core configuration classes: `{Entity}Configuration : IEntityTypeConfiguration<{Entity}>`

**Frontend code (TypeScript/React):**
- Component files and exports: PascalCase (`KpiTile.tsx`, `ReadingSheet.tsx`)
- Hook files and exports: `use` prefix camelCase (`useDashboard.ts`, `useSubmitReading.ts`)
- Utility/API client files: camelCase (`readingApi.ts`, `tariffApi.ts`)
- Types and interfaces: PascalCase (`ReadingResponse`, `DashboardData`)
- Event handler props: `on{Event}` (`onSave`, `onClose`, `onSuccess`)
- Local handler functions: `handle{Event}` (`handleSave`, `handleClose`)
- TanStack Query cache keys: `[resource, flatId, ...params]` tuple — e.g., `['dashboard', flatId]`, `['decomposition', flatId, { startDate, endDate }]`
- Mutation invalidations target the first segment to flush all related queries

### Structure Patterns

**Frontend feature folders mirror backend slices exactly:**
```
client/src/
├── features/
│   ├── dashboard/
│   │   ├── components/       # KpiTile.tsx, TrendChart.tsx
│   │   ├── hooks/            # useDashboard.ts
│   │   └── api/              # dashboardApi.ts
│   ├── readings/
│   ├── tariffs/
│   ├── smart-plug-import/
│   ├── decomposition/
│   ├── insights/
│   ├── flat-structure/
│   ├── onboarding/
│   └── settings/
├── components/ui/             # shadcn/ui generated only — never hand-edited
├── lib/
│   ├── apiClient.ts           # base fetch wrapper (auth header, error parsing)
│   ├── queryClient.ts         # TanStack Query client instance
│   └── i18n.ts                # i18next initialisation
└── main.tsx
```

**Test placement:**
- Backend: `api.Tests/` project at monorepo root, mirroring `api/Features/` — e.g., `api.Tests/Features/Readings/SubmitReadingTests.cs`
- Frontend: co-located `.test.tsx` / `.test.ts` files next to the file under test

### Format Patterns

**HTTP status codes:**

| Status | Used for |
|---|---|
| 200 OK | GET, PUT, PATCH success |
| 201 Created | POST resource creation; include `Location` header |
| 202 Accepted | Async operation accepted (import upload) |
| 204 No Content | DELETE success |
| 400 Bad Request | Input validation failures |
| 403 Forbidden | Authenticated but wrong tenant |
| 404 Not Found | Resource not found within tenant |
| 422 Unprocessable Entity | Business rule violation (tariff lock, etc.) |
| 503 Service Unavailable | Import or insight discovery unavailable |

401 is handled by SWA Easy Auth before requests reach Functions — Functions never return 401.

**API response shapes:**
- Single resource success: direct object (no envelope)
- Small collection: direct array
- Paginated collection (if needed): `{ "items": [...], "totalCount": N }`
- All errors: Problem Details RFC 9457 — `{ "type", "title", "status", "detail" }`

**JSON field naming:** camelCase throughout. Configured once in `Program.cs`:
```csharp
.AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
```

**Decimal values in JSON:** Serialised as JSON numbers (not strings). All calculations happen in C# `decimal`; the frontend only displays values via `Intl.NumberFormat` — no arithmetic on received values, so IEEE 754 double precision is sufficient for display.

**Datetimes in JSON:** ISO 8601 with explicit timezone offset always — `"2026-06-21T14:30:00+02:00"`. Never local-time-only strings. Formatted at render time with `Intl.DateTimeFormat` using the active locale.

### Backend Language Patterns

**AD-23: DTOs are C# records**
All request and response DTOs use `record` types — not classes. Records provide immutability, value equality, and `with`-expression support with minimal boilerplate. EF Core entity classes remain regular classes (EF Core requires mutable reference types for change tracking).

```csharp
// ✅ Correct — DTO as record
public record SubmitReadingRequest(decimal KwhValue, DateTimeOffset ReadingDate);
public record ReadingResponse(Guid ReadingId, decimal KwhValue, DateTimeOffset ReadingDate, bool IsCorrected);

// ❌ Wrong — DTO as class
public class SubmitReadingRequest { public decimal KwhValue { get; set; } }
```

**AD-24: C# language version — `<LangVersion>latest</LangVersion>`**
Set in `api/energy-tracker-api.csproj`. Enables C# 13 on .NET 10. All agents use current language idioms:
- Primary constructors for dependency injection in service classes
- Collection expressions `[item1, item2]` over `new List<T> { ... }`
- Pattern matching (`is`, `switch` expressions) over type-checking chains
- Raw string literals for multi-line strings or JSON templates
- `required` members on records where a field must always be provided

```csharp
// Primary constructor (C# 12+)
public class TariffResolver(AppDbContext db)
{
    public async Task<Tariff?> ResolveAsync(Guid flatId, DateTimeOffset date, CancellationToken ct) ...
}

// Collection expression (C# 12+)
List<string> errors = [..existingErrors, "New validation error"];
```

### Process Patterns

**Backend error handling:**
- Catch domain exceptions inside `RunAsync`, map to Problem Details + HTTP status
- `ArgumentException` → 400; business rule violation → 422; tenant mismatch → 403; unexpected → 500
- Never propagate raw exception messages to HTTP responses — log to Application Insights, return generic `detail`
- Import error categories (FR-28) mapped at the Function layer, not inside parsers

**Backend decimal invariant:**
`decimal` for all kWh, cost, tariff, and baseline values in entities, DTOs, and computation. `float` and `double` are banned for energy and monetary values. Any story touching these types must explicitly note this constraint.

**CancellationToken threading:**
Every async method accepts and forwards `CancellationToken ct` — Function entry, service methods, EF Core queries (`.ToListAsync(ct)`, `.FirstOrDefaultAsync(ct)`). No `CancellationToken.None` in implementation code.

**Frontend loading states — TanStack Query signals only:**
- Initial load (no cached data): `isLoading === true` → show skeleton
- Background refetch (stale data present): `isFetching === true` → subtle indicator, data stays visible
- Never implement a manual `isLoading` boolean alongside TanStack Query for the same data

**Frontend error handling by context:**
- Server errors from Query: inline error state within the component (not a global toast for data fetch failures)
- Mutation errors: inline per UX spec (sheet stays open, error near Save button — never dismiss on failure)
- Unexpected render errors: React Error Boundary at route level
- User-facing message: parse Problem Details `detail` field; never expose `type` or stack traces

**Frontend forms:**
One zod schema per form, co-located in the feature folder. No shared schema files across features. Two features needing the same shape each define it independently — premature schema sharing creates hidden coupling.

### Enforcement Summary

All agents working on this codebase MUST:

1. Use `decimal` for all energy (kWh) and monetary (€) values — never `float` or `double`
2. Use `DateTimeOffset` for all timestamps — never `DateTime` without timezone context
3. Use C# `record` types for all DTOs; `class` types for EF Core entities only
4. Name Function entry methods `RunAsync`; suffix all async methods with `Async`
5. Use Fluent API for all EF Core entity configuration — no Data Annotation attributes for DB concerns
6. Return Problem Details (RFC 9457) for all HTTP errors — no custom error envelopes
7. Scope every EF Core query to the resolved tenant (UserId/FlatId) — no unscoped queries
8. Thread `CancellationToken ct` through all async call chains
9. Use camelCase for all JSON fields; ISO 8601 with offset for all datetimes
10. Co-locate zod schemas with their feature; follow `[resource, flatId, ...params]` cache key pattern

---

## Project Structure & Boundaries

### Complete Project Directory Structure

```
energy-tracker/                            # monorepo root
├── .github/
│   └── workflows/
│       └── azure-static-web-apps.yml      # CI/CD: Vite build + .NET publish (ReadyToRun)
├── client/                                # Vite + React + TypeScript frontend
├── api/                                   # .NET 10 Azure Functions isolated worker
├── api.Tests/                             # Backend test project
├── staticwebapp.config.json               # SWA routing + linked Functions reference + nav fallback
├── .gitignore
└── README.md
```

**`client/` — Frontend**

```
client/
├── public/
│   └── favicon.svg
├── src/
│   ├── components/
│   │   └── ui/                            # shadcn/ui generated — never hand-edited
│   │       ├── button.tsx
│   │       ├── sheet.tsx
│   │       ├── dropdown-menu.tsx
│   │       ├── toast.tsx
│   │       ├── dialog.tsx
│   │       ├── form.tsx
│   │       └── ...
│   ├── features/
│   │   ├── dashboard/                     # FR-14, FR-15, FR-16, FR-17
│   │   │   ├── components/
│   │   │   │   ├── EuroBurnBackground.tsx  # gradient encodes kWh vs daily budget
│   │   │   │   ├── KpiTile.tsx             # kWh + € pair, budget delta, pulse on update
│   │   │   │   ├── KpiGrid.tsx             # 2×2 phone / 4-across tablet layout
│   │   │   │   ├── TrendChart.tsx          # recharts bar chart; amber spike bars (FR-17)
│   │   │   │   └── EnterReadingButton.tsx  # full-width pill phone / icon-only tablet
│   │   │   ├── hooks/
│   │   │   │   └── useDashboard.ts         # ['dashboard', flatId]
│   │   │   └── api/
│   │   │       └── dashboardApi.ts
│   │   ├── readings/                      # FR-8, FR-9
│   │   │   ├── components/
│   │   │   │   ├── ReadingSheet.tsx        # Enter Reading bottom sheet; numeric keyboard
│   │   │   │   └── ReadingHistorySheet.tsx # edit-with-log flow
│   │   │   ├── hooks/
│   │   │   │   ├── useSubmitReading.ts     # invalidates ['dashboard', flatId] on success
│   │   │   │   └── useReadingHistory.ts    # ['readings', flatId]
│   │   │   ├── api/
│   │   │   │   └── readingApi.ts
│   │   │   └── schemas/
│   │   │       └── readingSchema.ts        # zod: kwhValue > 0, readingDate required
│   │   ├── tariffs/                       # FR-10, FR-11, FR-12, FR-13
│   │   │   ├── components/
│   │   │   │   ├── TariffList.tsx
│   │   │   │   ├── TariffForm.tsx
│   │   │   │   └── TariffLockIndicator.tsx # inline lock + "Locked — contract active"
│   │   │   ├── hooks/
│   │   │   │   ├── useTariffs.ts           # ['tariffs', flatId]
│   │   │   │   └── useCreateTariff.ts
│   │   │   ├── api/
│   │   │   │   └── tariffApi.ts
│   │   │   └── schemas/
│   │   │       └── tariffSchema.ts
│   │   ├── onboarding/                    # FR-4, FR-5, FR-6, FR-7
│   │   │   ├── components/
│   │   │   │   ├── OnboardingGate.tsx      # blocks all main routes until complete
│   │   │   │   ├── OnboardingIntro.tsx     # locale dropdown + "Get Started"
│   │   │   │   ├── OnboardingFlatName.tsx  # step 1
│   │   │   │   └── OnboardingContract.tsx  # step 2: baseline presets + tariff fields
│   │   │   ├── hooks/
│   │   │   │   └── useCompleteOnboarding.ts
│   │   │   ├── api/
│   │   │   │   └── onboardingApi.ts
│   │   │   └── schemas/
│   │   │       └── onboardingSchema.ts
│   │   ├── settings/                      # FR-7, FR-40, FR-41, FR-42
│   │   │   ├── components/
│   │   │   │   ├── SettingsRoot.tsx
│   │   │   │   ├── FlatSettingsCard.tsx
│   │   │   │   ├── LocaleSettings.tsx      # locale dropdown; PUT /user/settings
│   │   │   │   ├── AccountSettings.tsx
│   │   │   │   └── FlatDeleteConfirm.tsx   # type-to-confirm (FR-23)
│   │   │   ├── hooks/
│   │   │   │   ├── useUserSettings.ts      # ['settings'] — includes LocaleOverride
│   │   │   │   └── useUpdateLocale.ts
│   │   │   └── api/
│   │   │       └── settingsApi.ts
│   │   ├── insights/                      # FR-16, FR-17 trend (R1); FR-35–39, FR-43 (R2)
│   │   │   ├── components/
│   │   │   │   ├── InsightsTab.tsx
│   │   │   │   ├── InsightCard.tsx         # standby / replacement / budget / invoice types
│   │   │   │   └── InsightDiscoveryProgress.tsx  # FR-39 progress indicator
│   │   │   ├── hooks/
│   │   │   │   ├── useInsights.ts          # ['insights', flatId]
│   │   │   │   └── useTriggerInsights.ts   # POST trigger; polls run status (FR-38)
│   │   │   └── api/
│   │   │       └── insightsApi.ts
│   │   ├── decomposition/                 # FR-32, FR-33, FR-34 (R2)
│   │   │   ├── components/
│   │   │   │   ├── DecompositionTab.tsx
│   │   │   │   ├── ResidualCard.tsx        # always rendered first; never suppressed (FR-33)
│   │   │   │   ├── RoomCard.tsx
│   │   │   │   ├── DeviceCard.tsx          # rich (measured) or compact (estimated)
│   │   │   │   ├── SmartStripCard.tsx      # strip total + sub-device rows (FR-32)
│   │   │   │   └── DecompositionUnavailable.tsx  # FR-34 empty state + import prompt
│   │   │   ├── hooks/
│   │   │   │   └── useDecomposition.ts     # ['decomposition', flatId, { startDate, endDate }]
│   │   │   └── api/
│   │   │       └── decompositionApi.ts
│   │   ├── smart-plug-import/             # FR-24–28 (R2)
│   │   │   ├── components/
│   │   │   │   ├── ImportSurface.tsx
│   │   │   │   ├── FileUploadZone.tsx      # file picker + drag-drop (desktop/tablet)
│   │   │   │   ├── FileListItem.tsx        # auto-detected type + device association dropdown
│   │   │   │   └── ImportProgressCard.tsx  # persists on Decomposition tab during processing
│   │   │   ├── hooks/
│   │   │   │   ├── useUploadImport.ts      # POST import; returns jobId
│   │   │   │   └── useImportJobStatus.ts   # polls GET import/{jobId} with refetchInterval
│   │   │   └── api/
│   │   │       └── importApi.ts
│   │   └── flat-structure/                # FR-18–23 (R2)
│   │       ├── components/
│   │       │   ├── FlatStructureEditor.tsx  # default 5-room template on first open (FR-22)
│   │       │   ├── RoomEditor.tsx
│   │       │   ├── PowerPointEditor.tsx
│   │       │   └── DeviceEditor.tsx         # EU label / self-measured choice step
│   │       ├── hooks/
│   │       │   ├── useFlatStructure.ts      # ['flat-structure', flatId]
│   │       │   └── useUpdateFlatStructure.ts
│   │       └── api/
│   │           └── flatStructureApi.ts
│   ├── lib/
│   │   ├── apiClient.ts                   # base fetch: auth header, /api/v1 prefix, Problem Details parsing
│   │   ├── queryClient.ts                 # TanStack Query client (staleTime, retry config)
│   │   ├── i18n.ts                        # i18next init; browser-languagedetector; namespace loading
│   │   └── authClient.ts                  # SWA Easy Auth wrappers: login(), getMe(), logout() — /.auth/* endpoints
│   ├── locales/
│   │   ├── de-DE/
│   │   │   ├── common.json                # shared labels, buttons, errors, status
│   │   │   ├── dashboard.json
│   │   │   ├── readings.json
│   │   │   ├── tariffs.json
│   │   │   ├── insights.json
│   │   │   ├── decomposition.json
│   │   │   ├── import.json
│   │   │   ├── flat-structure.json
│   │   │   ├── onboarding.json
│   │   │   └── settings.json
│   │   └── en-US/
│   │       └── (same files)
│   ├── router.tsx                         # react-router-dom v7 route tree (lazy imports per tab)
│   ├── App.tsx                            # MsalProvider + QueryClientProvider + RouterProvider
│   ├── main.tsx                           # Vite entry point
│   └── index.css                          # Tailwind v4 base import
├── index.html
├── vite.config.ts                         # @tailwindcss/vite plugin; /api proxy → localhost:7071
├── tsconfig.json
├── tsconfig.app.json
├── tsconfig.node.json
├── components.json                        # shadcn/ui configuration
└── package.json
```

**`api/` — Backend**

```
api/
├── Features/
│   ├── Readings/                          # FR-8, FR-9
│   │   ├── SubmitReadingFunction.cs       # POST /api/v1/flats/{flatId}/readings
│   │   ├── GetReadingHistoryFunction.cs   # GET  /api/v1/flats/{flatId}/readings
│   │   ├── ReadingModels.cs               # record SubmitReadingRequest; record ReadingResponse
│   │   └── ReadingValidator.cs
│   ├── Dashboard/                         # FR-14, FR-15, FR-16, FR-17
│   │   ├── GetDashboardFunction.cs        # GET /api/v1/flats/{flatId}/dashboard
│   │   ├── KpiCalculator.cs               # daily avg, weekly avg, monthly projection, spike detection
│   │   └── DashboardModels.cs             # record DashboardSummary
│   ├── Tariffs/                           # FR-10, FR-11, FR-12, FR-13
│   │   ├── CreateTariffFunction.cs        # POST  /api/v1/flats/{flatId}/tariffs
│   │   ├── UpdateTariffFunction.cs        # PATCH /api/v1/flats/{flatId}/tariffs/{tariffId}
│   │   ├── GetTariffsFunction.cs          # GET   /api/v1/flats/{flatId}/tariffs
│   │   ├── TariffModels.cs
│   │   └── TariffValidator.cs             # enforces contract-period lock rule (FR-11)
│   ├── SmartPlugImport/                   # FR-24–28 (R2)
│   │   ├── UploadFunction.cs              # POST /api/v1/flats/{flatId}/imports → 202; writes blob
│   │   ├── ProcessImportFunction.cs       # [BlobTrigger] → parse + interpolate + store + reconcile
│   │   ├── GetImportStatusFunction.cs     # GET /api/v1/flats/{flatId}/imports/{jobId}
│   │   ├── EveHomeParser.cs               # FR-24: xlsx; local-time timestamps preserved
│   │   ├── MerossParser.cs                # FR-25: CSV BOM stripping, tab-comma quirk
│   │   ├── InterpolationEngine.cs         # FR-26: gap detection, linear interpolation, 7-day cap
│   │   ├── ReconciliationEngine.cs        # FR-27: attributed ≤ main meter total; residual
│   │   └── ImportModels.cs
│   ├── Decomposition/                     # FR-32, FR-33, FR-34 (R2)
│   │   ├── GetDecompositionFunction.cs    # GET /api/v1/flats/{flatId}/decomposition
│   │   ├── DecompositionEngine.cs         # daily data × flat structure × TariffResolver
│   │   └── DecompositionModels.cs         # ResidualKwh always present in response
│   ├── Insights/                          # FR-35–39, FR-43 (R2)
│   │   ├── TriggerInsightsFunction.cs     # POST /api/v1/flats/{flatId}/insights/trigger → 202
│   │   ├── ProcessInsightsFunction.cs     # [QueueTrigger] → runs detectors → updates InsightRun
│   │   ├── ScheduledInsightsFunction.cs   # [TimerTrigger("0 0 2 * * *")] → enqueues per flat
│   │   ├── GetInsightsFunction.cs         # GET /api/v1/flats/{flatId}/insights
│   │   ├── StandbyDetector.cs             # FR-35: interval data only; Meross excluded
│   │   ├── ReplacementDetector.cs         # FR-36
│   │   ├── BudgetAlertDetector.cs         # FR-37
│   │   ├── InvoiceDeviationDetector.cs    # FR-43; ±10% of AnnualKwhBaseline threshold
│   │   └── InsightModels.cs
│   ├── FlatStructure/                     # FR-18–23 (R2)
│   │   ├── GetFlatStructureFunction.cs    # GET /api/v1/flats/{flatId}/structure
│   │   ├── UpdateFlatStructureFunction.cs # PUT /api/v1/flats/{flatId}/structure
│   │   └── FlatStructureModels.cs
│   ├── Onboarding/                        # FR-4, FR-5, FR-6, FR-7
│   │   ├── CompleteOnboardingFunction.cs  # POST /api/v1/onboarding
│   │   ├── OnboardingModels.cs
│   │   └── OnboardingValidator.cs
│   └── Settings/                          # FR-7, FR-40, FR-41, FR-42
│       ├── GetUserSettingsFunction.cs     # GET /api/v1/user/settings
│       ├── UpdateUserSettingsFunction.cs  # PUT /api/v1/user/settings
│       └── SettingsModels.cs
├── Data/
│   ├── AppDbContext.cs
│   ├── Configurations/                    # one IEntityTypeConfiguration<T> per entity
│   │   ├── UserConfiguration.cs
│   │   ├── FlatConfiguration.cs
│   │   ├── TariffConfiguration.cs
│   │   ├── MeterReadingConfiguration.cs
│   │   ├── RoomConfiguration.cs
│   │   ├── PowerPointConfiguration.cs
│   │   ├── DeviceConfiguration.cs
│   │   ├── SmartPlugDailyDataConfiguration.cs
│   │   ├── SmartPlugIntervalDataConfiguration.cs
│   │   ├── ImportJobConfiguration.cs
│   │   ├── InsightRunConfiguration.cs
│   │   └── InsightConfiguration.cs        # Data column: nvarchar(max) JSON
│   ├── Entities/                          # EF Core entity classes — not records
│   │   ├── User.cs
│   │   ├── Flat.cs
│   │   ├── Tariff.cs
│   │   ├── MeterReading.cs
│   │   ├── Room.cs
│   │   ├── PowerPoint.cs
│   │   ├── Device.cs
│   │   ├── SmartPlugDailyData.cs
│   │   ├── SmartPlugIntervalData.cs
│   │   ├── ImportJob.cs
│   │   ├── InsightRun.cs
│   │   └── Insight.cs
│   └── Migrations/                        # EF Core generated; never hand-edited
├── Shared/
│   ├── TenantResolver.cs                  # reads X-MS-CLIENT-PRINCIPAL; returns UserId
│   ├── TariffResolver.cs                  # ResolveAsync(flatId, date, ct) — domain invariant
│   └── LocaleResolver.cs                  # Accept-Language negotiation + stored override
├── Program.cs                             # host builder: DI, EF Core, managed identity middleware
├── host.json                              # Functions runtime config (logging, retry policies)
├── local.settings.json                    # local dev secrets — gitignored
└── energy-tracker-api.csproj             # <LangVersion>latest</LangVersion>; <PublishReadyToRun>true</PublishReadyToRun>
```

**`api.Tests/`**

```
api.Tests/
├── Features/
│   ├── Dashboard/
│   │   └── KpiCalculatorTests.cs          # spike detection boundary conditions
│   ├── Readings/
│   │   └── SubmitReadingTests.cs
│   ├── Tariffs/
│   │   └── TariffValidatorTests.cs        # contract-lock enforcement (FR-11)
│   ├── SmartPlugImport/
│   │   ├── EveHomeParserTests.cs          # timezone preservation, deduplication
│   │   ├── MerossParserTests.cs           # BOM stripping, zero-value handling (FR-25)
│   │   ├── InterpolationEngineTests.cs    # gap detection, 7-day cap (FR-26)
│   │   └── ReconciliationEngineTests.cs   # attributed ≤ meter total (FR-27)
│   ├── Decomposition/
│   │   └── DecompositionEngineTests.cs    # period-accurate tariff costing invariant
│   └── Insights/
│       ├── BudgetAlertDetectorTests.cs
│       └── InvoiceDeviationDetectorTests.cs
├── Shared/
│   └── TariffResolverTests.cs             # boundary: finds correct tariff for any date
└── api.Tests.csproj
```

### Architectural Boundaries

**Auth boundary:** SWA Easy Auth validates OIDC tokens at the edge. The Functions app never validates tokens — it reads the pre-validated `X-MS-CLIENT-PRINCIPAL` header via `TenantResolver`. A missing or malformed header returns 403.

**Tenant boundary:** Every Function verifies `FlatId` belongs to the resolved `UserId` before touching any data. A valid session with a foreign `FlatId` returns 403. Enforcement is at the Function layer, not inside engines or calculators.

**Async boundary:** HTTP returns immediately for Tier 3 operations. `UploadFunction` → 202 + `importJobId`. `TriggerInsightsFunction` → 202 + `runId`. Processing runs entirely in blob/queue/timer triggered Functions. Frontend polls status endpoints until completion.

**Data boundary:** Frontend has no direct access to Azure SQL, Blob Storage, or Storage Queues. All data flows through `/api/v1/`.

### Data Flow — Key Scenarios

**UJ-1: Submit Reading**
```
POST /api/v1/flats/{flatId}/readings
  → SubmitReadingFunction: validate → write MeterReading → 201 ReadingResponse
Frontend: invalidate ['dashboard', flatId] → KPI tiles refetch and pulse
```

**UJ-2: Smart Plug Import**
```
POST /api/v1/flats/{flatId}/imports
  → UploadFunction: create ImportJob (Pending) → write blob → 202 { importJobId }
Blob trigger:
  → ProcessImportFunction: parse → interpolate → store daily + interval rows
    → reconcile → update ImportJob (Complete | Failed)
Frontend: poll GET /api/v1/flats/{flatId}/imports/{jobId} every 3s
  → on Complete: invalidate ['decomposition', flatId]
```

**UJ-3: Insight Discovery**
```
POST /api/v1/flats/{flatId}/insights/trigger
  → TriggerInsightsFunction: create InsightRun (Pending) → enqueue → 202 { runId }
Queue trigger:
  → ProcessInsightsFunction: run detectors → write Insight rows → InsightRun (Complete)
Scheduled (02:00 UTC):
  → ScheduledInsightsFunction: enqueue per flat → same ProcessInsightsFunction handles it
Frontend: poll ['insights', flatId] while run in progress; prior cards remain visible
```

---

## Step 7 — Architecture Validation

_Validation date: 2026-06-21_

### Coherence Validation

**Decisions are compatible.** All 24 architectural decisions were cross-checked against each other and against the full technology stack. One inconsistency was found and corrected inline during this validation:

**Correction applied — AD-9 vs frontend library list:** `@azure/msal-react` was incorrectly listed as a frontend dependency. With SWA Easy Auth (AD-9), the SWA handles the full OIDC flow at the edge — the React app has no token-management responsibility. All auth interaction is via the three SWA-provided endpoints (`/.auth/login/aad`, `/.auth/me`, `/.auth/logout`). Removed `@azure/msal-react` from the npm install command and replaced `msalConfig.ts` with `authClient.ts` (wrapping the three SWA auth endpoints). `MsalProvider` is not needed in `App.tsx`; auth state is a simple `useAuth` hook that fetches `/.auth/me`.

All other combinations confirmed compatible:
- .NET 10 + Flex Consumption (Linux) + ReadyToRun `linux-x64`: GA as of .NET 10 release (Nov 2025)
- shadcn/ui v4 + Tailwind v4 + `@tailwindcss/vite`: no `tailwind.config.js` required
- EF Core Fluent API config + `decimal` + `DateTimeOffset` throughout: consistent, no attribute annotations anywhere
- C# records for DTOs / classes for entities: correct separation, no structural conflict
- VSA slice structure (backend) mirrors feature-folder structure (frontend): deliberate and consistent
- Problem Details RFC 9457 responses + TanStack Query error parsing in `apiClient.ts`: aligned
- Path-based API versioning `/api/v1/` in Function `Route` attribute; SWA proxies `/api/*`: correct

### Requirements Coverage

All 43 functional requirements and all 4 non-functional requirements are architecturally supported:

| FR Group | FRs | Architectural support |
|---|---|---|
| Auth | FR-1–3 | SWA Easy Auth (OIDC, Entra ID, swappable via config); `TenantResolver.cs` |
| Onboarding | FR-4–7 | `Onboarding/` slice; `OnboardingGate.tsx` blocks all routes until complete |
| Meter Reading | FR-8–9 | `Readings/` slice; retroactive entry supported by period-accurate tariff lookup |
| Tariff Management | FR-10–13 | `Tariffs/` slice; `TariffValidator.cs` (FR-11 contract lock); `TariffResolver.cs` domain invariant |
| KPI Dashboard | FR-14–15 | `Dashboard/KpiCalculator.cs`; TanStack Query invalidation on reading submit (FR-15 immediate update) |
| Trends / Spike | FR-16–17 | `KpiCalculator.cs` spike detection; `TrendChart.tsx` amber bars |
| Multi-Flat | FR-18–23 | `FlatStructure/` slice (R2); cascade delete via `FlatConfiguration.cs` |
| Smart Plug Import | FR-24–25 | `EveHomeParser.cs`; `MerossParser.cs`; blob-triggered `ProcessImportFunction` |
| Gap / Interpolation | FR-26 | `InterpolationEngine.cs`; interpolated rows flagged `IsInterpolated = true` |
| Reconciliation | FR-27 | `ReconciliationEngine.cs`; tolerance: ±0.1 kWh (clean periods), ±1.0 kWh (periods containing interpolated values) |
| Import Error Handling | FR-28 | Problem Details `type` field maps to three user-visible categories |
| Device Registry | FR-29–31 | `Device` entity; `ConsumptionApproach` enum; `DeviceEditor.tsx` two-path choice step |
| Decomposition | FR-32–34 | `DecompositionEngine.cs`; `ResidualCard.tsx` always rendered; `DecompositionUnavailable.tsx` for no-data periods |
| Standby Detection | FR-35 | `StandbyDetector.cs` on `SmartPlugIntervalData` (Eve Home only); Meross excluded by data format constraint (AD-2) |
| Insights | FR-36–39 | `Insights/` slice; queue-triggered processing; timer trigger at 02:00 UTC; `InsightDiscoveryProgress.tsx` |
| Localization | FR-40–42 | `LocaleResolver.cs`; `i18n.ts`; `Intl.NumberFormat` / `Intl.DateTimeFormat` at render time only; stored locale override in user profile |
| Invoice Deviation | FR-43 | `InvoiceDeviationDetector.cs`; threshold ±10% of `AnnualKwhBaseline` |
| NFR-1 Performance | — | Three-tier model; Tier 1 synchronous; Tier 3 blob + queue async |
| NFR-2 Security | — | Managed identity (AD-10); tenant isolation middleware (AD-12); `decimal` enforcement rule |
| NFR-3 i18n | — | ISO 8601 with offset storage; locale-neutral API layer; render-time `Intl.*` only |
| NFR-4 Reliability | — | Azure Blob Storage + blob trigger + Storage queues + Azure SQL (AD-1) |

**Open PRD questions resolved during architecture:**
- Q-7 (reconciliation tolerance for interpolated periods): **±1.0 kWh** (10× clean-data tolerance, reflecting interpolation uncertainty). Governs `ReconciliationEngine.cs` and CAP-9 success criteria.
- Q-8 (invoice deviation threshold): **±10% of `AnnualKwhBaseline`**. Governs `InvoiceDeviationDetector.cs`.
- A-8 (standby detection sub-daily data): **Eve Home retains interval rows in `SmartPlugIntervalData`; Meross excluded** by format constraint (daily aggregates only). Governs `StandbyDetector.cs` scope.

### Implementation Readiness

**Decision completeness:** All 24 architectural decisions (AD-1 through AD-24) include rationale and specific technology choices with versions or version channels.

**Structure completeness:** Full file trees for `client/`, `api/`, and `api.Tests/`. Every FR maps to a named file. Architectural boundaries (auth, tenant, async, data) are explicitly defined.

**Pattern completeness:** Naming conventions, 10-point enforcement rules, error handling (Problem Details + TanStack Query), loading states, form validation, and locale formatting patterns are all specified.

**Minor implementation details** (no architectural decision required; standard defaults apply):
- Azure Functions HTTP trigger authorization level: `AuthorizationLevel.Anonymous` on all triggers (SWA Easy Auth is the gate)
- Blob Storage container naming: `smart-plug-imports/{userId}/{flatId}/{importJobId}.{ext}`
- `host.json` retry policy: Azure Functions default (5 attempts, exponential backoff) for blob and queue triggers

### Architecture Completeness Checklist

- [x] Problem statement and goals documented
- [x] Technology stack with versions specified
- [x] Data store decision made and justified (Azure SQL Basic DTU)
- [x] Authentication and authorization approach defined (SWA Easy Auth + TenantResolver)
- [x] API design pattern specified (path-based versioning, Problem Details RFC 9457)
- [x] Frontend architecture defined (VSA feature folders, TanStack Query, react-hook-form + zod)
- [x] Backend architecture defined (VSA slices, EF Core Fluent API, isolated worker)
- [x] Data model entities identified (all entities, relationships, JSON column for Insights.Data)
- [x] Async processing pattern defined (blob trigger → queue → timer trigger)
- [x] Error handling strategy defined (Problem Details on backend; TanStack Query parsing on frontend)
- [x] Security approach defined (managed identity, no private endpoints, Key Vault provisioned)
- [x] Internationalization approach defined (ISO 8601 storage, render-time Intl.*, stored locale override)
- [x] Project structure specified (complete file trees for client, api, api.Tests)
- [x] All functional requirements traced to architectural components
- [x] Non-functional requirements addressed
- [x] Implementation patterns and consistency rules documented

### Overall Status

**READY FOR IMPLEMENTATION**

All 43 functional requirements are architecturally supported. All 4 non-functional requirements are addressed. All major technical decisions are made and mutually consistent. The one coherence gap found (MSAL vs SWA Easy Auth) was corrected in this validation pass. No critical gaps remain. Minor implementation details (auth level, container naming, retry policy) are standard defaults that require no further discussion.
