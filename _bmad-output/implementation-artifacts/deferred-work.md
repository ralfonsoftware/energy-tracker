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

## Deferred from: code review of 1-2-azure-infrastructure-provisioning (2026-06-27)

- No network isolation — all resources expose public endpoints (`infra/main.bicep`). Private endpoints would add cost beyond Basic-tier scope; revisit if security posture hardens.
- SQL API uses preview version `2022-11-01-preview` (`infra/main.bicep`). Upgrade to GA API version when available.
- No `Microsoft.Insights/diagnosticSettings` resources — Log Analytics workspace is unconnected to SQL, Key Vault, Storage, and Functions logs (`infra/main.bicep`). Wire up in a future observability story.
- Static Web App `stagingEnvironmentPolicy: 'Disabled'` removes PR preview environments (`infra/main.bicep`). Revisit if PR preview environments become needed.
- SQL Basic (5 DTUs) intentionally mismatched against up to 10 concurrent 2 GB Function instances — cost trade-off per story spec. Upgrade tier if load testing reveals contention.
- No `@minLength`/`@maxLength`/`@pattern` on storage/keyvault name params in Bicep — invalid names fail inside ARM after partial provisioning. Add Bicep parameter decorators in a hardening pass.
