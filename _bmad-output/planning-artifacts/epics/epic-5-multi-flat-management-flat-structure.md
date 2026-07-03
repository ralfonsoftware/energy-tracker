# Epic 5: Multi-Flat Management & Flat Structure

A user can create and manage multiple flats, switch between them via the header, and define the four-level physical hierarchy (Flat → Rooms → Power Points → Devices) for each. Flat deletion is permanent and requires type-to-confirm. This epic is the R2 foundation — the Flat Structure it produces is consumed by Smart Plug Import (Epic 6) and Decomposition (Epic 7).

## Story 5.1: Multi-Flat Backend — Create, List & Cascade Delete

As a user,
I want to create additional flats, list all my flats, and permanently delete a flat along with all its data,
So that I can manage multiple dwellings independently with complete data isolation between them.

**Acceptance Criteria:**

**Given** `GET /api/v1/flats`,
**When** called by an authenticated user,
**Then** `GetFlatsFunction` returns all Flats belonging to the resolved `UserId` as `FlatSummary` records (`FlatId`, `Name`, `AnnualKwhBaseline` decimal, `SpikeThreshold` decimal, `PlannedAnnualSpend` nullable decimal); HTTP 200; ≤ 2s; Flats belonging to other users are never returned.

**Given** `POST /api/v1/flats` with `{ name, annualKwhBaseline, plannedAnnualSpend }`,
**When** `CreateFlatFunction.RunAsync` executes,
**Then** a new `Flat` record is created scoped to the resolved `UserId`; `SpikeThreshold` defaults to `2.0`; HTTP 201 with `Location: /api/v1/flats/{flatId}`; no Tariff entries are created — the caller must add one via the Tariff endpoint.

**Given** `DELETE /api/v1/flats/{flatId}`,
**When** `DeleteFlatFunction.RunAsync` executes,
**Then** `TenantResolver` verifies `flatId` belongs to the resolved `UserId` (HTTP 403 otherwise); the Flat and all associated data are permanently deleted: all `MeterReadings`, `Tariffs`, `SmartPlugDailyData`, `SmartPlugIntervalData`, `ImportJobs`, `Rooms` (and their `PowerPoints`, `Devices`), `InsightRuns`, `Insights`; HTTP 204; no orphaned records remain.
**And** cascade delete is enforced at the database level via `OnDelete(DeleteBehavior.Cascade)` in Fluent API on all FK relationships from `Flats` — not application-side loops.
**And** `DeleteFlatFunctionTests` (HTTP-level, `api.Tests/Features/Flats/`) is a hard requirement for Story 5.1 to reach `done` — not optional polish — and must assert: (1) cascade completeness — zero rows remain in `MeterReadings`, `Tariffs`, `SmartPlugDailyData`, `SmartPlugIntervalData`, `ImportJobs`, `Rooms`, `PowerPoints`, `Devices`, `InsightRuns`, `Insights` for the deleted `flatId`; (2) wrong-owner rejection — a `DELETE` for a `flatId` not owned by the resolved `UserId` returns HTTP 403 and performs no deletion; (3) no-orphaned-records / sibling isolation — deleting one Flat leaves all data belonging to any other Flat (same or different owner) untouched.

**Given** `GET /api/v1/user/settings` and `PUT /api/v1/user/settings`,
**When** called,
**Then** the response includes `activeFlatId` (nullable guid); PUT accepts an `activeFlatId` field and persists it to `Users.ActiveFlatId` (new nullable column added via EF Core migration).

---

## Story 5.2: Flat Switcher, Add Flat & Deletion UI

As a user,
I want to switch between my flats from the app header, create additional flats, and delete a flat by typing its name to confirm,
So that I can move between dwellings quickly and remove a flat with friction appropriate to an irreversible action.

**Acceptance Criteria:**

**Given** the app header on any surface,
**When** rendered,
**Then** the active Flat's name is displayed as a tappable element; tapping it opens the flat switcher dropdown listing all Flats plus an "Add flat" option at the bottom; the active Flat is visually distinguished.

**Given** a different Flat is selected from the dropdown,
**When** the selection is made,
**Then** `PUT /api/v1/user/settings` is called with the new `activeFlatId`; all TanStack Query keys scoped to the previous `flatId` are invalidated; all surfaces reload data for the newly selected Flat; the header Flat name updates immediately.

**Given** the browser is closed and reopened,
**When** the app loads,
**Then** `GET /api/v1/user/settings` returns the stored `activeFlatId`; the app initialises with that Flat active without requiring re-selection (FR-20).

**Given** "Add flat" is tapped in the switcher,
**When** the add flat form opens,
**Then** the user can enter a flat name (required) and Annual kWh Baseline using the same preset + custom pattern from onboarding; submitting calls `POST /api/v1/flats` and on success switches to the new Flat; a prompt guides the user to Settings → Tariff to add an initial tariff.

**Given** the user navigates to Settings → Account and taps "Delete Flat",
**When** `FlatDeleteConfirm.tsx` opens,
**Then** a text input shows the prompt `Type "{flatName}" to delete`; the Delete button is disabled until the typed value matches the Flat name exactly (case-sensitive); tapping Delete calls `DELETE /api/v1/flats/{flatId}` and on 204: switches to another available Flat or redirects to onboarding if no Flats remain.

---

## Story 5.3: Flat Structure Backend — Rooms, Power Points & Devices

As a user,
I want the server to store and return the four-level physical hierarchy of my flat including Smart Plug and Smart Power Strip assignments,
So that imported smart plug data can be correctly attributed and Decomposition can group consumption by room and device.

**Acceptance Criteria:**

**Given** EF Core migrations for `Rooms`, `PowerPoints`, and `Devices`,
**When** reviewed,
**Then** `RoomConfiguration` defines `RoomId` (guid PK), `FlatId` (FK, cascade delete), `Name`, `SortOrder` (int). `PowerPointConfiguration` defines `PowerPointId` (guid PK), `RoomId` (FK, cascade delete), `Name`, `PlugId` (nullable nvarchar — assigned smart plug identifier; never derived from file metadata). `DeviceConfiguration` defines `DeviceId` (guid PK), `PowerPointId` (FK, cascade delete), `Name`, `Type`, `Manufacturer`, `Model`, `PurchaseDate` (nullable datetimeoffset), `ConsumptionApproach` (enum: None/EuLabel/SelfMeasured), `EuLabelClass` (nullable), `EuAnnualKwh` (nullable decimal), `SelfMeasuredKwh` (nullable decimal), `SelfMeasuredPeriod` (nullable enum: Daily/Weekly). Zero Data Annotation attributes on any entity class.

**Given** `GET /api/v1/flats/{flatId}/structure`,
**When** called,
**Then** `GetFlatStructureFunction` returns the full nested hierarchy as a `FlatStructureResponse` record (Flat → Rooms → PowerPoints → Devices); each PowerPoint includes `plugId` (nullable) and a `hasDefaultTemplate` flag (true when no Rooms exist); HTTP 200; ≤ 2s; tenant-scoped.

**Given** `PUT /api/v1/flats/{flatId}/structure` with a complete structure payload,
**When** `UpdateFlatStructureFunction.RunAsync` executes,
**Then** the full structure is replaced atomically within a transaction (delete-and-reinsert); a `plugId` on a PowerPoint is stored as provided — never derived from file metadata; HTTP 200 with the updated structure; ≤ 2s.

**Given** a PUT payload where a `plugId` is assigned to more than one PowerPoint in the same Flat,
**When** validated,
**Then** HTTP 422 Problem Details is returned — each Smart Plug may be assigned to exactly one PowerPoint.

---

## Story 5.4: Flat Structure Editor Frontend

As a user,
I want to define and edit the rooms, power points, and devices in my flat using a structured editor pre-populated with a default room template,
So that I have the physical hierarchy ready before importing smart plug data.

**Acceptance Criteria:**

**Given** the Flat Structure editor opens for a Flat with no existing structure (`hasDefaultTemplate: true`),
**When** rendered,
**Then** `FlatStructureEditor.tsx` pre-populates five default Room entries: living room, bedroom, kitchen, bathroom, hallway; a prompt reads "These rooms were pre-filled — edit names or add your own."; no database write occurs until the user saves.

**Given** the editor with rooms present,
**When** rendered,
**Then** each Room shows its name (editable inline) and expands to reveal its PowerPoints; each PowerPoint shows its name, an optional Smart Plug / Smart Power Strip `plugId` assignment field, and its Devices; "Add Room", "Add Power Point", and "Add Device" controls are available at the appropriate hierarchy levels.

**Given** the user assigns a Smart Power Strip to a PowerPoint,
**When** saved,
**Then** Strip Outlet rows (one per device slot) appear beneath the PowerPoint for Device assignment; the PowerPoint's `plugId` is set at the strip level (FR-21).

**Given** a Device row in the editor at this stage,
**When** rendered,
**Then** `Name`, `Type`, `Manufacturer`, `Model` fields are editable; `ConsumptionApproach` defaults to `None`; an inline note reads "Configure consumption profile to include this device in Decomposition" — the EU label / self-measured entry UI is added in Epic 6 Story 6.5.

**Given** the user saves the structure,
**When** `useUpdateFlatStructure` calls `PUT /api/v1/flats/{flatId}/structure`,
**Then** on success: TanStack Query key `['flat-structure', flatId]` is invalidated; a success confirmation is shown.

**Given** a `plugId` conflict (same plug assigned to two PowerPoints),
**When** the user attempts to save,
**Then** an inline validation error appears: "This plug is already assigned to another power point"; Save is disabled until resolved.

---

## Story 5.5: UX Polish — Bottom Tab Bar Safe Area & Tariff Sheet Close Affordance

As a user,
I want the bottom navigation to respect my device's safe area and the tariff edit sheet to have a clearly reachable close control,
So that the app is comfortable to use on notched/home-indicator iOS devices and I can reliably dismiss the tariff edit sheet.

**Acceptance Criteria:**

**Given** `BottomTabBar` on an iOS device with a home indicator (e.g. Safari/iOS),
**When** rendered,
**Then** its bottom padding accounts for `env(safe-area-inset-bottom)` so the home indicator no longer overlaps the bar's content; the spec's exact 72px height is preserved as the content height with the safe-area inset added on top; the related scroll/layout quirk observed on tab-switch on Safari/iOS is also fixed.

**Given** the Tariff edit sheet (`TariffForm.tsx` in edit mode),
**When** rendered,
**Then** the close ("✕") affordance is resized/repositioned so it is easy to notice and reach (adequate tap target, clear visual placement); no change to the sheet's submission behavior.

[Source: _bmad-output/implementation-artifacts/epic-4-retro-2026-07-03.md#Action Items — items 6 and 7, "Epic 5 backlog, minor"]

---
