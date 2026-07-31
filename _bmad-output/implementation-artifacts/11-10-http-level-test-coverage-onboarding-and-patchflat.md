---
baseline_commit: a0c92b12b7a3ff32d46d2e2b44c990e95ff3b080
---

# Story 11.10: HTTP-Level Test Coverage — Onboarding & PatchFlat

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As the team maintaining this app,
I want the app's only two untested tenant-scoped write paths to have real Function-level test coverage,
so that a regression in onboarding completion or flat-baseline updates is caught before it reaches production.

## ⚠️ Read this before starting any implementation

**The epic's premise for this story is stale.** The gap it describes was already closed on **2026-07-03**, three weeks before this epic was scoped, by commit `4ee466a` ("test: backfill HTTP-level Function tests for CompleteOnboardingFunction and PatchFlatFunction"). `deferred-work.md`'s `epic-3-retro (2026-07-02)` entry describing this exact gap was never marked closed when that backfill shipped — that's the one real hygiene loose end this story exists to tie off (Task 2 below).

`api.Tests/Features/Onboarding/CompleteOnboardingFunctionTests.cs` (8 tests) and `api.Tests/Features/Flats/PatchFlatFunctionTests.cs` (18 tests) already exist with full `RunAsync`-level HTTP coverage, and have been kept current through five subsequent stories (9.9, 9.10, 11.4, 11.5, plus the original 2026-07-03 backfill). Verified during story creation:

```
dotnet test api.Tests/api.Tests.csproj --filter "FullyQualifiedName~CompleteOnboardingFunctionTests|FullyQualifiedName~PatchFlatFunctionTests"
→ Passed! - Failed: 0, Passed: 26, Skipped: 0, Total: 26
```

**Do not write a fresh test file or duplicate existing tests.** Task 1 is a verification pass against the AC below (with one corrected sub-clause — see AC bullet 2), not a from-scratch implementation. If the audit turns up a genuine gap, add only that missing case to the existing file, following its established `MakeDb`/`MakeFunctionContext`/`SeedFlatAsync` helper conventions.

## Acceptance Criteria

1. **Given** `CompleteOnboardingFunction.cs`'s current test coverage, **when** audited, **then** `api.Tests/Features/Onboarding/CompleteOnboardingFunctionTests.cs` covers: successful onboarding completion, validation failure, and the "already onboarded" conflict path — confirmed already present as `RunAsync_ValidRequest_CreatesFlatAndTariffReturns201`, `RunAsync_EmptyFlatName_Returns400ValidationErrorAndCreatesNothing`, and `RunAsync_UserAlreadyHasFlat_Returns409ConflictAndCreatesNothing` respectively (plus malformed-JSON-body and tariff-field-defaulting cases beyond the epic's minimum). No code or test changes required for this AC — this bullet is satisfied as-is.

2. **Correction to the epic's original text:** the epic asked for "the 403/404 tenant-check paths" on `CompleteOnboardingFunction` — **this path does not exist and should not be added.** `CompleteOnboardingFunction` takes no route/resource-ID parameter (`POST v1/onboarding`); it never looks up a caller-supplied resource, so there is nothing for it to return "not found" or "forbidden" against. The only "guard" it has is the 409 conflict already covered by AC1. A 403 for a missing/malformed `X-MS-CLIENT-PRINCIPAL` header is produced by `TenantResolverMiddleware` *before* the Function body runs at all — an infrastructure-level concern already out of scope per `project-context.md`'s "Do not test: `TenantResolverMiddleware` header parsing — covered in story 1.4 tests." **Given** this corrected understanding, **when** the story is closed, **then** no 403/404 test is added to `CompleteOnboardingFunctionTests.cs`, and this correction is recorded in the Change Log so a future reader doesn't re-open this as a "missed AC."

3. **Given** the equivalent ask for `PatchFlatFunction.cs`, **when** audited, **then** `api.Tests/Features/Flats/PatchFlatFunctionTests.cs` already provides full `RunAsync`-level coverage of every enumerated case: successful patch (`RunAsync_ValidNamePatch_...`, `RunAsync_ValidAnnualKwhBaselinePatch_...`, `RunAsync_MultipleFieldsInOnePatch_...`), every malformed-field case including Story 11.4's 400-not-500 fix (`RunAsync_NameNotAString_Returns400BadRequest`, `RunAsync_AnnualKwhBaselineNotANumber_...`, `RunAsync_AnnualKwhBaselineExplicitNull_...`, `RunAsync_JsonBodyIsNotAnObject_...`, `RunAsync_MalformedJsonBody_...`, `RunAsync_MalformedRowVersion_...`, `RunAsync_MissingRowVersion_...`), the tenant-check paths (`RunAsync_FlatDoesNotExist_Returns403Forbidden`, `RunAsync_FlatBelongsToDifferentUser_Returns403ForbiddenAndPersistsNothing`), and the `RowVersion` conflict path (`RunAsync_ConcurrentModification_Returns409Conflict`). No code or test changes required for this AC — this bullet is satisfied as-is.

4. **Given** `deferred-work.md`'s `## Deferred from: epic-3-retro (2026-07-02)` entry ("`CompleteOnboardingFunction` and `PatchFlatFunction` have zero dedicated test files... Worth a dedicated backfill pass") — the entry this story's epic text is itself paraphrasing — **when** this story is closed, **then** that entry is marked closed using this file's established `~~strikethrough~~` + `**Closed by Story 11.10 (2026-07-31).**` convention (see `deferred-work.md:259` or `:325` for the exact format), citing the 2026-07-03 backfill commit (`4ee466a`) as the actual closing work and noting the two follow-up items that backfill's own review deferred (`PatchFlatFunction` malformed-`name` 500→400, and the `AnnualKwhBaseline` explicit-null no-op) were separately closed by Stories 11.4 and 9.9 respectively.

## Tasks / Subtasks

- [x] Task 1: Verify existing coverage against AC 1–3 (AC: 1, 2, 3)
  - [x] Run `dotnet test api.Tests/api.Tests.csproj --filter "FullyQualifiedName~CompleteOnboardingFunctionTests|FullyQualifiedName~PatchFlatFunctionTests"` and confirm all pass (expect 26/26 as of story creation — re-run rather than trust this number, in case intervening changes altered it).
  - [x] Re-read `CompleteOnboardingFunction.cs` and `PatchFlatFunction.cs` in full; cross-check every branch (`return` statement) has at least one corresponding test in the respective test file. If a genuinely untested branch is found, add exactly one test for it to the existing file, matching its established helper conventions (`MakeDb`, `MakeFunctionContext`, `SeedFlatAsync` in the Flats file) — do not introduce a new test-setup pattern.
  - [x] Do not touch `OnboardingValidatorTests.cs` or `PatchFlatValidatorTests.cs` — those cover validator-branch exhaustiveness at the unit level already and are out of scope for this Function-level story.

- [x] Task 2: Close the stale `deferred-work.md` entry (AC: 4)
  - [x] Locate `## Deferred from: epic-3-retro (2026-07-02)` in `_bmad-output/implementation-artifacts/deferred-work.md` and strike through the "`CompleteOnboardingFunction` and `PatchFlatFunction` have zero dedicated test files..." bullet, appending `**Closed by Story 11.10 (2026-07-31).**` plus a short note naming commit `4ee466a` (2026-07-03) as the actual closing work, and Stories 11.4/9.9 as the closures for that backfill's own two follow-up deferrals.

- [x] Task 3: Record the correction and close out (AC: 2)
  - [x] Add a Change Log entry noting this story found its premise already resolved, corrected AC2's onboarding "403/404" clause, and closed one stale hygiene entry — so this shows up as a deliberate finding, not a skipped story.

### Review Findings

- [x] [Review][Patch] `sprint-status.yaml`'s header comment disagrees with its own live status field ("ready-for-dev" vs "review" for the same story/date) [_bmad-output/implementation-artifacts/sprint-status.yaml:2]
- [x] [Review][Patch] New `RunAsync_PlannedAnnualSpendNotANumber_Returns400BadRequest` test seeds `plannedAnnualSpend` at its null default, so `persisted.PlannedAnnualSpend.ShouldBeNull()` can't distinguish "rejected, no write" from "already null" — deviates from the sibling `RunAsync_AnnualKwhBaselineExplicitNull_...` test's convention of seeding a non-default value and asserting it survives unchanged [api.Tests/Features/Flats/PatchFlatFunctionTests.cs:292-306]
- [x] [Review][Patch] Story file's File List omits `sprint-status.yaml`, which this diff also modifies [_bmad-output/implementation-artifacts/11-10-http-level-test-coverage-onboarding-and-patchflat.md]
- [x] [Review][Defer] `PatchFlatFunction.cs` silently no-ops on explicit `"name": null` (falls through to the same no-op as an omitted field), inconsistent with `annualKwhBaseline` (rejects explicit null with 400) and `plannedAnnualSpend` (accepts explicit null as a clear), and untested [api/Features/Flats/PatchFlatFunction.cs:58-62] — deferred, pre-existing
- [x] [Review][Defer] `CompleteOnboardingFunction.cs` has no test for a request body that is the literal JSON value `null` (valid JSON, distinct entry path from the malformed/unparseable-JSON case that IS tested) [api/Features/Onboarding/CompleteOnboardingFunction.cs:22-30] — deferred, pre-existing
- [x] [Review][Defer] `PatchFlatFunction.cs` has no test asserting which error message wins when multiple scalar fields (e.g. `annualKwhBaseline` and `plannedAnnualSpend`) are simultaneously invalid in one PATCH body — current first-checked-field-wins ordering is implicit and unprotected [api/Features/Flats/PatchFlatFunction.cs:38-52] — deferred, pre-existing

## Dev Notes

### Why this story is unusually light on code changes

This is a verification-and-hygiene story, not a build-from-scratch one. Both target test files were created by a 2026-07-03 backfill (commit `4ee466a`, closing an Epic 3 retro item) — three weeks *before* Epic 11 was scoped on 2026-07-26 — and have been incrementally kept current by five stories since (9.9, 9.10, 11.4, 11.5, and the backfill itself). The epic's Note text ("verify current state, since a later story added narrow direct-validator tests as a stopgap, not full Function-level HTTP coverage") is itself out of date — the "stopgap" (`OnboardingValidatorTests.cs`/`PatchFlatValidatorTests.cs`) predates the *full* Function-level backfill by mere hours in the same 2026-07-02→07-03 window (see `deferred-work.md`'s `epic-3-retro` entry, written when only the stopgap existed, and the very next dated section, the 2026-07-03 backfill's own review, once the full backfill landed).

**Do not re-derive this from scratch** — if you find yourself about to write a new `CompleteOnboardingFunctionTests.cs` or `PatchFlatFunctionTests.cs` from a blank file, stop: both already exist at the paths named in the AC.

### Current test inventory (verified during story creation, both files read in full)

`api.Tests/Features/Onboarding/CompleteOnboardingFunctionTests.cs` (8 tests):
`RunAsync_ValidRequest_CreatesFlatAndTariffReturns201`, `RunAsync_ProviderNameAndContractDurationProvided_PersistsBothOnTariff`, `RunAsync_NoContractStartDateProvided_DefaultsTariffContractStartDateToUtcNow`, `RunAsync_ContractStartDateProvided_UsesProvidedValueNotUtcNow`, `RunAsync_UserAlreadyHasFlat_Returns409ConflictAndCreatesNothing`, `RunAsync_MalformedJsonBody_Returns400BadRequest`, `RunAsync_EmptyFlatName_Returns400ValidationErrorAndCreatesNothing`.

`api.Tests/Features/Flats/PatchFlatFunctionTests.cs` (18 tests):
`RunAsync_InvalidFlatIdFormat_Returns400BadRequest`, `RunAsync_FlatDoesNotExist_Returns403Forbidden`, `RunAsync_FlatBelongsToDifferentUser_Returns403ForbiddenAndPersistsNothing`, `RunAsync_ValidNamePatch_Returns200AndUpdatesName`, `RunAsync_ValidAnnualKwhBaselinePatch_Returns200AndUpdatesBaseline`, `RunAsync_MultipleFieldsInOnePatch_UpdatesAllThree`, `RunAsync_AnnualKwhBaselineExplicitNull_Returns400AndLeavesExistingValueUnchanged`, `RunAsync_EmptyPatchBody_Returns200NoOpAndPersistsNothing`, `RunAsync_AnnualKwhBaselineOmitted_LeavesExistingValueUnchanged`, `RunAsync_JsonBodyIsNotAnObject_Returns400BadRequest`, `RunAsync_PlannedAnnualSpendOmitted_LeavesExistingValueUnchanged`, `RunAsync_PlannedAnnualSpendExplicitNull_ClearsExistingValue`, `RunAsync_MalformedJsonBody_Returns400BadRequest`, `RunAsync_AnnualKwhBaselineNotANumber_Returns400BadRequest`, `RunAsync_NameNotAString_Returns400BadRequest`, `RunAsync_EmptyNamePatch_Returns400ValidationErrorAndPersistsNothing`, `RunAsync_MissingRowVersion_Returns400BadRequest`, `RunAsync_MalformedRowVersion_Returns400BadRequest`, `RunAsync_ConcurrentModification_Returns409Conflict`.

Both verified passing (26/26) via `dotnet test api.Tests/api.Tests.csproj --filter "FullyQualifiedName~CompleteOnboardingFunctionTests|FullyQualifiedName~PatchFlatFunctionTests"` on `a0c92b1` (this story's baseline commit).

### The corrected AC2 — full reasoning (so Task 1 doesn't waste effort chasing a non-existent path)

Traced via `TenantResolverMiddleware.cs` (registered in `Program.cs`) and `FunctionContextExtensions.GetUserId()`:
- The middleware reads `X-MS-CLIENT-PRINCIPAL`, and short-circuits with a 403 itself, *before* any Function's `RunAsync` runs, if the header is missing/malformed. It never queries the DB for a `User` row — no upsert, no existence check.
- `GetUserId()` just reads the context item the middleware already set; it throws only if called on a non-HTTP trigger (blob/queue/timer), which doesn't apply here.
- `CompleteOnboardingFunction.cs` takes no `{resourceId}` route parameter (`Route = "v1/onboarding"`) — it has no caller-supplied resource to look up and reject as "not found" or "not yours." Its only guard is the 409 "already onboarded" check, already covered.
- Contrast with the *real* tenant-check convention used everywhere a route DOES carry a resource ID (`PatchFlatFunction.cs:27`, `DeleteFlatFunction.cs`, `PatchTariffFunction.cs`, `GetFlatStructureFunction.cs`): `if (flat is null || flat.UserId != userId) return 403` — deliberately conflating true-404 and wrong-owner into one 403 to avoid leaking resource existence to a non-owner. `PatchFlatFunctionTests.cs` already tests both branches of this (`RunAsync_FlatDoesNotExist_...`, `RunAsync_FlatBelongsToDifferentUser_...`).

Conclusion: the epic's "403/404 tenant-check" phrase for onboarding was very likely copy-adapted from the `PatchFlatFunction` bullet without re-checking onboarding's actual route shape. Treat AC2 as authoritative over the original epic text.

### Testing standards summary

- Backend Function tests: mock `AppDbContext` (EF Core `InMemory`, one per test via `Guid.NewGuid()` database name) + mock `FunctionContext` (`context.Items["UserId"]`), call `RunAsync` directly — no HTTP layer, no `WebApplicationFactory`. This is the established pattern in both target files and across `api.Tests/Features/**`.
- Do not add InMemory-provider tests for anything requiring real FK/unique-constraint/decimal-precision enforcement (`InMemory` doesn't enforce these) — that tier is Story 11.12's scope (SQLite integration tier), not this story's.
- [Source: _bmad-output/project-context.md] — "Highest-value targets... Functions: test handler method directly with mock `AppDbContext` + `FunctionContext` — not via HTTP" and "`InMemory` provider for unit-speed tests — does not enforce FK constraints, column types, or `decimal` precision."

### Previous story intelligence (Story 11.9)

Story 11.9 touched only `TrendChart.tsx` (frontend, unrelated feature slice) — no shared surface area with this story's backend test files. Its one transferable lesson (exact-count assertions over loose ones) is already the prevailing style in both target test files (e.g. `.CountAsync()).ShouldBe(1)`, not `.ShouldBeGreaterThan(0)`). `deferred-work.md` was checked for a `blocks: Story 11.10` tag per this project's standing process — none found (the only `blocks:` tag in the file targets Stories 10.2/10.3, unrelated).

### Project Structure Notes

- Likely-touched files: `api.Tests/Features/Onboarding/CompleteOnboardingFunctionTests.cs`, `api.Tests/Features/Flats/PatchFlatFunctionTests.cs` (only if Task 1's audit finds a genuine gap — may end up untouched), `_bmad-output/implementation-artifacts/deferred-work.md` (Task 2, definite).
- No production code changes expected. No new dependencies, no migrations, no frontend changes.

### References

- [Source: _bmad-output/planning-artifacts/epics/epic-11-post-epic-10-hardening-and-technical-debt-resolution.md#Story 11.10] — original epic AC text (superseded in part by AC2's correction above).
- [Source: _bmad-output/implementation-artifacts/deferred-work.md — "## Deferred from: epic-3-retro (2026-07-02)"] — the original gap entry this story's epic text paraphrases; the entry Task 2 closes.
- [Source: _bmad-output/implementation-artifacts/deferred-work.md — "## Deferred from: code review of onboarding/flat-patch test backfill (2026-07-03)"] — the two follow-up deferrals from the original backfill's own review (name-field 500→400; AnnualKwhBaseline explicit-null no-op), both since closed by Stories 11.4 and 9.9.
- [Source: api/Features/Onboarding/CompleteOnboardingFunction.cs] and [Source: api/Features/Flats/PatchFlatFunction.cs] — full current implementations, read in full during story creation.
- [Source: api.Tests/Features/Onboarding/CompleteOnboardingFunctionTests.cs] and [Source: api.Tests/Features/Flats/PatchFlatFunctionTests.cs] — full current test suites, read in full during story creation; 26/26 passing verified on baseline commit `a0c92b1`.
- [Source: api/Shared/TenantResolverMiddleware.cs] and [Source: api/Shared/FunctionContextExtensions.cs] — basis for AC2's correction (no DB-backed user-existence check exists anywhere in the request pipeline).
- [Source: _bmad-output/implementation-artifacts/11-9-accessible-spike-bar-indicator-design-gated.md] — previous story in this epic; confirmed no shared surface area.
- [Source: _bmad-output/project-context.md] — backend testing conventions (Function-level test pattern, `InMemory` provider limitations) applied above.

## Change Log

- 2026-07-31: Story created. Verification pass on existing coverage found this story's premise already resolved by a 2026-07-03 backfill (`4ee466a`), pre-dating the epic itself. AC2 corrected in-place: no 403/404 tenant-check path exists or should be added for `CompleteOnboardingFunction` (it takes no resource-ID route parameter). Scope narrowed to: confirm coverage (AC1, AC3), close the stale `deferred-work.md` entry describing the now-resolved gap (AC4), record the correction (AC2).
- 2026-07-31: Story implemented. Re-ran the filtered test command (26/26 passing, matching story creation baseline). Re-read `CompleteOnboardingFunction.cs` and `PatchFlatFunction.cs` branch-by-branch against their test files: every `CompleteOnboardingFunction.cs` return path was already covered (AC1 confirmed as-is); `PatchFlatFunction.cs` had one genuinely untested branch — the `plannedAnnualSpend` non-number-non-null 400 path (mirroring the already-tested `annualKwhBaseline` "not a number" case) — added `RunAsync_PlannedAnnualSpendNotANumber_Returns400BadRequest` to close it, following the file's existing `SeedFlatAsync`/`MakeRequest`/`MakeFunctionContext` conventions. Full suite now 27/27 for the two target files, 480/480 for the whole backend suite (no regressions). AC2's corrected understanding (no 403/404 path for onboarding) reconfirmed unchanged — no test added. Closed the stale `epic-3-retro (2026-07-02)` `deferred-work.md` entry (AC4), citing commit `4ee466a` as the actual closing work and Stories 11.4/9.9 as closures for that backfill's own two follow-up deferrals.
- 2026-07-31: Code review (3 layers — Blind Hunter, Edge Case Hunter, Acceptance Auditor). 3 patch findings applied: fixed `sprint-status.yaml`'s stale header comment; fixed the new `plannedAnnualSpend` test to seed a non-default value (800m) so its "unchanged after rejection" assertion is meaningful rather than tautological; added `sprint-status.yaml` to the File List. 3 findings deferred to `deferred-work.md` (pre-existing, out of scope): `PatchFlatFunction.cs`'s inconsistent explicit-null handling for `name` vs its sibling fields; no test for a literal-`null` JSON body on `CompleteOnboardingFunction.cs`; no test for multi-field-invalid precedence in `PatchFlatFunction.cs`. 12 findings dismissed as noise (verified-accurate claims Blind Hunter flagged as unverifiable, and complaints about test patterns that are pre-existing file-wide conventions). All 20 `PatchFlatFunctionTests` re-verified passing after patches.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- `dotnet test api.Tests/api.Tests.csproj --filter "FullyQualifiedName~CompleteOnboardingFunctionTests|FullyQualifiedName~PatchFlatFunctionTests"` → 26/26 passing (pre-change baseline confirmation)
- Same filtered command after adding the new test → 27/27 passing
- `dotnet test api.Tests/api.Tests.csproj` (full suite) → 480/480 passing, no regressions

### Completion Notes List

- AC1: Confirmed as-is. Every `return` statement in `CompleteOnboardingFunction.cs` (malformed body, validation failure, 409 conflict, 201 success incl. tariff field defaulting) has a corresponding existing test. No changes needed.
- AC2: Confirmed as-is. Traced `TenantResolverMiddleware`/`FunctionContextExtensions.GetUserId()` — no DB-backed user-existence check exists in the request pipeline; `CompleteOnboardingFunction` takes no route resource-ID and has no 403/404 path to test. No test added, per the story's pre-corrected AC text.
- AC3: One genuine gap found and closed. `PatchFlatFunction.cs`'s `plannedAnnualSpend`-provided-but-not-a-number branch (the exact structural counterpart to the already-tested `annualKwhBaseline`-not-a-number branch) had no test. Added `RunAsync_PlannedAnnualSpendNotANumber_Returns400BadRequest` to `PatchFlatFunctionTests.cs`, matching existing helper conventions exactly. All other enumerated cases (success paths, malformed-field cases incl. Story 11.4's 400-not-500 fix, tenant-check 403s, RowVersion conflict) were already present and passing.
- AC4: Closed the stale `## Deferred from: epic-3-retro (2026-07-02)` entry in `deferred-work.md` using the file's established `~~strikethrough~~` + `**Closed by Story 11.10 (2026-07-31).**` convention, citing commit `4ee466a` (2026-07-03) as the actual closing work and Stories 11.4/9.9 as the closures for that backfill's own two follow-up deferrals.
- No production code was changed — this was a verification-and-hygiene story per its Dev Notes framing. Only one net-new test was added.

### File List

- `api.Tests/Features/Flats/PatchFlatFunctionTests.cs` (modified — added `RunAsync_PlannedAnnualSpendNotANumber_Returns400BadRequest`)
- `_bmad-output/implementation-artifacts/deferred-work.md` (modified — closed stale `epic-3-retro (2026-07-02)` entry; added Review Findings deferrals from this story's own code review)
- `_bmad-output/implementation-artifacts/sprint-status.yaml` (modified — status bumped to `review`)
