---
project_name: 'energy-tracker'
user_name: 'Ralf'
date: '2026-06-30'
sections_completed: ['technology_stack', 'language_rules', 'framework_rules', 'testing_rules', 'code_quality', 'workflow', 'critical_rules']
status: 'complete'
rule_count: 152
optimized_for_llm: true
---

# Project Context for AI Agents

_Critical rules and patterns AI agents must follow when implementing code in this project. Focuses on unobvious details agents commonly miss._

---

## Technology Stack & Versions

### Frontend
| Package | Version |
|---|---|
| React | 19.x |
| TypeScript | 6.0.x |
| Vite | 8.x |
| shadcn/ui | v4 (Tailwind v4 — no `tailwind.config.js`) |
| Tailwind CSS | v4 via `@tailwindcss/vite` plugin |
| TanStack Query | v5.101 |
| react-hook-form | v7.80 |
| zod | v4.4 |
| react-router-dom | v7.18 |
| react-i18next / i18next | v17 / v26 |
| recharts | v3.9 |
| lucide-react | v1.21 |
| Vitest | v4.1 |
| @testing-library/react | v16.3 |

### Backend
| Package | Version |
|---|---|
| .NET | 10 LTS (`<LangVersion>latest</LangVersion>` = C# 13) |
| Azure Functions Worker | v4 isolated (`Microsoft.Azure.Functions.Worker` 2.52) |
| EF Core (SqlServer) | 10.0.9 |
| FluentValidation | 12.1.1 |
| Azure.Identity | 1.21.0 |
| Azure.Storage.Blobs | 12.29.1 |
| Azure.Storage.Queues | 12.27.1 |
| ExcelDataReader | 3.9.0 |
| Microsoft.Data.SqlClient | 7.0.2 |

### Version Gotchas (things agents get wrong)

**TanStack Query v5**
- Mutations: `isPending` not `isLoading` — `isLoading` is `undefined`, spinner never shows
- `useQuery` requires object form: `useQuery({ queryKey: [...], queryFn: ... })` — positional overload removed
- `refetchInterval` function signature changed: receives `(query)` not `(data)` — use `query.state.data?.status` not `data?.status`
- `invalidateQueries({ queryKey: ['dashboard'] })` invalidates ALL keys starting with that segment — scope to `['dashboard', flatId]` for future-proofing

**zod v4**
- `.optional()` / `.nullable()` type inference is stricter; `null` and `undefined` are not interchangeable — use `.nullish()` only when both are valid
- `.transform()` after `.optional()` receives `string | undefined` — always handle `undefined` inside the transform
- Use `z.coerce.number().positive()` for numeric form fields — `z.number()` rejects empty string `""` from untouched inputs
- Never share one schema between form validation and API response typing — they have different shapes

**react-router-dom v7**
- This project uses lazy component imports only — no `loader`/`action` data router functions; generating a `loader` on a route silently does nothing
- `useNavigate` / `useParams` / `useLocation` only work inside the router tree (below `RouterProvider`) — never call from `App.tsx` level

**React 19**
- `use(promise)` requires a Suspense boundary — this project has none (TanStack Query owns loading states); do not use `use()` for data fetching

**shadcn/ui**
- `client/src/components/ui/` is generated — never hand-edit; create wrapper components or extend via CVA variants in the feature folder
- Use `<FormField control={form.control} name="..." render={...}>` (not `register()`) when using shadcn `Form` component; mixing the two breaks error display
- Do not nest `Sheet` inside `Dialog` — overlapping portals break `aria-hidden` and z-index

**Tailwind v4**
- No `tailwind.config.js` — custom tokens go in `@theme {}` block in `client/src/index.css`

**TypeScript 6**
- Stricter module handling; use `import type` for type-only imports; no CommonJS `require()`

**Azure Functions v4 isolated worker**
- Route template: `Route = "v1/..."` not `"api/v1/..."` — SWA strips `/api` before forwarding; writing `api/v1/...` causes 404 in all environments
- Logging: inject `ILogger<T>` via primary constructor — `context.GetLogger()` does not exist in isolated worker
- FluentValidation 12 DI: use `AddValidatorsFromAssembly()` not `AddFluentValidation()`
- `context.GetUserId()` throws on non-HTTP triggers (blob/queue/timer) — middleware skips auth for these; get `userId`/`flatId` from trigger payload instead

**C# 13 primary constructors**
- Do not add a separate `private readonly` backing field for injected services — the constructor parameter IS the field

**EF Core 10**
- Always use `DateTimeOffset` (not `DateTime` / `DateTime.UtcNow`) — loses offset and breaks timezone-sensitive queries
- `SingleOrDefaultAsync` for PK/unique-constrained lookups; `FirstOrDefaultAsync` only when multiple rows are expected
- `Insights.Data` is opaque `nvarchar(max)` JSON — deserialize in application layer; do not write LINQ predicates against its properties
- Cascade deletes are configured via Fluent API — do not manually pre-delete child entities before removing a `Flat`; EF handles it

**Vitest (globals: true)**
- `describe` / `it` / `expect` are global — do not import them from `vitest`

**Path alias**
- Always use `@/` (maps to `client/src/`) — never use relative paths climbing out of `src/`

## Critical Implementation Rules

### Language-Specific Rules

#### TypeScript / Frontend

**Imports**
- `@/` alias for all imports from `src/` — never relative paths like `../../lib/`
- `import type` for type-only imports (TS6 strict module mode)
- No barrel (`index.ts`) files — import directly from the file that declares the symbol

**Async patterns**
- All data fetching via TanStack Query hooks — no raw `fetch()` calls in components
- Mutations always use `useMutation`; never `useState` + manual fetch in an event handler
- `async/await` throughout; no `.then()` chains

**Null handling**
- `undefined` is the absent value throughout — avoid `null` except where the API explicitly returns it (nullable FK fields in responses)
- Optional chaining `?.` and nullish coalescing `??` — no `!` non-null assertions in feature code

**Component conventions**
- Named exports only — no default exports except page-level route components (`DashboardPage`, `OnboardingPage`)
- Event handler props: `on{Event}` — local handlers: `handle{Event}`
- No inline `style={{}}` — Tailwind classes only

#### C# / Backend

**Async methods**
- Every async method accepts `CancellationToken ct` as last parameter — always forward it to every awaitable: `.ToListAsync(ct)`, `.SaveChangesAsync(ct)`, `.FirstOrDefaultAsync(ct)`
- All async method names end in `Async` — `RunAsync`, `ResolveAsync`, `SaveChangesAsync`

**Records vs classes**
- DTOs (request/response): `record` types with positional parameters
- EF Core entities: `class` types (mutable, for change tracking)
- Internal value types (e.g., `ClientPrincipal`): `sealed record`

**C# 13 idioms to use**
- Primary constructors for all service/function classes with DI
- Collection expressions `[item1, item2]` over `new List<T> { ... }`
- Pattern matching (`is`, `switch` expressions) over type-checking chains
- Raw string literals for multi-line strings or inline JSON templates

**Decimal invariant**
- `decimal` for ALL kWh, cost, tariff, baseline, and budget values — at every layer (entity, DTO, computation)
- `float` and `double` are banned for energy and monetary values — no exceptions

**JSON deserialization in Functions**
- Deserialize request body via `JsonSerializer.DeserializeAsync<T>(req.Body, _jsonOptions, ct)` where `_jsonOptions` is a `private static readonly` field: `new() { PropertyNameCaseInsensitive = true }`
- Never use `req.ReadFromJsonAsync<T>()` — inconsistent with the stream-based pattern used throughout

### Framework-Specific Rules

#### React / TanStack Query

**Data fetching hooks**
- One custom hook per query, co-located in `features/{feature}/hooks/`
- Hook returns the full TanStack Query result: `return useQuery({ queryKey: [...], queryFn: ... })`
- Query key tuple pattern: `[resource, flatId]` or `[resource, flatId, { ...params }]`
- Use `isLoading` (initial, no data) vs `isFetching` (background refetch, data present) — never implement a manual boolean alongside them for the same data

**Mutations**
- One hook per mutation in `features/{feature}/hooks/`
- `onSuccess`: `await queryClient.invalidateQueries(...)` before closing the sheet — never fire-and-forget invalidation when the subscriber component may unmount immediately
- Scope invalidations to `['resource', flatId]` — not just `['resource']` — to future-proof for multi-flat
- `mutation.error` (typed as `Error & { detail?: string }`) is the source for server-side error messages — display as a separate error banner near the Save button, distinct from `form.formState.errors` (field-level zod failures)
- Mutation errors: sheet/form stays open; display error near Save button — never dismiss on failure
- Never optimistically update the cache unless the story spec explicitly calls for it

**API modules**
- One file per feature in `features/{feature}/api/` (e.g., `dashboardApi.ts`)
- All calls go through `apiClient` from `@/lib/apiClient` — never raw `fetch()`
- Paths start immediately after `/api/v1` — e.g., `apiClient.get('/flats/${flatId}/dashboard')` — never include the `/api/v1` prefix (it is already in `apiClient.ts`)

**Loading states (TanStack Query signals only)**
- `isLoading === true` → show skeleton (no cached data)
- `isFetching === true` → subtle indicator; keep data visible
- Never implement a parallel `isLoading` boolean in component state

**Error handling**
- Query errors: inline error state within the component — no global toast for data fetch failures
- Mutation errors: parse `(error as any).detail` from Problem Details — never expose `type` or stack traces to user
- Do not modify global `queryClient.ts` `staleTime` — it is the reactivity contract for the entire app; slice-level overrides go in individual `useQuery` calls

**Forms**
- One zod schema per form, co-located in `features/{feature}/schemas/`
- All forms: `useForm({ resolver: zodResolver(schema), mode: 'onBlur' })` — not default `'onSubmit'` mode
- Use shadcn `Form` + `FormField` + `Controller` — not bare `register()`
- `defaultValues` must match the zod schema's input type exactly
- Two features needing the same shape each define the schema independently — no shared schema files

**i18n**
- All user-visible strings via `useTranslation(namespace)` — no hardcoded English strings in JSX
- Namespace matches the feature folder name: `useTranslation('readings')` in readings components
- Only truly shared labels go in `'common'` namespace
- When adding a new feature namespace, add it to the `ns: [...]` array in `client/src/lib/i18n.ts` — the glob loader and namespace registry are separate; both must be updated
- Supported locales: `de-DE` and `en-US` only
- Formatting: `Intl.NumberFormat` / `Intl.DateTimeFormat` at render time only — never in the API layer

**VSA slice isolation**
- `flatId` is sourced from `useParams()` or passed as a prop — never by importing a hook from another feature slice
- Each slice is self-contained — cross-slice hook imports are forbidden
- Feature folder structure is mandatory even for single files: `components/`, `hooks/`, `api/`, `schemas/` subdirectories always present; never place component files at the feature root

**`staticwebapp.config.json`**
- The `navigationFallback` block must be preserved exactly — never restructure it when adding API route rules; new API exclusions go in `routes` only

#### Local Development

- Running Vite + `func start` directly means all requests hit Functions without the `X-MS-CLIENT-PRINCIPAL` header → 403 on every call
- Local auth simulation requires `swa start` (SWA CLI) — this is not an application code fix

#### Azure Functions / Backend

**Function class shape**
```csharp
public class {Feature}Function({Dependencies} dep1, dep2)  // primary constructor
{
    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    [Function("{FunctionName}")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "verb", Route = "v1/...")] HttpRequest req,
        FunctionContext context,
        CancellationToken ct)
    { ... }
}
```

**Tenant enforcement (HTTP functions only)**
- First meaningful line of `RunAsync`: `var userId = context.GetUserId();`
- Verify `flatId` (from route) belongs to `userId` before any data access — return 403 if not
- Non-HTTP triggers: userId/flatId come from trigger payload (e.g., blob path), not from context — `context.GetUserId()` throws on non-HTTP triggers

**Error responses — Problem Details only**
```csharp
return new BadRequestObjectResult(new {
    type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
    title = "Bad Request", status = 400, detail = "..."
});
```
- Anonymous objects only — no typed Problem Details class
- Never return raw exception messages — log via `ILogger<T>`, return generic `detail`

**DI lifetime rules**
- Services taking `AppDbContext` must be registered as `Scoped` — never `Singleton`
- Pure in-memory services (no DB) may be `Singleton`
- Never instantiate validators or services with `new` inside a Function — always resolve via DI
- Validators needing DB access: `Scoped`; pure validators: `Singleton`

**EF Core patterns**
- All entity configuration in `Data/Configurations/{Entity}Configuration.cs` implementing `IEntityTypeConfiguration<{Entity}>`
- No Data Annotation attributes on entity classes for DB concerns
- Every query scoped to tenant: filter by `UserId` (via `Flat`) on every data access
- `decimal` columns: configure precision explicitly — `.HasColumnType("decimal(18,4)")`
- `SingleOrDefaultAsync` for PK/unique-constrained lookups; `FirstOrDefaultAsync` only when multiple rows are expected
- Trust EF cascade deletes — do not manually pre-delete child entities before removing a `Flat`
- Migrations: run `dotnet ef migrations list` before adding a new migration to verify order; parent tables must exist before FK-referencing tables

#### Cascading Failure Rules (cross-cutting)

- Modifying global `queryClient.ts` breaks the reactivity contract for all slices — never change global config for a per-slice concern
- `staticwebapp.config.json` `navigationFallback` removal breaks all deep links and browser refresh — protect this block
- Scoped-as-Singleton service registration causes `ObjectDisposedException` on the second request — all services taking DbContext must be Scoped
- Wrong migration order (FK before parent) leaves DB in partially migrated state blocking all subsequent deploys — verify order before generating

### Testing Rules

#### Frontend (Vitest + @testing-library/react)

**Setup**
- `globals: true` — `describe`/`it`/`expect` are global; do not import them from `vitest`
- Setup file: `client/src/test-setup.ts` — `@testing-library/jest-dom` matchers are auto-imported
- Test environment: `jsdom`
- Test files: co-located `.test.tsx` / `.test.ts` next to the file under test

**Component test wrappers**
- Components using routing hooks: wrap in `MemoryRouter`
- Components using TanStack Query: wrap in `QueryClientProvider` with a fresh `QueryClient` per test
- Components using i18n: `vi.mock('react-i18next', () => ({ useTranslation: () => ({ t: (k: string) => k }) }))` — never rely on real i18n initialisation in tests

**Query selectors**
- Query by role / label / text — never by CSS class or `data-testid` unless unavoidable

**What to test**
- Render output, user interactions, conditional display — not implementation details
- Hooks: use `renderHook` from `@testing-library/react`
- Do NOT test `components/ui/` internals — generated and tested upstream

**Mocking**
- Mock API modules (`vi.mock('@/features/x/api/xApi')`) not `apiClient` directly
- Mock mutation hooks (`vi.mock('../hooks/useSubmitReading')`) not the underlying fetch
- Set mocks at module scope; reset in `beforeEach` if state bleeds between tests

#### Backend (xUnit + EF Core InMemory)

**Test placement**
- `api.Tests/Features/{Feature}/{Class}Tests.cs` — mirrors `api/Features/{Feature}/`
- `api.Tests/Shared/` for shared infrastructure (e.g., `TariffResolverTests.cs`)

**EF Core in tests**
- `InMemory` provider for unit-speed tests — does not enforce FK constraints, column types, or `decimal` precision
- Do not write InMemory tests that rely on SQL-specific behaviour (cascade deletes, JSON column queries, `decimal` overflow)
- Future integration tests use SQLite or real SQL Server — do not add new InMemory tests for schema-constraint scenarios

**Highest-value targets**
- Pure computation: `KpiCalculator`, `TariffResolver`, `InterpolationEngine`, `ReconciliationEngine`
- Validators: test all rule branches, especially contract-lock (`TariffValidator`)
- Functions: test handler method directly with mock `AppDbContext` + `FunctionContext` — not via HTTP

**Do not test**
- `{Entity}Configuration.cs` EF Core config classes — trust EF Core
- Generated migration code
- `TenantResolverMiddleware` header parsing — covered in story 1.4 tests

### Code Quality & Style Rules

#### Linting
- Linter: `oxlint` (not ESLint) — run via `npm run lint` in `client/`; do not generate `.eslintrc`
- TypeScript `strict: true` is implied — never use `// @ts-ignore` or `as any` in feature code

#### Naming conventions

**Frontend**
- Component files/exports: PascalCase (`KpiTile.tsx`, `ReadingSheet.tsx`)
- Hook files/exports: `use` prefix camelCase (`useDashboard.ts`, `useSubmitReading.ts`)
- API module files: camelCase (`dashboardApi.ts`, `readingApi.ts`)
- Types/interfaces: PascalCase (`ReadingResponse`, `DashboardData`)
- Event handler props: `on{Event}` — local handlers: `handle{Event}`
- Page-level route components: `{Feature}Page` (`DashboardPage`, `OnboardingPage`)

**Backend**
- Classes, methods, properties: PascalCase; private fields: `_camelCase`; locals/params: camelCase
- Async methods always suffix `Async`
- Function entry class: `{Feature}Function`; entry method: always `RunAsync`
- Request DTOs: `{Action}{Entity}Request` (`SubmitReadingRequest`)
- Response DTOs: `{Entity}Response` or `{Entity}Summary` (`ReadingResponse`, `DashboardSummary`)
- EF Core config classes: `{Entity}Configuration`

**Database (via EF Core Fluent API)**
- Tables: PascalCase singular (`User`, `Flat`, `MeterReading`)
- Columns: PascalCase (`UserId`, `KwhValue`, `IsInterpolated`)
- PKs: `{Entity}Id`; FKs: `{Referenced}Id` on dependent table
- Indexes: `IX_{Table}_{Column(s)}`

#### Comments
- Default: no comments
- Add only when WHY is non-obvious: hidden constraint, subtle invariant, framework workaround
- Never explain WHAT; never reference task/story/issue numbers

#### API response shapes
- Single resource: direct object; small collection: direct array; all errors: Problem Details RFC 9457
- JSON fields: camelCase; decimal values: JSON numbers (not strings); datetimes: ISO 8601 with explicit offset (`"2026-06-21T14:30:00+02:00"`)

### Development Workflow Rules

#### Local dev startup
- Frontend + auth simulation: `swa start` (SWA CLI) — not `npm run dev` alone; without SWA CLI all API calls return 403
- Backend: `func start` from `api/` with `local.settings.json` containing `SqlConnectionString`
- Vite proxy (`/api → localhost:7071`) is in `vite.config.ts` — no manual proxy setup needed
- `local.settings.json` is gitignored — never commit it; required keys: `SqlConnectionString`, `AzureWebJobsStorage`

#### Monorepo structure
- `client/` — all frontend; `api/` — all backend; `api.Tests/` — all backend tests
- `infra/` — Bicep only; do not modify without an explicit infra story
- No root-level `package.json` — all npm commands run from within `client/`

#### CI/CD
- Single pipeline: `.github/workflows/azure-static-web-apps.yml`
- Publish: `dotnet publish -c Release -r linux-x64 --no-self-contained /p:PublishReadyToRun=true`
- No separate frontend/backend pipelines

#### Git
- `main` is the single production branch — no staging environment
- Conventional commits: `feat:`, `fix:`, `chore:` prefixes

#### EF Core migrations
- No automatic migration on startup — applied manually or via deployment step
- `Data/Migrations/` is generated — never hand-edit migration files
- Always test `dotnet ef database update` locally before pushing

#### Known gaps — do not re-implement or work around
- No `dotnet test` / `npm test` in CI — tests do not run in CI pipeline
- No catch-all 404 route in React Router — unknown URLs render blank (deferred)
- No `EnableRetryOnFailure` on SQL connection (deferred)
- No React Error Boundary around `<Outlet />` in AppShell (deferred)

### Critical Don't-Miss Rules

#### The 10 non-negotiable backend rules
1. `decimal` for ALL kWh, cost, tariff, baseline, budget values — `float`/`double` are banned
2. `DateTimeOffset` for ALL timestamps — never `DateTime` or `DateTime.UtcNow`
3. `record` for all DTOs; `class` for EF Core entities — never swap these
4. Every EF Core query scoped to tenant — never an unscoped query against any entity
5. `CancellationToken ct` threaded through every async call — no `CancellationToken.None` in implementation code
6. Problem Details (RFC 9457) for all HTTP errors — no custom error envelopes
7. Route template: `"v1/..."` not `"api/v1/..."` — SWA strips `/api` before forwarding
8. `context.GetUserId()` only in HTTP-triggered functions — throws on blob/queue/timer triggers
9. Fluent API only for EF Core config — no Data Annotation attributes on entity classes for DB concerns
10. Services taking `AppDbContext` registered as `Scoped` — never `Singleton`

#### The 10 non-negotiable frontend rules
1. All paths to `apiClient` start after `/api/v1` — never include the prefix
2. `isPending` (not `isLoading`) on mutations — `isLoading` is `undefined` in TanStack Query v5
3. `useQuery` requires object form — `useQuery({ queryKey, queryFn })`, not positional args
4. Never hand-edit `client/src/components/ui/` — generated; create wrappers instead
5. No cross-feature hook imports — `flatId` from `useParams()` or props only
6. `await queryClient.invalidateQueries(...)` in `onSuccess` — never fire-and-forget when sheet may close
7. `mode: 'onBlur'` on all `useForm()` calls — default `'onSubmit'` gives no real-time feedback
8. `mutation.error.detail` for server errors; `form.formState.errors` for validation errors — never conflate
9. Every feature namespace added to `ns: [...]` in `i18n.ts` — glob loader and registry are separate
10. Tailwind custom tokens go in `@theme {}` in `index.css` — no `tailwind.config.js`

#### Security invariants
- Every HTTP Function verifies `flatId` belongs to resolved `userId` before touching data — 403 if not
- Never log or return raw exception messages in HTTP responses
- `local.settings.json` is never committed — contains connection strings
- `AuthorizationLevel.Anonymous` on all triggers — SWA Easy Auth is the gate, not Functions auth

#### Data integrity invariants
- Period-accurate tariff costing: every cost figure uses the tariff active on the date of consumption. **Correction (2026-07-13, Story 7.1 code review):** `TariffResolver.ResolveAsync(flatId, date, ct)` has zero real callers anywhere in this codebase (confirmed by full-repo grep) — it is dead code, not "the only correct path." The actual live pattern is `KpiCalculator.cs:155-164`'s in-memory `ResolveTariff(tariffs, date)`: load the flat's `Tariff` list once, then resolve each day in-memory (latest `Tariff` with `ContractStartDate <= date`). This avoids an N+1 per-day DB round-trip. Every engine needing period-accurate costing (`KpiCalculator`, `DecompositionEngine`) duplicates this helper verbatim per this codebase's established per-engine duplication convention — do not call `TariffResolver.ResolveAsync` in a per-day loop
- `Insights.Data` JSON column is opaque — deserialize in application layer; no LINQ predicates against its properties
- `IsInterpolated = true` must be set on all gap-filled rows in `SmartPlugDailyData`
- `IsCorrected = true` + `OriginalKwhValue` preserved on edited meter readings — no hard delete of reading history

---

## Usage Guidelines

**For AI Agents**
- Read this file before implementing any code in this project
- The Critical Don't-Miss Rules section is the fastest orientation — start there
- When a rule conflicts with a general best practice, this file wins
- If you add a new pattern not covered here, flag it for human review

**For Humans**
- Update when technology versions change (especially major version bumps)
- Add rules when a recurring agent mistake is identified — one incident is enough
- Remove rules that have become obvious or are now enforced by tooling
- The Version Gotchas section should shrink as the stack stabilises

_Last updated: 2026-07-13_
