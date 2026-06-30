# Epic 1: Project Foundation & Authenticated App Shell

Users can authenticate via Azure Entra ID and reach a working app shell — routing, navigation, design system tokens, and CI/CD pipeline are all in place. This is the deployable skeleton that every subsequent epic builds on.

## Story 1.1: Monorepo Scaffold & CI/CD Pipeline

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

## Story 1.2: Azure Infrastructure Provisioning

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

## Story 1.3: Database Schema — Users Table & EF Core Migration Baseline

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

## Story 1.4: SWA Easy Auth & TenantResolver Middleware

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

## Story 1.5: App Shell — Design System Tokens, Routing & Navigation

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
