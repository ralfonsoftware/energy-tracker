---
stepsCompleted: [1]
inputDocuments: []
session_topic: 'How the app should behave when a new Device is added to the registry — device-existence-period awareness in energy tracking/decomposition, specifically whether/how Device.PurchaseDate should gate estimated-device consumption in DecompositionEngine.cs'
session_goals: 'Surface the full space of ways the system could reason about "when did this device start existing/consuming" — beyond the obvious PurchaseDate-gating fix — including edge cases, UX implications, and alternative framings, ahead of scoping a dedicated story for a new epic'
selected_approach: 'ai-recommended'
techniques_used: ['Question Storming', 'Morphological Analysis', 'Six Thinking Hats']
ideas_generated: []
context_file: ''
technique_execution_complete: true
stepsCompleted: [1, 2, 3, 4]
session_active: false
workflow_completed: true
---

# Brainstorming Session Results

**Facilitator:** Ralf
**Date:** 2026-08-01

## Session Overview

**Topic:** How the app should behave when a new Device is added to the registry — device-existence-period awareness in energy tracking/decomposition, specifically whether/how `Device.PurchaseDate` should gate estimated-device consumption in `DecompositionEngine.cs`.

**Goals:** Surface the full space of ways the system could reason about "when did this device start existing/consuming" — beyond the obvious PurchaseDate-gating fix — including edge cases, UX implications, and alternative framings, ahead of scoping a dedicated story for a new epic.

### Context Guidance

_Carried over from an architecture discussion with Winston: measured devices (smart-plug attached) are already correct by construction, since `SmartPlugDailyData` only has rows from when the plug actually reported data. Estimated devices (EU label / self-measured, no smart plug) are not — `EstimateDailyKwh(device) * dayCount` is applied uniformly across the entire query period in `DecompositionEngine.cs`, with no reference to `Device.PurchaseDate` (which exists on the entity per FR-29 but is currently metadata-only)._

### Session Setup

_AI-Recommended Techniques approach selected — facilitator will propose techniques tailored to this topic._

## Technique Selection

**Approach:** AI-Recommended Techniques
**Analysis Context:** Device-existence-period awareness in energy tracking/decomposition, with focus on surfacing the full space of ways the system could reason about "when did this device start existing/consuming" ahead of scoping a dedicated story.

**Recommended Techniques:**

- **Question Storming (deep):** Map the real problem space before designing anything — what "device exists" actually means (purchase vs. installation vs. first-data vs. registration), and surface adjacent edge cases (removal, replacement, moves between power points).
- **Morphological Analysis (deep):** Systematically list parameters (existence-marking event, capture mechanism, edge-case handling) and options for each, combining them into the full space of candidate designs rather than collapsing early to the obvious "gate by PurchaseDate" fix.
- **Six Thinking Hats (structured):** Stress-test the leading candidate design(s) across facts, risks, benefits, creative extensions, and implementation fit, to arrive at a de-risked direction ready for story-scoping.

**AI Rationale:** Complex/abstract technical topic with an explicit "surface the full space" goal → deep + structured categories, sequenced broad → systematic → sharpened, sized for a ~50-60 min session.

## Technique Execution Results

### Question Storming

**Interactive Focus:** Mapping what "a device exists and consumes power" actually means, before designing any fix.

**Questions Generated:**

1. When was the device purchased?
2. When was the device installed and started consuming?
3. Was the device moved to a different power point/room?
4. When was the device decommissioned/physically removed?
5. How does a mid-life switch between consumption approaches (EU-label ↔ self-measured ↔ smart-plug-measured) get handled?
6. If devices already exist without this concept, what synthetic default applies?
7. Does a Power Point reassignment reset a single "in use since" date, or open a second tracked period?
8. Do Power Points need the same lifecycle concept as Devices, given smart strips get physically relocated/repurposed with different devices attached? (open tension, carried into Morphological Analysis)
9. Is "decommissioned" a status flag or an end-date on the existing row?
10. Is a full lifecycle event history justified for a single-user project, or do two dates cover it?
11. Do seasonal/idle devices (unplugged for months, replugged later) need a third state distinct from decommissioned?

**Key Breakthroughs (resolved through dialogue, carried forward as working decisions):**

- `PurchaseDate` stays pure metadata (warranty-relevant) — separate from the field gating decomposition inclusion.
- The gating concept is two dates: **"in use since"** (renamed from "installation date") and **"decommissioned"** (nullable end date) — together defining the window a device's kWh may enter the decomposition.
- A Power Point move does **not** reset the in-use window — it opens a second period for *room attribution* only; the device keeps consuming throughout.
- Fallback for pre-existing devices: synthetic "in use since" from the earliest available creation-related signal (plug data / room / account).
- Two dates are sufficient — no full event-sourced history needed.
- No third "idle" state — seasonal/unplugged devices ride as-is; usually plug-measured anyway, so the data naturally reflects zero.

**Open Tension Carried Forward:** Whether Power Points need their own in-use/decommissioned lifecycle mirroring Devices (question 8) — unresolved, anchor question for Morphological Analysis.

**User Creative Strengths:** Ralf answered in decisive, opinionated design terms rather than abstract questions — the session moved unusually fast from problem-mapping to near-consensus on a two-date model.

**Energy Level:** High, focused, convergent — technique concluded early (2 rounds) because answers were already crystallizing; moved to Morphological Analysis by mutual agreement rather than exhaustion.

### Morphological Analysis

**Interactive Focus:** Systematically mapping options for 5 parameters (lifecycle location, move representation, fallback defaults, engine application, plug/strip identity), then combining into candidate designs.

**Parameters & Chosen Options:**

- **P1 (lifecycle location):** Device gets its own `InUseSince`/`DecommissionedDate` window; PowerPoint does not get its own dates.
- **P2 (move representation):** Full dedicated table for device room-assignment history (not a side-list).
- **P3 (fallback default):** Nullable fields; `null` = no gating (backward-compatible no-op); UI pre-fills a suggested default (earliest available signal) the user can accept or edit.
- **P4 (engine application):** Day-level clamp in `DecompositionEngine`, scoped to estimated devices only (measured devices already self-correct via data presence); query-level pre-filter kept as an optional perf optimization layered on top.
- **P5 (plug/strip identity on relocation):** **Scoped out** — no dedicated plug-hardware lifecycle table. A relocated/repurposed smart strip is handled as a new plug via existing manual delete/re-add in the Flat Structure editor (Story 6.0 affordance). Explicitly accepted simplification, low priority for this user.

**Key Breakthrough — Priority Pivot:** The session's most valuable insight came from resolving a false symmetry — Device-level room-move attribution (a device physically relocated within the flat, wanting kWh split across rooms by date) and plug/strip-hardware relocation (P5) look similar but are **not** equally important to the user. Device-level moves are the real priority ("attribute power usage as best as possible"); plug/strip identity tracking is explicitly low-priority, with a manual-cleanup simplification accepted.

**Candidate Design (combined):**

1. `Device.InUseSince` / `Device.DecommissionedDate` (nullable) — gates estimated-device inclusion in `DecompositionEngine`, day-clamped, scoped to estimated devices only. Solves the original bug (estimated devices counted for periods before they existed).
2. A `DeviceAssignmentPeriod`-style table — `(DeviceId, PowerPointId, FlatId, From, To-nullable)` — tracks a device's room history over time so decomposition can split a device's kWh across rooms it passed through during a query period.
3. No plug/PowerPoint-level lifecycle tracking — relocated hardware is manual cleanup via existing UI (accepted simplification).
4. Nullable-default fallback with UI-suggested pre-fill; optional query-level pre-filter as perf polish.

**Flagged Risk (carried into Six Thinking Hats):** Item 2 is a materially bigger lift than item 1 — `DecompositionEngine` currently loads Rooms → PowerPoints → Devices as a single *current* structure snapshot and groups by that; it has no concept of "which room did this device's kWh belong to on day N." Making item 2 real requires the room-grouping logic itself to become date-aware, not just adding columns.

**Energy Level:** High engagement, decisive — technique concluded in a single combination pass because the parameter picks converged cleanly and one clarifying question (device- vs. plug-level move priority) resolved the only real ambiguity.

### Six Thinking Hats

**Interactive Focus:** Stress-testing the candidate design (existence-window gating + device room-assignment history) from six angles before shaping it into a story.

**⚪ White Hat (Facts):** `DecompositionEngine` today groups by a *current-structure snapshot* only (`Rooms.Include(PowerPoints).ThenInclude(Devices)`); no per-day room resolution exists anywhere, for measured or estimated devices. `PowerPoint.PlugId` has no history/versioning today — consistent with scoping P5 (plug/strip identity) out. Device room-history is therefore the deeper engine change; existence-window gating is a shallow clamp on existing math.

**⚫ Black Hat (Risks):**
1. Smart-strip pool math (`BuildSmartStripDecomposition`) would need to become date-sliced too if a device joins/leaves a strip mid-period — compounds the engine change beyond just room-grouping.
2. Display ambiguity — how a device split across two rooms in one period renders in the UI/response shape is undesigned.
3. Migration/backfill gap — real regression risk: if the engine resolves rooms only via the new period table with no backfill, every existing device vanishes from every report on ship day.
4. The full-replace `PUT /structure` write path doesn't naturally emit "this device moved" events — needs a diff-and-stamp step added to the save handler.
5. Proportionality — is a ~3×/year event worth this lift? (resolved in Yellow/Green Hat below)

**🟡 Yellow Hat (Benefits):**
- Existence-window gating (item 1) is cheap, self-contained, and fixes an already-identified real bug on its own merits, independent of the room-history feature.
- Room-history (item 2) prevents a *worse* failure mode than "unsupported" — silently and permanently wrong historical numbers after every real flat reorganization, with no way to ever correct them.
- Both features apply the same period-resolution idiom the codebase already trusts for `Tariff`/`TariffResolution` — not a new pattern, an extension of an established one.
- The write-path diff cost is smaller than it looks: a single field comparison (`PowerPointId` changed?) during an existing save, not a general audit system.
- Assignment-period infrastructure is reusable later for relocation-triggered Insights (fits the existing `InsightType` enum pattern) at near-zero incremental cost.

**🟢 Green Hat (Creative Extension):** Instead of a full period-management UI, capture a move **implicitly** — the moment the Flat Structure save detects a device's `PowerPointId` changed, silently close the old assignment period and open a new one dated "now." Zero new UI, zero manual date entry, most of the value for a fraction of the cost. **User-validated in real time** against a live example: a Hue light strip installed today (new device, must not be back-dated to "day one") plus an old strip moved to a different room the same day (existing device, needs its room history split at today's date) — both halves of the design map directly onto lived, current need, not a hypothetical. Confirmed usage frequency: sporadic, ~3×/year, unpredictable timing — validates the minimal/implicit approach over a heavier date-management UI.

**🔵 Blue Hat (Process & Implementation Fit):**
- `Device` gains two nullable columns: `InUseSince`, `DecommissionedDate` (Fluent API config, no Data Annotations).
- New entity `DeviceAssignmentPeriod`: `(Id, DeviceId FK cascade, PowerPointId FK, FlatId FK cascade, From DateTimeOffset, To DateTimeOffset? nullable = current)` — resolved via the same "latest period with `From <= date`" idiom as `TariffResolution.Resolve`.
- Migration backfill: one open-ended period per existing device (`From = InUseSince ?? Flat.CreatedAt`, `PowerPointId` = current, `To = null`) — closes the Black Hat regression risk.
- Write path: full-replace `PUT /structure` handler diffs each device's incoming `PowerPointId` against its persisted value; on change, closes the open period and opens a new one dated now (Green Hat's implicit-stamp mechanism).
- `DecompositionEngine`: resolves each device's room per day via the period table instead of the current live-structure snapshot; existence-window clamp (item 1) applies separately, scoped to estimated devices only.
- No new shared/reusable helper class yet — a private resolution method inside `DecompositionEngine` is enough (single consumer today), consistent with this codebase's Rule-of-Three convention already demonstrated by `TariffResolution` (only extracted after six duplicates existed).

**Overall Creative Journey:** The six hats resolved the one open risk (proportionality/cost vs. value) directly through dialogue — Green Hat's implicit-stamp idea, validated instantly against Ralf's real same-day example (new Hue strip + relocated old strip), converted an open cost concern into a confidently-scoped, moderate story rather than either overbuilding (full period-management UI) or dropping the room-history half entirely.

### Creative Facilitation Narrative

This session moved unusually fast from problem-mapping to convergence — Ralf answered Question Storming's prompts with decisive, opinionated design positions rather than more open questions, which let Morphological Analysis start from an already-rich parameter set. The single most valuable moment was the priority pivot during Morphological Analysis: what looked like two symmetric relocation problems (device-level room moves vs. plug/strip-hardware relocation) turned out to have very different real-world priority, and naming that asymmetry reshaped the whole design. Six Thinking Hats then closed the loop by testing that design's cost against a concrete, same-day real example rather than an abstract one — turning a live "is this worth it" risk into a confidently-scoped answer.

### Session Highlights

**User Creative Strengths:** Fast convergence to decisive, opinionated positions; volunteered concrete real-world grounding (the Hue light strip example) exactly when it was needed to resolve an open risk.
**AI Facilitation Approach:** Question-first mapping before any solutioning, systematic parameter combination to avoid anchoring on the obvious fix, six-angle stress test before committing to scope.
**Breakthrough Moments:** The device-vs-plug relocation priority pivot (Morphological Analysis); the implicit-stamp Green Hat idea validated live against a real same-day example.
**Energy Flow:** Consistently high and convergent throughout — no stuck points, no need for re-energizing techniques or pivots.

## Idea Organization and Prioritization

**Thematic Organization:**

**Theme 1: Existence-Window Gating** — small, self-contained, fixes the originally-flagged bug.
- `Device.InUseSince` / `Device.DecommissionedDate` (nullable) gate estimated-device (EU-label/self-measured) inclusion in `DecompositionEngine`.
- Day-level clamp, scoped to estimated devices only — measured devices already self-correct via smart-plug data presence.
- Nullable-default fallback; no forced migration semantics; UI may suggest a default, never require one.

**Theme 2: Device Room-Move Attribution** — the real priority, per Ralf's own framing.
- New `DeviceAssignmentPeriod` entity `(DeviceId, PowerPointId, FlatId, From, To)`, resolved via the same date-resolution idiom already trusted for `Tariff`/`TariffResolution`.
- Captured implicitly — the full-replace Flat Structure save detects a `PowerPointId` change and stamps a period boundary automatically; no new UI, no manual date entry.
- `DecompositionEngine`'s room-grouping becomes date-aware (today it's a current-structure snapshot only — a real, if bounded, engine change).
- Migration backfills one open period per existing device so nothing vanishes on ship day.

**Scoped Out (explicit decision, not a gap):** Plug/strip-hardware relocation tracking (`PlugId` lifecycle). A relocated/repurposed strip is handled as manual delete-and-re-add via the existing Flat Structure editor (Story 6.0 affordance). Deliberately not modeled — worth a one-line note in `deferred-work.md` so it is never later mistaken for an oversight.

**Prioritization Results:**

- **Both themes ship together, in one story:** they share the same entity (`Device`) and the same engine file (`DecompositionEngine`); Theme 2's migration backfill logic benefits from Theme 1's columns already existing. Validated as one coherent real-world event (new device added today + old device moved today, same day, same user).
- **Quick-win half:** Theme 1 alone is low-risk and could ship independently if the story needs to be split for delivery reasons.
- **Higher-value, higher-lift half:** Theme 2, de-risked from "full period-management UI" down to "implicit stamp on structure save" during Six Thinking Hats' Green Hat pass.

**Action Planning:**

1. Stand up a new epic (none exists yet for this work) covering device-lifecycle-aware decomposition.
2. Draft the story from this session's converged design — entities, migration, write-path diff, engine change — using `bmad-create-epics-and-stories` / `bmad-create-story`.
3. Add a one-line note to `deferred-work.md` documenting the plug/strip-relocation scoping-out decision and its rationale, so it reads as a deliberate choice rather than a gap if revisited later.

## Session Summary and Insights

**Key Achievements:**

- A suspected architecture bug (estimated-device consumption not gated by existence) was mapped into its true, larger domain question ("what does device existence even mean") via Question Storming.
- Morphological Analysis surfaced a false symmetry — device-level room moves vs. plug/strip-hardware relocation looked alike but have very different real priority — and resolving that reshaped the whole design.
- Six Thinking Hats de-risked the costliest part of the design (date-aware room grouping) down to an implicit, UI-free mechanism, validated live against a real same-day example (new Hue light strip + relocated old strip).
- Landed on a converged, moderately-scoped two-part design ready for story-shaping, with one part explicitly and deliberately scoped out.

**Session Reflections:**

This session moved faster than typical brainstorming sessions because Ralf answered generative prompts with decisive design positions rather than more open-ended ideas — the facilitator's role shifted from idea-generation toward structured pressure-testing earlier than usual. The real-world grounding volunteered at exactly the moment it was needed (the Hue light strip example, offered unprompted while discussing usage frequency) was the single most load-bearing moment in the session — it converted an abstract cost/value tradeoff into a concrete, confidently-resolved scope decision.
