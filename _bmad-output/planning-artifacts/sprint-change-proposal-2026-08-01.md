# Sprint Change Proposal — 2026-08-01 (Epic 12: Device Lifecycle & Date-Aware Decomposition Attribution)

## Section 1: Issue Summary

**Trigger:** Architecture review with Winston (this session) noted that `Device.PurchaseDate` is captured at registration (FR-29) but never consulted anywhere in `DecompositionEngine.cs` — every device is treated as having existed for the entirety of any queried period. A dedicated brainstorming session (`_bmad-output/brainstorming/brainstorming-session-2026-08-01-14-56.md`, techniques: Question Storming → Morphological Analysis → Six Thinking Hats) explored the full problem space and converged on a two-part design, validated against a concrete same-day real example: a new Hue light strip installed today (must not be back-dated to "day one") plus an old strip relocated to a different room the same day (needs its room history split at today's date).

**Problem:** `DecompositionEngine.cs`'s standalone-device estimate path (`ResolveStandaloneApproach`, `dailyEstimate * dayCount`) applies a device's estimated daily kWh uniformly across the entire query period regardless of when the device was actually added or removed. Separately, room-level attribution (`Rooms.Include(PowerPoints).ThenInclude(Devices)`) is resolved from a single *current* Flat Structure snapshot — a device that changed rooms mid-period has its full-period consumption attributed wholesale to wherever it currently sits, with no historical record that a move ever happened.

**Evidence:** `DecompositionEngine.cs:227-242` (uniform estimate multiplication, no date gating); `DecompositionEngine.cs:50-54` (current-structure-only room grouping, no per-day resolution anywhere in the engine); `Device.cs:24` (`PurchaseDate` field exists, unused); `requirements-inventory.md:174` (FR-29 scope confirmed as metadata-only); Ralf's real, live example (new + relocated device, same day) confirming both halves of the design address a genuine, current need rather than a hypothetical.

## Section 2: Impact Analysis

**Epic Impact:** No existing epic modified. New **Epic 12** added to the roster, sequenced to start after Epic 11's retrospective completes (Ralf's explicit sequencing decision — Epic 11's stories are all `done`; only its optional retrospective is pending). No planned epic is invalidated or resequenced.

**Story Impact:** Two new stories — 12.1 (existence-window gating, self-contained, low risk) and 12.2 (room-assignment history, a real engine change to `DecompositionEngine`'s room-grouping, moderate risk, de-risked during Six Thinking Hats by an implicit zero-UI capture mechanism rather than a full period-management UI).

**Artifact Conflicts:**
- PRD: two new FRs added under §4.9 Device Registry (FR-52, FR-53), following the precedent of FR-50 (a late addition to the same section). New §6.4 "Release 4" MVP scope bullet added (renumbering old §6.4 Out of Scope to §6.5), following the Release 3 precedent set by Epic 8. Header `updated` date bumped.
- Epic docs: new `epic-12-device-lifecycle-and-date-aware-decomposition-attribution.md` file created (2 stories, Given/When/Then ACs). `epic-list.md` gains the Epic 12 summary entry.
- `requirements-inventory.md`: new "Release 4" FR bucket; FR Coverage Map gains FR-52/FR-53 lines.
- `sprint-status.yaml`: new `epic-12: backlog` block with both stories `backlog`; header `last_updated` comment updated; inline comment documents the Epic-11-retro sequencing dependency.
- Architecture: new AD-8b documenting the existence-window + assignment-period pattern (explicitly reusing the `TariffResolution` period-resolution idiom); `Devices` entity table row gains two nullable columns; new `DeviceAssignmentPeriods` table row added.
- UI/UX: no new UX-DR. Story 12.1 adds one optional date field to the existing Device form; Story 12.2 is implemented transparently (implicit stamping on existing Flat Structure save, per the brainstorming session's Green Hat resolution) — a deliberate design choice to avoid new UI for a ~3×/year event.
- `deferred-work.md`: **not modified** — that file's established convention is strictly "Deferred from: code review of story X" entries; this is a planning-time scoping decision (plug/strip relocation explicitly out of scope), already documented in Epic 12's intro paragraph and this proposal, which is the correct and sufficient record.

**Technical Impact:** New EF Core migration (two `Device` columns + new `DeviceAssignmentPeriod` table + backfill of one open-ended period per existing device, keyed off current `PowerPointId`). `DecompositionEngine.cs` changes: (1) day-clamp added to the standalone estimated-device path only — measured devices and Smart Power Strip pool math are explicitly unaffected by this epic; (2) room-grouping changes from a single current-structure pass to per-day resolution via a new period-lookup helper, mirroring `TariffResolution.Resolve`'s existing idiom. `UpdateFlatStructureFunction`'s full-replace save gains a diff step (detect `PowerPointId` change per device, close/open assignment periods) — additive to the existing save path, no contract change.

## Section 3: Recommended Approach

**Selected: Option 1 — Direct Adjustment.** Add Epic 12 to the roster; amend the PRD with FR-52/FR-53; no rollback of any existing work; no MVP scope reduction (MVP — Releases 1-3 — is already fully shipped; this is new scope in the same category as Epic 10's Insights or Epic 8's UI-consistency additions).

**Rationale:** Self-contained new capability that reuses an already-trusted pattern (`TariffResolution`'s period-resolution idiom) rather than inventing new architecture. Scope was actively de-risked during the brainstorming session's Six Thinking Hats pass — the costliest design option (a full period-management UI) was replaced with an implicit, zero-UI stamping mechanism once grounded against real usage frequency (~3 events/year). Effort: **Medium**. Risk: **Low**.

**Design decisions confirmed with Ralf during this proposal:**
1. **Sequencing:** Epic 12 explicitly waits for Epic 11's retrospective to close before starting.
2. **Scope boundary:** Plug/strip-hardware (`PlugId`) relocation tracking is deliberately excluded — a relocated/repurposed strip is handled as a new plug via existing manual delete/re-add, not a new tracked concept.
3. **Story 12.1/12.2 boundary:** Smart Power Strip pool math is explicitly out of scope for date-slicing in both stories (a sub-device joining/leaving a strip mid-period), tracked as a future follow-up rather than silently unsupported.

## Section 4: Detailed Change Proposals

### PRD — FR-52 and FR-53 added
**File:** `prd.md` §4.9 (new FRs after FR-50), §6 MVP Scope (new §6.4 Release 4, renumbering old §6.4 to §6.5), header `updated` date — applied.

### Epic 12 — new epic file
**File:** `epic-12-device-lifecycle-and-date-aware-decomposition-attribution.md` — applied. 2 stories in full Given/When/Then form.

### epic-list.md — new entry
**File:** `epic-list.md` — applied. Epic 12 summary added after Epic 11.

### requirements-inventory.md — new FR bucket + coverage map
**File:** `requirements-inventory.md` — applied. Release 4 bucket (FR-52, FR-53); FR Coverage Map updated.

### architecture.md — new AD-8b + entity model update
**File:** `architecture.md` — applied. AD-8b added after AD-8a; `Devices` row gains 2 columns; new `DeviceAssignmentPeriods` row added.

### sprint-status.yaml — new backlog entries
**File:** `sprint-status.yaml` — applied. `epic-12: backlog` block added with both stories `backlog`; header comment updated.

## Section 5: Implementation Handoff

**Scope classification: Moderate** — new epic/entities/migration/engine-logic change, not a single-file fix; the epic/PRD/sprint bookkeeping is already done as part of this proposal, but implementation itself (migration, engine changes, write-path diffing, tests) is real, sequenced work spanning 2 stories.

**Handoff:** Epic 12 is in `backlog`, sequenced after `epic-11-retrospective`. Once that retrospective closes, recommended next step: `bmad-create-story` for Story 12.1 to materialize its full story-context file, then `bmad-dev-story` for implementation; Story 12.2 follows the same path once 12.1 is done, since 12.2's migration backfill reads `Device.InUseSince` (introduced by 12.1).

**Deliverables produced by this proposal:**
- This Sprint Change Proposal document
- Updated PRD (FR-52, FR-53 added; §6 MVP Scope extended)
- New Epic 12 file (2 stories)
- Updated `epic-list.md`
- Updated `requirements-inventory.md`
- Updated `architecture.md` (AD-8b + entity model)
- Updated `sprint-status.yaml`
