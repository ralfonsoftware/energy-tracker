---
baseline_commit: f067ed0
---

# Story 3.6: Reading History — View & Correction

Status: done

## Story

As a user,
I want to view all my past meter readings and correct any that were entered incorrectly,
so that my consumption history is accurate and the original value is preserved for reference.

## Acceptance Criteria

1. **Reading history endpoint** — Given `GET /api/v1/flats/{flatId}/readings`, when called, then `GetReadingHistoryFunction` returns all Readings for the Flat in reverse-chronological order (`ReadingDate` descending) as an array of `ReadingResponse` records (`ReadingId`, `KwhValue` decimal, `ReadingDate` datetimeoffset, `IsCorrected` bool, `OriginalKwhValue` nullable decimal); HTTP 200; tenant-scoped (403 if `flatId` doesn't belong to the resolved `UserId`); ≤ 2s response time.

2. **Reading History sheet — list** — Given tapping the clock/list icon in the trend chart card header, when the Reading History bottom sheet opens, then `useReadingHistory` (TanStack Query key: `['readings', flatId]`) fetches the list; entries render in reverse-chronological order showing date/time and kWh value; entries with `IsCorrected = true` show a "corrected" note inline (displaying the original value, per the UX spec's "Original value was X kWh" wording).

3. **Tap entry to edit** — Given a reading entry is tapped, when the edit form opens (within the same sheet), then the kWh value field is pre-populated with the entry's current value and auto-focused with `inputmode="numeric"`; the entry's date/time is displayed for reference; all interactive elements meet 44×44pt tap targets; focus remains trapped within the sheet (inherited from the existing `Sheet` primitive).

4. **Save correction** — Given the edit form is saved with a new kWh value, when `PATCH /api/v1/flats/{flatId}/readings/{readingId}` is called, then `KwhValue` is updated; `IsCorrected` is set to `true`; `OriginalKwhValue` is set to the value the reading had *before this edit* — but only if the reading had never been corrected before (i.e. don't overwrite `OriginalKwhValue` on a second or later correction, so it always holds the very first submitted value); HTTP 200 with the updated `ReadingResponse`; on success, TanStack Query keys `['readings', flatId]` and `['dashboard', flatId]` are both invalidated.

5. **Load failure** — Given the Reading History sheet fails to load, when the fetch errors, then the sheet shows "Couldn't load reading history." with a Retry link; the sheet stays open (no auto-close, no navigation away).

## Tasks / Subtasks

- [x] **Task 0: Backend — `GET /readings` (history) + `PATCH /readings/{readingId}` (correction)** (AC: 1, 4)
  - [x] `api/Features/Readings/ReadingModels.cs` — add `public record PatchReadingRequest(decimal KwhValue);` (append-only; `SubmitReadingRequest`/`ReadingResponse` unchanged)
  - [x] `api/Features/Readings/PatchReadingValidator.cs` (NEW) — `AbstractValidator<PatchReadingRequest>`; single rule `RuleFor(r => r.KwhValue).GreaterThan(0m).WithMessage("kwhValue must be greater than 0.")` — mirror `ReadingValidator`'s style exactly
  - [x] `api/Features/Readings/GetReadingHistoryFunction.cs` (NEW) — see Dev Notes for full implementation
  - [x] `api/Features/Readings/PatchReadingFunction.cs` (NEW) — see Dev Notes "OriginalKwhValue Preservation Guard" for the exact, mandatory correction logic
  - [x] `api/Program.cs` (MODIFY) — add `builder.Services.AddSingleton<PatchReadingValidator>();` next to the existing `AddSingleton<ReadingValidator>();` line (pure validator, no DB access → Singleton, per project rule)
  - [x] `api.Tests/Features/Readings/GetReadingHistoryFunctionTests.cs` (NEW) — see Dev Notes "New Backend Tests"
  - [x] `api.Tests/Features/Readings/PatchReadingFunctionTests.cs` (NEW) — see Dev Notes "New Backend Tests"
  - [x] `dotnet test api.Tests` passes

- [x] **Task 1: `readingApi.ts` — add history fetch + patch** (AC: 1, 4)
  - [x] `client/src/features/readings/api/readingApi.ts` (MODIFY) — add `getReadingHistory(flatId)` and `patchReading(flatId, readingId, body)`; see Dev Notes for exact signatures

- [x] **Task 2: `useReadingHistory` + `usePatchReading` hooks** (AC: 2, 4)
  - [x] `client/src/features/readings/hooks/useReadingHistory.ts` (NEW) — `useQuery(['readings', flatId])`, `enabled: !!flatId` (mirror `useDashboard.ts`)
  - [x] `client/src/features/readings/hooks/usePatchReading.ts` (NEW) — `useMutation`; `onSuccess` invalidates both `['readings', flatId]` and `['dashboard', flatId]` (mirror `useSubmitReading.ts` — **no** optimistic update, per project rule "never optimistically update unless the story spec explicitly calls for it"; this story's AC doesn't)

- [x] **Task 3: `ReadingHistorySheet.tsx` — replace placeholder with list + inline edit** (AC: 2, 3, 4, 5)
  - [x] `client/src/features/readings/components/ReadingHistorySheet.tsx` (MODIFY — replaces the Story 3.5 placeholder body; same file, same component name, per the Story 3.5 "Story Boundary" note) — see Dev Notes for structure guidance

- [x] **Task 4: Translation keys** (AC: 2, 3, 4, 5)
  - [x] `client/src/locales/en-US/readings.json` — replace `history.comingSoon` with the real key set (see Dev Notes)
  - [x] `client/src/locales/de-DE/readings.json` — German equivalents

- [x] **Task 5: Tests** (AC: all)
  - [x] `client/src/features/readings/hooks/useReadingHistory.test.ts` (NEW)
  - [x] `client/src/features/readings/hooks/usePatchReading.test.ts` (NEW)
  - [x] `client/src/features/readings/components/ReadingHistorySheet.test.tsx` (MODIFY — replaces the 2 placeholder tests entirely; see Dev Notes "Task 5 Test Guidance" for the new minimum set)

- [x] **Task 6: Final verification**
  - [x] `dotnet test api.Tests` exits 0
  - [x] `cd client && npm run build` exits 0 with zero TypeScript errors
  - [x] `cd client && npm test` — all tests pass including all pre-existing tests
  - [x] `cd client && npm run lint` exits 0
  - [x] Update File List in this story

### Review Findings

- [x] [Review][Patch] No-op correction still marks reading as corrected — Saving an unchanged kWh value still sets `IsCorrected = true` and `OriginalKwhValue` to the same (unchanged) value, permanently flagging an untouched reading as "corrected" and showing a misleading "Original value was X kWh" note where original equals current. Decision (2026-07-02): skip the mutation entirely when `request.KwhValue == reading.KwhValue` — return the existing `ReadingResponse` unchanged. Fixed + regression test added (`RunAsync_UnchangedKwhValue_DoesNotMutateOrMarkAsCorrected`). [api/Features/Readings/PatchReadingFunction.cs:83]
- [x] [Review][Patch] No distinguishable "no readings yet" empty state — When the reading list resolves to an empty array (or `flatId` is transiently undefined on initial mount), the sheet renders an empty list with no explanatory copy, indistinguishable from a loading glitch; the Story 3.5 placeholder's test for the `flatId=undefined` case was removed without an equivalent replacement. Decision (2026-07-02): add a new `history.empty` i18n key ("No readings yet." / "Noch keine Ablesungen.") rendered when the list is empty. Fixed + regression test added (`ReadingHistorySheet_EmptyList_RendersEmptyStateTextAndNoListItems`). [client/src/features/readings/components/ReadingHistorySheet.tsx:70-72]
- [x] [Review][Patch] Corrected-note interpolates a raw unformatted number, inconsistent with the locale-formatted current value on the same row — fixed by wrapping with the existing `formatNumber` helper. [client/src/features/readings/components/ReadingHistorySheet.tsx:85]
- [x] [Review][Patch] Edit-view kWh input pre-fills via `String(reading.kwhValue)` (JS dot-decimal), which `parseLocaleNumber` misreads under de-DE (dots treated as thousands separators) — an untouched or partially edited fractional reading could silently save a corrupted value (e.g. 120.5 → 1205). Fixed with a new `toLocaleInputString` helper that swaps the decimal separator for de-DE without touching digit grouping (preserves full precision, unlike the display-only `formatNumber`). [client/src/features/readings/components/ReadingHistorySheet.tsx:19-20,108]
- [x] [Review][Patch] Correction Save button has no double-submit guard (unlike `EnterReadingSheet.tsx`'s `submittingRef` pattern) — a fast double-tap before `isPending` re-renders could fire two PATCH requests for the same correction. Fixed with an equivalent `submittingRef` guard, reset via a `useEffect` on `isPending` transitioning to `false`. [client/src/features/readings/components/ReadingHistorySheet.tsx:106,114-116,119-122]
- [x] [Review][Patch] "Back"/"Retry" buttons only constrained height (`min-h-11`), not width, falling short of AC-3's 44×44pt tap-target requirement — fixed by adding `min-w-11` to both. [client/src/features/readings/components/ReadingHistorySheet.tsx:64,123]
- [x] [Review][Defer] No validation of corrected value against adjacent readings (monotonicity/plausibility) [api/Features/Readings/PatchReadingFunction.cs] — deferred, pre-existing gap shared with original SubmitReadingFunction/ReadingValidator, out of scope for this story's AC
- [x] [Review][Defer] No optimistic-concurrency protection on PATCH (races between overlapping corrections) [api/Features/Readings/PatchReadingFunction.cs] — deferred, consistent with rest of codebase (no RowVersion/ETag pattern anywhere; documented known gap)
- [x] [Review][Defer] `SaveChangesAsync` not wrapped in try/catch [api/Features/Readings/PatchReadingFunction.cs:928] — deferred, consistent with existing Functions relying on host-level exception handling
- [x] [Review][Defer] No time/business-window restriction on correcting old readings [api/Features/Readings/PatchReadingFunction.cs] — deferred, architectural/product question not addressed by any AC or architecture doc
- [x] [Review][Defer] `GetReadingHistoryFunction` has no pagination/limit [api/Features/Readings/GetReadingHistoryFunction.cs:826] — deferred, consistent with "no caching layer, keep queries simple" and no other list endpoint paginates
- [x] [Review][Defer] `usePatchReading`'s `Promise.all` invalidation could theoretically surface a false "save failed" state if one `invalidateQueries` call rejects [client/src/features/readings/hooks/usePatchReading.ts:14] — deferred, exact code mandated verbatim by story Dev Notes; low real-world likelihood
- [x] [Review][Defer] Missing negative/malformed-input backend test coverage (negative kwhValue, missing property, non-numeric payload) [api.Tests/Features/Readings/PatchReadingFunctionTests.cs] — deferred, nice-to-have beyond the story's mandated test list
- [x] [Review][Defer] Test setup duplication (`MakeDb`/`MakeFunctionContext`/`SeedFlatAsync`) across test files [api.Tests/Features/Readings/GetReadingHistoryFunctionTests.cs, PatchReadingFunctionTests.cs] — deferred, pre-existing convention already used in 4+ other test files
- [x] [Review][Defer] Frontend test couples to DOM ordering via `getAllByRole('button')[0]` instead of a more specific query [client/src/features/readings/components/ReadingHistorySheet.test.tsx:206] — deferred, minor test-robustness nitpick

## Dev Notes

### Design Gap Resolved During Story Creation — Date/Time Field Is Read-Only in the Edit Form

**Why this needed a decision:** The epic AC-3 says the edit form's "kWh value and date/time fields are pre-populated with current values," and EXPERIENCE.md describes the edit form as reusing "the same numeric input pattern" as Enter Reading. But epic AC-4 (the PATCH contract) only ever mentions `KwhValue` being updated — there is no mention of `ReadingDate` in the PATCH request or response semantics anywhere in the epic, PRD, or architecture. Additionally, `MeterReadingConfiguration.cs` enforces a **unique index on `(FlatId, ReadingDate)`** — allowing date edits would require conflict handling (two readings colliding on the same instant) that no AC, mockup, or architecture note addresses. This is the same category of scope boundary Story 3.5 hit with the Reading History placeholder itself.

**Resolution:** The edit form's date/time field is **displayed for reference only, not editable, and not sent in the PATCH body**. Only `KwhValue` is editable and submitted. `PatchReadingRequest` is therefore a single-field record (`KwhValue` only) — do not add `ReadingDate` to it. This satisfies "pre-populated with current values" (both are shown) while keeping the correction flow to exactly what AC-4 specifies and avoiding the unique-index collision surface entirely.

### OriginalKwhValue Preservation Guard (mandatory logic — implement exactly)

AC-4 requires `OriginalKwhValue` to hold the value from **before the very first correction**, never a later intermediate value. The guard is a single `if`, evaluated *before* mutating `KwhValue`:

```csharp
if (!reading.IsCorrected)
    reading.OriginalKwhValue = reading.KwhValue;   // only set on the FIRST correction
reading.KwhValue = request.KwhValue;
reading.IsCorrected = true;
```

Trace through two corrections to confirm: Reading starts `KwhValue=100, IsCorrected=false, OriginalKwhValue=null`. Correction 1 (new value 120): guard fires (`IsCorrected` was `false`) → `OriginalKwhValue=100`, then `KwhValue=120`, `IsCorrected=true`. Correction 2 (new value 125): guard does NOT fire (`IsCorrected` already `true`) → `OriginalKwhValue` stays `100`, `KwhValue=125`. This is the exact scenario a naive `reading.OriginalKwhValue = reading.KwhValue` (unconditional, no guard) gets wrong — it would silently overwrite `100` with `120` on the second correction, losing the true original value. Cover both steps in tests (see below).

### `GetReadingHistoryFunction.cs` — full implementation

Mirrors `GetDashboardFunction.cs`'s tenant-check shape exactly (same `AsNoTracking`, same 403 Problem Details body, same `Guid.TryParse` 400 guard). Project into `ReadingResponse` directly in the query so EF Core generates a single projecting `SELECT` (same as how other list endpoints in this codebase avoid loading full entities when a DTO projection suffices — there's no other precedent for this exact shape yet, but it follows AD-23's "DTOs are records" + AD-6 "no caching layer, keep queries simple" spirit):

```csharp
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;

namespace EnergyTracker.Api.Features.Readings;

public class GetReadingHistoryFunction(AppDbContext db)
{
    [Function("GetReadingHistory")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/flats/{flatId}/readings")]
        HttpRequest req,
        string flatId,
        FunctionContext context,
        CancellationToken ct)
    {
        var userId = context.GetUserId();

        if (!Guid.TryParse(flatId, out var flatGuid))
            return new BadRequestObjectResult(new
            {
                title = "Bad Request", status = 400,
                detail = "Invalid flatId format."
            });

        var flat = await db.Flats.AsNoTracking()
            .SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId, ct);
        if (flat is null)
            return new ObjectResult(new
            {
                title = "Forbidden", status = 403,
                detail = "Flat not found or access denied."
            }) { StatusCode = 403 };

        var readings = await db.MeterReadings.AsNoTracking()
            .Where(r => r.FlatId == flatGuid)
            .OrderByDescending(r => r.ReadingDate)
            .Select(r => new ReadingResponse(r.ReadingId, r.KwhValue, r.ReadingDate, r.IsCorrected, r.OriginalKwhValue))
            .ToListAsync(ct);

        return new OkObjectResult(readings);
    }
}
```

Note the route `v1/flats/{flatId}/readings` is identical to `SubmitReadingFunction`'s route — this is correct and safe: Azure Functions HTTP triggers differentiate by **HTTP method** (`"get"` here vs `"post"` on `SubmitReadingFunction`), not just route template. Do not add a suffix or change either route to "avoid collision" — there is no collision.

### `PatchReadingFunction.cs` — full implementation

Same request-parsing shape as `SubmitReadingFunction.cs` (deserialize via `JsonSerializer.DeserializeAsync<T>` with the shared `_jsonOptions` pattern per project-context.md — **not** `req.ReadFromJsonAsync<T>()`). Adds a second existence check (the reading itself, scoped to the already-verified `flatGuid` — this is what makes the lookup tenant-safe: a `readingId` for a different Flat, even one the same or a different user owns, will not match `r.FlatId == flatGuid` and correctly falls into the 404 branch):

```csharp
using EnergyTracker.Api.Data;
using EnergyTracker.Api.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EnergyTracker.Api.Features.Readings;

public class PatchReadingFunction(AppDbContext db, PatchReadingValidator validator)
{
    private static readonly JsonSerializerOptions _jsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    [Function("PatchReading")]
    public async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/flats/{flatId}/readings/{readingId}")]
        HttpRequest req,
        string flatId,
        string readingId,
        FunctionContext context,
        CancellationToken ct)
    {
        var userId = context.GetUserId();

        if (!Guid.TryParse(flatId, out var flatGuid) || !Guid.TryParse(readingId, out var readingGuid))
            return new BadRequestObjectResult(new
            {
                title = "Bad Request", status = 400,
                detail = "Invalid id format."
            });

        var flat = await db.Flats.SingleOrDefaultAsync(f => f.FlatId == flatGuid && f.UserId == userId, ct);
        if (flat is null)
            return new ObjectResult(new
            {
                title = "Forbidden", status = 403,
                detail = "Flat not found or access denied."
            }) { StatusCode = 403 };

        var reading = await db.MeterReadings
            .SingleOrDefaultAsync(r => r.ReadingId == readingGuid && r.FlatId == flatGuid, ct);
        if (reading is null)
            return new NotFoundObjectResult(new
            {
                title = "Not Found", status = 404,
                detail = "Reading not found."
            });

        PatchReadingRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<PatchReadingRequest>(req.Body, _jsonOptions, ct);
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult(new
            {
                title = "Bad Request", status = 400,
                detail = "Invalid JSON in request body."
            });
        }

        if (request is null)
            return new BadRequestObjectResult(new
            {
                title = "Bad Request", status = 400,
                detail = "Request body is required."
            });

        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return new BadRequestObjectResult(new
            {
                title = "Validation Error", status = 400,
                detail = errors
            });
        }

        if (!reading.IsCorrected)
            reading.OriginalKwhValue = reading.KwhValue;
        reading.KwhValue = request.KwhValue;
        reading.IsCorrected = true;

        await db.SaveChangesAsync(ct);

        var response = new ReadingResponse(
            reading.ReadingId, reading.KwhValue, reading.ReadingDate, reading.IsCorrected, reading.OriginalKwhValue);
        return new OkObjectResult(response);
    }
}
```

Note `db.MeterReadings.SingleOrDefaultAsync(...)` here is **tracked** (no `.AsNoTracking()`) — deliberately, since this Function mutates the entity and calls `SaveChangesAsync`. This differs from `GetDashboardFunction`/`GetReadingHistoryFunction`'s `AsNoTracking()` reads; don't copy the no-tracking pattern here.

### New Backend Tests

**`GetReadingHistoryFunctionTests.cs`** (follow `GetDashboardFunctionTests.cs`'s `MakeDb`/`MakeFunctionContext`/`SeedFlatAsync` fixture pattern exactly):
1. `RunAsync_MultipleReadings_ReturnsReverseChronologicalOrder` — seed 3 readings with distinct dates out of order; assert the returned array's `ReadingDate`s are strictly descending.
2. `RunAsync_ReadingWithCorrection_IncludesIsCorrectedAndOriginalKwhValue` — seed a reading with `IsCorrected = true, OriginalKwhValue = 100m`; assert both fields round-trip in the response.
3. `RunAsync_NoReadings_ReturnsEmptyArray` — seed a flat with zero readings; assert `200` with an empty list (not an error).
4. `RunAsync_FlatNotOwnedByUser_Returns403` — mirror `SubmitReadingTests.RunAsync_FlatNotOwnedByUser_Returns403`.
5. `RunAsync_InvalidFlatIdGuid_Returns400` — mirror `SubmitReadingTests.RunAsync_InvalidFlatIdGuid_Returns400`.

**`PatchReadingFunctionTests.cs`** (same fixture pattern, plus seed a `MeterReading` directly into `db.MeterReadings`):
1. `RunAsync_ValidCorrection_UpdatesKwhValueSetsIsCorrectedAndOriginalKwhValue` — seed reading with `KwhValue=100m, IsCorrected=false`; PATCH with `kwhValue=120m`; assert response `KwhValue=120m, IsCorrected=true, OriginalKwhValue=100m`.
2. `RunAsync_SecondCorrection_PreservesFirstOriginalKwhValue` — seed reading already corrected once (`KwhValue=120m, IsCorrected=true, OriginalKwhValue=100m`); PATCH with `kwhValue=125m`; assert `OriginalKwhValue` is **still** `100m` (not `120m`) — this is the guard test, the one most likely to be gotten wrong without the exact `if (!reading.IsCorrected)` logic above.
3. `RunAsync_ValidCorrection_PersistsToDatabase` — mirror `SubmitReadingTests`'s persistence-check pattern via a fresh `db.MeterReadings` query after `RunAsync`.
4. `RunAsync_KwhValueZero_Returns400AndDoesNotMutate` — assert 400 and that the reading's `KwhValue`/`IsCorrected` are unchanged in the DB afterward.
5. `RunAsync_ReadingNotFound_Returns404` — valid `flatId`/`readingId` GUIDs but no matching row.
6. `RunAsync_ReadingBelongsToDifferentFlat_Returns404` — seed two Flats for the same user, a reading under Flat A, PATCH using Flat B's `flatId` with Flat A's `readingId` — must be 404, not 403 (the tenant check on `flatId` alone passes since the user owns Flat B; the reading-scoped-to-`flatGuid` query is what correctly returns nothing).
7. `RunAsync_FlatNotOwnedByUser_Returns403` — mirror existing pattern.
8. `RunAsync_InvalidReadingIdGuid_Returns400`.

Use `Shouldly` assertions throughout, matching every existing test in `api.Tests/Features/Readings/`.

### Frontend: `readingApi.ts` additions

```typescript
export const getReadingHistory = (flatId: string) =>
  apiClient.get<ReadingResponse[]>(`/flats/${flatId}/readings`)

export type PatchReadingRequest = { kwhValue: number }

export const patchReading = (flatId: string, readingId: string, body: PatchReadingRequest) =>
  apiClient.patch<ReadingResponse>(`/flats/${flatId}/readings/${readingId}`, body)
```

`ReadingResponse` (already exported from this file) is reused as-is for both — same shape as the POST response.

### Frontend: `useReadingHistory.ts` and `usePatchReading.ts`

```typescript
// useReadingHistory.ts — mirrors useDashboard.ts exactly
import { useQuery } from '@tanstack/react-query'
import { getReadingHistory } from '@/features/readings/api/readingApi'

export function useReadingHistory(flatId: string | undefined) {
  return useQuery({
    queryKey: ['readings', flatId],
    queryFn: () => getReadingHistory(flatId as string),
    enabled: !!flatId,
  })
}
```

```typescript
// usePatchReading.ts — mirrors useSubmitReading.ts; invalidates two keys, no optimistic update
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { patchReading } from '@/features/readings/api/readingApi'

export function usePatchReading(flatId: string | undefined) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ readingId, kwhValue }: { readingId: string; kwhValue: number }) => {
      if (!flatId) throw new Error('flatId is required')
      return patchReading(flatId, readingId, { kwhValue })
    },
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ['readings', flatId] }),
        queryClient.invalidateQueries({ queryKey: ['dashboard', flatId] }),
      ]),
  })
}
```

The `Promise.all` (not two separate `await` statements) ensures both invalidations are in flight before the mutation's own promise resolves — matches the "await invalidation before closing the sheet" rule from project-context.md, generalized to two keys.

### `ReadingHistorySheet.tsx` — structure guidance

**No dedicated mockup exists** for the Reading History list or edit-form visuals — `enter-reading-sheet.html` (referenced by EXPERIENCE.md for "edit form state") only contains the *Enter Reading* sheet's four frames (empty / valid / warning / error), not a Reading History variant. Build this screen by reusing established tokens/patterns from sibling components, not by inventing new visual language:

- Sheet chrome (drag handle, title) — keep the existing placeholder's opening lines (`<div aria-hidden="true" className="mx-auto mb-4 h-1 w-9 rounded-full bg-white/25" />` + `<h2 className="text-body text-text-primary">`) — don't touch `TrendChart.tsx`'s `SheetContent` styling (glass/rounded-sheet treatment already applied there, one level up).
- List rows: reuse `text-body`/`text-body-sm`/`text-caption`/`text-tertiary` type roles and `text-text-primary`/`text-text-secondary`/`text-text-tertiary` colors already defined in `client/src/index.css` — no new tokens needed. A `min-h-11` (44px) row height satisfies the tap-target requirement for the whole row being tappable.
- Date/time + kWh formatting: **do not reinvent** — `DashboardGrid.tsx` already has the exact pattern for both (`new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium', timeStyle: 'short' }).format(date)` for "Last read:", and a local `formatNumber`/`formatKwh` pair using `Intl.NumberFormat`). Per VSA slice isolation these are not exported/importable across features — re-declare equivalent small local functions in `ReadingHistorySheet.tsx` itself (same as `DashboardGrid.tsx` and `TrendChart.tsx` each declare their own local formatters; this is the established pattern, not an oversight to fix).
- Corrected note text: use the UX spec's literal wording via i18n interpolation, e.g. `t('history.correctedNote', { value: reading.originalKwhValue })` → `"Original value was 100 kWh"` (EN) — see translation keys below.
- Edit form: reuse `EnterReadingSheet.tsx`'s kWh input markup/classes (`h-14 w-full rounded-input border border-white/15 bg-white/[0.08] px-4 text-2xl text-text-primary outline-none focus:border-white/60`, `inputMode="numeric"`) and Save button markup/classes (`h-14 w-full rounded-pill border border-white/40 bg-white/[0.12] text-body text-text-primary disabled:opacity-40`) for visual consistency — this is the "same numeric input pattern" AC-3 refers to. Auto-focus the kWh input when the edit view mounts (a `useRef` + `useEffect(() => inputRef.current?.focus(), [])` is sufficient — the sheet-level `onOpenAutoFocus` trick from `EnterReadingSheet.tsx` doesn't apply here since this is an internal view switch, not a fresh sheet open).
- Use `parseLocaleNumber` from `@/lib/localeNumber` (already used by `EnterReadingSheet.tsx`) to parse the kWh input the same locale-aware way.
- State shape: a single `useState<ReadingResponse | null>(null)` for "which reading is being edited" is sufficient — `null` renders the list, non-null renders the edit form for that reading. No router/URL involvement (this is a within-sheet view switch per EXPERIENCE.md's flow description, not a navigation).
- Load error (AC-5): `isError` from `useReadingHistory` → render `role="alert"` text + a Retry `<button>` that calls the query's `refetch()` — mirror `DashboardPage.tsx`'s `role="alert"` convention for query errors (not a toast).
- Loading state: simple skeleton rows (2–3 pulsing `div`s) while `isLoading` — mirrors `TrendChart.tsx`'s skeleton-bars approach at the same loading-state granularity; don't over-engineer this, it's a secondary state.
- `flatId` prop: keep the existing `Props = { flatId: string | undefined }` signature (already correct in the placeholder) — pass it straight into `useReadingHistory(flatId)` and `usePatchReading(flatId)`.

### Translation Keys

`client/src/locales/en-US/readings.json` — **replace** the `history` object (the `sheet` object is untouched):
```json
"history": {
  "title": "Reading History",
  "loadError": "Couldn't load reading history.",
  "retry": "Retry",
  "correctedNote": "Original value was {{value}} kWh",
  "editSaveButton": "Save",
  "editSaveError": "Couldn't save — try again.",
  "backToList": "Back"
}
```

`client/src/locales/de-DE/readings.json` — German equivalents (match the tone of the existing `sheet` keys in that file):
```json
"history": {
  "title": "Ablesehistorie",
  "loadError": "Ablesehistorie konnte nicht geladen werden.",
  "retry": "Erneut versuchen",
  "correctedNote": "Ursprünglicher Wert war {{value}} kWh",
  "editSaveButton": "Speichern",
  "editSaveError": "Konnte nicht gespeichert werden — versuch's noch mal.",
  "backToList": "Zurück"
}
```

`comingSoon` is removed from both files — it has no remaining consumer after this story (the placeholder it described is being replaced).

### Task 5 Test Guidance

**`useReadingHistory.test.ts`** (mirror `useSubmitReading.test.ts`'s `vi.mock('@/features/readings/api/readingApi')` + `renderHook` pattern, minimum 2):
1. Resolves with the mocked `getReadingHistory` array, query key is `['readings', flatId]`.
2. `flatId === undefined` → `enabled: false` behavior — assert `getReadingHistory` is never called (mirror `useSubmitReading.test.ts`'s `flatId undefined` test intent, adapted for a query instead of a mutation).

**`usePatchReading.test.ts`** (mirror `useSubmitReading.test.ts` exactly, minimum 2):
1. `onSuccess` invalidates **both** `['readings', flatId]` and `['dashboard', flatId]` (assert `invalidateQueries` called with each).
2. `flatId === undefined` → mutation rejects, `patchReading` never called.

**`ReadingHistorySheet.test.tsx`** (replaces the 2 existing placeholder tests entirely — mock both `useReadingHistory` and `usePatchReading` at module scope like `EnterReadingSheet.test.tsx` mocks `useSubmitReading`; minimum 6):
1. Loading state: renders skeleton placeholders, no list items.
2. Populated list: renders each reading's formatted date and kWh value; reverse-chronological order is a given (list state trusts the API's order — this component doesn't re-sort).
3. Corrected entry: renders the `correctedNote` text with the interpolated `originalKwhValue`.
4. Load error: `role="alert"` text visible, Retry button present; clicking Retry calls the mocked `refetch`.
5. Tapping an entry switches to the edit view: kWh input pre-populated with that reading's `kwhValue`, auto-focused.
6. Saving in the edit view calls the mocked `usePatchReading().mutate` with `{ readingId, kwhValue: <parsed value> }`, and on the mutation's `onSuccess` callback firing, the view returns to the list (assert list content becomes visible again, mirroring how `EnterReadingSheet.test.tsx` asserts post-mutation state via the `mutate.mock.calls[0]` options callback).

### Architecture Compliance Checklist

- [ ] `import type` for all type-only imports (TS6 strict module mode)
- [ ] No barrel files — import directly from the declaring file
- [ ] `@/` alias for all imports — never relative paths
- [ ] No `!` non-null assertions in feature code
- [ ] All user-visible strings via `useTranslation('readings')` — no hardcoded strings in JSX
- [ ] `decimal` throughout the backend: `PatchReadingRequest.KwhValue`, `MeterReading.KwhValue`/`OriginalKwhValue` all stay `decimal`
- [ ] `CancellationToken ct` threaded through every new async call (`SingleOrDefaultAsync(..., ct)`, `ToListAsync(ct)`, `SaveChangesAsync(ct)`, `ValidateAsync(..., ct)`)
- [ ] Problem Details anonymous-object shape for all new error responses (400/403/404) — no typed exception classes
- [ ] `PatchReadingValidator` registered `Singleton` in `Program.cs` (pure, no DB dependency)
- [ ] JSON field naming stays camelCase automatically via the existing `Program.cs` serializer config
- [ ] Every feature namespace already registered in `i18n.ts` (`readings` — no new namespace needed, only new keys within it)

### Previous Story Intelligence (Story 3.5)

- `ReadingHistorySheet.tsx` currently exists as an **intentional placeholder** — this story replaces its body, it does not create a new file or rename anything. `TrendChart.tsx` already renders it inside a single-shared-`Sheet`-root (`Sheet` → `SheetTrigger asChild` → one `SheetContent`) with the glass/rounded-sheet styling already applied at the `SheetContent` level — do not touch `TrendChart.tsx` or duplicate that styling inside `ReadingHistorySheet.tsx` itself.
- The single-shared-`Sheet`-root pattern (not two `Sheet`/`SheetContent` roots) was a Story 3.4 review fix — stay within the existing single root; this story doesn't add any new `Sheet` usage, it only changes what renders *inside* the existing one.
- `useDashboard(flatId)` / `useUserSettings()` both return the full TanStack Query result or a destructured subset (`{ settings, isLoading, isError }`) — `useReadingHistory` should follow the plain "return the full TanStack Query result" convention (like `useDashboard`), not the destructured-subset convention (like `useUserSettings`), since the component needs `refetch` for the Retry button, which the full result exposes for free.
- `client/src/test-setup.ts` already stubs `window.matchMedia` and `window.ResizeObserver` (guarded, added in Story 3.5) — this story's components don't need either, but don't be surprised if they're present; no new stub is needed here.
- `recharts`/`ResizeObserver` concerns from Story 3.5 are irrelevant to this story — no charts here, plain list/form markup only.

### Git Intelligence

Recent commits (`8b1c6c1` Story 3.1 backend, `8432120`/`75925fb`/`80604ce` Story 3.2 backend, `003d8a0` Story 3.3 frontend, `6afb7b5` Story 3.4 CTA+sheet+animation, `f067ed0` Story 3.5 trend chart+spike+placeholder sheet) show a consistent backend-slice-then-frontend-slice rhythm within each story, with small scoped backend additions when the API contract needs a new field or endpoint. This story is backend-and-frontend-paired by design (two new endpoints, matching new hooks/UI) rather than one-sided like 3.4/3.5 — treat Task 0 (backend) as a genuine prerequisite for Tasks 1–3 (frontend), not an optional add-on.

### Project Structure — New/Modified Files

```
api/Features/Readings/
├── ReadingModels.cs                 ← MODIFY (add PatchReadingRequest record)
├── PatchReadingValidator.cs         ← NEW
├── GetReadingHistoryFunction.cs     ← NEW
└── PatchReadingFunction.cs          ← NEW

api/Program.cs                       ← MODIFY (register PatchReadingValidator as Singleton)

api.Tests/Features/Readings/
├── GetReadingHistoryFunctionTests.cs  ← NEW
└── PatchReadingFunctionTests.cs       ← NEW

client/src/features/readings/
├── api/readingApi.ts                ← MODIFY (add getReadingHistory, patchReading, PatchReadingRequest type)
├── hooks/
│   ├── useReadingHistory.ts         ← NEW
│   ├── useReadingHistory.test.ts    ← NEW
│   ├── usePatchReading.ts           ← NEW
│   └── usePatchReading.test.ts      ← NEW
└── components/
    ├── ReadingHistorySheet.tsx      ← MODIFY (replaces placeholder body)
    └── ReadingHistorySheet.test.tsx ← MODIFY (replaces placeholder tests)

client/src/locales/en-US/readings.json  ← MODIFY (replace history.comingSoon with real key set)
client/src/locales/de-DE/readings.json  ← MODIFY (German equivalents)
```

### References

- Epic source: `_bmad-output/planning-artifacts/epics/epic-3-meter-reading-kpi-dashboard-reading-history.md#Story 3.6`
- FR-8/FR-9 (reading entry/retroactive): `_bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md`
- Architecture placement: `_bmad-output/planning-artifacts/architecture.md` — `Readings/` slice explicitly lists `SubmitReadingFunction, GetReadingHistoryFunction, ReadingModels`; `readings/hooks/useReadingHistory.ts` and `readings/components/ReadingHistorySheet.tsx` (edit-with-log flow) are named in the frontend file tree
- AD-8 (hard deletes throughout; correction-in-place for readings): `_bmad-output/planning-artifacts/architecture.md` — "Meter Readings are edited in-place with `IsCorrected = true` and `OriginalKwhValue` preserved on the row (not a separate audit table)"
- Reading Correction Flow (full UX flow, 7 steps): `_bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/EXPERIENCE.md` lines 319–334
- State patterns table (`Reading History — default`, `— edit`, `— load failed`): `EXPERIENCE.md` lines 134–135, 158
- Design tokens (glass card, `body`/`body-sm`/`caption`/`label-caps`, spacing): `_bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/DESIGN.md` — all referenced tokens already exist in `client/src/index.css`, no new theme tokens required
- No dedicated Reading History mockup exists in `ux-designs/.../mockups/` — `enter-reading-sheet.html` only covers the Enter Reading sheet's 4 states, not Reading History or its edit form (verified directly during story creation)
- Previous story: `_bmad-output/implementation-artifacts/3-5-trend-chart-and-spike-detection.md` (placeholder `ReadingHistorySheet.tsx` this story replaces; single-shared-`Sheet`-root pattern; `DashboardGrid.tsx`'s local date/kWh formatters referenced above)
- Existing code read in full during story creation: `api/Features/Readings/{ReadingModels,ReadingValidator,SubmitReadingFunction}.cs`, `api/Features/Dashboard/GetDashboardFunction.cs`, `api/Features/Flats/{PatchFlatFunction,PatchFlatValidator}.cs`, `api/Data/Entities/MeterReading.cs`, `api/Data/Configurations/MeterReadingConfiguration.cs`, `api/Program.cs`, `api.Tests/Features/Readings/SubmitReadingTests.cs`, `api.Tests/Features/Dashboard/GetDashboardFunctionTests.cs`, `client/src/features/readings/{api/readingApi.ts,hooks/useSubmitReading.ts,hooks/useSubmitReading.test.ts,components/EnterReadingSheet.tsx,components/EnterReadingSheet.test.tsx,components/ReadingHistorySheet.tsx,components/ReadingHistorySheet.test.tsx,schemas/readingSchema.ts}`, `client/src/features/dashboard/{DashboardPage.tsx,hooks/useDashboard.ts,components/TrendChart.tsx,components/DashboardGrid.tsx}`, `client/src/features/settings/{hooks/usePatchFlat.ts,hooks/useUserSettings.ts,api/settingsApi.ts}`, `client/src/lib/{apiClient.ts,i18n.ts}`, `client/src/components/ui/sheet.tsx`, `client/src/index.css`

## Change Log

- Story created: 2026-07-02 — Reading History View & Correction; Epic 3 sixth (final) story; resolved one undocumented scope gap during creation (date/time field in the edit form is display-only, not part of the PATCH contract, due to the unique `(FlatId, ReadingDate)` index and the epic's PATCH AC only ever mentioning `KwhValue`)
- Story implemented: 2026-07-02 — backend `GET`/`PATCH` readings endpoints, frontend hooks, `ReadingHistorySheet` list + inline edit UI, and full test coverage added; existing `TrendChart.test.tsx` updated to mock the new reading-history hooks (regression fix, since it now renders the real `ReadingHistorySheet` instead of the Story 3.5 placeholder)
- Code review (2026-07-02): 3-layer adversarial review (Blind Hunter, Edge Case Hunter, Acceptance Auditor) found no AC violations. 6 patch findings fixed (no-op correction guard, empty-state copy, corrected-note locale formatting, edit-input locale round-trip bug, double-submit guard, tap-target width); 9 items deferred as pre-existing/out-of-scope; 12 dismissed as false positives or already-justified by spec. All verification gates re-run green (83/83 backend, 99/99 frontend, build + lint clean). Status → done.

## Dev Agent Record

### Agent Model Used

Claude Sonnet 5

### Debug Log References

None — no blocking issues encountered.

### Completion Notes List

- Implemented `GetReadingHistoryFunction` and `PatchReadingFunction` exactly per Dev Notes reference implementations, including the `OriginalKwhValue` preservation guard (`if (!reading.IsCorrected) reading.OriginalKwhValue = reading.KwhValue;` evaluated before mutating `KwhValue`); covered by a dedicated `RunAsync_SecondCorrection_PreservesFirstOriginalKwhValue` test.
- `PatchReadingValidator` registered as `Singleton` in `Program.cs` (pure, no DB dependency), matching `ReadingValidator`'s registration.
- Frontend: `useReadingHistory`/`usePatchReading` mirror `useDashboard`/`useSubmitReading` conventions exactly; `usePatchReading` invalidates both `['readings', flatId]` and `['dashboard', flatId]` via `Promise.all`, no optimistic update.
- `ReadingHistorySheet.tsx` replaces the Story 3.5 placeholder body only — the shared `Sheet`/`SheetContent` root in `TrendChart.tsx` was not touched. List view and an internal `ReadingEditView` sub-component (own `useRef`+`useEffect` for auto-focus) handle the within-sheet view switch; date/time field is read-only in the edit form per the Dev Notes scope-gap resolution.
- Fixed a regression in `TrendChart.test.tsx`: it previously rendered `ReadingHistorySheet` without any TanStack Query context (fine for the static Story 3.5 placeholder); now that the component uses real `useReadingHistory`/`usePatchReading` hooks, the test mocks both hooks at module scope so it stays isolated from network/query behavior.
- All verification gates green: `dotnet test api.Tests` — 82/82 passed; `npm run build` — 0 TypeScript errors; `npm test` — 98/98 passed; `npm run lint` — exit 0 (pre-existing unrelated warnings in `router.tsx` only).

### File List

- `api/Features/Readings/ReadingModels.cs` (MODIFY)
- `api/Features/Readings/PatchReadingValidator.cs` (NEW)
- `api/Features/Readings/GetReadingHistoryFunction.cs` (NEW)
- `api/Features/Readings/PatchReadingFunction.cs` (NEW)
- `api/Program.cs` (MODIFY)
- `api.Tests/Features/Readings/GetReadingHistoryFunctionTests.cs` (NEW)
- `api.Tests/Features/Readings/PatchReadingFunctionTests.cs` (NEW)
- `client/src/features/readings/api/readingApi.ts` (MODIFY)
- `client/src/features/readings/hooks/useReadingHistory.ts` (NEW)
- `client/src/features/readings/hooks/useReadingHistory.test.ts` (NEW)
- `client/src/features/readings/hooks/usePatchReading.ts` (NEW)
- `client/src/features/readings/hooks/usePatchReading.test.ts` (NEW)
- `client/src/features/readings/components/ReadingHistorySheet.tsx` (MODIFY)
- `client/src/features/readings/components/ReadingHistorySheet.test.tsx` (MODIFY)
- `client/src/features/dashboard/components/TrendChart.test.tsx` (MODIFY — regression fix, mocks new reading-history hooks)
- `client/src/locales/en-US/readings.json` (MODIFY)
- `client/src/locales/de-DE/readings.json` (MODIFY)
