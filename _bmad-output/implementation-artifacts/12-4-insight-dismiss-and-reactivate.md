---
baseline_commit: 964a7c2bd3f6b961aaad81eddb01a9d711d29a81
---

# Story 12.4: Insight Dismiss and Reactivate

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to dismiss an Insight I've already acted on or don't care about, and bring it back later if I change my mind,
so that my Insights view stays focused on things that still need my attention, without losing the ability to undo a dismissal by mistake.

## Acceptance Criteria

1. **Given** `Insight.cs`, **when** reviewed, **then** it gains two new columns: `IsDismissed` (`bool`, not null, default `false`) and `DismissedAt` (`DateTimeOffset?`); Fluent API only via `InsightConfiguration.cs`; the migration sets `IsDismissed = false` for all pre-existing rows — no behavior change for undismissed data.

2. **Given** a new `PatchInsightFunction` (`PATCH v1/flats/{flatId}/insights/{insightId}`, modeled on `PatchFlatFunction.cs`'s tenant-check + body-parse shape), **when** the request body sets `isDismissed: true`, **then** the targeted `Insight` row is updated with `IsDismissed = true, DismissedAt = now`; when `isDismissed: false`, `IsDismissed = false, DismissedAt = null`. Tenant check: `flatId` must belong to the resolved `userId`, and `insightId` must belong to `flatId` — 403/404 otherwise.

3. **Given** `GetInsightsFunction.cs`'s per-identity grouping, **when** the default request is made (no `status` param, or `status=active`), **then** rows with `IsDismissed = true` are excluded from the per-identity selection; when `status=dismissed` is passed, only the current `IsDismissed = true` row per identity is returned, using the same grouping logic.

4. **Given** `InsightDeduplication.IsNearDuplicateOfMostRecentAsync` and its four call sites (`StandbyDetector`, `ReplacementDetector`, `BudgetAlertDetector`, `InvoiceDeviationDetector`), **when** the most-recently-stored row for a `(FlatId, Type, DeviceId)` identity has `IsDismissed = true`, **then** no new `Insight` row is persisted for that identity regardless of FR-51's 5% tolerance comparison — the identity stays suppressed until reactivated.

5. **Given** a reactivated Insight (`IsDismissed` flipped back to `false`), **when** a subsequent discovery run evaluates that identity, **then** FR-51's normal 5%-tolerance comparison resumes — a new row persists only if the new figure differs by more than 5% from the reactivated row's stored value.

6. **Given** `InsightCard.tsx` (currently a pure display component with no action row) and `InsightsTab.tsx`, **when** implemented, **then** `InsightCard` gains a dismiss icon button in the default "Active" view (aria-label per UX-DR11) and a reactivate icon button when rendered in a "Dismissed" view; `InsightsTab` gains an Active/Dismissed toggle that switches the query param passed to `useInsights` and determines which action button renders.

7. **Given** `insightsApi.ts` and `useInsights.ts`, **when** implemented, **then** two new mutation hooks are added (`useDismissInsight`, `useReactivateInsight`), each calling the new PATCH endpoint and invalidating `['insights', flatId]` in `onSuccess`, per the project's standard mutation-hook pattern.

8. **Given** backend and frontend test suites, **when** run, **then** tests cover: dismissed identity suppresses persistence regardless of tolerance (`InsightDeduplicationTests.cs`); dismiss/reactivate toggle and tenant-isolation 403 (`PatchInsightFunction` tests); active vs dismissed filtering (`GetInsightsFunction` tests); toggle switches view and correct action button renders per state (`InsightsTab`/`InsightCard` tests).

## Tasks / Subtasks

- [x] **Task 1: Entity + migration** (AC: #1)
  - [x] 1.1 Add `IsDismissed` (`bool`) and `DismissedAt` (`DateTimeOffset?`) properties to `api/Data/Entities/Insight.cs`.
  - [x] 1.2 In `api/Data/Configurations/InsightConfiguration.cs` add `builder.Property(i => i.IsDismissed).IsRequired().HasDefaultValue(false);` and `builder.Property(i => i.DismissedAt).IsRequired(false);` (mirrors `MeterReadingConfiguration.cs`'s `IsCorrected` pattern exactly).
  - [x] 1.3 Run `dotnet ef migrations list` (from `api/`) first to confirm current migration order, then `dotnet ef migrations add AddInsightDismissal`. Verify the generated `Up()` uses `AddColumn<bool>(..., defaultValue: false)` and `AddColumn<DateTimeOffset>(..., nullable: true)` — no manual SQL needed; `defaultValue: false` on a NOT NULL column backfills existing rows automatically.
  - [x] 1.4 Run `dotnet ef database update` locally to verify the migration applies cleanly before proceeding.

- [x] **Task 2: `PatchInsightFunction`** (AC: #2)
  - [x] 2.1 Create `api/Features/Insights/PatchInsightFunction.cs`, constructor `PatchInsightFunction(AppDbContext db)` — no validator needed (single boolean field).
  - [x] 2.2 Route: `[HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/flats/{flatId}/insights/{insightId}")]`, params `string flatId, string insightId, FunctionContext context, CancellationToken ct`.
  - [x] 2.3 First line: `var userId = context.GetUserId();`. Parse `flatId` as Guid (400 if invalid). Look up `Flat` by `flatId`; 403 if not found or `flat.UserId != userId` (mirror `PatchFlatFunction.cs` exactly, including the Problem Details shape).
  - [x] 2.4 Parse `insightId` as Guid (400 if invalid). Look up `Insight` by `InsightId == insightGuid && FlatId == flatGuid` (tracked, not `AsNoTracking` — it will be saved); 404 if not found.
  - [x] 2.5 Parse body via `JsonNode.Parse` (same try/catch-then-type-check shape as `PatchFlatFunction.cs`). Require `isDismissed` as a JSON boolean; 400 `"isDismissed is required and must be a boolean."` if missing, null, or non-boolean.
  - [x] 2.6 Apply: `if (isDismissed) { insight.IsDismissed = true; insight.DismissedAt = DateTimeOffset.UtcNow; } else { insight.IsDismissed = false; insight.DismissedAt = null; }`. Call `await db.SaveChangesAsync(ct);`.
  - [x] 2.7 Add `public record PatchInsightResponse(Guid InsightId, bool IsDismissed, DateTimeOffset? DismissedAt);` to `api/Features/Insights/InsightModels.cs`. Return `new OkObjectResult(new PatchInsightResponse(insight.InsightId, insight.IsDismissed, insight.DismissedAt));`.
  - [x] 2.8 No `Program.cs` change needed — `AppDbContext` is already registered `Scoped`; the Function class needs no additional DI registration (consistent with `GetInsightsFunction`).

- [x] **Task 3: `GetInsightsFunction.cs` active/dismissed filter** (AC: #3)
  - [x] 3.1 Read the `status` query param: `var status = req.Query["status"].ToString(); var wantDismissed = string.Equals(status, "dismissed", StringComparison.OrdinalIgnoreCase);`.
  - [x] 3.2 Change the existing insights query's `.Where(i => i.FlatId == flatGuid)` to `.Where(i => i.FlatId == flatGuid && i.IsDismissed == wantDismissed)`. **Do not touch anything else in the function** — the existing per-identity grouping/selection loop (pick first candidate with valid JSON, ordered by `CreatedAt` desc then `InsightId` desc) is reused unchanged for both branches; it now naturally operates on a pre-filtered set.
  - [x] 3.3 Also select `i.IsDismissed` and `i.DismissedAt` is **not** required on `InsightDto` — do not add fields to the wire contract; the frontend already knows which view it requested (AC6 handles that via component props, not response data). Keep the response contract additive-only, matching the sprint-change-proposal's "no breaking API contract changes."

- [x] **Task 4: Dedup short-circuit** (AC: #4, #5)
  - [x] 4.1 In `api/Shared/InsightDeduplication.cs`, immediately after the existing `if (mostRecent is null) return false;` check, add: `if (mostRecent.IsDismissed) return true;` — this must come *before* `ExtractPrimaryValue`/tolerance comparison, so it is unconditional regardless of how far the new figure has drifted (AC4).
  - [x] 4.2 No changes needed to the four detector call sites (`StandbyDetector.cs`, `ReplacementDetector.cs`, `BudgetAlertDetector.cs`, `InvoiceDeviationDetector.cs`) — they all call the shared helper as-is; the short-circuit is transparent to them.
  - [x] 4.3 AC5 (resume-on-reactivate) requires no additional code: once `IsDismissed` is cleared back to `false` (Task 2), the new short-circuit no longer triggers and the existing tolerance comparison runs normally against that row.

- [x] **Task 5: Frontend API + hooks** (AC: #7)
  - [x] 5.1 In `client/src/features/insights/api/insightsApi.ts`: add `export type InsightsStatus = 'active' | 'dismissed'`. Change `getInsights` to `export const getInsights = (flatId: string, status: InsightsStatus = 'active') => apiClient.get<InsightsResponse>(\`/flats/${flatId}/insights?status=${status}\`)`. Add `export const patchInsight = (flatId: string, insightId: string, isDismissed: boolean) => apiClient.patch<{ insightId: string; isDismissed: boolean; dismissedAt: string | null }>(\`/flats/${flatId}/insights/${insightId}\`, { isDismissed })`.
  - [x] 5.2 In `client/src/features/insights/hooks/useInsights.ts`: add a `status: InsightsStatus = 'active'` parameter, thread it into both `queryKey: ['insights', flatId, status]` and `queryFn: () => getInsights(flatId as string, status)`. Leave `refetchInterval` untouched.
  - [x] 5.3 Create `client/src/features/insights/hooks/useDismissInsight.ts` — one hook per mutation, mirrors `useTriggerInsights.ts` exactly: `useMutation({ mutationFn: (insightId: string) => { if (!flatId) throw new Error('flatId is required'); return patchInsight(flatId, insightId, true) }, onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: ['insights', flatId] }) } })`. Invalidating the unscoped `['insights', flatId]` key (not `[..., status]`) invalidates **both** the active and dismissed cached queries per TanStack Query v5's prefix-match semantics — required so a dismiss action updates both views' caches.
  - [x] 5.4 Create `client/src/features/insights/hooks/useReactivateInsight.ts` — identical shape, calls `patchInsight(flatId, insightId, false)`.

- [x] **Task 6: Frontend UI — `InsightCard` action buttons** (AC: #6)
  - [x] 6.1 Add props to `InsightCard`: `view: 'active' | 'dismissed'`, `onDismiss: (insightId: string) => void`, `onReactivate: (insightId: string) => void`.
  - [x] 6.2 Add an icon-button row inside the card (reuse the existing icon-button convention from `FlatStructureEditor.tsx`: `min-h-11 min-w-11 flex items-center justify-center rounded-full` wrapper, icon `size` ~16, `aria-hidden="true"` on the icon, `aria-label` on the `<button>` — see Dev Notes for exact classes). When `view === 'active'`, render one button (lucide `X` icon) calling `onDismiss(insight.insightId)`, `aria-label={t('card.dismissLabel')}`. When `view === 'dismissed'`, render one button (lucide `RotateCcw` icon) calling `onReactivate(insight.insightId)`, `aria-label={t('card.reactivateLabel')}`.
  - [x] 6.3 Add `card.dismissLabel` / `card.reactivateLabel` keys to both `client/src/locales/en-US/insights.json` and `de-DE/insights.json`.

- [x] **Task 7: Frontend UI — `InsightsTab` Active/Dismissed toggle** (AC: #6)
  - [x] 7.1 Add `const [view, setView] = useState<'active' | 'dismissed'>('active')`.
  - [x] 7.2 Call `useInsights(flatId, view)` instead of `useInsights(flatId)`. Call `const dismissInsight = useDismissInsight(flatId)` and `const reactivateInsight = useReactivateInsight(flatId)`.
  - [x] 7.3 Render a two-button toggle (reuse the `role="radiogroup"` + `role="radio"` + `aria-checked` pattern from `DeviceEditor.tsx:209-239`, not the dropdown/listbox pattern from `InsightsPeriodSelector.tsx` — this is a binary either/or toggle, not a multi-option picker). Place it between the refresh button and the cards container. Labels: `t('toggle.active')` / `t('toggle.dismissed')`.
  - [x] 7.4 Pass `view={view}`, `onDismiss={id => dismissInsight.mutate(id)}`, `onReactivate={id => reactivateInsight.mutate(id)}` to every `<InsightCard>`.
  - [x] 7.5 Empty-state branching is **Active-view-specific** today (driven by `runStatus`/`readingHistoryDays`, neither of which has meaning for a dismissed list). Restructure so those concepts only apply when `view === 'active'`; add a plain `t('emptyState.noDismissed')` message for `view === 'dismissed'` with zero results. See Dev Notes for the exact merge with existing gating.
  - [x] 7.6 `isDiscovering` progress banner (`InsightDiscoveryProgress`) is also Active-only semantically — gate it with `view === 'active' && isDiscovering` (currently just `isDiscovering`).
  - [x] 7.7 Add `toggle.active` / `toggle.dismissed` / `emptyState.noDismissed` keys to both locale files.

- [x] **Task 8: Tests** (AC: #8)
  - [x] 8.1 `api.Tests/Shared/InsightDeduplicationTests.cs`: add `IsNearDuplicateOfMostRecentAsync_MostRecentRowIsDismissed_ReturnsTrueRegardlessOfTolerance` — seed a dismissed row with a value far outside 5% tolerance (e.g. stored `100`, new value `1000`), assert result is `true`.
  - [x] 8.2 New `api.Tests/Features/Insights/PatchInsightFunctionTests.cs` (mirror `PatchFlatFunctionTests.cs`'s structure and `GetInsightsFunctionTests.cs`'s `MakeDb`/`MakeFunctionContext`/`SeedFlatAsync` helpers): dismiss sets `IsDismissed=true`+`DismissedAt` populated; reactivate (isDismissed:false) clears both; foreign `flatId` → 403; `insightId` not belonging to `flatId` → 404; invalid body (`isDismissed` missing/non-bool) → 400; invalid `flatId`/`insightId` GUID format → 400.
  - [x] 8.3 `api.Tests/Features/Insights/GetInsightsFunctionTests.cs`: add tests for default/`status=active` excluding dismissed rows for an identity entirely (not falling back to an older non-dismissed row for that same identity, since none exists in the normal flow); `status=dismissed` returning only the dismissed row; an identity with no dismissed row returning nothing under `status=dismissed`.
  - [x] 8.4 `client/src/features/insights/components/InsightCard.test.tsx`: update all existing render calls to pass the new required `view`/`onDismiss`/`onReactivate` props; add tests asserting the dismiss button renders (and calls `onDismiss` with the insight's id) when `view="active"`, and the reactivate button renders (and calls `onReactivate`) when `view="dismissed"`.
  - [x] 8.5 `client/src/features/insights/components/InsightsTab.test.tsx`: mock `useDismissInsight`/`useReactivateInsight` (same `vi.mock` + `vi.mocked` pattern as `useTriggerInsights`); add tests that clicking the Dismissed toggle switches `useInsights`'s second call argument to `'dismissed'` and renders reactivate buttons instead of dismiss buttons; verify the existing Active-view empty-state tests still pass unchanged.

## Dev Notes

### Architecture context (AD-8c)

`architecture.md` AD-8c: *"`IsDismissed`/`DismissedAt` on the `Insight` row doubles as both 'hide from default view' and 'suppress future detection for this identity' — a dismissed identity's dedup check short-circuits to 'skip' regardless of the 5% comparison, and reactivating (clearing the flag) restores both the view and normal dedup evaluation in one step."* This is a single boolean flag on the existing per-identity representative row — **no new suppression table**. [Source: `_bmad-output/planning-artifacts/architecture.md:214-215`]

### Why the GetInsightsFunction filter is safe (read before implementing Task 3)

In the current codebase, the only `Insight` row a user can ever see (and therefore the only row a dismiss action can ever target) is the single most-recently-stored row per `(Type, DeviceId)` identity — FR-51's existing per-identity collapsing already hides every older/superseded row from the API response entirely. So in practice, at most one row per identity will ever have `IsDismissed = true` at a time, and it will always be the newest row for that identity. This means simply filtering the base query by `IsDismissed == wantDismissed` *before* the existing "pick first candidate with valid JSON, newest first" selection loop is safe: when the newest row for an identity is dismissed, the active-view filter removes it and — critically — does **not** cause an older row to resurface as the new "active" representative, because older rows are excluded from the result set entirely by the identity-grouping's own JSON-array construction (`insights` is fetched already filtered by `IsDismissed`, so an older non-dismissed row for a dismissed identity simply isn't in the candidate list — but such a row shouldn't exist anyway per the paragraph above). Net effect: a dismissed identity vanishes from Active and appears exactly once in Dismissed. Do not add extra logic to "look for an older active row" — that would contradict AD-8c's whole-identity-suppression design.

### `InsightDeduplication.cs` exact change (Task 4)

```csharp
public static async Task<bool> IsNearDuplicateOfMostRecentAsync(
    AppDbContext db, Guid flatId, InsightType type, Guid? deviceId, decimal newPrimaryValue, CancellationToken ct)
{
    var mostRecent = await db.Insights.AsNoTracking()
        .Where(i => i.FlatId == flatId && i.Type == type && i.DeviceId == deviceId)
        .OrderByDescending(i => i.CreatedAt)
        .ThenByDescending(i => i.InsightId)
        .FirstOrDefaultAsync(ct);

    if (mostRecent is null)
        return false;

    if (mostRecent.IsDismissed)          // NEW — unconditional suppression, before tolerance logic
        return true;

    var existingValue = ExtractPrimaryValue(mostRecent.Data, type);
    // ...unchanged from here
}
```
The query already orders by `CreatedAt`/`InsightId` descending across *all* rows (dismissed or not) for the identity — that's correct and unchanged; it will pick up the dismissed row as "most recent" and the new check short-circuits on it.

### `PatchInsightFunction.cs` reference shape

Model directly on `api/Features/Flats/PatchFlatFunction.cs` (read in full — 97 lines) for the tenant-check + `JsonNode` body-parse shape, Problem Details error format, and Function class structure. Key differences from that file: no `FluentValidation` validator (single boolean field doesn't warrant one), and the entity lookup needs a *compound* key (`InsightId` **and** `FlatId`) rather than `FlatId` alone, since the 404 case (AC2: "`insightId` must belong to `flatId`") is a distinct failure mode from the 403 case (flat doesn't belong to user):

```csharp
public class PatchInsightFunction(AppDbContext db)
{
    [Function("PatchInsight")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/flats/{flatId}/insights/{insightId}")] HttpRequest req,
        string flatId, string insightId, FunctionContext context, CancellationToken ct)
    {
        var userId = context.GetUserId();

        if (!Guid.TryParse(flatId, out var flatGuid))
            return new BadRequestObjectResult(new { type = "https://tools.ietf.org/html/rfc7231#section-6.5.1", title = "Bad Request", status = 400, detail = "Invalid flatId format." });

        var flat = await db.Flats.AsNoTracking().SingleOrDefaultAsync(f => f.FlatId == flatGuid, ct);
        if (flat is null || flat.UserId != userId)
            return new ObjectResult(new { type = "https://tools.ietf.org/html/rfc7231#section-6.5.3", title = "Forbidden", status = 403, detail = "Flat not found or access denied." }) { StatusCode = 403 };

        if (!Guid.TryParse(insightId, out var insightGuid))
            return new BadRequestObjectResult(new { type = "https://tools.ietf.org/html/rfc7231#section-6.5.1", title = "Bad Request", status = 400, detail = "Invalid insightId format." });

        var insight = await db.Insights.FirstOrDefaultAsync(i => i.InsightId == insightGuid && i.FlatId == flatGuid, ct);
        if (insight is null)
            return new NotFoundObjectResult(new { type = "https://tools.ietf.org/html/rfc7231#section-6.5.4", title = "Not Found", status = 404, detail = "Insight not found." });

        // ...body parse (isDismissed bool, required) -> apply -> SaveChangesAsync -> OkObjectResult(PatchInsightResponse)
    }
}
```
Use `SingleOrDefaultAsync` for the `Flat` lookup (PK/unique-constrained — matches `GetInsightsFunction.cs`'s convention) but `FirstOrDefaultAsync` for the `Insight` lookup isn't strictly needed either way since `(InsightId, FlatId)` is still unique on PK — either works; `FirstOrDefaultAsync` shown above for consistency with `PatchFlatFunction.cs`'s single-entity lookup style, but `SingleOrDefaultAsync` is equally correct here (PK lookup) — either is acceptable, prefer `SingleOrDefaultAsync` per project-context's PK-lookup rule if in doubt.

### Frontend: `InsightsTab.tsx` empty-state restructure (Task 7.5) — exact merge

Current structure (`client/src/features/insights/components/InsightsTab.tsx:90-108`):
```tsx
{!isPending && !isError && (
  <>
    {isDiscovering && <InsightDiscoveryProgress />}
    {insights.length > 0 ? (
      <div className="grid ...">{insights.map(insight => <InsightCard key={insight.insightId} insight={insight} />)}</div>
    ) : isDiscovering ? null : runStatus?.status === 'Failed' ? (
      <p>...emptyState.runFailed</p>
    ) : readingHistoryDays < 30 ? (
      <p>...emptyState.insufficientData</p>
    ) : (
      <p>...emptyState.noFindings</p>
    )}
  </>
)}
```
Target structure — wrap the `isDiscovering`-gated branch and the `runFailed`/`insufficientData`/`noFindings` cascade in a `view === 'active'` check, add a `view === 'dismissed'` branch that's just cards-or-empty-message with no run-status/history logic:
```tsx
{!isPending && !isError && (
  <>
    {view === 'active' && isDiscovering && <InsightDiscoveryProgress />}
    {insights.length > 0 ? (
      <div className="grid ...">
        {insights.map(insight => (
          <InsightCard key={insight.insightId} insight={insight} view={view}
            onDismiss={id => dismissInsight.mutate(id)} onReactivate={id => reactivateInsight.mutate(id)} />
        ))}
      </div>
    ) : view === 'dismissed' ? (
      <p className="text-body-sm text-text-secondary">{t('emptyState.noDismissed')}</p>
    ) : isDiscovering ? null : runStatus?.status === 'Failed' ? (
      <p>...emptyState.runFailed</p>
    ) : readingHistoryDays < 30 ? (
      <p>...emptyState.insufficientData</p>
    ) : (
      <p>...emptyState.noFindings</p>
    )}
  </>
)}
```
This preserves every existing Active-view test (`InsightsTab_Loading_RendersSkeletonBlocks`, `..._RunPending_ShowsProgressAndPriorCardsRemainVisible`, `..._NoInsightsAndReadingHistoryUnderThirtyDays_ShowsInsufficientDataMessage`, etc. — all implicitly `view === 'active'`, the default) byte-for-byte.

### `InsightCard.tsx` icon-button convention (Task 6.2)

Copy the icon-button shape from `client/src/features/flat-structure/components/FlatStructureEditor.tsx:420-424` (delete button), not a bare `<button>`:
```tsx
<button
  type="button"
  onClick={() => onDismiss(insight.insightId)}
  aria-label={t('card.dismissLabel')}
  className="min-h-11 min-w-11 flex items-center justify-center rounded-full shrink-0 text-white/50 hover:text-white transition-colors"
>
  <X className="h-4 w-4" aria-hidden="true" />
</button>
```
Import `X` and `RotateCcw` from `lucide-react` (already a project dependency — `Zap, Recycle, AlertTriangle, Receipt, ArrowUp, ArrowDown` are already imported in this file from the same package).

### `InsightsTab.tsx` toggle convention (Task 7.3)

Copy the `role="radiogroup"`/`role="radio"`/`aria-checked` shape from `client/src/features/flat-structure/components/DeviceEditor.tsx:209-239` — **not** `InsightsPeriodSelector.tsx`'s Popover/listbox pattern, which is for a 3-option picker with a dropdown trigger and is the wrong shape for a persistent 2-way toggle:
```tsx
<div className="mx-4 flex gap-2" role="radiogroup" aria-label={t('toggle.sectionLabel') /* or omit if a visible label already exists */}>
  <button type="button" role="radio" aria-checked={view === 'active'} onClick={() => setView('active')} className="min-h-11 ...">
    {t('toggle.active')}
  </button>
  <button type="button" role="radio" aria-checked={view === 'dismissed'} onClick={() => setView('dismissed')} className="min-h-11 ...">
    {t('toggle.dismissed')}
  </button>
</div>
```
Exact visual styling (active/inactive state classes) is not specified by UX-DR22 beyond "reuses existing card chrome and icon-button conventions — no new glass-surface pattern"; use judgement consistent with the existing refresh-button styling (`bg-white/[0.07] border border-white/[0.12]`) for the pressed/selected state vs. a plain/transparent unselected state.

### Testing Rules (from project context)

- Backend: xUnit + EF Core `InMemory`, `Shouldly` assertions, `Moq` for `ILogger<T>`. Test placement `api.Tests/Features/{Feature}/{Class}Tests.cs`, mirrors `api/Features/{Feature}/`.
- Frontend: Vitest + `@testing-library/react`, `globals: true` (no `describe`/`it`/`expect` imports — note `InsightsTab.test.tsx` already omits `describe`/`it`/`expect` imports and `InsightCard.test.tsx` explicitly imports them from `vitest` — **follow whichever import style already exists in the file being edited**, don't reconcile the two).
- `react-i18next` mock in both test files returns raw keys (with `|`-joined interpolation JSON for keys with options) — assert against translation keys, not rendered text.
- Query by role/label/text — the new icon buttons must be queryable via `getByRole('button', { name: t('card.dismissLabel') })` etc., which is why the `aria-label` is mandatory, not decorative.
- Run `npm test -- --run`, `npx tsc -b`, `npm run lint` (all from `client/`) — `npx tsc --noEmit` is a silent no-op in this repo (Story 12.1/12.2/12.3 note), don't use it.
- Backend: run `dotnet test` (no CI gate exists yet per project-context's "Known gaps" — run it manually before considering the story done).

### Project Structure Notes

- No conflicts with unified project structure — all new files land in existing VSA feature folders (`api/Features/Insights/`, `client/src/features/insights/{api,hooks,components}/`), matching every existing Insights file's placement.
- `PatchInsightFunction.cs` follows the codebase's `{Action}{Entity}Function` naming exactly (matches `PatchFlatFunction`, `PatchTariffFunction`, `PatchReadingFunction`).
- `useDismissInsight.ts` / `useReactivateInsight.ts` follow "one hook per mutation" — do not combine them into a single parameterized hook even though they're nearly identical; this matches `useTriggerInsights.ts`'s precedent of one file per mutation in this feature folder already.

### References

- [Source: `_bmad-output/planning-artifacts/epics/epic-12-device-lifecycle-and-date-aware-decomposition-attribution.md#Story 12.4`] — epic-level AC, used verbatim.
- [Source: `_bmad-output/planning-artifacts/sprint-change-proposal-2026-08-01-insight-dismiss.md`] — full origin/rationale; confirms single-flag design (no new suppression table), whole-identity dismiss/reactivate scope, Epic 12 placement rationale.
- [Source: `_bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md:494-509`] — FR-51 (existing dedup/retention) and FR-55 (this story) full text and testable consequences.
- [Source: `_bmad-output/planning-artifacts/epics/requirements-inventory.md:153`] — UX-DR22 (dismiss/reactivate affordances, no new visual pattern).
- [Source: `_bmad-output/planning-artifacts/epics/requirements-inventory.md:131`] — UX-DR11 (accessibility floor: explicit aria-labels on all icon buttons, 44×44pt tap targets).
- [Source: `_bmad-output/planning-artifacts/architecture.md:214-215,227,233,913`] — AD-8c design rationale; `Insights` entity table row with new columns; Requirements Coverage row.
- [Source: `api/Data/Entities/Insight.cs`, `api/Data/Configurations/InsightConfiguration.cs`] — full files read; current shape to extend.
- [Source: `api/Shared/InsightDeduplication.cs`] — full file read; exact short-circuit insertion point.
- [Source: `api/Features/Insights/GetInsightsFunction.cs`, `InsightModels.cs`] — full files read; per-identity grouping/selection loop reused unchanged, response contract.
- [Source: `api/Features/Insights/StandbyDetector.cs`, `ReplacementDetector.cs`, `BudgetAlertDetector.cs`, `InvoiceDeviationDetector.cs`] — full files read; confirmed all four call sites use the shared helper identically, no per-detector changes needed.
- [Source: `api/Features/Flats/PatchFlatFunction.cs`] — full file read; tenant-check + body-parse shape modeled directly.
- [Source: `api/Data/Configurations/MeterReadingConfiguration.cs:17`] — `IsCorrected` bool-with-default Fluent API pattern, mirrored for `IsDismissed`.
- [Source: `api/Data/Migrations/20260801161342_AddDeviceExistenceWindow.cs`] — recent nullable-column migration for `AddColumn`/`DropColumn` shape reference.
- [Source: `client/src/features/insights/components/InsightCard.tsx`, `InsightsTab.tsx`] — full files read; exact current render structure being extended.
- [Source: `client/src/features/insights/api/insightsApi.ts`, `hooks/useInsights.ts`, `hooks/useTriggerInsights.ts`] — full files read; API/hook contracts and mutation-hook pattern to replicate.
- [Source: `client/src/features/flat-structure/components/FlatStructureEditor.tsx:420-424`] — icon-button convention (tap target, aria-label, aria-hidden icon).
- [Source: `client/src/features/flat-structure/components/DeviceEditor.tsx:209-239`] — `role="radiogroup"`/`role="radio"` toggle convention, correct shape for the Active/Dismissed toggle (vs. the dropdown pattern in `InsightsPeriodSelector.tsx`, which is the wrong shape here).
- [Source: `client/src/lib/apiClient.ts`] — confirmed `get<T>` takes only a path (no query-param helper) — query strings are built manually into the path string, as `GetDecompositionFunction`'s frontend caller and this story's `getInsights` both do.
- [Source: `client/src/locales/en-US/insights.json`, `de-DE/insights.json`] — full files read; existing key structure to extend for `toggle.*`, `card.dismissLabel`/`reactivateLabel`, `emptyState.noDismissed`.
- [Source: `api.Tests/Shared/InsightDeduplicationTests.cs`, `Features/Insights/GetInsightsFunctionTests.cs`] — full files read; `MakeDb`/`MakeFunctionContext`/`SeedFlatAsync`/`SeedInsightAsync` helper patterns and `Fact` naming convention (`Method_State_Result`) to follow for new tests.
- [Source: `client/src/features/insights/components/InsightCard.test.tsx`, `InsightsTab.test.tsx`] — full files read; existing mock/test conventions, including the `describe`/`it`/`expect` import inconsistency noted above.
- [Source: `_bmad-output/project-context.md`] — mutation-hook pattern (`invalidateQueries` before close, scoped to `['resource', flatId]`), "one hook per mutation," VSA slice isolation, testing rules, `dotnet ef migrations list` order-check rule.

### Review Findings

- [x] [Review][Patch] (resolved from Decision) Dismissing an insight can resurrect a stale older row in the Active view — `GetInsightsFunction.cs` filters `.Where(i => i.FlatId == flatGuid && i.IsDismissed == wantDismissed)` *before* the per-identity "most recent" selection loop runs. `Insight` rows accumulate historically whenever `InsightDeduplication`'s 5% tolerance is exceeded (old rows are never deleted). If the current (most-recent) row for an identity is dismissed but an older, still-non-dismissed row exists for that same identity, the older row will now surface as "the" active insight instead of the identity disappearing from Active entirely — contradicting AD-8c's whole-identity-suppression design and AC3's intent. The Dev Notes assume this can't happen ("at most one row per identity will ever have `IsDismissed = true`... it will always be the newest row") but nothing in the code enforces that invariant; it only holds until a tolerance-breaking re-detection has ever fired for that identity before a dismiss. [`api/Features/Insights/GetInsightsFunction.cs:51-52`]
- [x] [Review][Patch] (resolved from Decision) `Insight` has no optimistic-concurrency (`RowVersion`) protection, unlike every other mutable entity in the codebase (`Flat`, `Tariff`, `MeterReading`, `Device`, `Room`, `PowerPoint`, `ImportJob`, `InsightRun` all configure `.IsRowVersion()`), and every existing PATCH function (`PatchFlatFunction`, `PatchTariffFunction`, `PatchReadingFunction`, `UpdateFlatStructureFunction`) catches `DbUpdateConcurrencyException` → 409. `PatchInsightFunction` is the first user-initiated write path ever added to `Insight` and has no such handling — a concurrent cascade-delete of the parent `Flat` (or two racing PATCH calls) surfaces as an unhandled `DbUpdateConcurrencyException` → 500 instead of a clean 404/409. [`api/Features/Insights/PatchInsightFunction.cs`, `api/Data/Entities/Insight.cs`]
- [x] [Review][Patch] (resolved from Decision) Dismiss/reactivate mutation failures have no error-surfacing UI — neither `useDismissInsight.ts` nor `useReactivateInsight.ts` define `onError`, and `InsightsTab.tsx` never reads `dismissInsight.isError`/`.error` or `reactivateInsight.isError`/`.error`. A failed PATCH (403 after access revoked, 404 if already deleted, network failure) produces no visible feedback — the click appears to silently do nothing. No existing project convention covers error-surfacing for icon-button (non-form, non-sheet) mutations. [`client/src/features/insights/hooks/useDismissInsight.ts`, `useReactivateInsight.ts`, `client/src/features/insights/components/InsightsTab.tsx`]
- [x] [Review][Patch] Dismiss/reactivate buttons aren't disabled while their mutation is pending — rapid double-clicks fire duplicate PATCH requests for the same insight before the query invalidates. [`client/src/features/insights/components/InsightsTab.tsx:126-131`, `client/src/features/insights/components/InsightCard.tsx:117-135`]
- [x] [Review][Patch] `status` query param on `GetInsightsFunction` is accepted by absence rather than validated — any value other than exactly `"dismissed"` (typo, unsupported value, comma-joined duplicate param) silently falls into the "active" branch with no error. [`api/Features/Insights/GetInsightsFunction.cs:51-52`]
- [x] [Review][Patch] Missing mirror test: the active/default status path is only ever tested against a dismissed-only dataset, never against a mixed active+dismissed dataset proving the dismissed row is excluded while a different active-identity row remains visible (the `status=dismissed` path does have this mirror test). [`api.Tests/Features/Insights/GetInsightsFunctionTests.cs`]
- [x] [Review][Patch] `PatchInsightFunctionTests.RunAsync_InsightIdDoesNotBelongToFlat_Returns404` uses a brand-new random GUID that doesn't exist in the DB at all, not an insight that exists but belongs to a different flat — the production code's compound `InsightId`+`FlatId` lookup does handle the real cross-flat case correctly by construction, but the AC8-named "tenant-isolation" test coverage for this specific path is missing. [`api.Tests/Features/Insights/PatchInsightFunctionTests.cs`]
- [x] [Review][Patch] New dedup test (`IsNearDuplicateOfMostRecentAsync_MostRecentRowIsDismissed_ReturnsTrueRegardlessOfTolerance`) only checks a far-outside-tolerance value (1000 vs 100) against a dismissed row — add a within-5%-tolerance value case to distinguish "suppressed because dismissed" from "suppressed because it would have deduped anyway." [`api.Tests/Shared/InsightDeduplicationTests.cs`]
- [x] [Review][Defer] Active/Dismissed toggle uses `role="radiogroup"`/`role="radio"` without roving-tabindex/arrow-key keyboard support — deferred, pre-existing (same gap already present in `DeviceEditor.tsx`'s toggle that this story's Dev Notes explicitly directed to copy). [`client/src/features/insights/components/InsightsTab.tsx:65-91`, `client/src/features/flat-structure/components/DeviceEditor.tsx:208-217`]

### Dismissed as noise (7)

- "Dedup short-circuit permanently mutes an identity once dismissed regardless of value drift" — this is literally AC4's specified behavior, confirmed correct against the spec.
- "Re-dismissing an already-dismissed insight resets `DismissedAt`" — no functional consequence; `DismissedAt` isn't read anywhere in this diff and dedup only checks the boolean.
- "Repeated inline Problem Details literals in `PatchInsightFunction`" — matches `PatchFlatFunction.cs`'s own pre-existing pattern (10 occurrences there too); no shared helper exists anywhere in the codebase.
- "403 (not 401) returned on tenant mismatch" — mirrors `PatchFlatFunction.cs`'s exact convention per the spec's explicit instruction to copy that shape.
- "`IsDismissed` configured with `ValueGeneratedOnAdd()`" — standard EF Core behavior for a non-nullable property with a store default; not a functional issue.
- "Unconfirmed whether `apiClient` has a `.patch` method" — verified: `client/src/lib/apiClient.ts:39` does export `.patch`.
- "Mixed styling conventions between the new toggle and new empty-state text" — matches the pre-existing refresh button's own raw-utility-class styling; Dev Notes explicitly directed this.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5 (claude-sonnet-5)

### Debug Log References

- Generated EF migration `AddInsightDismissal` for `AppDbContext` (SQL Server) and applied it to the dev Azure SQL DB via `dotnet ef database update` (confirmed with user first, since `local.settings.json`'s `SqlConnectionString` targets the live Azure SQL server, not a local instance).
- Initial `dotnet test` run in `api.Tests` failed 5 SQLite integration tests with `PendingModelChangesWarning` — the test tier uses a separate `SqliteAppDbContext` with its own migration history under `api.Tests/Data/Migrations/Sqlite/`, which the SQL Server-targeted migration didn't touch. Fixed by generating a matching migration via `dotnet ef migrations add AddInsightDismissal --project api.Tests --startup-project api.Tests --context SqliteAppDbContext --output-dir Data/Migrations/Sqlite`. All 531 backend tests pass after that.
- `useInsights.test.ts` (pre-existing, not listed in the story's Task 8 file list) needed a one-line update: `getInsights` is now called with an explicit `'active'` second argument by default.

### Completion Notes List

- Task 1: Added `IsDismissed`/`DismissedAt` to `Insight.cs` and `InsightConfiguration.cs`; generated and applied EF migrations for both the production `AppDbContext` (SQL Server) and the test-only `SqliteAppDbContext` (SQLite integration tier) — the latter wasn't called out in the story but is required for `dotnet test` to pass, since it maintains its own independent migration history.
- Task 2: `PatchInsightFunction.cs` created per the story's reference shape; `PatchInsightResponse` added to `InsightModels.cs`. Backend builds clean.
- Task 3: `GetInsightsFunction.cs` filters by `IsDismissed == wantDismissed` derived from the `status` query param; response contract unchanged (additive-only, as specified).
- Task 4: One-line short-circuit added to `InsightDeduplication.cs` before the tolerance comparison; no detector call sites touched.
- Task 5: `insightsApi.ts`/`useInsights.ts` extended with `InsightsStatus`/`patchInsight`; `useDismissInsight.ts`/`useReactivateInsight.ts` added as separate one-hook-per-mutation files.
- Task 6: `InsightCard.tsx` gained `view`/`onDismiss`/`onReactivate` props and an icon-button (X / RotateCcw) per view; locale keys added to both `en-US` and `de-DE`.
- Task 7: `InsightsTab.tsx` gained the Active/Dismissed radiogroup toggle, wired the two new mutation hooks, and restructured the empty-state/`isDiscovering` branches to be Active-view-specific per the story's exact merge guidance, adding a dedicated `emptyState.noDismissed` branch for the Dismissed view.
- Task 8: Added `InsightDeduplicationTests.cs` dismissed-short-circuit test; new `PatchInsightFunctionTests.cs` (11 tests covering dismiss/reactivate, tenant 403, insight-not-found 404, and body-validation 400s); `GetInsightsFunctionTests.cs` active/dismissed filtering tests; updated `InsightCard.test.tsx` (new required props on all existing render calls, two new tests for the per-view action button) and `InsightsTab.test.tsx` (mocked the two new hooks, two new tests for the toggle switching `useInsights`'s status arg and the dismissed empty state). Also added `useDismissInsight.test.ts`/`useReactivateInsight.test.ts` mirroring the existing `useTriggerInsights.test.ts` pattern, and fixed the one pre-existing `useInsights.test.ts` assertion affected by the new default status argument.
- Full regression: backend `dotnet test` → 531/531 passed; frontend `npm test -- --run` → 492/492 passed; `npx tsc -b` → clean; `npm run lint` → clean (only pre-existing unrelated `router.tsx` warnings).

### File List

**Backend**
- `api/Data/Entities/Insight.cs` (modified)
- `api/Data/Configurations/InsightConfiguration.cs` (modified)
- `api/Data/Migrations/20260802100101_AddInsightDismissal.cs` (added)
- `api/Data/Migrations/20260802100101_AddInsightDismissal.Designer.cs` (added)
- `api/Data/Migrations/AppDbContextModelSnapshot.cs` (modified)
- `api/Features/Insights/PatchInsightFunction.cs` (added)
- `api/Features/Insights/InsightModels.cs` (modified)
- `api/Features/Insights/GetInsightsFunction.cs` (modified)
- `api/Shared/InsightDeduplication.cs` (modified)
- `api.Tests/Data/Migrations/Sqlite/20260802100534_AddInsightDismissal.cs` (added)
- `api.Tests/Data/Migrations/Sqlite/20260802100534_AddInsightDismissal.Designer.cs` (added)
- `api.Tests/Data/Migrations/Sqlite/SqliteAppDbContextModelSnapshot.cs` (modified)
- `api.Tests/Shared/InsightDeduplicationTests.cs` (modified)
- `api.Tests/Features/Insights/PatchInsightFunctionTests.cs` (added)
- `api.Tests/Features/Insights/GetInsightsFunctionTests.cs` (modified)

**Frontend**
- `client/src/features/insights/api/insightsApi.ts` (modified)
- `client/src/features/insights/hooks/useInsights.ts` (modified)
- `client/src/features/insights/hooks/useInsights.test.ts` (modified)
- `client/src/features/insights/hooks/useDismissInsight.ts` (added)
- `client/src/features/insights/hooks/useDismissInsight.test.ts` (added)
- `client/src/features/insights/hooks/useReactivateInsight.ts` (added)
- `client/src/features/insights/hooks/useReactivateInsight.test.ts` (added)
- `client/src/features/insights/components/InsightCard.tsx` (modified)
- `client/src/features/insights/components/InsightCard.test.tsx` (modified)
- `client/src/features/insights/components/InsightsTab.tsx` (modified)
- `client/src/features/insights/components/InsightsTab.test.tsx` (modified)
- `client/src/locales/en-US/insights.json` (modified)
- `client/src/locales/de-DE/insights.json` (modified)
