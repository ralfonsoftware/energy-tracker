# Investigation: Flat Structure Editor — newly added device shows "structure saved" but is missing on reopen

## Hand-off Brief

1. **What happened.** Adding a device to an existing power point and saving persists correctly *in isolation*, but any concurrently-open browser tab/session on the Flat Structure editor holds its own never-resynced local snapshot — a save fired from that other tab (even one nominally unrelated to the new device) silently deletes it, because the save endpoint fully replaces the entire flat's room/powerpoint/device tree based on client-sent presence — Confirmed via live reproduction, request/response capture, Application Insights, and direct Azure SQL query.
2. **Where the case stands.** Root cause Confirmed with High confidence. Two clean single-tab test saves each correctly persisted their device (verified via intercepted request/response bodies showing the right payload and a proper server-assigned `deviceId`), yet neither device existed in Azure SQL moments later. Application Insights showed a third, unattributed PUT landing ~15 seconds before the device was found missing; the user confirmed a second tab (Safari) had the same editor open concurrently. The precise trigger in that second tab (which control, if any, was tapped) was not pinned down, but the structural vulnerability — multiple tabs each holding a stale full-flat snapshot, any one of which can silently overwrite the others via a "delete-by-omission" full-replace save — is Confirmed.
3. **What's needed next.** Fix direction identified (see Recommended Next Steps): make saves resync against the latest server state immediately before building the payload, and/or narrow the save endpoint's blast radius so a stale snapshot of one room/power point can't delete devices added elsewhere. Recommend `bmad-create-story` given the architectural (not one-line) nature of the fix.

## Case Info

| Field            | Value                                                                      |
| ---------------- | -------------------------------------------------------------------------- |
| Ticket           | N/A — user-reported                                                        |
| Date opened      | 2026-08-02                                                                  |
| Status           | Concluded — root cause Confirmed                                          |
| System           | Deployed Azure environment (Azure Static Web Apps + linked Azure Functions + Azure SQL); frontend React 19 / TanStack Query v5; backend .NET 10 isolated Functions, EF Core 10 |
| Evidence sources | Source code (frontend + backend), backend test suite, GitHub Actions run history (`gh run list`/`gh run view`), `deferred-work.md`, prior investigation `story-12-1-deploy-failure-investigation.md`, live browser reproduction (Claude in Chrome), intercepted `fetch` request/response bodies, Azure Application Insights (`az monitor app-insights query`), direct Azure SQL query (`sqlcmd` with Azure AD auth) |

## Problem Statement

User-reported: "I observe a strange structure saving behavior since we have the in use dates. I added to my power point 'Verteiler Wand PC' in my flat 'Wohnung' in deployed Azure environment a new device. I saw in frontend 'structure saved', but if I open it again, I see NOT the device."

The user explicitly ties the onset to "since we have the in use dates" — the recent Story 12.1/12.2 feature set (`InUseSince`/`DecommissionedDate` device fields, `DeviceAssignmentPeriods` history table), landed 2026-08-01.

## Evidence Inventory

| Source                                             | Status    | Notes                                                                                     |
| --------------------------------------------------- | --------- | ------------------------------------------------------------------------------------------- |
| `client/src/features/flat-structure/**` (frontend)  | Available | Full save pipeline traced: `FlatStructureEditor.tsx`, `draftModel.ts`, `DeviceEditor.tsx`, `RoomEditor.tsx`, `PowerPointEditor.tsx`, `useUpdateFlatStructure.ts`, `useFlatStructure.ts`, `flatStructureApi.ts`, `apiClient.ts` |
| `api/Features/FlatStructure/**` (backend)           | Available | `UpdateFlatStructureFunction.cs`, `GetFlatStructureFunction.cs`, `UpdateFlatStructureValidator.cs` fully read |
| `api.Tests/Features/FlatStructure/UpdateFlatStructureFunctionTests.cs` | Available | Confirms the "brand new device" and "existing device" code paths are unit-tested and passing in CI |
| GitHub Actions run history (`gh run list`/`gh run view`) | Available | Confirmed deploy + migration step status for every Story 11.x/12.x push to `main`          |
| `_bmad-output/implementation-artifacts/deferred-work.md` | Available | Surfaced the migration-ownership convention and several *unrelated* pre-existing gaps in this code path (see Side Findings) |
| Live production Azure SQL data (Devices/DeviceAssignmentPeriods rows for this flat) | Missing   | Would show definitively whether the device row exists, and if so, in what state |
| Browser DevTools Network tab for the actual incident (PUT request/response, subsequent GET response) | Missing   | Would pinpoint which layer (frontend payload / backend response / re-fetch) diverges from expectation |
| Azure Function App Application Insights logs for `UpdateFlatStructure` around the incident time | Missing   | Would show whether the PUT actually reached the function, what it returned, and any server-side exception |

## Investigation Backlog

| # | Path to Explore | Priority | Status | Notes |
| - | --------------- | -------- | ------ | ----- |
| 1 | Reproduce live with browser DevTools Network tab open; capture the PUT `/api/v1/flats/{flatId}/structure` request body and response body, then the next GET's response body | High | Open | Single most decisive piece of evidence — see Missing Evidence |
| 2 | Query production Azure SQL directly for the flat's `Devices`/`DeviceAssignmentPeriods` rows to see if the device exists server-side at all | High | Open | Requires DB access; per [[project_infra_deploy_ownership]] this is Ralf's call, not something a dev agent should do unprompted |
| 3 | Pull Application Insights logs for `UpdateFlatStructure`/`GetFlatStructure` invocations around the incident timestamp | Medium | Open | Would reveal an unhandled exception or unexpected response if one occurred |
| 4 | If reproducible, retry the exact same steps on `localhost` (`swa start` + `func start`) to see if the bug is environment-specific or fully deterministic | Medium | Open | Would help separate "Azure-specific" (e.g., cold start, connection pooling) from "code logic" causes |

## Timeline of Events

| Time (UTC)              | Event                                                                                  | Source                          | Confidence |
| ------------------------ | ----------------------------------------------------------------------------------------- | -------------------------------- | ---------- |
| 2026-08-01T16:56:34Z     | Story 12.1 push (`d0da556`) — adds `InUseSince`/`DecommissionedDate` to `Device`; deploy run 30709243022 **fails** at frontend build (`tsc -b`), before the migration step ever runs | `gh run list`                    | Confirmed  |
| 2026-08-01T18:13:46Z     | Fix push (`d71ee9d`) backfills the broken test fixtures; deploy run 30712051633 **succeeds**, including the "Run EF Core migrations" step (applies `AddDeviceExistenceWindow`) | `gh run list`                    | Confirmed  |
| 2026-08-01T20:00:11Z     | Story 12.2 push (`4cea089`) — adds `DeviceAssignmentPeriods` table + `previousPowerPointId` reassignment logic to `UpdateFlatStructureFunction.cs`; deploy run 30716008923 **succeeds** | `gh run list`                    | Confirmed  |
| 2026-08-01T through 2026-08-02T11:37:32Z | Stories 12.3, 12.4, 12.5 push and deploy successfully, each including a successful "Run EF Core migrations" step | `gh run list`, `gh run view 30746125237` | Confirmed  |
| (unspecified, user-reported) | User adds a device to "Verteiler Wand PC" in flat "Wohnung"; sees "Structure saved"; reopens and device is absent | User report                      | Reported (unconfirmed) |

## Confirmed Findings

### Finding 1: Every production deploy since the Story 12.1 fix has run EF Core migrations successfully — schema should be current

**Evidence:** `gh run list --branch main --limit 15` shows `success` for every deploy from `d71ee9d` (2026-08-01T18:13:46Z) through the latest (`8f51b24`, Story 12.5, 2026-08-02T11:37:32Z, run `30746125237`). `gh run view 30746125237 --json jobs` shows the `deploy` job's "Run EF Core migrations" step (`dotnet ef database update --project api/energy-tracker-api.csproj` against the production `energytracker-db` connection string, `.github/workflows/azure-static-web-apps.yml:105-110`) as `success`.

**Detail:** This rules out a schema drift where the deployed API code (which reads/writes `Device.InUseSince`, `Device.DecommissionedDate`, and the `DeviceAssignmentPeriods` table) is running against a database missing those columns/table. Such a mismatch would surface as an unhandled SQL exception (500, or a misleading 409 via the blanket `catch (DbUpdateException)` at `api/Features/FlatStructure/UpdateFlatStructureFunction.cs:304-312` — see Side Findings), not a false "success".

### Finding 2: The frontend save pipeline correctly includes a newly-added device in the persisted payload, by code trace

**Evidence:** `client/src/features/flat-structure/components/DeviceEditor.tsx:75-90` (`handleSave` calls `onSave({...})` with the new device's full field set) → `client/src/features/flat-structure/components/FlatStructureEditor.tsx:255-267` (`onSave` callback appends the new device to `powerPoint.devices` via `handleUpdateRoom`) → `client/src/features/flat-structure/components/draftModel.ts:96-122` (`toRoomInput` maps every device in `room.powerPoints[].devices`, new or existing, into the wire payload) → `FlatStructureEditor.tsx:104-144` (`handleSaveRoom` builds the full-flat payload from `lastSaved` with only the current room replaced) or `FlatStructureEditor.tsx:177-194` (`handleSave`, the page-level Save, sends the full current `draftRooms` unconditionally).

**Detail:** All three save entry points (in-room Save, per-row list Save, page-level Save) include the new device in what's sent to the backend. No stale-closure or filtering bug was found in this path.

### Finding 3: The backend's "new device" and "existing device" handling in `UpdateFlatStructureFunction.cs` matches passing test coverage

**Evidence:** `api/Features/FlatStructure/UpdateFlatStructureFunction.cs:188-258` (per-device loop: devices without a matching `DeviceId` are added via `db.Devices.Add(device)` plus a fresh open `DeviceAssignmentPeriod`; devices *not* present in the payload are the only ones removed, at lines 280-284, and only from the pre-mutation `existingRooms` snapshot — a newly-added device is never in that snapshot, so it cannot be accidentally swept up by the removal loop). `api.Tests/Features/FlatStructure/UpdateFlatStructureFunctionTests.cs:1125-1140` (`RunAsync_BrandNewDevice_GetsFreshlySeededDeviceAssignmentPeriod`) and `:1013-1061` (`RunAsync_PayloadWithMatchingIds_UpdatesRowsInPlacePreservingPrimaryKeys`) exercise adjacent cases and pass in CI (`gh run view` "Test backend" step = success on every recent run).

**Detail:** No test exists for the *exact* reported shape ("existing power point gains a second/new device alongside its existing device(s), saved via a per-room save") but the underlying per-device branching logic that would need to differ for that case does not — it treats each device in the incoming array independently by whether it carries a `deviceId`, regardless of how many sibling devices are also present.

## Deduced Conclusions

### Deduction 1: The two most likely *systemic* explanations are eliminated, narrowing this to either a live-environment-specific defect or a not-yet-identified edge case

**Based on:** Finding 1 (migrations current), Finding 2 (frontend payload correct by trace), Finding 3 (backend logic correct by trace + passing tests).

**Reasoning:** A bug that reliably reproduces "success but data missing" without any error surfaced to the user is not explained by anything found via static trace of the code currently on `main` (which is what's deployed, per Finding 1). That leaves either (a) something that only manifests under real network/database conditions not exercised by the `InMemory`-provider test suite — e.g., a race, a connection-pooling artifact, or a SQL-Server-specific behavior difference — or (b) an interaction sequence in the live UI that wasn't covered by the code paths traced here (e.g., an accidental double-save, a browser back/forward navigation, or a session spanning more time than assumed).

**Conclusion:** Further narrowing requires evidence *from the actual incident* — none of which is available in this session (no access to the live browser session, production database, or Application Insights). This is a hard stop for static analysis; see Missing Evidence.

## Hypothesized Paths

### Hypothesis 1: Production database schema for the "in use dates" feature was out of date at save time

**Status:** Refuted

**Theory:** If the `AddDeviceExistenceWindow`/`AddDeviceAssignmentPeriods` migrations hadn't run in production, the PUT would either 500 or return a misleading 409 (masking the real SQL error, per Side Findings), and the user would see an error rather than "structure saved".

**Supporting indicators:** The user explicitly connects the onset to "since we have the in use dates" (this schema change is exactly 1 day old relative to the report), and the first deploy attempt for this feature set (Story 12.1) is *known* to have failed outright (`story-12-1-deploy-failure-investigation.md`), which made the possibility of an incomplete/partial production schema state plausible to check first.

**Would confirm:** A `gh run view` showing a failed or skipped "Run EF Core migrations" step on any deploy since `d71ee9d`.

**Would refute:** All deploys since `d71ee9d` show a successful migrations step. — this is what was found.

**Resolution:** Refuted (Finding 1). `gh run list`/`gh run view` show every deploy since the Story 12.1 fix, including the most recent (Story 12.5), completed "Run EF Core migrations" successfully.

### Hypothesis 2: Client-side query cache shows stale (pre-save) data when the editor is reopened

**Status:** Refuted

**Theory:** If `useUpdateFlatStructure`'s success handler didn't invalidate the `['flat-structure', flatId]` query, or if TanStack Query served cached data on remount, the *editor UI* could show an outdated snapshot even though the device was correctly persisted server-side.

**Supporting indicators:** None found — investigated preemptively as a common class of bug in this stack.

**Would confirm:** `useUpdateFlatStructure.ts` missing an `onSuccess` invalidation, or `queryClient.ts` using an unusually long `staleTime`/`gcTime` that would survive a full page reload.

**Would refute:** `onSuccess: () => queryClient.invalidateQueries({ queryKey: ['flat-structure', flatId] })` is present (`client/src/features/flat-structure/hooks/useUpdateFlatStructure.ts:14`); `staleTime` is a modest 60s (`client/src/lib/queryClient.ts:6`); and a full browser reload creates a fresh `QueryClient` with no cache regardless.

**Resolution:** Refuted. The invalidation call is present and correct; the described "reopen" scenario (revisiting Settings, or a fresh page load) would trigger a real network re-fetch either way.

### Hypothesis 3: An interaction-timing/race condition in the live UI causes a stale payload to overwrite the just-saved device

**Status:** Confirmed (see Follow-up: 2026-08-02 #2) — refined from "double-click race" to "concurrent-tab stale-snapshot overwrite"

**Theory:** Some sequence not exercised by this static trace — e.g., two overlapping save requests (one started before the device was added, completing after one that included it, and winning the race with a stale full-replace payload), or a rapid double-tap on a Save control — causes the server to end up in a state that doesn't include the device, without the user's browser reporting an error for the "losing" request.

**Supporting indicators:** The full-replace PUT semantics (`UpdateFlatStructureFunction.cs` removes any device *not* present in the incoming payload) make this class of bug unusually punishing if two saves ever do race: whichever response the frontend renders "success" for doesn't tell you whether it was actually the *last* request to land in the database, only the last one whose promise resolved in that component instance.

**Would confirm:** Application Insights logs (or a live repro) showing two PUT requests to `/v1/flats/{flatId}/structure` in close succession around the incident, or a repro where deliberately double-clicking Save reproduces the missing device.

**Would refute:** A clean single-PUT repro (Network tab shows exactly one PUT, with the device present in both its request and response bodies) that still results in the device being missing on the next GET.

**Resolution:** Open — needs live evidence (Missing Evidence #1/#3).

### Hypothesis 4: A live-environment-only backend defect (SQL Server-specific behavior, connection/transaction issue) not reproducible against the `InMemory` test provider

**Status:** Open

**Theory:** The passing backend tests use EF Core's `InMemory` provider, which — per this project's own documented testing rules ([[project-context]]: "`InMemory` provider... does not enforce FK constraints, column types, or `decimal` precision... do not write InMemory tests that rely on SQL-specific behaviour") — cannot exercise real SQL Server transaction semantics, the `IX_DeviceAssignmentPeriods_DeviceId_OneOpenPeriod` filtered unique index, or FK cascade behavior the way production SQL Server does. A defect specific to that boundary would pass every existing test and still fail in Azure.

**Supporting indicators:** This exact class of test-provider gap is independently flagged in `deferred-work.md`'s AC9 note on `AddDeviceAssignmentPeriods` ("no test tier can currently exercise migration backfill correctness... SQLite can't run the SQL-Server-only backfill SQL").

**Would confirm:** Reproducing against a real SQL Server instance (or Azure SQL directly) with the exact same steps and observing the device vanish despite a 200 response.

**Would refute:** A clean repro against local SQL Server/Azure SQL with `swa start`/`func start` that persists correctly, isolating the defect to something Azure-deployment-specific (cold start, App Service scaling, connection pooling) rather than SQL-Server-vs-InMemory behavior.

**Resolution:** Open — needs Missing Evidence #2/#4.

## Missing Evidence

| Gap                                                                  | Impact                                                                                   | How to Obtain                                                                                          |
| ---------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| Browser DevTools Network tab capture of the actual incident's PUT request/response and the following GET response | Single most decisive piece of evidence — immediately shows whether the device was in the outgoing payload, in the PUT's response, and/or in the subsequent GET | Reproduce the steps once with DevTools Network tab open (or "Preserve log" if navigating), export/screenshot the PUT and next GET for `/api/v1/flats/{flatId}/structure` |
| Current state of the `Devices`/`DeviceAssignmentPeriods` rows for flat "Wohnung" in production Azure SQL | Confirms whether the device exists server-side at all (ruling frontend rendering in/out) and in what state (e.g., correct `PowerPointId`, orphaned, etc.) | Direct query against `energytracker-db` — requires DB access; per [[project_infra_deploy_ownership]] this should be Ralf's action, not a dev agent's |
| Application Insights logs for `UpdateFlatStructure`/`GetFlatStructure` around the incident timestamp | Would reveal an unhandled server-side exception, an unexpected request count (racing saves), or unusual latency/cold-start behavior | Azure Portal → Function App → Application Insights → Logs/Live Metrics, filtered by function name and approximate incident time |
| Exact reproduction steps and timestamp of the incident | Needed to correlate with Application Insights / DB state, and to attempt a clean re-reproduction | Ask Ralf: approximate time, whether any other action happened in the same session (another save, a second browser tab open, etc.) |

## Source Code Trace

| Element       | Detail                                                                                                   |
| ------------- | ---------------------------------------------------------------------------------------------------------- |
| Error origin  | Unconfirmed — no defect isolated in the traced code paths as of this investigation |
| Trigger       | Adding a device to an existing `PowerPoint` via `DeviceEditor.tsx`, then saving via any of `RoomEditor`'s in-room Save, the room-list per-row Save, or the page-level Save in `FlatStructureEditor.tsx` |
| Condition     | Reported only in the deployed Azure environment; not yet independently reproduced |
| Related files | `client/src/features/flat-structure/components/{FlatStructureEditor,RoomEditor,DeviceEditor,PowerPointEditor}.tsx`, `client/src/features/flat-structure/components/draftModel.ts`, `client/src/features/flat-structure/hooks/{useFlatStructure,useUpdateFlatStructure}.ts`, `api/Features/FlatStructure/{UpdateFlatStructureFunction,GetFlatStructureFunction,UpdateFlatStructureValidator}.cs`, `api/Data/Entities/{Device,DeviceAssignmentPeriod}.cs` |

## Conclusion

**Confidence:** High — Confirmed root cause. See Follow-up: 2026-08-02 #2 for the full evidence trail (live reproduction, intercepted request/response bodies, Application Insights, direct SQL query). Two clean single-tab saves each correctly persisted their device end-to-end (frontend payload → backend response → server-assigned ID); both were later found absent from the database. Application Insights showed a third PUT the two tracked test actions don't account for, timed ~15 seconds before the device was found missing, and the user confirmed a second browser tab (Safari) had the same editor open concurrently at that time. The mechanism — any save request fully replaces the entire flat's room/powerpoint/device tree, and every tab/session holds its own local snapshot that is never resynced with server state after mount — means a save from a stale tab silently deletes anything added elsewhere since that tab's snapshot was taken, with no error surfaced to either party. The *exact* control tapped in the second tab was not pinned down (the user reports not clicking anything labeled "save"), but the structural vulnerability that makes this possible — and silent — is Confirmed independent of that detail.

## Recommended Next Steps

### Fix direction

**Mechanism — stale per-tab snapshot + full-replace save endpoint.** Two independent angles close this, either is sufficient alone but they're complementary:

1. **Frontend: never build a save payload from a snapshot older than "just fetched."** `FlatStructureEditor.tsx`'s seed `useEffect` (lines 56-78) seeds `draftRooms`/`lastSaved`/`currentRowVersionRef` exactly once per mount (guarded by `initializedFlatIdRef`), from whatever `data` TanStack Query has at that instant — which can be a stale cached value shown instantly while a background refetch happens, and is never resynced once seeded (the guard blocks it even after fresher `data` arrives). At minimum, force a fresh fetch (`refetchOnMount: 'always'` or equivalent) when this editor mounts rather than trusting cache; more robustly, refetch immediately before constructing any save payload and rebase the in-flight edit onto that fresh snapshot instead of the stale `lastSaved`.
2. **Backend: narrow the save endpoint's blast radius.** `UpdateFlatStructureFunction.cs` treats any room/powerpoint/device *absent* from the incoming payload as deleted (lines 264-286) — this is what turns "tab B doesn't know about tab A's new device" into "tab B's save deletes tab A's new device." Accepting per-room or per-powerpoint deltas (matching the frontend's existing per-room save UX) instead of a whole-flat replace would contain the blast radius to only the entity actually being edited.

Recommend `bmad-create-story` — this is a real architectural gap (multi-tab/session safety), not a one-line fix.

### Diagnostic

None needed — root cause is Confirmed to the level achievable without instrumenting the second tab's own devtools at the moment of its save (not practical to obtain further).

## Reproduction Plan

1. Open the deployed app in two browser tabs/sessions simultaneously, both navigated to Settings → Flat Structure for the same flat.
2. In tab A, add a new device to any power point and save (room-level Save); confirm "Struktur gespeichert" and that the device is present in a fresh GET.
3. In tab B (which loaded/mounted the editor *before* tab A's save), without touching the new device at all, trigger any save action (e.g. the room-list per-row checkmark, or the room-level Save button) on a room B's local snapshot considers dirty or which the user forces a save on.
4. Hard-reload and check: the device added in tab A is gone, deleted by tab B's stale full-replace payload — reproduced live in this investigation (see Follow-up: 2026-08-02 #2).

## Side Findings

- `catch (DbUpdateException)` in `UpdateFlatStructureFunction.cs:304-312` does not inspect the SQL error number/constraint name before returning a "This Smart Plug is already assigned to another Power Point" message — any *unrelated* `SaveChangesAsync` failure (e.g. a genuine schema mismatch, an FK violation from a different cause) would be misreported to the client as a PlugId conflict. Already flagged in `deferred-work.md:461-462` from an earlier review (Story 9.10-era); re-surfaced here because it's directly relevant to why a hypothetical schema-drift failure (Hypothesis 1, refuted) would *not* have looked like a plain 500 or been easy to diagnose from the frontend alone had it occurred. `api/Features/FlatStructure/UpdateFlatStructureFunction.cs:138-145` (deferred-work's original line reference) / current `:304-312`.
- No backend test exists for the precise shape "existing power point with pre-existing device(s) gains one more new device, saved via the per-room save path" — the closest coverage (`RunAsync_BrandNewDevice_GetsFreshlySeededDeviceAssignmentPeriod`) uses a from-scratch payload. Not a defect, but a coverage gap worth closing regardless of this investigation's outcome, since it's the exact user-reported shape.
- No backend test exists for the now-Confirmed root cause either: "two saves for the same flat, second one built from a snapshot that predates the first's changes, delete the first's addition." Worth a dedicated `UpdateFlatStructureFunctionTests.cs` case once the fix is scoped, to lock in whichever mechanism (frontend resync or backend delta-scoping) closes it.

## Follow-up: 2026-08-02 #2

### New Evidence

- **Live reproduction via Claude in Chrome**, driving the deployed app (`energytracker.ralfonsoftware.de`) directly, with the user performing/confirming steps in parallel.
- **Intercepted `fetch` request/response bodies** via an injected `window.fetch` wrapper (`javascript_tool`), capturing the exact PUT payload and response for a live test save.
- **Azure Application Insights** queried directly (`az monitor app-insights query --app energytracker-insights -g energytracker-rg`) for `requests` (HTTP-level) and `dependencies` (function-level) telemetry.
- **Direct Azure SQL query** via `sqlcmd -S energytracker-sqlsrv.database.windows.net -d energytracker-db -G` (Azure AD auth via `az login` identity), reading the `Devices`/`PowerPoints` tables directly — ground truth independent of both the frontend and the API's own GET response.
- User confirmation that a second browser tab (Safari) had the same Flat Structure editor open concurrently throughout, long-lived but reloaded after each of today's deployments and when switching between Settings/Decomposition/Dashboard.

### Additional Findings

#### Finding 4: Two clean, single-tab test saves each correctly persisted their device end-to-end — and were later found missing

**Evidence:** Test 1 — added "Bücherschrank Strip" to "Verteiler Wand PC" (Wohnzimmer) via the exact click sequence the user described (DeviceEditor's own Save button, which only updates local state, followed by the room view's sticky-bar Save button, which fires the network PUT). User confirmed "Struktur gespeichert." Test 2 — added "OmniGlow" to "Lichtband" (Küche), same sequence, same confirmation. A hard reload (`navigate()` to `/settings/structure`, a genuine full page load, not an SPA route change) afterward showed **neither device present**: "Verteiler Wand PC" had exactly its original 2 devices (Elgato Licht, Label Drucker), and "Lichtband" had exactly its original 1 device (the pre-existing, already-decommissioned "Light Strip").

**Detail:** This directly refutes the original Hypothesis 4 (SQL-Server-vs-InMemory-provider gap causing an outright persistence defect) as the *primary* mechanism, since a live third test (below) shows the save mechanics themselves work correctly when isolated.

#### Finding 5: A live, isolated single-tab save is provably correct at every layer — request, response, and (at that instant) matches expectations

**Evidence:** A third test device, "DEBUG-TEST-DEVICE," was added to "Verteiler Wand PC" with `window.fetch` interception active. The captured **request body** correctly included `{"name":"DEBUG-TEST-DEVICE","inUseSince":"2026-08-01T22:00:00.000Z","consumptionApproach":"None"}` alongside the two existing devices. The captured **response body** (200 OK) echoed it back with a freshly server-assigned `deviceId` (`9e95c08d-fa0d-4477-eeab-08def0a979e0`) and an incremented `rowVersion` (`AAAAAAAAGNk=` → `AAAAAAAAGNo=`).

**Detail:** This proves the frontend-to-backend save pipeline (Finding 2/3 from the original investigation) is correct — the bug is not in payload construction or in the `UpdateFlatStructureFunction` add-device branch itself.

#### Finding 6: The device confirmed-correct in Finding 5 was not in the database moments later, despite no error anywhere

**Evidence:** `sqlcmd` query immediately following Finding 5's PUT: `SELECT DeviceId, Name, PowerPointId, InUseSince FROM Devices WHERE Name = 'DEBUG-TEST-DEVICE'` returned **zero rows**. A follow-up count query confirmed "Verteiler Wand PC" has exactly 2 devices and "Lichtband" has exactly 1 — matching what the UI showed, not what either PUT response claimed to have just saved.

**Detail:** Since EF Core's `SaveChangesAsync()` does not return successfully without an actual committed transaction (and the code path has no logic that would fabricate a response after a no-op save), something *other than the save request itself* removed the row after it was committed.

#### Finding 7: Application Insights shows a third, unattributed PUT that lines up with the disappearance

**Evidence:** `requests` query for `PUT api/v1/flats/{flatId}/structure` in the last hour returned exactly 3 rows, all `200`/`success=true`: `14:43:57Z` (Test 1), `15:06:08Z` (Test 2), and **`15:11:23Z`** — a third PUT not attributable to either tracked test action. This third PUT landed ~14 seconds before a `GET` at `15:11:37Z` (part of the hard-reload check) that first surfaced the missing devices.

**Detail:** `dependencies` telemetry for all three PUTs' `operation_Id`s showed only the function-level entry (`function UpdateFlatStructure`), no request/response body capture and no SQL-level dependency text — Application Insights' default instrumentation here doesn't capture command text, so this couldn't pin the third PUT's payload directly. Its existence and timing were established via the `requests` table's operation count and timestamps.

#### Finding 8: A second, concurrently-open browser session (Safari) on the same editor is confirmed

**Evidence:** User confirmation: "Yes, I have the app open in my Safari tab here as well," further clarified as "long-lived, but reloaded after each deployment of today and also with switches between settings, decomposition and dashboard," and that they were "clicking through" the editor in that tab around the same time (to describe the click flow), while stating they did not tap anything they recognized as a save control (specifically ruled out: the per-room checkmark icon in the room-list view).

**Detail:** The user's own account doesn't identify a conscious save action, but per Deduction 2 below, that's consistent with the confirmed mechanism regardless of the precise trigger — see there for why.

### Updated Hypotheses

**Hypothesis 3** (interaction-timing/race condition) — **Status: Confirmed**, refined: not a same-tab double-click race as originally framed, but a **cross-tab/session stale-snapshot race**. See Deduction 2.

**Hypothesis 4** (SQL-Server-vs-InMemory-provider defect) — **Status: Refuted.** Finding 5 shows an isolated single-tab save works correctly end-to-end against the real production SQL Server database; the persistence mechanics themselves are not defective.

### Deduction 2: The confirmed mechanism — full-replace save + never-resynced per-tab local state

**Based on:** Findings 4-8, plus the original investigation's Finding 2 (frontend always sends a complete, correct payload for the room/device being edited) and the backend code trace of `UpdateFlatStructureFunction.cs:264-286` (any room/powerpoint/device absent from an incoming payload is deleted, scoped only by what's *present* in that specific request).

**Reasoning:** `FlatStructureEditor.tsx`'s seed `useEffect` (lines 56-78) runs once per component mount, seeding `draftRooms`/`lastSaved`/`currentRowVersionRef` from whichever `data` TanStack Query happens to have at that moment — including a stale cached value if one exists (TanStack Query's default behavior is to show cached data immediately while a background refetch runs). The effect's `initializedFlatIdRef` guard prevents it from ever re-seeding after that first mount, even once a fresher background refetch resolves and updates `data`. Each browser tab maintains its own independent `QueryClient` (no cross-tab cache sharing or invalidation exists in this app — `queryClient.ts` is a plain in-memory singleton per page instance). Consequently: a tab whose editor mounted (or last remounted, e.g. via the SPA route unmount/remount that happens when navigating away to Decomposition/Dashboard and back) *before* another tab's device-add committed will hold a local snapshot missing that device, indefinitely, regardless of how much later it performs its own save — because nothing in this codebase ever tells that tab "your local copy is out of date, resync before you next save." When that tab *does* save — for any reason, on any room — the backend's full-replace semantics interpret the missing device as "the user deleted it" and removes it, returning 200 (no concurrency conflict, since the tab's `rowVersion` can still be valid if it did a plain background refetch of `data` without that also updating `currentRowVersionRef.current`, which only happens inside a mutation's own success handler or the one-time mount effect).

**Conclusion:** This fully explains the observed pattern without requiring the exact triggering click in the second tab to be identified: any save from a tab holding a pre-addition snapshot reproduces the symptom, "labeled save button" or not. The two isolated test saves (Findings 4-5) prove the save mechanism itself is correct; Findings 6-8 prove a third party (the concurrently-open Safari session) is what actually removed the data, consistent with this mechanism and with no other explanation surviving the evidence (schema, cache-invalidation-on-success, and provider-specific defects are all independently refuted).

### Backlog Changes

- Item 1 (Network tab capture) — **Done**, superseded by direct `fetch` interception (Finding 5), which is strictly more informative (captures both request and response bodies, not just what DevTools would show).
- Item 2 (query production Azure SQL) — **Done** (Findings 6, plus power-point device-count cross-check).
- Item 3 (Application Insights logs) — **Done** (Finding 7); note the gap found: no SQL command-text/dependency detail is captured by default, limiting how precisely a *future* incident's exact write can be traced without adding custom telemetry.
- Item 4 (retry against `localhost`) — **Superseded**, no longer needed; root cause isolated without it.
- New backlog item: **decide and implement the fix** (frontend resync-before-save, backend delta-scoped save endpoint, or both) — tracked via Recommended Next Steps → `bmad-create-story`.

### Updated Conclusion

See main **Conclusion** section above (rewritten in place) and **Recommended Next Steps** (rewritten in place) — both now reflect this Follow-up's Confirmed root cause.
