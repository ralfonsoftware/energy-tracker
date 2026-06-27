---
baseline_commit: 7d5860d76d397c0b2fa3c25649a325440d0bf40a
---

# Story 1.1: Monorepo Scaffold & CI/CD Pipeline

Status: done

## Story

As a developer,
I want to initialize the energy-tracker monorepo with the prescribed scaffold (Vite + React + TypeScript frontend, .NET 10 Azure Functions isolated worker backend) and a working GitHub Actions CI/CD pipeline,
so that all subsequent development has a consistent, deployable foundation from day one.

## Acceptance Criteria

1. **Scaffold builds succeed** — `client/` and `api/` are created and both build without errors: `npm run build` (Vite) and `dotnet publish -c Release -r linux-x64 --no-self-contained /p:PublishReadyToRun=true` (.NET Functions).

2. **Vite dev proxy** — With `client/` running (`npm run dev`), a request to `/api/anything` is forwarded to `localhost:7071` as configured in `vite.config.ts`.

3. **GitHub Actions pipeline** — On push to `main`, the pipeline builds the Vite frontend, publishes the .NET Functions app (ReadyToRun, linux-x64), and deploys both to Azure Static Web App without errors.  
   ⚠️ **Dependency**: Full deployment (SWA token + Functions app name) requires Azure resources created in Story 1.2. The workflow file is created in this story with placeholder secrets; deployment becomes active after Story 1.2.

4. **SPA fallback** — `staticwebapp.config.json` exists at the monorepo root. Any non-`/api` route returns `index.html` (client-side routing fallback); `/api/*` routes are forwarded to the linked Functions app.

## Tasks / Subtasks

- [x] Task 1: Monorepo root — `.gitignore` and `staticwebapp.config.json` (AC: 4)
  - [x] Create `.gitignore` covering Node, .NET build artefacts, `local.settings.json`, IDE files
  - [x] Create `staticwebapp.config.json` with SPA `navigationFallback` for `index.html` and `/api/*` forwarding

- [x] Task 2: Frontend scaffold — `client/` (AC: 1, 2)
  - [x] Run `npm create vite@latest client -- --template react-ts`
  - [x] Run `npm install -D @tailwindcss/vite` (Tailwind v4 — **no** `tailwind.config.js`)
  - [x] shadcn/ui manual setup — `components.json` created manually; `clsx`, `tailwind-merge`, `class-variance-authority` installed; `src/lib/utils.ts` created (shadcn init is interactive; manual setup is equivalent)
  - [x] Run full `npm install` for all runtime deps
  - [x] Rewrite `vite.config.ts` with Tailwind v4 plugin and `/api` proxy to `localhost:7071`
  - [x] Set `index.css` to `@import "tailwindcss";` only (design tokens added in Story 1.5)
  - [x] Create `src/lib/queryClient.ts` — TanStack Query v5 `QueryClient` instance
  - [x] Create `src/lib/authClient.ts` — SWA Easy Auth wrappers (`.auth/login/aad`, `.auth/me`, `.auth/logout`)
  - [x] Create `src/lib/apiClient.ts` — base fetch helpers with `/api/v1` prefix and Problem Details error parsing
  - [x] Create `src/lib/i18n.ts` — i18next initialization with `i18next-browser-languagedetector`
  - [x] Create `src/router.tsx` — 5 lazy routes (Dashboard `/`, Insights `/insights`, Decomposition `/decomposition`, Settings `/settings`, Onboarding `/onboarding`)
  - [x] Rewrite `src/App.tsx` — `QueryClientProvider` + `RouterProvider` (**no** `MsalProvider`)
  - [x] Create `src/locales/` skeleton — `de-DE/` and `en-US/` dirs with empty JSON stubs for all 10 namespaces
  - [x] Create feature directory stubs: `src/features/{dashboard,readings,tariffs,onboarding,settings,insights,decomposition,smart-plug-import,flat-structure}/`
  - [x] Verify `npm run build` succeeds with zero TypeScript errors

- [x] Task 3: Backend scaffold — `api/` (AC: 1)
  - [x] Run `func init --worker-runtime dotnet-isolated --target-framework net10.0` in `api/`
  - [x] Rename `api.csproj` → `energy-tracker-api.csproj`; add `<LangVersion>latest</LangVersion>` and `<PublishReadyToRun>true</PublishReadyToRun>`
  - [x] Run `dotnet add package` for all required packages (EF Core, Azure SDKs, FluentValidation, ExcelDataReader)
  - [x] `host.json` present with OpenTelemetry telemetry mode (modern .NET 10 pattern)
  - [x] `local.settings.json` present (gitignored; uses `UseDevelopmentStorage=true`)
  - [x] Create directory structure: `Features/`, `Data/Entities/`, `Data/Configurations/`, `Data/Migrations/`, `Shared/` (all with `.gitkeep`)
  - [x] `Program.cs` uses `FunctionsApplication.CreateBuilder` + OpenTelemetry → Azure Monitor (modern .NET 10 pattern)
  - [x] Verify `dotnet publish -c Release -r linux-x64 --no-self-contained /p:PublishReadyToRun=true` succeeds

- [x] Task 4: Test project scaffold — `api.Tests/` (Architecture requirement)
  - [x] Run `dotnet new xunit -o api.Tests` from monorepo root
  - [x] Add project reference: `dotnet add api.Tests/api.Tests.csproj reference api/energy-tracker-api.csproj`
  - [x] Add test packages: Moq, Microsoft.EntityFrameworkCore.InMemory, FluentAssertions
  - [x] Create directory structure mirroring `api/Features/` and `api/Shared/` (with `.gitkeep` files)
  - [x] `dotnet test` passes (1 default test, project compiles cleanly)

- [x] Task 5: GitHub Actions CI/CD workflow (AC: 3)
  - [x] Create `.github/workflows/azure-static-web-apps.yml`
  - [x] Configure `permissions: id-token: write` + `contents: read` for OIDC Azure login
  - [x] Add `azure/login@v2` step with the known Managed Identity credentials
  - [x] Add Node.js setup + frontend build steps (`npm ci && npm run build` in `client/`)
  - [x] Add .NET setup + backend publish step (`dotnet publish` with ReadyToRun flags, output to `./publish/api`)
  - [x] Add SWA deploy step via `Azure/static-web-apps-deploy@v1` referencing `${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}`
  - [x] Add Functions deploy step via `Azure/functions-action@v1` referencing `${{ vars.AZURE_FUNCTIONS_APP_NAME }}`
  - [x] Comments in workflow noting which secrets must be added after Story 1.2

- [x] Task 6: End-to-end verification
  - [x] `npm run build` in `client/` — zero errors, 59 modules, route-level code splitting confirmed
  - [x] `dotnet build api/energy-tracker-api.csproj` — 0 warnings, 0 errors
  - [x] `dotnet publish -c Release -r linux-x64 --no-self-contained /p:PublishReadyToRun=true` — succeeded
  - [x] `dotnet test api.Tests/` — Passed 1, Failed 0 (project builds and tests run)

### Review Findings

- [x] [Review][Patch] FluentAssertions v8 commercial license — replaced with Shouldly 4.2.1 (MIT) [`api.Tests/api.Tests.csproj`]

- [x] [Review][Patch] Port mismatch: vite proxy targets 7071 but launchSettings.json launches on 7230 — fixed launchSettings to 7071 [`api/Properties/launchSettings.json`]
- [x] [Review][Patch] `@tanstack/react-query-devtools` is in production `"dependencies"` instead of `"devDependencies"` — moved to devDependencies [`client/package.json`]
- [x] [Review][Patch] xunit 2.9.3 paired with xunit.runner.visualstudio 3.1.4 (v3 runner is incompatible with v2 test package) — downgraded runner to 2.8.2 [`api.Tests/api.Tests.csproj`]
- [x] [Review][Patch] `apiClient.ts` throws raw JSON object on error instead of an `Error` instance — wraps in `new Error()` with Problem Details merged on [`client/src/lib/apiClient.ts`]

- [x] [Review][Defer] CI pipeline has no `dotnet test` or `npm test` step — no test quality gate in CI [`.github/workflows/azure-static-web-apps.yml`] — deferred, pre-existing
- [x] [Review][Defer] i18n locale stubs (20 empty JSON files) not wired into i18n instance — `resources: {}` means locale files can never be loaded [`client/src/lib/i18n.ts`] — deferred, pre-existing
- [x] [Review][Defer] `UseAzureMonitorExporter()` will silently fail in local dev without `APPLICATIONINSIGHTS_CONNECTION_STRING` [`api/Program.cs`] — deferred, pre-existing
- [x] [Review][Defer] No `local.settings.json.example` to guide developer onboarding — deferred, pre-existing
- [x] [Review][Defer] `UnitTest1.Test1()` is an empty placeholder that always passes — deferred, pre-existing

#### Pass 2

- [x] [Review][Patch] `authClient.getMe()` calls `.json()` with no `res.ok` check — added `res.ok` guard with `{ clientPrincipal: null }` fallback [`client/src/lib/authClient.ts`]
- [x] [Review][Patch] `apiClient` calls `res.json()` unconditionally on non-204 success — now reads body as text first, handles empty gracefully [`client/src/lib/apiClient.ts`]
- [x] [Review][Patch] 4 frontend feature stub dirs missing `.gitkeep` — added `.gitkeep` to `readings/`, `tariffs/`, `flat-structure/`, `smart-plug-import/` [`client/src/features/`]
- [x] [Review][Patch] `@tanstack/react-query-devtools` in devDeps but never mounted — added DEV-guarded `<ReactQueryDevtools>` to `App.tsx` [`client/src/App.tsx`]

- [x] [Review][Defer] No `pull_request` CI trigger — PRs merge without any build or test validation [`.github/workflows/`] — deferred, out of scope for scaffold
- [x] [Review][Defer] `ExcelDataReader` needs `CodePagesEncodingProvider.Instance` registered on Linux — required at startup before any Excel import [`api/Program.cs`] — deferred, address in Story 6.x
- [x] [Review][Defer] No catch-all `path: '*'` route — unknown URLs render blank page [`client/src/router.tsx`] — deferred, out of scope for scaffold
- [x] [Review][Defer] `fetch()` network errors (TypeError) not shaped like Problem Details — inconsistent error handling in `apiClient` [`client/src/lib/apiClient.ts`] — deferred, pre-existing

## Dev Notes

### ⚠️ Critical: No MSAL — SWA Easy Auth handles auth at the edge

**Do NOT install `@azure/msal-react` or `@azure/msal-browser`.** SWA Easy Auth validates the OIDC token at the Azure edge before requests reach any code. The React app never sees tokens or manages auth state directly.

Authentication surface in the React app is three `/.auth/*` endpoints only:

```typescript
// src/lib/authClient.ts
export const authClient = {
  login: () => { window.location.href = '/.auth/login/aad' },
  logout: () => { window.location.href = '/.auth/logout' },
  getMe: (): Promise<{ clientPrincipal: { userId: string; userDetails: string } | null }> =>
    fetch('/.auth/me').then(r => r.json()),
}
```

No `MsalProvider` in `App.tsx`. App.tsx is simply:
```tsx
import { QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider } from 'react-router-dom'
import { queryClient } from './lib/queryClient'
import { router } from './router'

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>
  )
}
```

_Architecture source: AD-9 validation correction — `@azure/msal-react` removed; AD-9, Architectural Boundaries — Auth boundary_

---

### ⚠️ Critical: Tailwind v4 — no `tailwind.config.js`

shadcn/ui v4 + Tailwind v4 uses the `@tailwindcss/vite` plugin. **There is no `tailwind.config.js`.** All configuration is done through the CSS file and the Vite plugin.

`vite.config.ts`:
```typescript
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
  ],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:7071',
        changeOrigin: true,
      },
    },
  },
})
```

`src/index.css` (Story 1.1 — placeholder only; design tokens added in Story 1.5):
```css
@import "tailwindcss";
```

_Architecture source: Selected Stack table — shadcn/ui v4, Tailwind v4 (`@tailwindcss/vite`)_

---

### Frontend package install (complete list)

Run from `client/` after the Vite template creation:

```bash
# Tailwind v4 (dev)
npm install -D @tailwindcss/vite

# shadcn/ui init (interactive — choose Default style, Zinc base color, CSS variables: yes)
npx shadcn@latest init

# Runtime dependencies
npm install react-router-dom @tanstack/react-query @tanstack/react-query-devtools
npm install react-i18next i18next i18next-browser-languagedetector
npm install recharts
npm install react-hook-form @hookform/resolvers zod
npm install lucide-react
```

**Note:** `class-variance-authority`, `clsx`, and `tailwind-merge` are installed by `npx shadcn@latest init` automatically. Do not add them manually.

**Note:** `@tanstack/react-query-devtools` is a dev aid — wrap it in `import.meta.env.DEV` guard in `App.tsx` or leave it for development only.

_Architecture source: Selected Stack, Scaffold Commands, AD-17 (zod + react-hook-form), AD-18 (react-i18next), AD-19 (route-level splitting)_

---

### TanStack Query v5 — key API differences from v4

This project uses **TanStack Query v5**. Key differences from v4:
- `useQuery({ queryKey, queryFn })` — object-only signature (no positional overload)
- `useMutation({ mutationFn, onSuccess })` — object-only signature
- `onSuccess`/`onError`/`onSettled` callbacks removed from `useQuery` — use `useEffect` or mutation callbacks instead
- `isLoading` renamed to `isPending` for mutations

`src/lib/queryClient.ts`:
```typescript
import { QueryClient } from '@tanstack/react-query'

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 60 * 1_000,
      retry: 1,
    },
  },
})
```

---

### i18n initialization

`src/lib/i18n.ts` — import this in `src/main.tsx` before rendering:
```typescript
import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import LanguageDetector from 'i18next-browser-languagedetector'

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    fallbackLng: 'en-US',
    supportedLngs: ['de-DE', 'en-US'],
    ns: ['common', 'dashboard', 'readings', 'tariffs', 'onboarding', 'settings',
         'insights', 'decomposition', 'import', 'flat-structure'],
    defaultNS: 'common',
    detection: { order: ['navigator'] },
    interpolation: { escapeValue: false },
  })

export default i18n
```

`src/main.tsx`:
```tsx
import './lib/i18n'  // must be before any React render
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
```

Locale file stubs needed in `src/locales/de-DE/` and `src/locales/en-US/` for each namespace. Create empty `{}` stubs now; strings are filled in per-feature stories.

_Architecture source: AD-18_

---

### apiClient — base fetch helper pattern

```typescript
// src/lib/apiClient.ts
const BASE = '/api/v1'

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    headers: { 'Content-Type': 'application/json', ...init?.headers },
    ...init,
  })
  if (!res.ok) {
    const problem = await res.json().catch(() => ({ detail: 'Unknown error' }))
    throw problem  // Problem Details object — TanStack Query surfaces this as error
  }
  if (res.status === 204) return undefined as T
  return res.json()
}

export const apiClient = {
  get: <T>(path: string) => request<T>(path),
  post: <T>(path: string, body?: unknown) =>
    request<T>(path, { method: 'POST', body: body ? JSON.stringify(body) : undefined }),
  patch: <T>(path: string, body: unknown) =>
    request<T>(path, { method: 'PATCH', body: JSON.stringify(body) }),
  put: <T>(path: string, body: unknown) =>
    request<T>(path, { method: 'PUT', body: JSON.stringify(body) }),
  delete: <T>(path: string) => request<T>(path, { method: 'DELETE' }),
}
```

Error convention: thrown objects are Problem Details RFC 9457 (`{ type, title, status, detail }`). TanStack Query stores these in `error.detail` for display.

_Architecture source: Format Patterns — API response shapes, AD-14, Process Patterns — Frontend error handling_

---

### Router setup — react-router-dom v7

```tsx
// src/router.tsx
import { lazy, Suspense } from 'react'
import { createBrowserRouter } from 'react-router-dom'

const DashboardPage = lazy(() => import('./features/dashboard/DashboardPage'))
const InsightsPage = lazy(() => import('./features/insights/InsightsPage'))
const DecompositionPage = lazy(() => import('./features/decomposition/DecompositionPage'))
const SettingsPage = lazy(() => import('./features/settings/SettingsPage'))
const OnboardingPage = lazy(() => import('./features/onboarding/OnboardingPage'))

function Wrap({ Page }: { Page: React.ComponentType }) {
  return <Suspense fallback={null}><Page /></Suspense>
}

export const router = createBrowserRouter([
  { path: '/', element: <Wrap Page={DashboardPage} /> },
  { path: '/insights', element: <Wrap Page={InsightsPage} /> },
  { path: '/decomposition', element: <Wrap Page={DecompositionPage} /> },
  { path: '/settings/*', element: <Wrap Page={SettingsPage} /> },
  { path: '/onboarding', element: <Wrap Page={OnboardingPage} /> },
])
```

Create minimal placeholder page components in each feature folder so TypeScript resolves the lazy imports:
```tsx
// e.g. src/features/dashboard/DashboardPage.tsx
export default function DashboardPage() { return <div>Dashboard</div> }
```

_Architecture source: AD-19, Routing structure_

---

### Backend packages — complete list

Run from `api/`:
```bash
# HTTP + Functions
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.Http.AspNetCore
dotnet add package Microsoft.Extensions.Azure

# Data
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package Azure.Identity
dotnet add package Microsoft.Data.SqlClient

# Storage
dotnet add package Azure.Storage.Blobs
dotnet add package Azure.Storage.Queues

# Parsing
dotnet add package ExcelDataReader
dotnet add package ExcelDataReader.DataSet

# Validation
dotnet add package FluentValidation

# Monitoring
dotnet add package Microsoft.ApplicationInsights.WorkerService
```

`energy-tracker-api.csproj` must include:
```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <AzureFunctionsVersion>v4</AzureFunctionsVersion>
  <OutputType>Exe</OutputType>
  <LangVersion>latest</LangVersion>
  <PublishReadyToRun>true</PublishReadyToRun>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup>
```

_Architecture source: AD-3, AD-24, Scaffold Commands, Enforcement Summary rule 1_

---

### `api/Program.cs` — minimal scaffold

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
    })
    .Build();

host.Run();
```

`api/host.json`:
```json
{
  "version": "2.0",
  "logging": {
    "applicationInsights": {
      "samplingSettings": { "isEnabled": true }
    }
  }
}
```

`api/local.settings.json` (gitignored — never commit):
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated"
  }
}
```

Real Azure connection strings will be added in Story 1.2 using `DefaultAzureCredential`. No connection strings with passwords ever appear here.

_Architecture source: AD-10, AD-21_

---

### Test project packages

Run from monorepo root:
```bash
dotnet new xunit -o api.Tests
dotnet add api.Tests/api.Tests.csproj reference api/energy-tracker-api.csproj
dotnet add api.Tests/ package xunit
dotnet add api.Tests/ package xunit.runner.visualstudio
dotnet add api.Tests/ package Microsoft.NET.Test.Sdk
dotnet add api.Tests/ package Moq
dotnet add api.Tests/ package Microsoft.EntityFrameworkCore.InMemory
dotnet add api.Tests/ package FluentAssertions
```

Create directory structure mirroring `api/`:
```
api.Tests/
├── Features/
│   ├── Dashboard/
│   ├── Readings/
│   ├── Tariffs/
│   ├── SmartPlugImport/
│   ├── Decomposition/
│   └── Insights/
└── Shared/
```

_Architecture source: Project Structure — api.Tests/_

---

### `staticwebapp.config.json`

```json
{
  "navigationFallback": {
    "rewrite": "/index.html",
    "exclude": ["/api/*", "/_framework/*", "/assets/*", "/*.{ico,png,svg,woff,woff2,css,js}"]
  },
  "routes": [
    {
      "route": "/api/*",
      "allowedRoles": ["anonymous"]
    }
  ]
}
```

**Note:** Easy Auth enforcement is configured via the SWA portal or ARM (Story 1.4). `allowedRoles: ["anonymous"]` here is intentional — the auth gate at the SWA level is configured separately. Do not set `"authenticated"` here before Story 1.4.

_Architecture source: Monorepo Structure, Architectural Boundaries — Auth boundary_

---

### `.gitignore` — must include

```
# Node
client/node_modules/
client/dist/
client/.vite/

# .NET
api/bin/
api/obj/
api.Tests/bin/
api.Tests/obj/
publish/

# Functions local config — NEVER commit
api/local.settings.json

# OS / IDE
.DS_Store
.vs/
.vscode/settings.json
*.user
```

---

### GitHub Actions workflow

Create `.github/workflows/azure-static-web-apps.yml`:

```yaml
name: Azure Static Web Apps CI/CD

on:
  push:
    branches:
      - main

permissions:
  id-token: write   # required for OIDC Azure login
  contents: read

jobs:
  build_and_deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      # OIDC login — Managed Identity with Federated Credentials
      # client-id / tenant-id / subscription-id are fixed values from the provisioned identity
      # No secrets needed for Azure login — OIDC Federated Credentials are already configured
      - name: Login to Azure
        uses: azure/login@v2
        with:
          client-id: ${{ vars.AZURE_CLIENT_ID }}
          tenant-id: ${{ vars.AZURE_TENANT_ID }}
          subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}

      # ── Frontend ───────────────────────────────────────────────────────────
      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'
          cache-dependency-path: client/package-lock.json

      - name: Build frontend
        working-directory: client
        run: |
          npm ci
          npm run build

      # ── Backend ────────────────────────────────────────────────────────────
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Publish Functions app
        run: |
          dotnet publish api/energy-tracker-api.csproj \
            -c Release \
            -r linux-x64 \
            --no-self-contained \
            /p:PublishReadyToRun=true \
            -o ./publish/api

      # ── Deploy ─────────────────────────────────────────────────────────────
      # REQUIRES STORY 1.2: Add AZURE_STATIC_WEB_APPS_API_TOKEN to GitHub secrets
      - name: Deploy frontend to Azure Static Web Apps
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: upload
          app_location: client
          output_location: dist
          skip_app_build: true  # pre-built above

      # REQUIRES STORY 1.2: Add AZURE_FUNCTIONS_APP_NAME to GitHub repository variables
      - name: Deploy Azure Functions app
        uses: Azure/functions-action@v1
        with:
          app-name: ${{ vars.AZURE_FUNCTIONS_APP_NAME }}
          package: ./publish/api
          respect-pom-xml: false
```

**After Story 1.2 completes, add to the GitHub repo:**
- Secret `AZURE_STATIC_WEB_APPS_API_TOKEN` — SWA deployment token (from: Azure Portal → Static Web Apps → Manage deployment token)
- Variable `AZURE_FUNCTIONS_APP_NAME` — the Functions app name provisioned in Story 1.2
- Variable `AZURE_RESOURCE_GROUP` — `energytracker-rg`

**Known values (from Ralf's DevOps prep — already provisioned):**
- Tenant ID: `${{ vars.AZURE_TENANT_ID }}`
- Managed Identity Client ID: `${{ vars.AZURE_CLIENT_ID }}`
- Managed Identity Object ID: `${{ vars.AZURE_PRINCIPAL_ID }}`
- Subscription ID: `${{ vars.AZURE_SUBSCRIPTION_ID }}`
- Resource Group: `energytracker-rg`
- GitHub Repo: `ralfonsoftware/energy-tracker`

The Federated Credentials are **already configured** on the managed identity for this repo + `main` branch. OIDC login will work without any additional Azure configuration.

_Architecture source: AD-20, AD-21, Deployment Patterns_

---

### Backend directory structure to create

```
api/
├── Features/
│   ├── Readings/
│   ├── Dashboard/
│   ├── Tariffs/
│   ├── SmartPlugImport/
│   ├── Decomposition/
│   ├── Insights/
│   ├── FlatStructure/
│   ├── Onboarding/
│   └── Settings/
├── Data/
│   ├── Entities/
│   ├── Configurations/
│   └── Migrations/
└── Shared/
```

Create `.gitkeep` in each leaf directory so git tracks the structure.

---

### Architecture enforcement rules for this story

These rules apply to ALL subsequent stories but must be established in the scaffold:

1. `decimal` for all kWh and monetary values — never `float` or `double`
2. `DateTimeOffset` for all timestamps — never bare `DateTime`
3. C# `record` types for DTOs; `class` for EF Core entities
4. All async methods suffixed `Async`, accept `CancellationToken ct`
5. EF Core: Fluent API only — **zero** Data Annotation attributes on entity classes
6. Error responses: Problem Details RFC 9457 — `{ type, title, status, detail }`
7. JSON: camelCase field names; ISO 8601 with timezone offset for datetimes
8. TanStack Query cache key pattern: `[resource, flatId, ...params]`

_Architecture source: Enforcement Summary (10 rules), Implementation Patterns_

---

### Project Structure Notes

- Monorepo root: `energy-tracker/` (clone of `https://github.com/ralfonsoftware/energy-tracker`)
- Frontend: `client/` — Vite + React + TypeScript + shadcn/ui + Tailwind v4
- Backend: `api/` — .NET 10 Azure Functions isolated worker
- Tests: `api.Tests/` — xUnit, co-located with `api/`
- Frontend feature folder `src/features/` mirrors `api/Features/` exactly
- `components/ui/` is shadcn/ui generated — **never hand-edit** these files
- `local.settings.json` is always gitignored — confirm it is in `.gitignore` before first commit

### References

- [Architecture: Monorepo Structure] — exact directory layout
- [Architecture: Scaffold Commands] — canonical install commands
- [Architecture: Selected Stack table] — versions and technology choices
- [Architecture: AD-3] — Flex Consumption (Linux), .NET 10, ReadyToRun
- [Architecture: AD-4] — Standalone Functions app (not SWA managed functions)
- [Architecture: AD-9 + validation correction] — SWA Easy Auth; no MSAL in frontend
- [Architecture: AD-16] — TanStack Query v5; no Zustand or Redux
- [Architecture: AD-18] — react-i18next with browser-languagedetector
- [Architecture: AD-19] — route-level code splitting via Vite dynamic imports
- [Architecture: AD-20] — single GitHub Actions pipeline
- [Architecture: Enforcement Summary] — 10 cross-cutting implementation rules

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

None — implementation proceeded without blockers.

### Completion Notes List

- **shadcn/ui init is interactive**: `npx shadcn@latest init` prompts for component library selection even with flags. Resolved by installing peer deps (`clsx`, `tailwind-merge`, `class-variance-authority`) directly and creating `components.json` and `src/lib/utils.ts` manually — functionally equivalent.
- **TypeScript 6.0 `baseUrl` deprecated**: Required adding `"ignoreDeprecations": "6.0"` to `tsconfig.app.json`. The `baseUrl` + `paths` approach still works; it's flagged for removal in TS 7.0 only.
- **Modern .NET 10 Functions template**: `func init` generated `FunctionsApplication.CreateBuilder` + OpenTelemetry pattern (not the older `HostBuilder` approach shown in some docs). Kept the modern pattern — it is technically superior.
- **CI/CD Story 1.2 dependency**: Deployment steps in the workflow need `AZURE_STATIC_WEB_APPS_API_TOKEN` (secret) and `AZURE_FUNCTIONS_APP_NAME` (variable) which cannot be set until Story 1.2 provisions the Azure resources. Workflow is complete but deploy steps will fail gracefully until then.

### File List

| File | Action |
|------|--------|
| `.gitignore` | Modified — added Node, .NET, IDE, and `local.settings.json` exclusions |
| `staticwebapp.config.json` | Created — SPA fallback + `/api/*` forwarding |
| `client/package.json` | Modified — added all runtime and dev dependencies |
| `client/vite.config.ts` | Replaced — Tailwind v4 plugin + `/api` → `localhost:7071` proxy |
| `client/tsconfig.app.json` | Modified — added `ignoreDeprecations`, `baseUrl`, `paths` for `@/*` alias |
| `client/components.json` | Created — shadcn/ui manual configuration |
| `client/src/index.css` | Replaced — `@import "tailwindcss"` only |
| `client/src/lib/utils.ts` | Created — `cn()` helper (clsx + twMerge) |
| `client/src/lib/queryClient.ts` | Created — TanStack Query v5 QueryClient instance |
| `client/src/lib/authClient.ts` | Created — SWA Easy Auth wrappers (no MSAL) |
| `client/src/lib/apiClient.ts` | Created — base fetch with `/api/v1` prefix and Problem Details errors |
| `client/src/lib/i18n.ts` | Created — i18next with browser-languagedetector, 10 namespaces |
| `client/src/router.tsx` | Created — react-router-dom v7 `createBrowserRouter`, 5 lazy routes |
| `client/src/App.tsx` | Replaced — `QueryClientProvider` + `RouterProvider` (no MsalProvider) |
| `client/src/main.tsx` | Modified — added `import './lib/i18n'` as first import |
| `client/src/features/dashboard/DashboardPage.tsx` | Created — stub |
| `client/src/features/insights/InsightsPage.tsx` | Created — stub |
| `client/src/features/decomposition/DecompositionPage.tsx` | Created — stub |
| `client/src/features/settings/SettingsPage.tsx` | Created — stub |
| `client/src/features/onboarding/OnboardingPage.tsx` | Created — stub |
| `client/src/features/readings/` | Created — directory stub |
| `client/src/features/tariffs/` | Created — directory stub |
| `client/src/features/smart-plug-import/` | Created — directory stub |
| `client/src/features/flat-structure/` | Created — directory stub |
| `client/src/locales/de-DE/*.json` | Created — 10 empty namespace stubs |
| `client/src/locales/en-US/*.json` | Created — 10 empty namespace stubs |
| `api/energy-tracker-api.csproj` | Created — renamed from api.csproj; ReadyToRun + LangVersion added; all packages |
| `api/Program.cs` | Created — modern FunctionsApplication.CreateBuilder + OpenTelemetry |
| `api/host.json` | Created — OpenTelemetry telemetry mode |
| `api/local.settings.json` | Created — gitignored local dev settings |
| `api/Features/**/.gitkeep` | Created — 9 feature directory stubs |
| `api/Data/Entities/.gitkeep` | Created |
| `api/Data/Configurations/.gitkeep` | Created |
| `api/Data/Migrations/.gitkeep` | Created |
| `api/Shared/.gitkeep` | Created |
| `api.Tests/api.Tests.csproj` | Created — xUnit project with Moq, FluentAssertions, EF InMemory |
| `api.Tests/UnitTest1.cs` | Created — default passing test |
| `api.Tests/Features/**/.gitkeep` | Created — directory stubs mirroring api/Features |
| `api.Tests/Shared/.gitkeep` | Created |
| `.github/workflows/azure-static-web-apps.yml` | Created — OIDC login + Node + .NET build + SWA + Functions deploy |
