# Investigation: Insights tab shows every finding twice

## Hand-off Brief

1. **What happened.** `GetInsightsFunction` returns every `Insight` row ever written for a flat, with no filter to the most recent `InsightRun` and no cleanup of a prior completed run's rows — so once a second `InsightRun` completes for the same flat (here, the nightly 02:00 UTC `ScheduledInsightsFunction`, the day after the user's manual trigger), the tab renders both runs' near-identical output side by side (Confirmed via code trace, api/Features/Insights/GetInsightsFunction.cs:49-53).
2. **Where the case stands.** Root cause fully traced through the code with no remaining ambiguity; only production DB timestamps for this specific flat's `Insight.CreatedAt`/`RunId` values would upgrade the timeline from Deduced to Confirmed-in-production, but the mechanism doesn't depend on timing or a race, so this isn't required to act.
3. **What's needed next.** Fix direction is a scope/plan decision (query-side de-dup vs. run-cleanup vs. both) — route to `bmad-correct-course` or directly to `bmad-create-story` for a new story.

## Case Info

| Field            | Value                                                                                     |
| ---------------- | ------------------------------------------------------------------------------------------- |
| Ticket           | N/A                                                                                          |
| Date opened      | 2026-07-27                                                                                   |
| Status           | Concluded                                                                                    |
| System           | Production — energytracker.ralfonsoftware.de (Azure Static Web Apps + Functions isolated v4) |
| Evidence sources | Screenshot (production UI), source code (`api/Features/Insights/*`, `client/src/features/insights/*`), git history |

## Problem Statement

User manually triggered Insights discovery yesterday (2026-07-26) and the tab showed correct, single output. Opening the Insights tab today (2026-07-27), every card is shown twice with identical values (screenshot: "Hochgerechneter Jahresverbauch" and "Geschirrspüler" each appear as two visually identical cards).

## Evidence Inventory

| Source                          | Status    | Notes                                                                 |
| -------------------------------- | --------- | ---------------------------------------------------------------------- |
| Screenshot (production UI)       | Available | Shows exactly 2x duplication of 2 distinct card types, same values     |
| `GetInsightsFunction.cs`         | Available | Query is unscoped by `RunId` — stronghold                              |
| `ProcessInsightsFunction.cs`     | Available | Stale-cleanup only scoped to the *same* `RunId`, not prior runs        |
| `ScheduledInsightsFunction.cs`   | Available | Nightly `TimerTrigger("0 0 2 * * *")`, creates a new run unconditionally for every flat |
| `TriggerInsightsFunction.cs`     | Available | Manual trigger path, confirms no cap on completed runs per flat        |
| `InsightModels.cs`               | Available | `InsightDto` has no `RunId` field — client structurally cannot dedupe  |
| `InsightsTab.tsx`                | Available | Renders `insightsData.insights` 1:1, keyed by `insightId`, no filtering |
| Production DB (`Insight`/`InsightRun` rows for this flat) | Missing | Would give exact `RunId`/`CreatedAt` timestamps to confirm the timeline directly; no DB access from this environment |
| Application/Function logs        | Missing | Would show the `ScheduledInsights enqueued {Count}...` log line and the `ProcessInsights` invocation for this flat around 02:00 UTC today |

## Investigation Backlog

| # | Path to Explore | Priority | Status | Notes |
| - | --------------- | -------- | ------ | ----- |
| 1 | Query production `InsightRun`/`Insight` tables for this flat to confirm 2 distinct `RunId`s ~24h apart | Low | Open | Would upgrade Deduced timeline to Confirmed; not required to act since the mechanism is deterministic, not timing-dependent |
| 2 | Check Application Insights / Function logs for `ScheduledInsights enqueued` around 02:00 UTC 2026-07-27 | Low | Open | Same purpose as #1 |

## Timeline of Events

| Time                          | Event                                                                                   | Source                                     | Confidence |
| ------------------------------ | ---------------------------------------------------------------------------------------- | ------------------------------------------- | ---------- |
| 2026-07-26 (daytime, user-reported) | User manually triggers insights via `TriggerInsightsFunction` — creates `InsightRun` A, `ProcessInsightsFunction` completes it, writes N `Insight` rows, tab shows correct single set | User report + `TriggerInsightsFunction.cs:46-56` | Deduced |
| 2026-07-27 02:00 UTC           | `ScheduledInsightsFunction`'s nightly timer fires for **every** `Flat` unconditionally, creates `InsightRun` B for this flat, enqueues a discovery message | `ScheduledInsightsFunction.cs:15-36` (`TimerTrigger("0 0 2 * * *")`, no active-run check, no opt-out) | Deduced |
| 2026-07-27 ~02:00 UTC          | `ProcessInsightsFunction` processes B's message; stale-cleanup only deletes `Insight` rows where `RunId == B` (none exist yet) — A's rows are untouched; detectors run against near-identical underlying meter data and write a fresh, near-identical set of `Insight` rows under `RunId` B | `ProcessInsightsFunction.cs:78-83` (`db.Insights.Where(i => i.RunId == discoveryMessage.RunId)`) | Deduced |
| 2026-07-27 (user opens tab)    | `GetInsightsFunction` queries `db.Insights.Where(i => i.FlatId == flatGuid)` — **all Insight rows ever written for this flat**, no `RunId` scoping, no de-dup — returns both A's and B's rows | `GetInsightsFunction.cs:49-53` | Confirmed |
| same                           | `InsightsTab.tsx` renders `insights.map(...)` 1:1 keyed by `insightId` — no client-side filtering possible since `InsightDto` doesn't even carry `RunId` | `InsightsTab.tsx:94-99`, `InsightModels.cs:14` | Confirmed |

## Confirmed Findings

### Finding 1: `GetInsightsFunction` returns all-time insights for the flat, unscoped by run

**Evidence:** `api/Features/Insights/GetInsightsFunction.cs:49-53`

```csharp
var insights = await db.Insights.AsNoTracking()
    .Where(i => i.FlatId == flatGuid)
    .OrderByDescending(i => i.CreatedAt)
    .Select(i => new { i.InsightId, i.Type, i.DeviceId, i.Data, i.CreatedAt })
    .ToListAsync(ct);
```

No filter to `mostRecentRun.RunId` (which is already fetched two lines above for the `runStatus` DTO but never reused here), and no cap/limit. Every `Insight` row ever persisted for this flat is returned every time.

### Finding 2: The only `Insight` cleanup that exists is scoped to the *same* `RunId`, never a prior run

**Evidence:** `api/Features/Insights/ProcessInsightsFunction.cs:78-83`

```csharp
var staleInsights = await db.Insights.Where(i => i.RunId == discoveryMessage.RunId).ToListAsync(ct);
if (staleInsights.Count > 0)
{
    db.Insights.RemoveRange(staleInsights);
    await db.SaveChangesAsync(ct);
}
```

This guards against redelivery of the *same* message (a prior attempt for the *same* `RunId` was killed mid-run), per Story 10.2/11.2's explicit intent. It was never meant to, and does not, remove a different, already-completed `RunId`'s rows. A repo-wide search confirms `db.Insights.RemoveRange`/`.Remove(` appears nowhere else in `api/Features/Insights/`.

### Finding 3: A new `InsightRun` is created nightly for every flat, unconditionally, with no relation to prior completed runs

**Evidence:** `api/Features/Insights/ScheduledInsightsFunction.cs:14-36`

```csharp
[TimerTrigger("0 0 2 * * *")] TimerInfo timer, ...
var flatIds = await db.Flats.Select(f => f.FlatId).ToListAsync(ct);
...
foreach (var flatId in flatIds)
{
    var run = new InsightRun { FlatId = flatId, Status = InsightRunStatus.Pending, StartedAt = DateTimeOffset.UtcNow };
    db.InsightRuns.Add(run);
    await db.SaveChangesAsync(ct);
    ... enqueue discovery message ...
}
```

Runs every night at 02:00 UTC for every `Flat` row, with no check for whether a run already completed recently (only `TriggerInsightsFunction`'s `IX_InsightRuns_FlatId_ActiveOnly` filtered index prevents a *second concurrent Pending/Processing* run — it does not, and structurally cannot, prevent this new run from being a fourth, fifth, Nth *sequential completed* run).

### Finding 4: The client has no way to filter by run even in principle

**Evidence:** `api/Features/Insights/InsightModels.cs:14`, `client/src/features/insights/components/InsightsTab.tsx:94-99`

`InsightDto` = `record InsightDto(Guid InsightId, InsightType Type, Guid? DeviceId, JsonElement Data, DateTimeOffset CreatedAt)` — no `RunId` field. `InsightsTab.tsx` renders `insights.map(insight => <InsightCard key={insight.insightId} .../>)` directly against whatever the API returns, with no grouping or filtering logic. The duplication is not a rendering bug; the API is handing the client two full sets of cards to render.

## Deduced Conclusions

### Deduction 1: Every additional completed `InsightRun` for a flat permanently adds another full set of visible cards

**Based on:** Findings 1, 2, 3

**Reasoning:** Finding 3 shows a new `InsightRun` is created for every flat every night unconditionally (plus on any manual trigger). Finding 2 shows nothing ever deletes a *different* run's `Insight` rows. Finding 1 shows the read path returns the unfiltered union of all of them. There is no step anywhere in the pipeline that caps or ages out old runs' output.

**Conclusion:** This is not a one-off race or a rare timing coincidence — it is a deterministic, structural gap that will reproduce for **every** flat, **every night**, once a second run completes, growing without bound (3 cards the day after that, 4 the day after, etc., until detector output naturally changes and the sets stop being visually identical — but the row count keeps growing regardless of whether they look different).

### Deduction 2: This is unrelated to the Story 11.2 concurrency-race fix

**Based on:** Findings 1-3, and story 11.2's diff scope (`_bmad-output/implementation-artifacts/11-2-insights-discovery-redelivery-db-level-idempotency-guard.md`)

**Reasoning:** Story 11.2 (already merged as commit `4ac3900`, pushed to `origin/main`) added a `RowVersion`-based exclusive claim to `ProcessInsightsFunction` so that two *concurrent* invocations for the *same* `RunId` can't both write detector output. That fix is scoped entirely to same-`RunId` races. It does not touch `GetInsightsFunction.cs` (confirmed unmodified in the 11.2 diff and untouched per that story's explicit "What NOT to touch" list) and does not add any cross-run cleanup. The screenshot's duplication is explained fully by two **sequential, non-racing, both-successful** runs (yesterday's manual trigger + last night's scheduled trigger), each correctly writing its own rows — the bug is that both sets are kept and both are shown, not that either run corrupted the other.

**Conclusion:** Deploying/verifying Story 11.2 does not and will not fix what the user is seeing. A separate fix is needed.

## Hypothesized Paths

### Hypothesis 1 (user's implicit premise): This is the same concurrency-race bug just addressed in Story 11.2

**Status:** Refuted

**Theory:** Two overlapping invocations of `ProcessInsightsFunction` for the same `RunId` both wrote detector output, producing duplicates.

**Supporting indicators:** Timing coincidence with the just-completed Story 11.2 code review session; superficially similar symptom (duplicate `Insight` rows).

**Would confirm:** Two `Insight` rows sharing the *same* `RunId` with near-simultaneous `CreatedAt` timestamps.

**Would refute:** Two `Insight` rows with *different* `RunId`s and `CreatedAt` timestamps ~24h apart (matching a manual trigger + the next night's 02:00 UTC scheduled trigger) — which is what Findings 1-3 deduce must be the case, since `GetInsightsFunction` is provably unscoped by run regardless of whether any race occurred.

**Resolution:** Refuted by code trace — the symptom is fully and deterministically explained by Findings 1-3 without requiring any race condition. `ScheduledInsightsFunction`'s unconditional nightly trigger (Finding 3) is sufficient on its own, even with Story 11.2's fix perfectly in place.

## Missing Evidence

| Gap                                                        | Impact                                                                 | How to Obtain                                                                 |
| ------------------------------------------------------------ | ------------------------------------------------------------------------- | -------------------------------------------------------------------------------- |
| Production `Insight`/`InsightRun` rows for this flat          | Would upgrade the Timeline from Deduced to Confirmed (exact `RunId`s, timestamps) | Query production DB directly, or add a temporary diagnostic log/endpoint          |
| Application Insights / Function execution logs around 02:00 UTC 2026-07-27 | Would directly show `ScheduledInsights enqueued {Count}` firing for this flat | Azure Portal → Function App → this deployment's Application Insights / Log Stream |

Neither gap blocks a fix decision — the causal chain is deterministic (Deduction 1), not probabilistic, so it doesn't need a production data pull to act on.

## Source Code Trace

| Element       | Detail                                                                                                    |
| ------------- | ------------------------------------------------------------------------------------------------------------ |
| Error origin  | `api/Features/Insights/GetInsightsFunction.cs:49-53` — unscoped `db.Insights.Where(i => i.FlatId == flatGuid)` |
| Trigger       | Any second (or later) `InsightRun` completing for the same flat — nightly via `ScheduledInsightsFunction` (guaranteed, every flat, every night) or a second manual trigger |
| Condition     | At least 2 completed `InsightRun`s exist for the flat with each having written at least 1 `Insight` row that wasn't cleaned up (cleanup never happens across runs) |
| Related files | `api/Features/Insights/ProcessInsightsFunction.cs` (writes, only same-run cleanup), `api/Features/Insights/ScheduledInsightsFunction.cs` (nightly run creation), `api/Features/Insights/InsightModels.cs` (`InsightDto` lacks `RunId`), `client/src/features/insights/components/InsightsTab.tsx` (unfiltered render) |

## Conclusion

**Confidence:** High

Root cause is Confirmed at the code level and is architectural, not timing-dependent: `GetInsightsFunction` returns every `Insight` row ever written for a flat (Finding 1), nothing ever removes a prior completed run's rows (Finding 2), and a new run is created for every flat every night regardless of whether a run already completed recently (Finding 3). The observed 2x duplication is the minimum-case manifestation of a bug that will keep compounding nightly for this and every other flat. This is unrelated to Story 11.2's redelivery-race fix (Deduction 2, Hypothesis 1 Refuted) — that fix is already merged and pushed but does not touch any of the three root-cause locations.

## Recommended Next Steps

### Fix direction

Two mechanisms, either sufficient alone but likely both wanted for defense in depth:

1. **Read-side scope fix (`GetInsightsFunction.cs`):** Filter `db.Insights` to `i.RunId == mostRecentRun.RunId` (the `mostRecentRun` is already fetched) instead of returning all-time rows for the flat, restricting an `AsNoTracking` query rather than requiring any write-path change. Fast, low-risk, fixes the symptom immediately regardless of how many stale rows already exist in production.
2. **Write-side cleanup fix (`ProcessInsightsFunction.cs` and/or `ScheduledInsightsFunction.cs`):** When a new run starts (or completes) for a flat, delete/archive the flat's prior completed run's `Insight` rows — stops unbounded row growth, which the read-side fix alone does not address (the table still grows forever; only display is capped).

A decision is needed on whether historical insights should ever be retrievable (e.g., a future "history" view) — if yes, mechanism 1 alone plus a background retention job is the shape; if no, mechanism 2 (hard delete on new-run start) is simpler and sufficient. This is exactly the kind of scope question `bmad-correct-course` or a fresh story's Dev Notes should settle explicitly, since two "What NOT to touch" style decisions are involved (retention policy, and whether cross-run cleanup happens on the write path or a separate job).

### Diagnostic

Not required to proceed — see Missing Evidence table for the optional production-data confirmation.

## Reproduction Plan

1. Seed a `Flat` with a completed `InsightRun` A and its `Insight` rows (mirrors `SeedFlatAndRunAsync()` pattern in `ProcessInsightsFunctionTests.cs`).
2. Seed a second completed `InsightRun` B for the same flat with its own `Insight` rows (simulating either a second manual trigger or a `ScheduledInsightsFunction` night-cycle run).
3. Call `GetInsightsFunction.RunAsync` for the flat.
4. Expected (bug): `InsightsResponse.Insights` contains the union of both runs' rows.
5. Expected (fixed): `InsightsResponse.Insights` contains only run B's (most recent) rows.

This is directly portable into a new xUnit test in `api.Tests/Features/Insights/GetInsightsFunctionTests.cs` (a test file for this Function does not currently appear to exist — worth checking as part of the fix story).

## Side Findings

- `ScheduledInsightsFunction.cs:19-20`'s comment ("No `IsActive` flag exists on User/Flat — every Flat row belongs to a user who completed onboarding, so 'active users' means all Flat rows, full stop") confirms there is no per-flat opt-out or throttle for the nightly run — every flat gets a fresh run every single night indefinitely, which is what guarantees this bug surfaces for every user within 24-48h of first use, not just occasionally.
- `api.Tests/Features/Insights/GetInsightsFunctionTests.cs:75-91` (`RunAsync_MultipleInsights_ReturnsSortedByCreatedAtDescending`) already exists and explicitly seeds three `Insight` rows for a flat and asserts **all three** are returned, sorted by `CreatedAt` — i.e. the current all-time, unscoped-by-run behavior is locked in as the *intended* contract by an existing passing test, not an accidental gap. Any read-side fix (Recommended Next Steps, mechanism 1) must update or replace this test, since "return everything for the flat" and "return only the latest run's rows" are mutually exclusive contracts.

## Follow-up: 2026-07-27
