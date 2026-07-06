# Epic 6: Smart Plug Import & Device Registry

A user can upload Eve Home Excel and Meross CSV exports, parsed into a unified daily kWh timeline, gap-interpolated, and reconciled against the main meter. Devices can be registered with EU label or self-measured consumption profiles.

## Story 6.0: Pre-Epic-6 Hardening — CI Test Gate, Onboarding Validator Fix & Flat Structure Delete Affordance

As a user and as the team maintaining this app,
I want the existing test suite to actually gate merges, the onboarding
form to validate the same fields its siblings already validate, and a
way to remove a mistakenly-added room/power point/device,
So that Epic 6 builds on a codebase where regressions are caught before
merge, a known validation bypass is closed, and flat structure
management isn't a one-way ratchet.

**Acceptance Criteria:**

**Given** `.github/workflows/azure-static-web-apps.yml`,
**When** the workflow is updated,
**Then** it adds a `dotnet test` step (running `api.Tests`) and an
`npm test` step (running the Vitest suite in `client/`), both required
to pass before the build/publish jobs run; the trigger block adds
`pull_request: branches: [main]` alongside the existing trigger, so
every PR runs the same gate before merge — not just pushes to `main`.

**Given** `api/Features/Onboarding/OnboardingValidator.cs`,
**When** `PlannedAnnualSpend` is validated,
**Then** it receives the same rule already present on
`CreateFlatValidator`/`PatchFlatValidator` for the identical
`Flat.PlannedAnnualSpend` column: `GreaterThan(0)`, `LessThan(50000)`,
`PrecisionScale(18, 4, true)`; a new or extended
`OnboardingValidatorTests.cs` case asserts a value with more than 4
decimal places, and a value outside the (0, 50000) range, are both
rejected with 400 Problem Details.

**Given** the Flat Structure editor
(`client/src/features/flat-structure/components/`),
**When** a user views an existing Room, Power Point, or Device,
**Then** a delete affordance is present for each; tapping it removes
the item from the client-side draft model (removing a Room also
removes its child Power Points/Devices from the draft); a single
confirmation step (inline or modal) is required before removal to
guard against accidental loss; on Save, the removal is carried by the
existing `PUT /api/v1/flats/{flatId}/structure` full-replace contract
(delete-and-reinsert transaction) — no new backend endpoint is needed.

**Given** the three fixes above,
**When** the story reaches `done`,
**Then** `dotnet test` and `npm test` both pass locally and (per AC1,
now) in CI; `OnboardingValidatorTests.cs` covers the new rule;
`FlatStructureEditor.test.tsx` (or equivalent) covers delete-with-
confirm for a Room, a Power Point, and a Device.

---

## Story 6.1: Import Pipeline Infrastructure — Upload, Job Tracking & Blob Trigger

As a user,
I want to upload a smart plug file and get immediate confirmation it was received with a job ID to track processing,
So that I am not blocked waiting for the file to process and can navigate freely while it runs in the background.

**Acceptance Criteria:**

**Given** EF Core migrations for `ImportJobs`, `SmartPlugDailyData`, and `SmartPlugIntervalData`,
**When** reviewed,
**Then** `ImportJobConfiguration` defines `ImportJobId` (guid PK), `FlatId` (FK, cascade delete), `Status` (enum: Pending/Processing/Complete/Failed), `CreatedAt` (datetimeoffset), `CompletedAt` (nullable datetimeoffset), `ErrorCategory` (nullable enum: DataUnreadable/ProcessingFailed/ServiceUnavailable). `SmartPlugDailyDataConfiguration` defines `Id` (guid PK), `PlugId` (nvarchar), `FlatId` (FK, cascade delete), `Date` (date), `KwhValue` (decimal), `IsInterpolated` (bool); unique index on `(FlatId, PlugId, Date)`. `SmartPlugIntervalDataConfiguration` defines `Id` (guid PK), `PlugId` (nvarchar), `FlatId` (FK, cascade delete), `Timestamp` (datetimeoffset), `WhValue` (decimal); index on `(FlatId, PlugId, Timestamp)`. Zero Data Annotation attributes on any entity class.

**Given** `POST /api/v1/flats/{flatId}/imports` with a multipart file upload,
**When** `UploadFunction.RunAsync` executes,
**Then** an `ImportJob` record is created with `Status = Pending`; the file is written to Azure Blob Storage at `smart-plug-imports/{userId}/{flatId}/{importJobId}.{ext}` using Managed Identity; HTTP 202 is returned with `{ importJobId }`; server-side time to 202 response is ≤ 2s.

**Given** `GET /api/v1/flats/{flatId}/imports/{jobId}`,
**When** called,
**Then** `GetImportStatusFunction` returns `{ importJobId, status, createdAt, completedAt, errorCategory, gapNotifications? }`; HTTP 200; tenant-scoped.

**Given** the blob trigger fires on `ProcessImportFunction`,
**When** executed,
**Then** it sets `Status = Processing`; reads the blob; detects file type from extension (`.xlsx` → Eve Home, `.csv` → Meross); routes to the appropriate parser; on unhandled exception: `Status = Failed`, `ErrorCategory = ProcessingFailed`; on storage outage: `ErrorCategory = ServiceUnavailable`; on unreadable/corrupt file: `ErrorCategory = DataUnreadable`; raw exception messages are logged to Application Insights only — never returned to the user.

**Given** import error categorization (FR-28),
**When** `ImportJob.ErrorCategory` is set,
**Then** the frontend maps it to exactly one user-facing message: `DataUnreadable` → "Data cannot be read."; `ProcessingFailed` → "Processing failed — try again."; `ServiceUnavailable` → "Service temporarily unavailable — try again later."

**Given** `ImportJob`, `SmartPlugDailyData`, and `SmartPlugIntervalData` can be written concurrently (e.g., a retried blob trigger re-processing the same job while a status poll or a structure edit is in flight),
**When** `ImportJobConfiguration` is defined,
**Then** `ImportJob` includes a `RowVersion` (SQL Server `rowversion`) column configured via EF Core `.IsRowVersion()`; a `DbUpdateConcurrencyException` on save is caught and surfaces as `ImportJob.Status = Failed`, `ErrorCategory = ProcessingFailed` — never an unhandled 500 or a silent overwrite; this is the codebase's first concurrency-token pattern, deliberately scoped to these three new tables only — `Flat`, `Tariff`, and `MeterReading` remain last-write-wins, tracked separately in `deferred-work.md`.

---

## Story 6.2: Eve Home Excel Parser

As a user,
I want to upload an Eve Home `.xlsx` export and have it parsed correctly into a daily kWh timeline with raw interval rows retained,
So that the app can compute daily consumption and later detect standby offenders using the high-resolution interval data.

**Acceptance Criteria:**

**Given** a valid Eve Home `.xlsx` file (single sheet `Gesamtverbrauch`, reverse-chronological ~10-minute interval rows in Wh),
**When** `EveHomeParser.cs` processes it,
**Then** device name is extracted from cell A1; Room name from cell A2 (informational only, not used for structure matching); rows with empty values are skipped; all remaining rows are parsed as `(Timestamp: datetimeoffset, WhValue: decimal)`; timestamps are treated as local time and are **not** converted to UTC — conversion would corrupt daily aggregation boundaries by shifting interval assignments across midnight.

**Given** two overlapping Eve Home exports for the same `plugId`,
**When** both are imported,
**Then** rows are deduplicated by timestamp before daily aggregation — duplicate timestamps produce a single daily total, not a doubled value (FR-24).

**Given** the parsed interval rows,
**When** written to the database,
**Then** all rows are inserted into `SmartPlugIntervalData` (`PlugId`, `FlatId`, `Timestamp`, `WhValue`); daily totals are aggregated and upserted into `SmartPlugDailyData` (`KwhValue` decimal, `IsInterpolated = false`); existing rows for the same `(FlatId, PlugId, Date)` are updated, not duplicated.

**Given** `EveHomeParserTests.cs` in `api.Tests/Features/SmartPlugImport/`,
**When** run,
**Then** tests cover: valid file produces correct daily totals; UTC conversion is absent (local-time daily boundaries preserved); overlapping exports produce deduplicated totals; rows with empty values are skipped; device name extracted from A1.

---

## Story 6.3: Meross CSV Parser

As a user,
I want to upload a Meross `.csv` export and have it parsed correctly, with zero-consumption days stored as valid data rather than treated as gaps,
So that my Meross plug timeline is accurate and days with no consumption are not incorrectly interpolated.

**Acceptance Criteria:**

**Given** a valid Meross `.csv` file (UTF-8 with optional BOM, tab-separated with per-value comma prefix, filename `Power Monitor Day Data - {device_name} - {YYYYMMDD}.csv`),
**When** `MerossParser.cs` processes it,
**Then** device name is extracted from the filename using the specified pattern; UTF-8 BOM is stripped before parsing; trailing whitespace and empty rows are removed; each remaining row is parsed as `(Date: date, KwhValue: decimal)`.

**Given** a Meross row with `Power Consumption-(kWh)` value of `0.000`,
**When** parsed,
**Then** it is stored in `SmartPlugDailyData` with `KwhValue = 0` and `IsInterpolated = false` — it is **not** treated as a missing date subject to gap detection or interpolation (FR-25).

**Given** the parsed Meross rows,
**When** written to the database,
**Then** rows are upserted into `SmartPlugDailyData` only — no `SmartPlugIntervalData` rows are created for Meross imports; standby detection is not available for this format.

**Given** `MerossParserTests.cs` in `api.Tests/Features/SmartPlugImport/`,
**When** run,
**Then** tests cover: valid file produces correct daily totals; UTF-8 BOM file parses correctly; zero-value row stored as valid data not flagged as a gap; device name extracted from filename; empty rows ignored.

---

## Story 6.4: Gap Detection, Interpolation & Main Meter Reconciliation

As a user,
I want gaps in my smart plug timeline to be automatically filled by interpolation with a notification, and total attributed consumption to always be reconciled against my main meter,
So that my decomposition data is complete and never over-counts my actual usage.

**Acceptance Criteria:**

**Given** `InterpolationEngine.cs` processing a plug's daily timeline after parsing,
**When** missing dates are detected within the plug's covered period (first to last date in the import),
**Then** gap date ranges are recorded for notification; missing days are filled by linear interpolation between surrounding anchor values, capped at the 7-day per-day average preceding the gap; filled rows are inserted into `SmartPlugDailyData` with `IsInterpolated = true`; zero-valued rows are **not** treated as gaps — only absent dates trigger interpolation (FR-26).

**Given** dates outside the plug's covered period (before the first export date or after the last),
**When** gap detection runs,
**Then** those periods are not treated as gaps — gap detection applies only within the covered range of the export.

**Given** `InterpolationEngine` detects one or more gaps,
**When** `ImportJob` is completed,
**Then** a `gapNotifications` payload is stored on the `ImportJob` listing affected `plugId` and date ranges; `GET /api/v1/flats/{flatId}/imports/{jobId}` returns this payload for frontend display: "Gap detected: {date range}. Missing days have been interpolated."

**Given** `ReconciliationEngine.cs` running after all plugs for a period are processed,
**When** it computes the period total,
**Then** `Residual = MainMeterTotal − sum(SmartPlugDailyData.KwhValue for all plugs in the period)`; the invariant `attributed + Residual = MainMeterTotal` holds within ±0.1 kWh for clean periods and within ±1.0 kWh for periods containing any interpolated values (FR-27); if attributed kWh exceeds the main meter total, `ImportJob.Status = Failed` with `ErrorCategory = ProcessingFailed`.

**Given** `InterpolationEngineTests.cs` and `ReconciliationEngineTests.cs`,
**When** run,
**Then** interpolation tests cover: gap within covered period is filled; 7-day cap is applied; zero-value days not flagged; periods outside covered range unaffected. Reconciliation tests cover: attributed + residual = meter total within tolerance for clean and interpolated periods; over-attribution triggers failure.

---

## Story 6.5: Device Registry — EU Label & Self-Measured Consumption

As a user,
I want to configure a consumption profile for each device using either its EU energy label or a self-measured average,
So that devices without a direct smart plug contribute an estimated baseline to the Decomposition view.

**Acceptance Criteria:**

**Given** a Device with `ConsumptionApproach = None` in the Flat Structure editor,
**When** the user taps "Configure consumption profile",
**Then** a Choice Step presents two mutually exclusive selector cards: "EU energy label" and "Self-measured average"; selecting one reveals only that approach's fields; the other approach's fields are hidden, not disabled (UX-DR17).

**Given** "EU energy label" is selected,
**When** the EU label fields render,
**Then** an energy class rating field (text) and an annual kWh field (`inputmode="numeric"`) are shown; saving updates `Device.ConsumptionApproach = EuLabel`, `EuLabelClass`, and `EuAnnualKwh` (decimal); the derived daily estimate (`EuAnnualKwh ÷ 365`) is displayed below the field for confirmation (FR-30).

**Given** "Self-measured average" is selected,
**When** the self-measured fields render,
**Then** a Daily/Weekly toggle is shown with "Daily" pre-selected; the kWh input label updates instantly on toggle switch ("kWh per day" / "kWh per week"); saving updates `Device.ConsumptionApproach = SelfMeasured`, `SelfMeasuredKwh` (decimal), and `SelfMeasuredPeriod` (Daily/Weekly) (FR-31).

**Given** saving a consumption profile,
**When** `PUT /api/v1/flats/{flatId}/structure` is called,
**Then** HTTP 200 is returned; TanStack Query key `['flat-structure', flatId]` is invalidated; all decimal values are stored as `decimal` in the database — no float or double.

---

## Story 6.6: Import UI — Upload Zone, File List & Progress Card

As a user,
I want to select or drag smart plug files, see auto-detected file types with device associations, and track processing progress from the Decomposition tab,
So that I can upload exports in a few taps and continue using the app while they process in the background.

**Acceptance Criteria:**

**Given** the Import surface on phone,
**When** rendered,
**Then** `FileUploadZone.tsx` shows a file picker button; tapping it opens the native file chooser accepting `.xlsx` and `.csv`; drag-and-drop is not available on phone.

**Given** the Import surface on desktop or tablet,
**When** rendered,
**Then** the upload zone also accepts drag-and-drop; dragging valid files onto the zone triggers the same file-selection flow as the file picker.

**Given** one or more files are selected,
**When** the file list renders,
**Then** each `FileListItem.tsx` shows: filename, auto-detected type label ("Eve Home" for `.xlsx`, "Meross" for `.csv`), and a device association dropdown populated from the Flat Structure PowerPoints with assigned `plugId` values; if the filename contains a known device name (case-insensitive match), that device is auto-pre-selected; "Upload Files" is active only when all files have an association.

**Given** "Upload Files" is tapped,
**When** `useUploadImport` calls `POST /api/v1/flats/{flatId}/imports` for each file,
**Then** on 202: a Progress Card (`ImportProgressCard.tsx`) appears on the Decomposition tab — amber-tinted glass card (`residual-tint` overlay) with status label and description; the app remains fully navigable.

**Given** `useImportJobStatus` polling `GET .../imports/{jobId}` every 3 seconds,
**When** status reaches `Complete`,
**Then** the Progress Card disappears and TanStack Query key `['decomposition', flatId, ...]` is invalidated; when status reaches `Failed`: the Progress Card shows the categorized error message with a Retry action.

**Given** a gap notification in the completed job response,
**When** displayed,
**Then** the message reads "Gap detected: {date range}. Missing days have been interpolated." — shown as a non-blocking notification.

**Given** `ErrorCategory = DataUnreadable` on a file row,
**When** displayed,
**Then** the file row shows an `accent-error` left border and "Data cannot be read." inline; the user can remove the file and try another.

---
