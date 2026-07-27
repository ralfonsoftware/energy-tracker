# Epic 11: Post-Epic-10 Hardening & Technical Debt Resolution

A prioritized batch of deferred technical-debt, correctness, and consistency items cleared after Epic 10 (Actionable Insights) closed out the full original PRD scope — sourced from the Epic 10 retrospective (`_bmad-output/implementation-artifacts/epic-10-retro-2026-07-26.md`, Action Items #1–#5) and a full audit of `deferred-work.md`'s ~68 accumulated entries. Follows the same "prioritized hardening batch" pattern established by Story 6.0 and Epic 9 Part 2. Every item below was individually re-verified against current code before being scoped here — several `deferred-work.md` entries that looked open were found to already be resolved by Epic 9 (decimal precision, optimistic concurrency, PATCH null-semantics for `PlannedAnnualSpend`/`ContractDurationMonths`/`ProviderName`, meter-reset handling, the 404 route) and are excluded from this epic as already closed.

Ralf selected all four categories surfaced during scoping: correctness/data-integrity fixes, API consistency, accessibility/UX consistency, and test coverage/engineering hygiene. One story (11.9) is decision-gated — it requires a Ralf/Sally design decision as its first AC before implementation proceeds, matching this project's established design-gate pattern (Stories 8.4, 9.1, 9.6).

**Story 11.13 was added 2026-07-27** via `bmad-correct-course`, sourced from a production investigation (`insights-duplicated-across-runs-investigation.md`) rather than the original Epic 10 retro batch — a user reported every Insight card doubled the day after a manual trigger. Root cause: the nightly `ScheduledInsightsFunction` run and any manual trigger both write fresh `Insight` rows with no cross-run de-duplication, and nothing ever deletes a prior run's rows (a deliberate retention choice, not an oversight — see FR-51). Recommended to be picked up before Stories 11.3–11.12 given it is a live, user-visible correctness bug rather than a latent gap.

**Story 11.14 was added 2026-07-27**, same day as 11.13, via a second `bmad-correct-course` pass once the retention tradeoff FR-51 made became concrete: unlimited historical retention means the Insights tab accumulates every legitimately-distinct finding forever with no dismiss feature yet to manage the list. FR-51 was amended (not replaced) to keep full retention in the data store while scoping the *default read* to one row per `(Type, DeviceId)` identity.

**FRs covered:** FR-51 (Story 11.13, added 2026-07-27 following production investigation `insights-duplicated-across-runs-investigation.md`) — otherwise this epic is entirely engineering-hardening/bugfix work, consistent with the precedent set by Story 6.0 and Epic 9 Part 2.
**UX items:** A new UX-DR may be assigned during Story 11.9's design gate, same pattern as Stories 9.1/9.6.

## Story 11.1: Centralize `ResolveTariff` Into a Shared Utility

As the team maintaining this app,
I want the tariff-resolution logic to exist in one place instead of six independent copies,
So that a correctness fix only needs to be made once, and a future change to the resolution rule can't silently drift across files.

**Note (2026-07-26, flagged by the Epic 10 retrospective, Action Item #1):** `ResolveTariff(IReadOnlyList<Tariff> tariffs, DateTimeOffset date)` is byte-for-byte duplicated in `api/Features/Dashboard/KpiCalculator.cs:160`, `api/Features/Decomposition/DecompositionEngine.cs:248`, `api/Features/Insights/StandbyDetector.cs:98`, `ReplacementDetector.cs:144`, `BudgetAlertDetector.cs:108`, and `InvoiceDeviationDetector.cs:117`. The original `TariffResolver` class this logic came from was correctly deleted as dead code during the Epic 9 retrospective cleanup (zero real callers at the time) — the duplication grew afterward as each of Epic 10's four detectors needed the identical logic and, per each story's own Dev Notes, was explicitly told to duplicate rather than share (a reasonable per-story call that, at six copies, no longer holds up at the epic level).

**Acceptance Criteria:**

**Given** six identical private `ResolveTariff` methods, all containing the same tie-break defect (`t.ContractStartDate > best.ContractStartDate` — a strict comparison that silently favors whichever tariff the unordered `db.Tariffs` query happens to enumerate first when two tariffs share the exact same `ContractStartDate`),
**When** implemented,
**Then** a single shared static utility (e.g. `api/Shared/TariffResolution.cs`, a pure function taking an already-loaded `IReadOnlyList<Tariff>` and a `DateTimeOffset` — no DB access, preserving every call site's existing in-memory-resolution performance characteristic) replaces all six duplicated methods, and adds a deterministic secondary sort key (`TariffId`) so two tariffs sharing a `ContractStartDate` resolve consistently regardless of query enumeration order.

**Given** the six call sites (`KpiCalculator`, `DecompositionEngine`, `StandbyDetector`, `ReplacementDetector`, `BudgetAlertDetector`, `InvoiceDeviationDetector`),
**When** migrated to the shared utility,
**Then** each call site's existing tests continue to pass unmodified except where a test specifically exercised the old non-deterministic tie-break (those are updated to assert the new deterministic behavior), and a new dedicated test file for the shared utility covers: no tariff active on the date (returns null), single active tariff, multiple tariffs with the target date landing between two contract starts, and the tie-break case (two tariffs sharing a `ContractStartDate`).

## Story 11.2: Insights Discovery Redelivery — DB-Level Idempotency Guard

As the team maintaining this app,
I want overlapping redelivery of the same insight-discovery queue message to be safe rather than racy,
So that a slow discovery run doesn't produce duplicate or corrupted `Insight` rows under Azure Storage Queue's visibility-timeout retry behavior.

**Note (2026-07-26, flagged by the Epic 10 retrospective, Action Item #3):** Story 10.2 added an idempotency guard to `ProcessInsightsFunction.cs` that deletes any pre-existing `Insight` rows for the `RunId` before detectors run, closing the simple "message redelivered after the first attempt fully finished" case. It does not close the case where the queue's visibility timeout expires *while the first attempt is still running* — Azure re-delivers the same message to a second concurrent invocation, and both invocations can pass the "no stale rows yet" check before either has written anything, then both proceed to write detector output concurrently. No DB-level lease or lock serializes this today.

**Acceptance Criteria:**

**Given** `ProcessInsightsFunction.cs`'s current guard only checks for and clears *existing* `Insight` rows, with no mechanism preventing two concurrent invocations for the same `RunId` from both passing that check,
**When** implemented,
**Then** the function acquires an exclusive claim on the `InsightRun` row before proceeding — e.g. an `UPDATE InsightRuns SET Status = Processing WHERE RunId = @runId AND Status = @expectedPriorStatus` executed via EF Core's optimistic-concurrency check (reusing the existing `RowVersion` pattern from Story 9.10, or a `SaveChangesAsync` guarded by the current `Status` value in the `WHERE` predicate) such that only one concurrent invocation can win the transition into `Processing`.

**Given** a second, redelivered invocation loses the claim (because the first invocation already transitioned the row out of the expected prior status),
**When** it detects this,
**Then** it logs the redelivery via `ILogger<ProcessInsightsFunction>` and returns without running any detector or touching `Insight` rows — a normal, expected outcome, not an error.

**Given** the fix,
**When** tested,
**Then** a new test in `ProcessInsightsFunctionTests.cs` simulates two concurrent invocations for the same `RunId` (e.g. by racing two calls to `RunAsync` against the same in-memory DB context set) and asserts exactly one set of detector writes results, with no duplicate or partial `Insight` rows.

## Story 11.3: Enforce Unique `PlugId` Across Power Points

As the team maintaining this app,
I want two Power Points to never share the same smart-plug `PlugId`,
So that Standby/Replacement insight detection and Decomposition attribution can't silently misattribute one plug's readings to two different devices.

**Note (2026-07-26, flagged by the Epic 10 retrospective, Action Item #4):** `PowerPointConfiguration.cs` currently declares `PlugId` as `HasMaxLength(200).IsRequired(false)` with no uniqueness constraint at all. This was a latent schema gap before Epic 10, but Epic 10's Standby/Replacement detectors now actively query `SmartPlugIntervalData`/`SmartPlugDailyData` by `PlugId` — a duplicate `PlugId` across two Power Points would double-count or cross-attribute real smart-plug data between two unrelated devices in a live, user-visible insight.

**Acceptance Criteria:**

**Given** no unique constraint exists on `PowerPoint.PlugId`,
**When** implemented,
**Then** `PowerPointConfiguration.cs` adds a filtered unique index on `PlugId` scoped appropriately (a `null` `PlugId` — an unconfigured Power Point — must remain unconstrained; only non-null values must be unique, following the same filtered-unique-index pattern already used for the Epic 10.1 `InsightRun` dedup index), and a migration is generated for it.

**Given** the existing `findPlugIdConflict` frontend validation (`client/src/features/flat-structure/components/draftModel.ts`) already prevents a user from saving a duplicate `PlugId` within the Flat Structure editor's own draft state,
**When** the DB constraint is added,
**Then** `UpdateFlatStructureFunction.cs`'s `SaveChangesAsync` catches the resulting `DbUpdateException` (unique-constraint violation) and returns a 409 Conflict Problem Details response as a defense-in-depth backstop — not just relying on the frontend's pre-save check — since the frontend check cannot see concurrent edits from another session/tab.

**Given** the new constraint,
**When** tested,
**Then** a new test confirms two Power Points cannot be saved with the same non-null `PlugId` (returns 409, not an unhandled 500), and that two Power Points with `PlugId = null` save without conflict.

## Story 11.4: `PatchFlatFunction` — Malformed `name` Field Returns 400, Not 500

As a developer integrating with this API,
I want a wrong-typed `name` field in a PATCH request to return a clear validation error,
So that a malformed request never surfaces as an unhandled server error.

**Acceptance Criteria:**

**Given** `PatchFlatFunction.cs:59`'s `Name: obj["name"]?.GetValue<string>()` — confirmed the only unguarded `GetValue<T>()` call in the entire `api/Features/` tree (every other field on every other PATCH endpoint uses the guarded `is JsonValue ... && TryGetValue<T>(...)` pattern) — a request body like `{"name": 123}` throws an uncaught `InvalidOperationException` inside `GetValue<string>()`, propagating as an unhandled 500 rather than the 400 Problem Details response every other malformed-field case on this same endpoint returns,
**When** implemented,
**Then** the `name` field is read using the same guarded `is JsonValue nameVal && nameVal.TryGetValue<string>(out var name)` pattern already used for every other field in this file, returning `400` with `detail: "name must be a string."` on a type mismatch.

**Given** the fix,
**When** tested,
**Then** a new test in `PatchFlatFunctionTests.cs` submits `{"name": 123}` and asserts a 400 Problem Details response (not a 500), alongside the existing valid-string and omitted-field cases continuing to pass unmodified.

## Story 11.5: RFC 9457 `type` Field Consistency Sweep

As a developer integrating with this API,
I want every error response to include the `type` field RFC 9457 requires,
So that clients get a consistent, spec-compliant Problem Details shape regardless of which endpoint failed.

**Note (2026-07-26, verified during epic scoping):** of the 19 Functions returning Problem-Details-shaped error objects, only 3 (`UpdateUserSettingsFunction`, `CreateTariffFunction`, `CompleteOnboardingFunction`) consistently include a `type` field. `PatchTariffFunction` includes `type` on exactly one of its several error branches (the `422 tariff-locked` case) but not its `400`/`403`/`404`/`409` branches in the same file. The other 15 Functions omit `type` entirely. This has been individually noted as "pre-existing, systemic" in at least four separate deferred-work entries without ever being swept — this story is that sweep.

**Acceptance Criteria:**

**Given** the 15 Functions currently missing `type` on any error response (`TriggerInsightsFunction`, `GetInsightsFunction`, `GetTariffsFunction`, `UpdateFlatStructureFunction`, `GetFlatStructureFunction`, `GetReadingHistoryFunction`, `GetDashboardFunction`, `SubmitReadingFunction`, `PatchReadingFunction`, `UploadFunction`, `GetImportStatusFunction`, `DeleteFlatFunction`, `PatchFlatFunction`, `CreateFlatFunction`, `GetDecompositionFunction`) plus `PatchTariffFunction`'s inconsistent branches,
**When** implemented,
**Then** every Problem Details error response across all of these Functions includes a `type` field using the existing convention already present in the three compliant Functions (an `https://tools.ietf.org/html/rfc7231#section-6.5.1`-style URI matching the HTTP status semantics, or a domain-specific slug like `PatchTariffFunction`'s existing `"tariff-locked"` where the error is domain-specific rather than a generic HTTP status).

**Given** this is a pure error-shape addition,
**When** implemented,
**Then** no existing test asserting `title`/`status`/`detail` needs to change (this is additive), and each modified Function gains or updates at least one test asserting the `type` field's presence and value on its primary error path.

## Story 11.6: Frontend Network-Error Reshaping in `apiClient`

As a user,
I want a dropped network connection to show a sensible error message,
So that I'm not left looking at a broken or blank error state when I'm simply offline.

**Acceptance Criteria:**

**Given** `apiClient.ts`'s `request()` function calls `await fetch(...)` with no `try`/`catch` around it — a genuine network failure (offline, DNS failure, connection drop) throws a raw `TypeError: Failed to fetch` (or the fetch spec's equivalent) that is never reshaped into the `Error & { detail?: string }` shape every calling hook's error-handling code already expects per this project's established convention,
**When** implemented,
**Then** `request()` wraps the `fetch()` call in a `try`/`catch`; on a caught network-level exception (not an HTTP error response — those are already handled), it throws a new `Error` with a `detail` field set to a generic, i18n-friendly network-error message key, matching the shape every other error path in this function already produces.

**Given** the fix,
**When** tested,
**Then** a new test in `apiClient.test.ts` (or the nearest existing test file covering `apiClient`) mocks `fetch` to reject with a `TypeError` and asserts the thrown error has the expected `detail` field, and at least one consuming hook's existing error-handling test is confirmed to still pass unmodified (proving the reshaping is transparent to callers already handling `error.detail`).

## Story 11.7: Keyboard-Accessible Custom Dropdowns

As a keyboard-only user,
I want every dropdown in this app to support arrow-key navigation like a native `<select>`,
So that I can use the app without a mouse.

**Note (2026-07-26, verified during epic scoping):** `LocaleDropdown.tsx`, `FlatSwitcher.tsx`, `PeriodSelector.tsx` (Decomposition), and `InsightsPeriodSelector.tsx` all share the identical hand-rolled `role="listbox"`/`role="option"` structure over a shadcn `Popover` — confirmed via direct inspection that none of the four have `onKeyDown`, arrow-key handling, or `aria-expanded` on their trigger. This gap has been independently flagged in at least three separate code reviews since Epic 2 without ever being fixed, each time deferred as "pre-existing, matches the component it was told to copy."

**Acceptance Criteria:**

**Given** four independent components sharing the same `Popover` + `role="listbox"`/`role="option"` shape with no keyboard model,
**When** implemented,
**Then** a single shared hook or utility (e.g. `client/src/lib/useRovingListboxNav.ts`) implements arrow-key (up/down) roving-tabindex navigation, Home/End jump-to-first/last, and `aria-expanded` reflecting the Popover's open state, following the WAI-ARIA listbox keyboard pattern.

**Given** the shared hook,
**When** retrofitted,
**Then** `LocaleDropdown.tsx`, `FlatSwitcher.tsx`, `PeriodSelector.tsx`, and `InsightsPeriodSelector.tsx` all adopt it, with no change to their existing visual appearance or click-based interaction (this is a keyboard-access addition, not a redesign).

**Given** the retrofit,
**When** tested,
**Then** each of the four components' existing test files gains a keyboard-navigation test (arrow-down moves focus/selection, Enter/Space selects, Escape closes) using `@testing-library/user-event`, and all four components' existing click-based tests continue to pass unmodified.

## Story 11.8: Room-List Per-Row Save-State Consistency

As a user,
I want saving one room's Power Points to only show a saving/disabled state on that room, not every room in the list,
So that the room list's save feedback tells me what's actually happening.

**Note (2026-07-26, verified during epic scoping):** `FlatStructureEditor.tsx` derives every per-room Save button's disabled/spinner/label state from one page-scoped `isPending` (from `useUpdateFlatStructure(flatId)`) — confirmed at line 384 (`disabled={!isDirty || isPending || isSaveBlocked}`) and line 390 (spinner) and line 341 (label). Saving any single room, or the page-level batch Save, currently spins and disables every other room's Save button too. This was explicitly out of scope for Story 9.3 (a presentation-only restyle) and flagged there for future tracking.

**Acceptance Criteria:**

**Given** the single shared `isPending` flag currently drives all per-room and page-level Save button states together,
**When** implemented,
**Then** each in-flight save (whether triggered by a single room's inline Save icon or the page-level batch Save button) tracks which specific room key(s) it is saving, and only those rooms' Save buttons show the disabled/spinner state — rooms not involved in the in-flight save remain fully interactive.

**Given** a blocked (disabled) per-room Save icon today gives no visible reason why it's blocked (just dims via `disabled:opacity-40`), unlike `RoomEditor.tsx`'s equivalent full-screen editor which always shows explicit blank-name/plug-ID-conflict text above its Save button,
**When** implemented,
**Then** a blocked per-room Save button in the room list shows the same specific blocking reason (blank name or plug-ID conflict) inline near that row, matching `RoomEditor.tsx`'s established pattern rather than a global banner covering all rooms.

**Given** this changes real interaction behavior (not just presentation, unlike Story 9.3),
**When** implemented,
**Then** existing `FlatStructureEditor.test.tsx` coverage of `isRoomDirty`/save/dirty-state logic is extended (not replaced) with cases for: saving Room A does not disable Room B's Save button, and Room B's dirty state is preserved and remains savable while Room A's save is in flight.

## Story 11.9: Accessible Spike-Bar Indicator — Design-Gated

As a user relying on assistive technology,
I want to know which days had a consumption spike without relying on bar color alone,
So that the trend chart's spike information isn't invisible to me.

**Note (2026-07-26, decision required before implementation):** `TrendChart.tsx`'s spike bars are communicated via color alone (amber vs. the normal bar color) — a WCAG 1.4.1 (Use of Color) concern first flagged during Story 3.5's second review round and never resolved, explicitly noted at the time as "needs UX/accessibility design input." **This story cannot proceed until Ralf (with Sally's input, matching the Story 9.1/9.6 design-gate pattern) decides the accessible treatment** — options include a pattern/hatch fill (the same visual language already used for the meter-reset indicator added in Story 9.8), a small icon/badge on spike bars, or a text-based summary below the chart. Whichever is chosen must also carry a screen-reader-accessible text equivalent, mirroring how Story 9.8's meter-reset indicator was implemented.

**Acceptance Criteria:**

**Given** the pending design decision above,
**When** resolved,
**Then** this story's first task is recording the chosen approach (and updating this AC with the concrete visual spec) before any implementation begins — no code changes proceed on an undecided design.

**Given** the approved design,
**When** implemented in `TrendChart.tsx`,
**Then** spike days are distinguishable without relying on color alone, with a screen-reader-accessible text equivalent for each spike day, and a regression test covers both the visual marker and the accessible text.

## Story 11.10: HTTP-Level Test Coverage — Onboarding & PatchFlat

As the team maintaining this app,
I want the app's only two untested tenant-scoped write paths to have real Function-level test coverage,
So that a regression in onboarding completion or flat-baseline updates is caught before it reaches production.

**Note (2026-07-26, flagged originally in the Epic 3 retrospective, never picked up):** `CompleteOnboardingFunction` and `PatchFlatFunction` have zero dedicated Function-level test files (`api.Tests/Features/Onboarding/CompleteOnboardingFunctionTests.cs` does not exist; `api.Tests/Features/Flats/PatchFlatFunctionTests.cs` — verify current state, since a later story added narrow direct-validator tests as a stopgap, not full Function-level HTTP coverage). These are the only tenant-scoped write paths in the app without their own Function test file.

**Acceptance Criteria:**

**Given** `CompleteOnboardingFunction.cs`'s current test coverage (if any) is limited to validator-level tests rather than the Function's `RunAsync` handler directly,
**When** implemented,
**Then** `api.Tests/Features/Onboarding/CompleteOnboardingFunctionTests.cs` is created following this codebase's established Function-test pattern (mock `AppDbContext` + `FunctionContext`, call `RunAsync` directly, no HTTP layer), covering: successful onboarding completion, validation failure, and the 403/404 tenant-check paths.

**Given** the equivalent gap in `PatchFlatFunction.cs`,
**When** implemented,
**Then** `api.Tests/Features/Flats/PatchFlatFunctionTests.cs` is created (or extended, if a stopgap file already exists) with full `RunAsync`-level coverage: successful patch, each malformed-field case (including Story 11.4's new 400-not-500 case), the tenant-check paths, and the `RowVersion` conflict path.

## Story 11.11: `localDate.ts` Correctness Hardening

As the team maintaining this app,
I want the shared date-handling utility to be correct at its boundaries,
So that fixing a date bug once actually fixes it everywhere, instead of leaving known gaps in the single shared implementation every caller now depends on.

**Note (2026-07-26, verified during epic scoping):** three known issues remain in `client/src/lib/localDate.ts`, all confirmed still present: (1) `addMonths`'s `setMonth()` arithmetic overflows on month-end boundaries (e.g. Jan 31 + 1 month lands on Mar 3, not Feb 28) because JS `Date` rolls excess days into the following month; (2) no function in the file guards against `NaN`/Invalid Date — a malformed input silently produces `"NaN-NaN-NaN"` from `toLocalDateString` or an uncaught `RangeError` from any `Intl.DateTimeFormat.format()` call site; (3) `TariffForm.tsx`'s create-flow write path (`contractStartDate: \`${data.contractStartDate}T00:00:00Z\`` at `TariffForm.tsx:126`) always encodes literal UTC midnight regardless of the user's local timezone, while `parseLocalDate`'s read path extracts local calendar-date parts — a user west of UTC picking "today" can see that tariff's date rendered one calendar day earlier after the fact.

**Acceptance Criteria:**

**Given** `addMonths`'s current unguarded `setMonth()` call,
**When** implemented,
**Then** it clamps to the last valid day of the target month instead of overflowing (e.g. Jan 31 + 1 month → Feb 28, not Mar 3), with a regression test covering at least one month-end overflow case per month-length variant (28/29/30/31-day target months).

**Given** no `NaN`/Invalid-Date guarding exists anywhere in the file,
**When** implemented,
**Then** `parseLocalDate` and `toLocalDateString` detect an invalid resulting date and throw a clear, descriptive error rather than silently producing `"NaN-NaN-NaN"` or deferring to an uncaught `RangeError` at a distant call site — since the backend always returns valid `DateTimeOffset` values today, this is a fail-fast/clarity improvement, not a new runtime safety net for a reachable production path.

**Given** `TariffForm.tsx`'s create-flow write-path asymmetry (the last of three instances of this exact bug class in this codebase — the other two were already fixed for the "upcoming" comparison in Story 4.2 and `TariffList`'s display in the `localDate.ts` extraction),
**When** implemented,
**Then** the create-flow submit path constructs the ISO string using the same local-calendar-date convention `parseLocalDate` expects to read back (not a hardcoded UTC-midnight suffix), with a regression test confirming a tariff created "today" round-trips to display as the same calendar date the user picked, run with a mocked timezone offset on at least one side of UTC.

## Story 11.12: SQLite Integration Test Tier for Schema-Constraint Scenarios

As the team maintaining this app,
I want a lightweight integration test tier that actually enforces database constraints,
So that cascade-delete paths, unique indexes, and decimal-precision truncation can be verified by an automated test instead of only by manual `dotnet ef database update` runs and production incidents.

**Note (2026-07-26, this codebase's own testing rules already name this as the intended future direction — not previously scoped as a story):** `api.Tests` uses EF Core's `InMemory` provider exclusively, which does not enforce FK constraints, unique indexes, column types, or `decimal` precision/scale. This is a deliberate, documented tradeoff for unit-test speed — but it means schema-constraint defects (like Story 10.1's SQL Server multi-cascade-path deploy failure, only caught when the actual Azure SQL migration failed in CI/CD) have no automated safety net today, and Story 11.3's new unique-index behavior needs a real constraint-enforcing test to be meaningfully verified rather than asserted only against application-layer logic.

**Acceptance Criteria:**

**Given** no integration test project exists that runs against a real constraint-enforcing database engine,
**When** implemented,
**Then** a new test collection (e.g. `api.Tests/Integration/`, or a `[Collection]`-scoped subset of the existing project) uses EF Core's SQLite provider (`Microsoft.EntityFrameworkCore.Sqlite`, an in-process, no-external-dependency engine — chosen over spinning up a real SQL Server container, consistent with this project's "boring technology" architecture preference and its single-developer local-dev workflow) against a real (non-InMemory) database file or `:memory:` connection, applying the actual EF Core migrations rather than `EnsureCreated()`.

**Given** the new tier,
**When** populated,
**Then** it initially covers exactly the scenarios the `InMemory` provider cannot verify: the full cascade-delete chain from `DeleteFlatFunction` (confirming no multi-cascade-path rejection — the exact class of defect that caused Story 10.1's deploy failure), Story 11.3's new `PlugId` unique-index enforcement, and at least one `decimal(18,4)`/`decimal(18,6)` column-scale truncation case.

**Given** SQLite's known type-affinity differences from SQL Server (e.g. it does not enforce `decimal` precision/scale the same way natively),
**When** a test relies on decimal truncation behavior,
**Then** the test documents and works around this known SQLite-vs-SQL-Server gap explicitly (e.g. via an EF Core value converter matching production's precision/scale) rather than silently asserting something SQLite wouldn't actually reject.

## Story 11.13: Insight De-duplication — Skip Writing Near-Identical Findings

As a user,
I want the Insights tab to show one card per distinct finding instead of near-identical repeats from every discovery run,
So that the tab stays trustworthy and a future ability to dismiss a specific finding has a stable, non-noisy set of rows to act on.

**Note (2026-07-27, flagged by production investigation `insights-duplicated-across-runs-investigation.md`):** `ScheduledInsightsFunction` creates a new `InsightRun` for every flat every night unconditionally (FR-38), and `ProcessInsightsFunction`'s stale-cleanup only removes `Insight` rows sharing the *same* `RunId` as the current invocation (Story 10.2/11.2's redelivery guard) — never a different, earlier completed run's rows. Since the four detectors (`StandbyDetector.cs:82`, `ReplacementDetector.cs:98`, `BudgetAlertDetector.cs:66`, `InvoiceDeviationDetector.cs:80`) each unconditionally `db.Insights.Add(...)` whenever their own threshold condition is met, two runs a day apart produce two near-identical `Insight` rows for the same finding (confirmed in production: a user saw every card doubled the day after a manual trigger, caused by the next night's scheduled run). This story adds FR-51's write-time de-duplication guard.

**Acceptance Criteria:**

**Given** a new shared utility `api/Shared/InsightDeduplication.cs` does not yet exist,
**When** implemented,
**Then** it exposes a static method (e.g. `IsNearDuplicateOfMostRecentAsync(AppDbContext db, Guid flatId, InsightType type, Guid? deviceId, decimal newPrimaryValue, CancellationToken ct)`) that queries `db.Insights.Where(i => i.FlatId == flatId && i.Type == type && i.DeviceId == deviceId).OrderByDescending(i => i.CreatedAt).FirstOrDefaultAsync(ct)`, extracts that row's primary quantified figure from its `Data` JSON via `JsonDocument.Parse` (property name keyed by `Type`: `estimatedMonthlyCost` for Standby, `estimatedSavingsEur` for Replacement, `overspendEur` for Budget, `impliedDeltaEur` for InvoiceDeviation), and returns `true` when `Math.Abs(newValue - existingValue) <= 0.05m * Math.Max(Math.Abs(newValue), Math.Abs(existingValue))` (both zero counts as a match); returns `false` when no prior row exists for that identity.

**Given** the four detectors' unconditional `db.Insights.Add(...)` calls (`StandbyDetector.cs:82`, `ReplacementDetector.cs:98`, `BudgetAlertDetector.cs:66`, `InvoiceDeviationDetector.cs:80`),
**When** implemented,
**Then** each call site first awaits `InsightDeduplication.IsNearDuplicateOfMostRecentAsync(...)` with its own computed primary value (`estimatedMonthlyCost`, `estimatedSavingsEur`, the `projectedAnnualCost - PlannedAnnualSpend` overspend amount, `impliedDeltaEur` respectively) and skips the `Add` (continues the loop for Standby/Replacement; skips the `if` block for Budget/InvoiceDeviation) when it returns `true` — no other behavior in these detectors changes.

**Given** the fix,
**When** tested,
**Then** each of the four detectors' existing test files gains a case asserting: a finding within 5% of the most recently stored Insight for the same Type/Device does not create a new row, and a finding beyond 5% does create a new row alongside the untouched prior one; a new `InsightDeduplicationTests.cs` in `api.Tests/Shared/` covers the utility directly: no prior row (not a duplicate), within tolerance, beyond tolerance, and the zero/zero edge case; all existing detector tests continue to pass unmodified.

**Given** this changes real write behavior across all four detectors,
**When** implemented,
**Then** no `Insight` row is ever deleted or modified by this change — only whether a *new* row gets written; `GetInsightsFunction.cs` and its existing tests require no changes, since the read path was already correct for whatever rows exist.

**Amendment (2026-07-27, FR-51 amended, see Story 11.14):** the last AC above ("`GetInsightsFunction.cs` ... requires no changes") reflected FR-51's original wording and no longer holds — FR-51 was amended the same day once the unlimited-retention tradeoff's practical consequence (an ever-growing default view) became concrete without a dismiss feature to manage it. `GetInsightsFunction.cs` is now scoped by Story 11.14. This AC is retained here for historical accuracy of what 11.13 itself implemented; it does not describe current expected behavior.

## Story 11.14: Scope Default Insights Read to Most-Recent-Per-Identity

As a user,
I want the Insights tab to show only the current, most relevant finding per device/type,
So that the tab doesn't accumulate an ever-growing list of stale historical findings while no dismiss/history feature exists yet to manage them.

**Note (2026-07-27, FR-51 amended via `bmad-correct-course` following today's production cleanup):** Story 11.13 added a write-time dedup guard (`InsightDeduplication.IsNearDuplicateOfMostRecentAsync`) but deliberately left `GetInsightsFunction.cs:49-53` unchanged, per FR-51's original wording ("both remain visible"). Amended FR-51 now requires the default read to show only the most-recently-stored `Insight` row per `(Type, DeviceId)` identity — matching the same identity definition the write-time guard already uses — while still never deleting any row.

**Acceptance Criteria:**

**Given** `GetInsightsFunction.cs:49-53` currently returns `db.Insights.Where(i => i.FlatId == flatGuid)` unfiltered — every row ever written for the flat,
**When** implemented,
**Then** the query is changed to return only the single most-recently-stored row (by `CreatedAt`, tie-broken by `InsightId` descending) per distinct `(Type, DeviceId)` identity for the flat — no `RunId` filtering (a `RunId` filter would incorrectly hide a type that didn't fire in the latest run but is still current) — with no schema change and no `Insight` row ever deleted or modified by this read.

**Given** `GetInsightsFunctionTests.cs:75-91` (`RunAsync_MultipleInsights_ReturnsSortedByCreatedAtDescending`) currently seeds three `Insight` rows for a flat and asserts all three are returned, locking in the old all-time-unscoped contract,
**When** implemented,
**Then** this test is updated to reflect the new contract: seeding rows across distinct identities continues to return one row per identity, while seeding two rows for the *same* identity at different `CreatedAt` values asserts only the most recent is returned.

**Given** the new scoping,
**When** tested,
**Then** new test cases cover: a flat with 3 distinct `(Type, DeviceId)` identities each with 1 row (all 3 returned), a flat with 1 identity having 2 historical rows (only the newer returned), and the `CreatedAt` tie-break case (two rows with identical `CreatedAt`, higher `InsightId` wins) — matching the tie-break already established in `InsightDeduplication.cs:31` for consistency.
