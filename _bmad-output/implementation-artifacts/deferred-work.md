# Deferred Work

## Deferred from: code review of 1-1-monorepo-scaffold-and-cicd-pipeline (2026-06-27)

- CI pipeline has no `dotnet test` or `npm test` step — no test quality gate runs in CI (`.github/workflows/azure-static-web-apps.yml`). Address in a future story or CI hardening pass.
- i18n locale stubs (20 empty JSON files in `src/locales/`) are not wired into the i18n instance — `resources: {}` means those files can never be loaded. A loader mechanism (`i18next-http-backend` or static imports) is needed before real translations can work. Address when Story 2.x adds real translation strings.
- `UseAzureMonitorExporter()` in `api/Program.cs` will silently fail in local dev without `APPLICATIONINSIGHTS_CONNECTION_STRING` set in `local.settings.json`. Add graceful degradation or document the required env var.
- No `local.settings.json.example` file — new developers cloning the repo have no guide to required local config keys. Add an example file alongside `.gitignore` exclusion.
- `UnitTest1.Test1()` is an empty `[Fact]` that always passes — replace with a real smoke test or delete once the first real test is added.

## Deferred from: code review of 1-1-monorepo-scaffold-and-cicd-pipeline pass 2 (2026-06-27)

- No `pull_request` CI trigger — PRs merge without any build or test validation. Add `on: pull_request: branches: [main]` to the workflow.
- `ExcelDataReader` needs `System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` called at startup on Linux — required before any Excel import. Address in Story 6.x when the import pipeline is built.
- No catch-all `path: '*'` route in the React Router — unknown URLs render a blank page with no feedback. Add a 404 page in a future UX story.
- `fetch()` network errors (e.g., `TypeError: Failed to fetch`) are not reshaped into Problem Details format in `apiClient.ts` — inconsistent error shape for callers. Address when the API client is hardened.

## Deferred from: code review of 1-3-database-schema-users-table-and-ef-core-migration-baseline (2026-06-28)

- No SQL transient-fault retry policy (`EnableRetryOnFailure`) configured on `UseSqlServer` in `Program.cs`. Azure SQL emits transient errors on throttling/failover — add `EnableRetryOnFailure` in a production-hardening pass.
- No migration execution path defined for Azure Functions production runtime — `dotnet ef database update` must be run manually or wired into CI/CD. Define the migration deployment strategy in a deployment-hardening story.
- InMemory EF provider used in all DB tests — doesn't enforce `nvarchar` column types, max-length, or SQL Server-specific constraints. Add integration tests (SQLite or real SQL) to validate schema constraints in a future test-hardening story.
- `LocaleOverride` accepts arbitrary strings — no BCP-47 or allowed-values validation. Address when locale settings story (2.1/2.5) is implemented.
- No guard against concurrent duplicate-PK inserts for `UserId` — a `DbUpdateException` PK-violation is not distinguishable from other DB errors. Add conflict detection in the service layer when user write paths are built.
- Migration `Down` (`DropTable("Users")`) will fail if future migrations add FK references to `Users` and rollback order is wrong — future migration authors must drop FKs before rolling back this migration.

## Deferred from: code review of 1-4-swa-easy-auth-and-tenantresolver-middleware (2026-06-28)

- `InternalsVisibleTo` via MSBuild `<AssemblyAttribute>` in `api/energy-tracker-api.csproj` — if `AssemblyInfo.cs` auto-generation is enabled in future, the duplicate attribute will cause a compile error. Switch to `[assembly: InternalsVisibleTo(...)]` in `AssemblyInfo.cs` at that time.

## Deferred from: code review of 1-5-app-shell-design-system-tokens-routing-and-navigation (2026-06-28)

- ~~`accent-over-budget` and `accent-spike` share the same hex `#f59e0b`~~ — **RESOLVED in Epic 1 retrospective**: `accent-over-budget` changed to `#fb923c` (orange-400); spike stays amber `#f59e0b`. Semantic distinction: spike = data observation (amber), over-budget = financial alarm (orange), error = system failure (coral `#f87171`). [client/src/index.css, 2026-06-28]
- `useAuth` doesn't expose `isError`/`error` — spec explicitly scopes return type to `{ user, isLoading }`; callers cannot distinguish network error from unauthenticated state. Expand when hook is wired to UI.
- No 404/catch-all route — unmatched paths render a blank AppShell with no user feedback. Pre-existing gap (also noted from 1-1 review). Add a 404 page in a future UX story.
- No iOS safe-area-inset (`env(safe-area-inset-bottom)`) on `BottomTabBar` — home indicator on iPhones overlaps the fixed tab bar. Spec specifies exact 72px height; address safe-area handling in a UX-polish story.
- No `errorElement` / React error boundary around `<Outlet />` in `AppShell` — a runtime throw in any child page unmounts the entire shell. Add resilience in a dedicated hardening pass.
- `queryKey: ['auth', 'me']` has no tenant/user scope — risks cross-account React Query cache pollution in multi-tab logout/login scenarios. Revisit when multi-flat or multi-user story lands.

## Deferred from: code review of 2-1-i18n-infrastructure-and-locale-settings-api (2026-06-28)

- No guard on `GetUserId()` null result (`api/Features/Settings/GetUserSettingsFunction.cs:17`) — auth guaranteed by SWA Easy Auth + TenantResolverMiddleware (Story 1.4); revisit only if middleware is bypassed.
- In-memory DB tests don't enforce FK constraints — pre-existing project-wide pattern (also deferred from 1-3 review); tracked as a future integration test hardening item.
- Multi-user SPA session cache leak: `queryKey: ['settings']` not user-scoped (`client/src/features/settings/hooks/useUserSettings.ts`) — personal single-user app; low risk in current deployment; revisit if multi-user or multi-flat support is added.
- AnnualKwhBaseline/SpikeThreshold negative/zero value validation (`api/Data/Entities/Flat.cs`) — Flat record creation is Story 2.4's scope; add range validation in the create-flat handler then.
- `LocaleSync + retry:false` may silently permanently fail on transient network error (`client/src/App.tsx`) — `retry: false` is explicitly spec-specified (Task 8); SWA auth makes auth-timing concern moot; address if reliability requirements change.

## Deferred from: code review of 2-2-onboarding-gate-and-intro-screen (2026-06-29)

- `Get Started` CTA advances `step` to `'flat-name'` but no component renders for that step — intentional per story spec; Stories 2.3/2.4 will fill it in.
- No `aria-haspopup` on locale dropdown trigger button (`OnboardingIntro.tsx`) — assistive technology won't announce the control as a dropdown; address in a UX-accessibility polish pass.
- `useUpdateLocale` mock set at module scope outside `beforeEach` in `OnboardingIntro.test.tsx` — subsequent tests inherit mock state if implementation is overridden mid-suite; refactor when adding more OnboardingIntro tests.
- `OnboardingIntro` tests assert against real translated English strings without explicitly mocking `react-i18next` — may fail if test environment i18n is not configured; add a `vi.mock('react-i18next')` setup if test stability becomes an issue.

## Deferred from: code review of 1-2-azure-infrastructure-provisioning (2026-06-27)

- No network isolation — all resources expose public endpoints (`infra/main.bicep`). Private endpoints would add cost beyond Basic-tier scope; revisit if security posture hardens.
- SQL API uses preview version `2022-11-01-preview` (`infra/main.bicep`). Upgrade to GA API version when available.
- No `Microsoft.Insights/diagnosticSettings` resources — Log Analytics workspace is unconnected to SQL, Key Vault, Storage, and Functions logs (`infra/main.bicep`). Wire up in a future observability story.
- Static Web App `stagingEnvironmentPolicy: 'Disabled'` removes PR preview environments (`infra/main.bicep`). Revisit if PR preview environments become needed.
- SQL Basic (5 DTUs) intentionally mismatched against up to 10 concurrent 2 GB Function instances — cost trade-off per story spec. Upgrade tier if load testing reveals contention.
- No `@minLength`/`@maxLength`/`@pattern` on storage/keyvault name params in Bicep — invalid names fail inside ARM after partial provisioning. Add Bicep parameter decorators in a hardening pass.

## Deferred from: code review of 2-3-onboarding-step-1-flat-name (2026-06-29)

- Blank screen when user reaches 'contract' step — intentional placeholder; Story 2.4 adds `OnboardingContract` component. (`OnboardingPage.tsx`)
- Locale switcher copy-pasted into `OnboardingFlatName` from `OnboardingIntro` — spec explicitly says "same pattern"; extract into a shared `<LocalePill>` component after Story 2.4 completes the full wizard. (`OnboardingFlatName.tsx`)
- Direct `i18n` singleton import (`import i18n from '@/lib/i18n'`) instead of `useTranslation`-returned instance — pre-existing Story 2.2 pattern in `OnboardingIntro.tsx`; change consistently across all onboarding components in a cleanup pass. (`OnboardingFlatName.tsx:5`)
- `flatName` not forwarded to contract render block — intentional; Story 2.4 adds `OnboardingContract` and will receive it via `initialFlatName` prop. (`OnboardingPage.tsx`)
- Tests use live `react-i18next` without mocking — pre-existing Story 2.2 pattern; works because i18n key names contain matched substrings. Consider adding a `vi.mock('react-i18next')` setup if test stability becomes an issue. (`OnboardingFlatName.test.tsx`)
- No max-length constraint on flat name — Story 2.4 adds the backend call; add `z.string().max(N)` and `maxLength={N}` on `<input>` when DB column width is confirmed. (`onboardingSchema.ts`)
- Optimistic locale rollback causes a language flash with no user feedback — pre-existing Story 2.2 pattern; address with a loading indicator or debounce in a UX-polish pass. (`OnboardingFlatName.tsx:72-76`)
- Locale dropdown lacks ARIA roles (`role="listbox"`) and keyboard navigation (Escape to close, arrow keys) — pre-existing Story 2.2 pattern; address in a UX-accessibility polish pass.
- Touch events not handled for locale dropdown dismiss (`mousedown` only, no `touchstart`) — pre-existing Story 2.2 pattern; touch-only mobile devices cannot tap-outside to dismiss.
- `useUpdateLocale` mock uses `as any` and is set at module scope without `beforeEach` reset — pre-existing Story 2.2 pattern; refactor if the test suite expands.
