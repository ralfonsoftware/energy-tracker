# Epic 13: Flat Structure Save Integrity — Device- and Room-Scoped Saves

Closes a Confirmed production data-loss bug: `UpdateFlatStructureFunction.cs`'s `PUT /v1/flats/{flatId}/structure` fully replaces the entire flat's room/power-point/device tree on every save — anything absent from the incoming payload is deleted server-side. Combined with `FlatStructureEditor.tsx` seeding its local edit snapshot once per mount and never resyncing it against fresher server state, any save fired from a browser tab/session holding an older snapshot silently deletes devices (or room/power-point edits) added elsewhere since that snapshot was taken — with no error surfaced to either party. Sourced from `_bmad-output/implementation-artifacts/investigations/structure-editor-device-not-persisted-investigation.md` (2026-08-02, root cause Confirmed with High confidence via live reproduction, intercepted request/response capture, Azure Application Insights, and a direct Azure SQL query proving two separately-verified-correct saves were each later reverted by a third, unrelated save from a concurrently-open second browser tab).

Ralf additionally flagged, independent of the investigation, that `DeviceEditor.tsx`'s own Save button does not match user expectation: clicking it today only updates local React state and returns to the room view — it never calls the network, and gives no success/failure feedback of its own. A user who saves a device and sees no error reasonably believes it persisted; today that belief is only correct if a *separate*, easy-to-miss room-level or page-level Save is also clicked afterward, and the full-flat replace semantics that resulted are exactly what makes that a live data-loss risk rather than a harmless UX quirk.

**Story 13.1** closes the reported bug directly: devices get their own create/update/delete endpoints, are removed entirely from the whole-flat save payload's write semantics, and `DeviceEditor`'s Save button calls the network directly with its own success/failure feedback — so a device save is atomic, immediate, and can never be silently reverted by an unrelated save elsewhere.

**Story 13.2** closes the same class of risk one level up: room name and power-point name/`plugId` edits currently ride the same whole-flat replace payload (even via the existing per-room Save UI, which *looks* room-scoped but isn't, underneath). A dedicated per-room endpoint removes the remaining whole-flat blast radius for structural edits.

**FRs covered:** None — this is entirely engineering-hardening/bugfix work sourced from a production investigation, consistent with the precedent set by Story 6.0, Epic 9 Part 2, and Epic 11's Stories 11.13/11.14.
**UX items:** None new — `DeviceEditor.tsx`'s save-result feedback follows this project's established mutation-error-banner convention (`mutation.error.detail` displayed near the Save button, distinct from field-level validation errors), already used throughout the app (e.g. `RoomEditor.tsx`'s existing `saveError`/`saveSuccess` messages in its `StickyActionBar`).

## Story 13.1: Device-Scoped Save API & Immediate Save Feedback

As a user editing my flat's devices,
I want clicking Save on the device edit page to immediately persist that one device and tell me whether it worked,
So that a device I save is never silently lost because of an unrelated save happening elsewhere, and I always know for certain whether my change is safe.

**Note (2026-08-02, sourced from `structure-editor-device-not-persisted-investigation.md`):** Confirmed root cause — `UpdateFlatStructureFunction.cs:264-286` deletes any device absent from the incoming payload, and `FlatStructureEditor.tsx`'s seed `useEffect` (lines 56-78) never resyncs its local `draftRooms`/`lastSaved` snapshot after initial mount, even once a fresher background refetch arrives (each browser tab/session has its own independent, uninvalidated `QueryClient`). A save fired from a stale tab — for *any* reason, not necessarily touching the affected device — silently deletes devices added elsewhere since that tab's snapshot was taken. Live-reproduced twice in the investigation with two different devices, each independently confirmed correct end-to-end (frontend payload → 200 response with server-assigned `deviceId` → gone from Azure SQL minutes later).

**Acceptance Criteria:**

**Given** devices are currently created/updated only as part of `UpdateFlatStructureFunction.cs`'s whole-flat replace payload, with no standalone endpoint,
**When** implemented,
**Then** new dedicated Functions exist following this codebase's established per-verb Function pattern (mirroring `Features/Tariffs/CreateTariffFunction.cs`'s shape: tenant ownership check via `db.Flats`/`db.PowerPoints` lookup, Problem Details errors, `CancellationToken` threaded through): `POST /v1/flats/{flatId}/powerpoints/{powerPointId}/devices` (create — returns `201` with the new `DeviceResponse`, including a real `DeviceId` and a freshly-seeded open `DeviceAssignmentPeriod` exactly as `UpdateFlatStructureFunction.cs`'s existing new-device branch does today at lines 227-257), `PUT /v1/flats/{flatId}/powerpoints/{powerPointId}/devices/{deviceId}` (update all editable device fields — name, type, manufacturer, model, purchaseDate, inUseSince, decommissionedDate, consumptionApproach and its dependent fields — returning `200` with the updated `DeviceResponse`; if `powerPointId` differs from the device's current one, closes the current open `DeviceAssignmentPeriod` and opens a new one, exactly as the existing reassignment branch does today at lines 209-222), and `DELETE /v1/flats/{flatId}/powerpoints/{powerPointId}/devices/{deviceId}` (deletes the device; EF cascade delete already handles its `DeviceAssignmentPeriod` rows per `DeviceConfiguration.cs`'s existing `OnDelete(DeleteBehavior.Cascade)`).

**Given** these three endpoints each use EF Core's `RowVersion` concurrency token already present on `Device` (`DeviceConfiguration.cs:27`, currently unused for concurrency checks anywhere in the codebase),
**When** implemented,
**Then** the update and delete endpoints require the client to send back the device's current `rowVersion` (matching this codebase's established `ApplyRowVersionCheck` pattern from `UpdateFlatStructureFunction.cs`/`Shared/`) and return `409 Conflict` Problem Details on a mismatch — this is the endpoint-level backstop that makes a genuinely concurrent edit to the *same* device (not the whole-flat blast-radius case this story otherwise eliminates) fail loudly instead of silently.

**Given** `UpdateFlatStructureFunction.cs` currently treats a device absent from its payload as deleted, and a device present-with-no-`deviceId` as new,
**When** implemented,
**Then** device create/update/delete is removed entirely from `UpdateFlatStructureFunction.cs`'s write semantics — the `Devices` array is no longer read from the request body at all (any devices arriving in a room/power-point's payload are ignored for writes; `GetFlatStructureFunction.cs`'s response is unaffected and continues returning devices nested under their power point exactly as today, since reads carry no data-loss risk) — such that no whole-flat save can ever create, modify, or delete a device again, regardless of what any tab's stale snapshot contains.

**Given** `DeviceEditor.tsx`'s Save button currently only calls its `onSave` prop (pure local state update via `FlatStructureEditor.tsx`'s `handleUpdateRoom`, no network call, per the investigation's Finding 2),
**When** implemented,
**Then** a new `useSaveDevice` hook (`client/src/features/flat-structure/hooks/useSaveDevice.ts`, one hook per mutation per this project's established convention) wraps the three new endpoints behind a single `mutate` call that picks POST vs. PUT based on whether the device being edited has an existing `deviceId`, and `DeviceEditor.tsx`'s Save button calls this hook directly — the device is persisted (or the save fails) the instant this button is clicked, no longer deferred to a later room/page-level save.

**Given** this project's established mutation-feedback convention (`mutation.error.detail` displayed as a banner near the Save button, distinct from field-level `form.formState.errors`; success acknowledged inline, matching `RoomEditor.tsx`'s existing `saveError`/`saveSuccess` treatment in its `StickyActionBar`),
**When** implemented,
**Then** `DeviceEditor.tsx` shows an explicit inline success message on `onSuccess` and an explicit inline error banner (using `mutation.error.detail`) on `onError` near its own Save button, staying open (not returning to the room view) on failure so the user can retry without re-entering data — matching this codebase's established "mutation errors: sheet/form stays open" convention.

**Given** the removal of devices from `UpdateFlatStructureFunction.cs`'s write path,
**When** implemented,
**Then** `FlatStructureEditor.tsx`'s local state management for devices is updated to match: `handleUpdateRoom`'s device-array mutation callers (the `DeviceEditor` `onSave` wiring) are replaced by direct cache updates from the new `useSaveDevice` mutation's response (e.g. via `queryClient.setQueryData` or `invalidateQueries` scoped to `['flat-structure', flatId]`, per this codebase's established TanStack Query convention), and `PowerPointEditor.tsx`'s existing device-delete confirm flow is wired to call the same hook's delete path immediately (also no longer deferred to a later structural save).

**Given** the new endpoints and the removal of devices from the old endpoint's write path,
**When** tested,
**Then** new `api.Tests/Features/FlatStructure/` test files cover each new Function's `RunAsync` directly (success, tenant-check 403, validation 400, `RowVersion` conflict 409, and — for create — the fresh `DeviceAssignmentPeriod` seeding; for update-with-reassignment — the period-close/period-open pair, mirroring the existing coverage pattern in `UpdateFlatStructureFunctionTests.cs`), `UpdateFlatStructureFunctionTests.cs`'s existing device-related test cases are updated or removed to reflect that this endpoint no longer processes `Devices` (room/power-point-only cases continue to pass unmodified), and a new frontend test in `DeviceEditor.test.tsx` (or `useSaveDevice.test.ts`) asserts the Save button triggers the network call and both the success and error feedback paths render.

## Story 13.2: Room- and Power-Point-Scoped Saves

As a user editing my flat's rooms and power points,
I want saving a room to only ever touch that one room,
So that renaming or reconfiguring a power point in one room can never be silently lost because of an unrelated edit to a different room elsewhere.

**Note (2026-08-02, follow-on from Story 13.1):** Story 13.1 removes devices from `UpdateFlatStructureFunction.cs`'s write semantics, which closes the highest-frequency, highest-severity instance of the confirmed whole-flat-replace risk. The same underlying mechanism still applies one level up: `FlatStructureEditor.tsx`'s existing per-room Save UI (`handleSaveRoom`, both the in-room sticky-bar button and the room-list's per-row checkmark) *looks* scoped to one room, but its payload (`toWireRequest`) still carries every other room's data from the same never-resynced `lastSaved` snapshot — so a stale tab's "just this room" save can still silently revert a different room's rename or power-point edit made elsewhere in the meantime.

**Acceptance Criteria:**

**Given** room/power-point creation, rename, and deletion currently flow only through `UpdateFlatStructureFunction.cs`'s whole-flat replace payload,
**When** implemented,
**Then** new dedicated Functions exist, following the same per-verb pattern as Story 13.1's device endpoints and this codebase's existing `Features/Tariffs/` convention: `POST /v1/flats/{flatId}/rooms` (create room), `PUT /v1/flats/{flatId}/rooms/{roomId}` (update room name and its power points' name/`plugId` — not devices, which Story 13.1 already scoped out entirely), `DELETE /v1/flats/{flatId}/rooms/{roomId}` (delete room, cascading power points and devices exactly as today), each requiring the room's current `rowVersion` and returning `409` on a concurrency mismatch, matching Story 13.1's concurrency-check pattern.

**Given** `FlatStructureEditor.tsx`'s existing `handleSaveRoom`/`handleSave`/`handleDeleteRoom` currently build a whole-flat payload via `toWireRequest`/`toUpdateRequest` (`draftModel.ts`),
**When** implemented,
**Then** these are rewired to call the new per-room endpoints directly, sending only the one room being saved/deleted — `lastSaved`'s role as a whole-flat snapshot used to backfill *other* rooms' payload data is eliminated (each save now only needs its own room's current draft state, nothing else), removing the mechanism that let one room's save affect another's.

**Given** this changes what `UpdateFlatStructureFunction.cs`'s `PUT /v1/flats/{flatId}/structure` is responsible for,
**When** implemented,
**Then** its scope is reduced to bulk/whole-flat operations only where genuinely needed (if any remain — e.g. the initial default-template seeding flow in `FlatStructureEditor.tsx`'s `createDefaultDraftRooms`); if no caller still needs a genuine whole-flat write after Stories 13.1 and 13.2, this endpoint is removed entirely rather than left as unused dead code, per this project's "delete rather than leave a shim" convention (matches the precedent set by the Epic 9 retrospective's `TariffResolver` deletion).

**Given** the new endpoints,
**When** tested,
**Then** new `api.Tests/Features/FlatStructure/` test files cover each new Function's `RunAsync` directly (success, tenant-check, validation, `PlugId` uniqueness conflict per Story 11.3's existing constraint, `RowVersion` conflict), and `FlatStructureEditor.test.tsx`'s existing per-room save/dirty-state coverage (including Story 11.8's room-list save-state-consistency tests) is updated to reflect the new per-room payload shape while preserving its existing assertions about which rooms show saving/disabled state.

## Story 13.3: Device–Flat Association Validation & Cleanup Scripts

As the team maintaining this app,
I want a repeatable way to find and remove any device that no longer has a valid chain up to a real flat,
So that a future bug, incomplete migration, or manual DB intervention can't leave silently-orphaned device data behind unnoticed.

**Note (2026-08-02, added following Story 13.1's scoping):** Not triggered by a known active incident — a live check against production during this epic's scoping (`Device.PowerPointId`, `PowerPoint.RoomId`, `Room.FlatId` all `IsRequired()` with `OnDelete(DeleteBehavior.Cascade)` per `DeviceConfiguration.cs`/`PowerPointConfiguration.cs`/`RoomConfiguration.cs`) found **zero** structural orphans at any level (Device→PowerPoint, PowerPoint→Room, Room→Flat), zero `DeviceAssignmentPeriod` rows referencing a non-existent device, zero devices with no assignment period at all, and zero `DeviceAssignmentPeriod.FlatId` values drifted from the device's actual current Room→Flat chain. This story exists as defense-in-depth: FK cascade constraints make these states *unreachable* through normal EF Core-mediated writes today, but provide no safety net against a raw SQL admin action, a future migration that loosens a constraint, or a bug in Story 13.1's new device endpoints. Scripts are deliverables to be run **manually, on demand** — not wired into CI/CD or any automated deploy step, per this project's established convention that dev agents/automation never execute destructive operations against live infra.

**Acceptance Criteria:**

**Given** no existing script or tooling validates device–flat referential integrity in production,
**When** implemented,
**Then** a new validation script (`scripts/db/validate-device-flat-associations.sql`, plain T-SQL runnable via `sqlcmd` against the production connection exactly as demonstrated during this epic's investigation) reports, without modifying any data: devices whose `PowerPointId` has no matching `PowerPoint` row, power points whose `RoomId` has no matching `Room` row, rooms whose `FlatId` has no matching `Flat` row (each surfaced per-device via a chain of `LEFT JOIN`s from `Devices` outward so every anomaly is reported in terms of the affected `DeviceId`), `DeviceAssignmentPeriod` rows referencing a non-existent `DeviceId`, devices with zero `DeviceAssignmentPeriod` rows, devices with more than one *open* (`To IS NULL`) period (a sanity check the DB's own filtered unique index should already prevent, included as a defense-in-depth cross-check), and `DeviceAssignmentPeriod` rows whose `FlatId` no longer matches the device's current Room→Flat chain (denormalized-field drift).

**Given** the validation script's findings,
**When** a device is confirmed to have no valid chain up to a real `Flat` (any of: missing `PowerPoint`, missing `Room`, or missing `Flat` in its chain),
**Then** a separate cleanup script (`scripts/db/cleanup-orphaned-devices.sql`) exists that deletes exactly those devices (and, for each, its own `DeviceAssignmentPeriod` rows first, to avoid relying on cascade-delete timing in a raw-SQL context) — scoped by an explicit `DeviceId` list the operator pastes in from the validation script's output (no implicit "delete everything the validation script found" auto-wiring between the two scripts), and the script is a plain `.sql` file with no wrapper automation — it is never invoked by CI/CD, a Function, a migration, or any other automated path; Ralf runs it manually via `sqlcmd`/Azure Portal Query Editor after reviewing the validation output, consistent with how this project already treats all direct production-database actions.

**Given** the current production baseline is confirmed clean (zero anomalies of every kind above, verified 2026-08-02 during this epic's scoping),
**When** delivered,
**Then** the validation script is run once against production as part of this story's own verification, its output (all-clear) is recorded in the story's Dev Agent Record / Completion Notes, and no cleanup script execution is required or attempted as part of this story — the cleanup script is delivered ready-to-use for whenever it's next needed, not run reactively now.

**Given** these are standalone SQL scripts, not EF Core migrations,
**When** implemented,
**Then** neither script is added to `api/Data/Migrations/` or referenced anywhere the `dotnet ef database update` deploy step (`.github/workflows/azure-static-web-apps.yml:105-110`) would pick it up — they live only in the new `scripts/db/` folder, and a short `scripts/db/README.md` documents what each script does, how to run it (`sqlcmd -S energytracker-sqlsrv.database.windows.net -d energytracker-db -G -i <file>.sql`, the exact invocation proven to work against this project's Azure SQL instance during this epic's investigation), and the manual-review-before-cleanup expectation.
