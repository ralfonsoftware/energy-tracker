# Sprint Change Proposal — Tariff Contract-Date Consolidation (2026-07-03)

## 1. Issue Summary

During the Epic 4 retrospective (2026-07-03), live testing on Safari/iOS surfaced a real-world defect: a tariff entered with a genuine, currently-valid contract (`ContractStartDate` = 01.10.2024, `ContractDurationMonths` = 12) showed the dashboard reporting "tariff covers 0 of 185 days" — i.e., cost calculations found no tariff covering the user's actual consumption history.

**Root cause:** the Tariff data model carries two separate temporal fields that were never reconciled in the user's mental model or the UI:
- `EffectiveDate` (required, drives `TariffResolver`'s period-accurate cost lookup, immutable after creation, no delete path) — defaults to "today" on the create form.
- `ContractStartDate` (optional, drives only `TariffLockPolicy.IsLocked` together with `ContractDurationMonths`) — the field a user naturally associates with "when my real contract began."

A user backdating a pre-existing real contract fills in `ContractStartDate` (the field that reads as "contract start" to a human) and leaves `EffectiveDate` at its default of "today," since nothing in the UI indicates the two are different or that the latter is the one driving cost math. Once created, `EffectiveDate` cannot be corrected (immutable via PATCH, no `DELETE` endpoint for tariffs).

This was flagged as a risk before Epic 4 even started — `deferred-work.md` item **W4**, logged during Onboarding (pre-Epic-3): *"future tariff range queries (Epic 4) must account for the discrepancy between EffectiveDate and ContractStartDate."* It went unaddressed through Epic 4 and surfaced exactly as predicted.

## 2. Impact Analysis

- **PRD Impact:** FR-10, FR-11, FR-12 (Tariff Management, §4.4) and two Glossary entries (Tariff, Contract Period) require rewording — `ContractStartDate` becomes the sole, required temporal anchor for both cost-period resolution and price locking; `ContractDurationMonths` becomes a purely informational renewal-reminder field, no longer a locking precondition. `EffectiveDate` is retired as a concept. A new scope note excludes dynamic/variable-rate tariffs (no fixed price per kWh) — confirmed explicitly out of scope by the user.
- **Architecture Impact:** `architecture.md`'s Tariffs data model table, the `IX_Tariff_FlatId_EffectiveDate` naming-convention example, and `TariffResolver`'s doc comment need updating. `TariffResolver.ResolveAsync(flatId, date, ct)`'s public signature is unchanged, so Epics 6 (Smart Plug Import) and 7 (Decomposition), which consume it, are unaffected.
- **Epic Impact:** Epic 4 (Tariff Management) only. Epic 4 is still `in-progress` in `sprint-status.yaml` (only its retrospective was marked done), so this lands as a new **Story 4.4** plus AC amendments to the already-`done` Stories 4.1 and 4.3. No impact to Epic 5 (Multi-Flat), which doesn't touch tariff internals.
- **Technical Impact:** DB migration (repurpose `EffectiveDate` → drop; `ContractStartDate` becomes required + unique natural key), a one-time data backfill for existing rows, two resolvers re-keyed (`TariffResolver.ResolveAsync` and `KpiCalculator.ResolveTariff`, the latter flagged for logic duplication in the Epic 3 retro), `TariffLockPolicy.IsLocked` simplified to a single condition, and a **net simplification** of `PatchTariffFunction`: the mutual-exclusion rule between price and contract-term fields (added in Story 4.1's review, the direct cause of Story 4.3's round-2 submit-guard-gap bug) is removed entirely, since the field it was protecting (`ContractStartDate`) becomes immutable. Frontend: `TariffForm` edit mode collapses from two sequential PATCH calls to one.
- **Data Impact:** exactly one real user, one tariff record. Backfill rule (`ContractStartDate = ContractStartDate ?? EffectiveDate`) directly corrects the reported bug, since the affected tariff's `ContractStartDate` was already entered correctly.

## 3. Recommended Approach

**Direct Adjustment** — amend Story 4.1 and Story 4.3's AC in `epic-4-tariff-management.md`, add a new Story 4.4 to the same epic file, update PRD FR-10/11/12 and two Glossary entries, and update `architecture.md`'s three affected references. No rollback of shipped code (most of the Story 4.1/4.2/4.3 structure survives unchanged — `GetTariffsFunction`/`CreateTariffFunction`/`TariffForm`/`TariffList` keep their shape); no MVP/scope reduction.

- **Effort:** Medium — one migration, two resolvers, one Function simplification, ~7 test files, one form simplification.
- **Risk:** Low–Medium — contained to the `Tariffs` slice; `TariffResolver`'s public contract is untouched so downstream epics (6, 7) don't feel it; single-user data migration.
- **Sequencing:** Story 4.4 must complete, and Epic 4 close out, before Epic 5 begins (per the Epic 4 retrospective's Action Item #1).

## 4. Detailed Change Proposals

### PRD (`_bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md`)

**FR-10 — Tariff configuration**
```
OLD: ...specifying: effective date (required), fixed monthly base fee..., price
per kWh (required), provider name (optional), contract start date (optional),
and contract duration in months...(optional).

NEW: ...specifying: contract start date (required) — the date this price takes
effect, whether in the past, present, or future — fixed monthly base fee...,
price per kWh (required), provider name (optional), and contract duration in
months...(optional, informational reminder only — see FR-11).
```

**FR-11 — Period-locked Tariff prices**
```
OLD: When a Tariff entry includes a contract start date and contract duration,
the entry's prices cannot be modified once the Contract Period has started.

NEW: Every Tariff entry's contract start date determines whether its price
fields are locked. If the contract start date is on or before today, price
fields require explicit override confirmation before they can be modified. If
the contract start date is in the future, price fields remain freely editable
— no consumption has been costed against them yet. Contract duration, if
provided, does not affect this lock; it is stored only as a reminder to review
the tariff around contract renewal.
```

**FR-12 — Future Tariff pre-entry**
```
OLD: ...create a Tariff entry with a future effective date. This does not
alter any cost calculations for periods before that effective date.

NEW: ...create a Tariff entry with a future contract start date. This does not
alter any cost calculations for periods before that date, and its price
fields remain freely editable per FR-11 until the date arrives.
```

**Glossary**
```
Tariff — OLD: "...with an effective date. Multiple Tariff entries form a
history; each covers its period until the next entry's effective date."
       — NEW: "...with a required contract start date. Multiple Tariff
entries form a history; each covers its period until the next entry's
contract start date."

Contract Period — OLD: "...While the Contract Period is active, that Tariff's
prices are locked."
                — NEW: "...stored as a reminder to review the tariff around
contract renewal — it has no effect on price locking (FR-11)."
```

**New scope note** (§4.4 description): "Dynamic/variable-rate tariffs with no fixed price per kWh are explicitly out of scope."

### Architecture (`_bmad-output/planning-artifacts/architecture.md`)

- Data model table: `Tariffs` row — `EffectiveDate` → `ContractStartDate` (datetimeoffset, required, unique natural key); `ContractDurationMonths` annotated "informational only, no locking effect."
- Naming convention example: `IX_Tariff_FlatId_EffectiveDate` → `IX_Tariffs_FlatId_ContractStartDate`.
- `TariffResolver` doc comment: note it resolves by `ContractStartDate`.

### Epic 4 (`_bmad-output/planning-artifacts/epics/epic-4-tariff-management.md`)

- Story 4.1: AC1–AC7 amended per Section 3.4 discussion above — field rename + required, 409/unique index moved, lock condition simplified, **mutual-exclusion PATCH rule removed**.
- Story 4.3: AC1 lock condition simplified (drop `ContractDurationMonths` requirement); AC2/AC3 override mechanism unchanged; AC6 helper text re-keyed to `ContractStartDate`.
- New **Story 4.4: Tariff Contract-Date Consolidation** — migration + backfill, `TariffResolver`/`KpiCalculator.ResolveTariff` re-keyed, `TariffLockPolicy` simplified, `PatchTariffFunction` simplified (single PATCH call, no mutual exclusion), frontend `TariffForm`/`TariffList`/`TariffLockIndicator` updated, closes `deferred-work.md` W4 and the Story 4.1 cross-field-validation-gap note, full test rework.

### `sprint-status.yaml`

- Add `4-4-tariff-contract-date-consolidation: backlog` under Epic 4.

## 5. Implementation Handoff

**Scope classification: Moderate** — epic-file and PRD text changes are applied directly (below); the actual code/migration/test work is a new story requiring standard `bmad-dev-story` execution, not a fundamental replan.

- **Routed to:** Developer agent (`bmad-agent-dev` / `bmad-dev-story`) — implement Story 4.4 once created.
- **Success criteria:** migration applied with correct backfill (verified against the one real affected tariff — dashboard shows correct coverage post-fix); `TariffResolver` and `KpiCalculator.ResolveTariff` both resolve by `ContractStartDate`; `PatchTariffFunction` accepts combined price + non-price fields in one request again; all 7 affected test files updated and green; `deferred-work.md` W4 and the cross-field-validation note marked resolved.

## Status

Approved by Ralf (2026-07-03), incrementally, section by section. PRD, Architecture, and Epic 4 file edits applied directly as described above.
