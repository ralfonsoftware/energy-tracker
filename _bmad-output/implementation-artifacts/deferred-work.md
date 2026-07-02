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

## Deferred from: code review of 2-4-onboarding-step-2-energy-contract-and-completion (2026-06-29)

- W1: de-DE locale silently treats dot as thousands separator — `parseLocaleNumber("1.5", "de-DE")` → 15; spec-defined behavior but UX risk. Future: input mask or warning. (`OnboardingContract.tsx`)
- W2: `OnboardingValidator` registered as `AddSingleton` instead of `AddTransient` — FluentValidation recommends Transient; stateless so no correctness bug today. (`Program.cs`)
- ~~W3: No upper-bound validation on `AnnualKwhBaseline`, `PricePerKwh`, `MonthlyBaseFee`~~ — **RESOLVED in Epic 3 retrospective (2026-07-02)**: added `LessThan(20000)` / `LessThan(10)` / `LessThan(1000)` respectively to `OnboardingValidator.cs` (and `LessThan(20000)` on `AnnualKwhBaseline` in `PatchFlatValidator.cs`, which has the same field). Bounds chosen by the team as clearly-unreasonable-input guards, not scientifically derived limits. New `OnboardingValidatorTests.cs`/`PatchFlatValidatorTests.cs` cover the new rules directly (no prior test file existed for either validator — see new gap noted below).
- W4: `EffectiveDate = DateTimeOffset.UtcNow` (insertion time) — spec-compliant per AC11, but future tariff range queries (Epic 4) must account for the discrepancy between EffectiveDate and ContractStartDate. (`CompleteOnboardingFunction.cs`)
- ~~W5: No UNIQUE constraint on `IX_Tariffs_FlatId_EffectiveDate`~~ — **RESOLVED in Epic 3 retrospective (2026-07-02)**: added `.IsUnique()` to the index in `TariffConfiguration.cs` (migration `MakeTariffEffectiveDateUnique`), matching `MeterReadingConfiguration`'s equivalent unique index. Also closes the related `TariffResolver`/`KpiCalculator.ResolveTariff` tie-break inconsistency noted in the 3-2 review below — with the constraint in place, the tie those two functions disagreed on can no longer occur.
- W6: No loading indicator after submit while `['settings']` re-fetch completes — user waits on contract screen after HTTP 201. (`OnboardingPage.tsx`)
- W7: Locale change mid-form doesn't re-normalize existing field values — switching locale with values already entered causes silent mismatch. (`OnboardingContract.tsx`)
- W8: FlatName leading/trailing whitespace not trimmed before DB insert — `NotEmpty()` rejects whitespace-only but doesn't trim valid names. (`CompleteOnboardingFunction.cs`)
- W9: `setTimeout(() => focus(), 0)` after preset click races with React's commit — AC2 focus requirement; low risk in practice. (`OnboardingContract.tsx`)
- W10: Test gaps — no tests verify tile visual deselection (AC3) or non-auto-select invariant (AC4). (`OnboardingContract.test.tsx`)
- W11: Invalid number error only surfaced on submit, not on blur — "inline validation" in AC15 typically implies on-blur. (`OnboardingContract.tsx`)
- W12: No test for `isPending` loading state — AC16 loading spinner untested. (`OnboardingContract.test.tsx`)
- W13: `X-MS-CLIENT-PRINCIPAL` forgeable if Azure Function URL exposed directly — pre-existing concern across all functions; SWA proxy is intended guard. (`TenantResolverMiddleware.cs`)
- W14: Derivation formula rendered above spend input; AC6 says "below the field" — minor layout deviation; current UX makes practical sense. (`OnboardingContract.tsx`)

## Deferred from: code review of 2-5-settings-flat-name-annual-kwh-baseline-and-locale (2026-06-30)

- D1: `GetUserId()` null guard absent in `PatchFlatFunction` — pre-existing pattern across all functions; auth guaranteed by SWA Easy Auth + TenantResolverMiddleware. (`api/Features/Flats/PatchFlatFunction.cs:20`)
- D2: `FlatBaselineEdit` form initialises with empty defaults if mounted via direct URL before `['settings']` cache is warm — edge case outside normal navigation flow; in normal flow settings are already in cache when user navigates from SettingsRoot. (`client/src/features/settings/components/FlatBaselineEdit.tsx:50`)
- D3: `handleSaveName` silent no-op when `settings.flatId` is undefined — prevented by upstream `hasFlat && flatName` guard in SettingsRoot; not reachable in practice. (`client/src/features/settings/components/FlatSettingsCard.tsx:36`)

## Deferred from: code review of pre-epic-3-prep (2026-06-30)

- D1: Rapid locale re-selection rollback race — if user selects locale A then B before A's API call settles and both fail, `onError` reverts to A instead of the original. Pre-existing pattern in all three original inline implementations; no change introduced by this extraction. (`client/src/components/LocaleDropdown.tsx`)
- D2: `parseLocaleNumber` DE branch uses `replace(',', '.')` (single replace) vs EN branch `replace(/,/g, '')` (all occurrences) — asymmetry pre-dates this change; multi-comma input silently parses partial result. Acceptable given upstream input constraints; align to `/,/g` if stricter validation is added later. (`client/src/lib/localeNumber.ts`) — still open after the Epic 3 retro's `formatNumberForInput` fix (that fix addressed the pre-fill/decimal-separator bug, not this multi-comma edge case). Low priority, but Epic 4 adds two more locale-sensitive numeric fields (`PricePerKwh`, `MonthlyBaseFee`) — worth a final look if it hasn't caused a real incident by then.

## Deferred from: code review of 3-1-meter-reading-submission-backend (2026-06-30)

- Error responses missing `type` field — all Function error paths use `{ title, status, detail }` without a `type` URI as required by RFC 9457. Pattern matches the established PatchFlatFunction convention; address in a global error-shape hardening pass across all Functions. (`api/Features/Readings/SubmitReadingFunction.cs`)
- No upper bound on `KwhValue` — explicitly deferred in spec dev notes; pattern consistent with existing validators (`OnboardingValidator`). Add `LessThanOrEqualTo(N)` when domain upper bound is decided. (`api/Features/Readings/ReadingValidator.cs`)
- No future-date rejection for `ReadingDate` — retroactive readings explicitly supported (AC3); no AC requires rejecting future dates. Add `Must(d => d <= DateTimeOffset.UtcNow)` if business rules evolve. (`api/Features/Readings/ReadingValidator.cs`)
- `ReadingDate = DateTimeOffset.MinValue` passes `NotNull` validation — sentinel minimum-value date stored silently. Add `GreaterThan(new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero))` if plausible-date enforcement is needed. (`api/Features/Readings/ReadingValidator.cs`)
- `KwhValue` with >4 decimal places: response reflects pre-save in-memory value; DB stores `decimal(18,4)` rounded value — EF does not refresh the entity after `SaveChangesAsync`. Add a `decimal.Round(v, 4) == v` validator rule or re-read the entity from DB before building the response. (`api/Features/Readings/SubmitReadingFunction.cs`)
- Concurrent flat deletion between auth check and `SaveChangesAsync` → unhandled `DbUpdateException` → 500 — low-probability race; add `catch (DbUpdateException)` → 404/409 when global exception handling is introduced. (`api/Features/Readings/SubmitReadingFunction.cs`)
- `InMemoryDatabase` does not enforce FK constraints or `decimal(18,4)` precision in tests — pre-existing project-wide decision; tracked from earlier reviews. Address in a future integration-test hardening pass. (`api.Tests/Features/Readings/SubmitReadingTests.cs`)
- No `CreatedAt` server-side audit timestamp on `MeterReading` — retroactive submissions indistinguishable from on-time submissions. Add `SubmittedAt = DateTimeOffset.UtcNow` field to entity + migration when audit requirements are clarified. (`api/Data/Entities/MeterReading.cs`)
- No test for unauthenticated / null `GetUserId()` path — auth guaranteed by SWA Easy Auth + TenantResolverMiddleware; add a test for this path if middleware becomes conditional or testable in isolation. (`api.Tests/Features/Readings/SubmitReadingTests.cs`)
- `OperationCanceledException` not caught → noisy 500 telemetry on client disconnect — pre-existing cross-cutting concern; add a global exception handler or middleware in a hardening pass. (`api/Features/Readings/SubmitReadingFunction.cs`)
- `ReadingDate` timezone offset not normalized to UTC before storage — AC3 explicitly requires storing the date as supplied; same-instant submissions with different offsets differ in the composite index. Revisit if deduplication logic is added later. (`api/Features/Readings/SubmitReadingFunction.cs`)

## ~~Deferred from: code review of 3-2-kpi-dashboard-backend-computation (2026-07-01)~~ RESOLVED 2026-07-01

- ~~`dailyAvgCost` silent underestimation~~ — **RESOLVED**: `DashboardSummary` restructured with `CostSummary?` nested record; `dailyAvgCost` now divides by `coveredDays` (intervals with tariff) not total span; `HasCostGap`, `CoveredDays`, `TotalDays`, `CostDetailAvailable` added; `Cost: null` signals cost cannot currently be computed (no tariff configured, fewer than 2 readings, or sub-day span). Implemented in Story 3.2 Amendment; frontend handling in Story 3.3 ACs 7–11.
- No test for null/empty userId (unauthenticated path) — pre-existing gap across all function tests; SWA Easy Auth makes this path unreachable in production. Address in a global test-hardening pass. (`api.Tests/Features/Dashboard/GetDashboardFunctionTests.cs`)

## Deferred from: code review of 3-2-kpi-dashboard-backend-computation (2026-07-01)

- `IsCorrected`/`OriginalKwhValue` never consulted — a meter reset/replacement (downward `KwhValue` jump) is clamped to 0 consumption and 0 cost for that interval instead of using `OriginalKwhValue` to bridge the gap. Out of scope for Story 3.2; needs a product decision on how meter resets should be reflected in KPIs before implementing. (`api/Features/Dashboard/KpiCalculator.cs`)
- ~~Non-deterministic tariff tie-break on duplicate `EffectiveDate`~~ — **RESOLVED in Epic 3 retrospective (2026-07-02)**: this stopped being low-risk once flagged as an Epic 4 blocker (Story 4.1 lets users create tariffs freely, unlike onboarding's single guaranteed-unique row). Fixed via `.IsUnique()` on `IX_Tariffs_FlatId_EffectiveDate` (same fix as W5 above) — the tie can no longer occur, so both resolvers are now provably equivalent without needing to touch their logic.
- `coveredDaysInt = Math.Min(Math.Ceiling(coveredDays), totalDaysInt)` silently clamps instead of surfacing an error if `coveredDays` ever exceeds `totalDays` — defensive-only masking; not currently reachable given readings/tariffs are pre-sorted and intervals don't overlap by construction. (`api/Features/Dashboard/KpiCalculator.cs`)
- Floating-point epsilon risk: `TotalDays`/`CoveredDays` derive from `TimeSpan.TotalDays` (double) before `Math.Ceiling`, so representation error could theoretically shift a displayed day count by one — unlikely to matter at typical meter-reading cadence. (`api/Features/Dashboard/KpiCalculator.cs`)
- AC6 "403 Problem Details" is still an anonymous object without a `type` field, not a literal RFC 9457 `ProblemDetails` — pre-existing gap carried forward from Story 3.1, already tracked as a cross-cutting concern. (`api/Features/Dashboard/GetDashboardFunction.cs`)

## Deferred from: code review of 3-3-kpi-dashboard-frontend-euro-burn-design-and-grid (2026-07-01)

- Zero test coverage for `EuroBurnGradient`, `CostGapBadge`, and `DashboardPage` wiring — pre-existing gap outside Task 9's literal scope (which only mandated `DashboardGrid`/`useDashboard` tests); would have caught the AC-5 cold-open gradient bug directly. (`client/src/features/dashboard/components/EuroBurnGradient.tsx`, `CostGapBadge.tsx`, `client/src/features/dashboard/DashboardPage.tsx`)
- `spikeDays` is fetched into `DashboardSummary` but never rendered anywhere in this diff — no AC in this story covers a spike indicator. (`client/src/features/dashboard/api/dashboardApi.ts:20`)
- AC-5's "Enter Reading CTA" is unimplemented — explicitly deferred to Story 3.4 in the Dev Agent Record; only the cold-open dash/gradient/last-read behavior was implemented here. (`client/src/features/dashboard/components/DashboardGrid.tsx`)
- `package-lock.json` has unrelated transitive-dependency flag changes (`@types/react-dom` dev→devOptional, `tslib` loses dev/optional flags) — likely an npm resolution side-effect of adding `@radix-ui/react-popover`, not a hand-edit. (`client/package-lock.json`)
- Architecture Compliance Checklist in the story file was left entirely unchecked despite the Dev Agent Record claiming full completion — process nit; several unchecked boxes correspond to patch findings from this review.

## Deferred from: code review of 3-4-enter-reading-cta-bottom-sheet-and-immediate-dashboard-update (2026-07-01)

- ~~`parseLocaleNumber` (`client/src/lib/localeNumber.ts`) mis-parses de-DE decimals typed with `.` (treats it as a thousands separator, e.g. `150.5` → `1505`)~~ — **RESOLVED in Epic 3 retrospective (2026-07-02)**: this stopped being theoretical when it manifested as a real bug in Story 3.6's edit view (pre-filling an existing decimal value via `String(value)` produced an ambiguous dot-decimal string that `parseLocaleNumber` misread under de-DE). Root fix: added `formatNumberForInput(value, locale)` to `localeNumber.ts` as the correct inverse of `parseLocaleNumber`, and migrated every pre-fill call site (`ReadingHistorySheet.tsx`, `FlatBaselineEdit.tsx` — the latter had the identical latent bug, never triggered) to use it instead of raw `String(...)`. Round-trip tests added. The multi-comma truncation defect noted below (D2, pre-epic-3-prep) is a separate, still-open issue in the parser itself — not addressed by this fix.
- No upper-bound/sanity check on the kWh value client-side in `EnterReadingSheet.tsx` — only `>0`/non-NaN enforced; matches existing precedent (`FlatBaselineEdit` also has no cap) and correctness depends on backend validation.
- No guard against future-dated readings in `EnterReadingSheet.tsx` — not required by any AC in this story; consider for the reading-history/correction story (3.6) if it becomes a real user problem.
- `flatId` is interpolated unencoded into the API URL path in `client/src/features/readings/api/readingApi.ts` (e.g. `` `/flats/${flatId}/readings` ``) — pre-existing repo-wide convention, `dashboardApi.ts` does the same; low risk since `flatId` is a GUID, but worth a consistent fix across all API modules if ever revisited.

## Deferred from: code review of 3-5-trend-chart-and-spike-detection (2026-07-02)

- `BuildDailySeries`/`DetectSpikes` walk the entire reading history on every dashboard load (unbounded O(n)) even though only ~14 days of data are ever needed, duplicating work the existing per-interval totals loop already does. Not a correctness issue at expected data volumes (manual meter readings, not high-frequency data); pre-filter to a bounded window if reading history grows large. `api/Features/Dashboard/KpiCalculator.cs`
- Rolling-average spike detection has no defense against a prior flagged spike inflating the following day's baseline, potentially masking a second consecutive spike. Inherent to the mandated Dev Notes algorithm (verified byte-for-byte compliant) — changing it means changing the spec, not just the code. `api/Features/Dashboard/KpiCalculator.cs:DetectSpikes`
- `ReadingValidator` has no upper bound on `ReadingDate` — a future-dated reading contributes to running totals/cost but silently falls outside the `DailyConsumption`/`SpikeDays` window (which only covers up to `now.Date`). Pre-existing gap from Story 3.1, not introduced by this diff. `api/Features/Readings/ReadingValidator.cs`
- No defensive re-sort/tie-break in `BuildDailySeries` for readings sharing the same instant; relies on the pre-existing "readings is pre-sorted ascending" invariant already trusted elsewhere in the file. `api/Features/Dashboard/KpiCalculator.cs`
- Negative period deltas (meter resets/corrections) are silently clamped to 0 kWh and spread across the span with no visual signal in the trend chart. Reuses the existing clamp behavior already used for `totalKwh`/cost elsewhere in `KpiCalculator`, consistent with established behavior — not a new design choice from this diff. `api/Features/Dashboard/KpiCalculator.cs:BuildDailySeries`
- `ResizeObserver` test stub (`client/src/test-setup.ts`) always reports a fixed `320×90` and its mock entry is missing most of the real `ResizeObserverEntry` interface — harmless today but a landmine for the next chart component that reads those fields. Frontend also has no defensive check against a `dailyConsumption` array with length != 7 or duplicate dates (React key collision), though the backend contract currently guarantees 7 unique dates by construction.
- `TrendChart` hides the entire card — including the Reading History entry point (history icon) — whenever the user has 0 or 1 total readings, per an undocumented-in-AC dev decision ("mirrors the KPI tiles' cold-open dash state"). Decision (2026-07-02): keep as-is, matches existing pattern, zero practical impact today since Reading History is still a placeholder; revisit if Story 3.6 makes this a real usability problem. `client/src/features/dashboard/components/TrendChart.tsx`

## Deferred from: code review of 3-5-trend-chart-and-spike-detection round 2 (2026-07-02)

- None of the three round-1 patches (timezone conversion, `threshold <= 0m` guard, UTC weekday label) have dedicated regression tests — the exact bugs just fixed have zero test coverage. `api.Tests/Features/Dashboard/KpiCalculatorTests.cs`, `client/src/features/dashboard/components/TrendChart.test.tsx`
- No test covers a reading period that straddles the 7-day display window boundary (begins before the window, ends inside it) — the common real-world partial-window case is untested. `api.Tests/Features/Dashboard/KpiCalculatorTests.cs`
- Spike days are communicated via bar color alone with no secondary indicator or accessible text equivalent for the underlying values — WCAG 1.4.1 concern, needs UX/accessibility design input. `client/src/features/dashboard/components/TrendChart.tsx`
- `TimeZoneInfo.ConvertTime` can throw for a `ReadingDate` near `DateTimeOffset.MaxValue` (previous `.Date` truncation silently produced a wrong date instead); same root cause as the already-deferred "no upper bound on ReadingDate" gap, negligible reachability. `api/Features/Dashboard/KpiCalculator.cs`
- `perDayKwh = periodKwh / spanDays` has no rounding — harmless today, worth rounding before any future tooltip/label surfaces the raw value. `api/Features/Dashboard/KpiCalculator.cs:BuildDailySeries`
- `TrendChart` flickers away on every background refetch that resolves to a 0/1-reading state (not just initial load), extending the already-accepted cold-open-hides-entry-point decision. `client/src/features/dashboard/DashboardPage.tsx`, `client/src/features/dashboard/components/TrendChart.tsx`

## Deferred from: code review of story-3.6 (2026-07-02)

- No validation of corrected value against adjacent readings (monotonicity/plausibility) — pre-existing gap shared with original `SubmitReadingFunction`/`ReadingValidator`, out of scope for this story's AC. `api/Features/Readings/PatchReadingFunction.cs`
- No optimistic-concurrency protection on PATCH (races between overlapping corrections) — consistent with rest of codebase (no `RowVersion`/ETag pattern anywhere; documented known gap). `api/Features/Readings/PatchReadingFunction.cs`
- `SaveChangesAsync` not wrapped in try/catch — consistent with existing Functions relying on host-level exception handling. `api/Features/Readings/PatchReadingFunction.cs:928`
- No time/business-window restriction on correcting old readings — architectural/product question not addressed by any AC or architecture doc. `api/Features/Readings/PatchReadingFunction.cs`
- `GetReadingHistoryFunction` has no pagination/limit — consistent with "no caching layer, keep queries simple" and no other list endpoint paginates. `api/Features/Readings/GetReadingHistoryFunction.cs:826`
- `usePatchReading`'s `Promise.all` invalidation could theoretically surface a false "save failed" state if one `invalidateQueries` call rejects — exact code mandated verbatim by story Dev Notes; low real-world likelihood. `client/src/features/readings/hooks/usePatchReading.ts:14`
- Missing negative/malformed-input backend test coverage (negative kwhValue, missing property, non-numeric payload) — nice-to-have beyond the story's mandated test list. `api.Tests/Features/Readings/PatchReadingFunctionTests.cs`
- Test setup duplication (`MakeDb`/`MakeFunctionContext`/`SeedFlatAsync`) across test files — pre-existing convention already used in 4+ other test files. `api.Tests/Features/Readings/GetReadingHistoryFunctionTests.cs`, `PatchReadingFunctionTests.cs`
- Frontend test couples to DOM ordering via `getAllByRole('button')[0]` instead of a more specific query — minor test-robustness nitpick. `client/src/features/readings/components/ReadingHistorySheet.test.tsx:206`

## Deferred from: epic-3-retro (2026-07-02)

- `CompleteOnboardingFunction` and `PatchFlatFunction` have zero dedicated test files (`api.Tests/Features/Onboarding/` doesn't exist; no `PatchFlatFunctionTests.cs` in `api.Tests/Features/Flats/`) — discovered while adding upper-bound rules to `OnboardingValidator`/`PatchFlatValidator` today; had to add narrow direct-validator tests (`OnboardingValidatorTests.cs`, `PatchFlatValidatorTests.cs`) instead of extending an existing Function test, since none exists. Pre-existing gap from Epic 2, not introduced today. Worth a dedicated backfill pass — these two Functions are the only tenant-scoped write paths in the app without HTTP-level test coverage.
- `parseLocaleNumber`'s DE-branch multi-comma truncation (D2, pre-epic-3-prep) remains open — today's fix addressed the decimal-separator/pre-fill bug via `formatNumberForInput`, not this separate defect in the parser itself. Still low priority, but Epic 4 adds two more locale-sensitive numeric fields (`PricePerKwh`, `MonthlyBaseFee`) to watch.
- Upper bounds added to `AnnualKwhBaseline` (20000), `PricePerKwh` (10), `MonthlyBaseFee` (1000) were chosen by team judgment as "clearly unreasonable input" guards, not derived from any product/business requirement — revisit if a real user ever legitimately needs a value near these limits (e.g., a very large flat or an unusual tariff).

## Deferred from: code review of story-4.1 (2026-07-02)

- PATCH cannot explicitly clear `ProviderName`/`ContractStartDate`/`ContractDurationMonths` — JSON `null` and an omitted field are indistinguishable in the record-based `PatchTariffRequest`, so mutation is skip-if-null rather than clear-if-null. Pre-existing pattern shared with other `Patch*` functions in this codebase, not required by any AC. `api/Features/Tariffs/TariffModels.cs:11-17`, `api/Features/Tariffs/PatchTariffFunction.cs:87-92`
- No cross-field validation ensuring `ContractStartDate` and `ContractDurationMonths` are supplied together — a tariff can end up with only one set, making `TariffLockPolicy.IsLocked` permanently `false` regardless of intent. Not required by any AC, out of scope for this story. `api/Features/Tariffs/TariffValidator.cs`, `api/Features/Tariffs/PatchTariffValidator.cs`
- Decimal values that pass validator bounds can exceed the DB column's configured scale (`decimal(18,6)`/`decimal(18,4)`), so the immediate POST/PATCH response can diverge from the persisted/rounded value. Systemic gap likely present wherever decimal validators exist in this codebase; needs a broader precision-validation policy decision, not a point fix. `api/Features/Tariffs/TariffValidator.cs`, `api/Features/Tariffs/PatchTariffValidator.cs`, `api/Data/Configurations/TariffConfiguration.cs`
- No optimistic concurrency control on `Tariff` PATCH updates — two concurrent PATCHes silently last-write-wins with no conflict detection. Consistent with rest of codebase (no `RowVersion`/ETag pattern anywhere). `api/Features/Tariffs/PatchTariffFunction.cs:102`

## Deferred from: code review of story-4.2 (2026-07-02)

- Inline `style={{}}` used throughout the new `TariffForm.tsx`/`TariffList.tsx`/`FlatSettingsCard.tsx` pill contradicts the documented "Tailwind only" rule, but Dev Notes explicitly instructed copying `FlatBaselineEdit.tsx`/`OnboardingContract.tsx` "verbatim," both of which already violate this rule extensively — pre-existing pattern being propagated, not newly introduced. `client/src/features/tariffs/components/TariffForm.tsx`, `TariffList.tsx`, `client/src/features/settings/components/FlatSettingsCard.tsx`
- `tariffFormSchema`'s `.min(1, 'Required')` messages are hardcoded English, bypassing i18n — but the schema mirrors `onboardingSchema.ts`'s `contractSchema` "exactly" per Dev Notes, which very likely has the same pattern already. `client/src/features/tariffs/schemas/tariffSchema.ts`
- `useCreateTariff.ts`'s `Promise.all([...])` in `onSuccess` has no `.catch()` — if either invalidation rejects, the mutation's `onError` fires a false "save failed" banner despite a successful create. Mirrors `usePatchReading.ts`'s existing multi-key invalidation convention (explicitly cited as the pattern to copy). `client/src/features/tariffs/hooks/useCreateTariff.ts:11-15`
- Duration toggle buttons (1/6/12/24 months) have no `aria-pressed` attribute, but this exact pattern is copied "verbatim" from `OnboardingContract.tsx` per Dev Notes — pre-existing accessibility gap. `client/src/features/tariffs/components/TariffForm.tsx`
