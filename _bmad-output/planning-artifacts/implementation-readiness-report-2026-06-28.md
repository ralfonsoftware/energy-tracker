---
stepsCompleted: [1, 2, 3, 4, 5, 6]
filesUsed:
  prd: _bmad-output/planning-artifacts/prds/prd-energy-tracker-2026-06-20/prd.md
  architecture: _bmad-output/planning-artifacts/architecture.md
  epics: _bmad-output/planning-artifacts/epics.md
  ux_design: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/DESIGN.md
  ux_experience: _bmad-output/planning-artifacts/ux-designs/ux-energy-tracker-2026-06-20/EXPERIENCE.md
---

# Implementation Readiness Assessment Report

**Date:** 2026-06-28
**Project:** energy-tracker

---

## PRD Analysis

### Functional Requirements

FR-1: Auth gate — unauthenticated requests redirect to OIDC login; return to originally requested route.
FR-2: Session persistence — authenticated sessions survive browser restarts until expiry/sign-out.
FR-3: Configurable identity provider — provider swappable via config only, no code changes.
FR-4: First-use onboarding gate — no main feature accessible until Flat name + Annual kWh Baseline + Tariff configured; planned annual spend auto-derived and editable.
FR-5: Annual kWh Baseline entry — specific value OR 4 household-size presets (1p≈1500, 2p≈2500, 3p≈3500, 4p≈4250 kWh).
FR-6: Onboarding Tariff entry — monthly base fee + price/kWh required; provider, contract start date, contract duration optional.
FR-7: Onboarding settings editability — Flat name, Annual kWh Baseline, Tariff, planned annual spend all editable from Settings.
FR-8: Meter Reading submission — kWh numeric, stored with timestamp; ≤2s server-side.
FR-9: Retroactive Reading — past date accepted; costed at Tariff active on that date.
FR-10: Tariff configuration — effective date, monthly base fee, price/kWh required; provider, contract start, duration optional.
FR-11: Period-locked Tariff prices — price fields locked once Contract Period started.
FR-12: Future Tariff pre-entry — future effective date accepted; no effect on past cost figures.
FR-13: Period-accurate historical costing — all calculations use Tariff active on consumption date.
FR-14: KPI Dashboard display — daily avg kWh, weekly avg kWh, daily cost, weekly cost, projected monthly cost.
FR-15: Immediate Dashboard update — figures update after new Reading without page refresh.
FR-16: Consumption trend visualization — bar chart of historical daily consumption.
FR-17: Spike detection — days exceeding configurable threshold (default 2×) above 7-day rolling average; amber bars in chart; user-configurable per Flat.
FR-18: Multiple Flat support — each Flat has own Readings, Tariffs, Smart Plug Data, Flat Structure.
FR-19: Flat switcher — header component switches active Flat; active Flat name displayed.
FR-20: Last active Flat persistence — last active Flat restored on session return.
FR-21: Flat Structure definition — Flat → Rooms → Power Points → Devices; Smart Plug/Strip assigned to exactly one Power Point; plug_id never from file metadata.
FR-22: Default room template — 5 default rooms (living room, bedroom, kitchen, bathroom, hallway) pre-populated on first structure setup.
FR-23: Flat deletion with cascade — permanent; all associated data removed; no orphans.
FR-24: Eve Home Excel import — single sheet "Gesamtverbrauch", reverse-chronological ~10-min Wh rows; timestamps as local time; overlapping exports deduplicated.
FR-25: Meross CSV import — UTF-8+BOM, tab-separated, per-value comma prefix; BOM/whitespace/empty rows stripped; zero-value rows stored as valid.
FR-26: Gap detection and linear interpolation — mid-period gaps detected, user notified, filled by linear interpolation capped at 7-day pre-gap average; interpolated values marked.
FR-27: Smart Plug reconciliation — attributed kWh + Residual = Main Meter total (±0.1 kWh clean; ±1.0 kWh with interpolated values).
FR-28: Import error categorization — 3 user-facing categories: DataUnreadable / ProcessingFailed / ServiceUnavailable; raw errors never exposed.
FR-29: Device metadata registration — Type, Manufacturer, Model required; PurchaseDate optional; assigned to Power Point.
FR-30: EU energy label consumption — energy class + annual kWh from label; daily estimate derived; marked "Estimated" in Decomposition.
FR-31: Self-measured consumption — daily or weekly kWh average; marked "Estimated" in Decomposition.
FR-32: Decomposition view — consumption per Room and Device for periods with Smart Plug Data; Smart Power Strip authoritative total with proportional sub-device shares.
FR-33: Residual always shown — present in Decomposition even when zero; never suppressed.
FR-34: Decomposition unavailable state — periods without Smart Plug Data show "unavailable" with import prompt; no zeros or partial figures.
FR-35: High-standby offender detection — Eve Home interval data only; Devices drawing >2W outside usage window; named with monthly cost.
FR-36: Replacement candidate detection — high-consumption Device with quantifiable payback; named with current cost and savings.
FR-37: Budget pressure alert — rolling monthly projection × 12 exceeds planned annual spend.
FR-38: Scheduled and manual insight discovery — daily at 02:00 UTC; manual trigger from Insights page; prior insights visible during new run.
FR-39: Discovery progress indicator — visible for full duration of discovery run.
FR-40: Locale selection — de-DE / en-US; immediate effect; stored server-side; restored across browsers.
FR-41: Locale-aware rendering — Intl.NumberFormat / Intl.DateTimeFormat; no hardcoded locale-specific formatting.
FR-42: Locale-neutral storage — ISO 8601 datetimes with offset, decimal-point numbers, fixed-decimal currency.
FR-43: Invoice deviation hint — rolling annual kWh ±10% vs Annual kWh Baseline; shows projected kWh, baseline, euro difference.

**Total FRs: 43**

### Non-Functional Requirements

NFR-1: Performance — Tier 1 ≤2s (standard server actions), Tier 2 ≤30s with UI hint, Tier 3 fully background (blob import, insight discovery). KPI Dashboard ≤2s for 2 years of Readings.
NFR-2: Security and Data Isolation — full tenant isolation by user ID; no unauthenticated endpoints except OIDC callback; currency as fixed-decimal `decimal` only.
NFR-3: Internationalization — all UI text through localization framework; all data stored locale-neutrally; locale preference stored server-side; formatting via Intl APIs at render time only.
NFR-4: Reliability and Async Processing — Blob Storage + blob-triggered Function for smart plug import; Azure Storage Queue for internal messaging; Azure SQL Basic DTU as persistent store.

**Total NFRs: 4**

### Additional Requirements (Architecture cross-cutting)

- Monorepo: Vite + React + TypeScript frontend; .NET 10 Azure Functions isolated worker backend
- Azure SQL + EF Core code-first migrations; Fluent API only (no Data Annotations on entities)
- SWA Easy Auth + TenantResolver middleware; X-MS-CLIENT-PRINCIPAL header
- Managed Identity for all service-to-service connections (DefaultAzureCredential)
- Vertical Slice Architecture (VSA) — backend and frontend feature folders mirror each other
- C# `record` types for all DTOs; entities as regular classes
- REST API versioned at `/api/v1/`; Problem Details RFC 9457 for all errors
- TanStack Query v5 for all frontend server state
- react-hook-form + zod, one schema per form co-located
- react-i18next with i18next-browser-languagedetector; namespace-split translation files
- GitHub Actions CI/CD; Application Insights attached to Functions app

---

## Epic Coverage Validation

### Coverage Matrix

| FR | Epic Coverage | Status |
|----|--------------|--------|
| FR-1 | Epic 1 — Story 1.4 (SWA Easy Auth) | ✓ Covered |
| FR-2 | Epic 1 — Story 1.4 | ✓ Covered |
| FR-3 | Epic 1 — Story 1.4 | ✓ Covered |
| FR-4 | Epic 2 — Story 2.2 (Onboarding Gate & Intro Screen) | ✓ Covered |
| FR-5 | Epic 2 — Story 2.4 (Onboarding Step 2) | ✓ Covered |
| FR-6 | Epic 2 — Story 2.4 | ✓ Covered |
| FR-7 | Epic 2 — Story 2.5 (Settings) | ✓ Covered |
| FR-8 | Epic 3 — Story 3.1 | ✓ Covered |
| FR-9 | Epic 3 — Story 3.1 | ✓ Covered |
| FR-10 | Epic 4 — Story 4.1 | ✓ Covered |
| FR-11 | Epic 4 — Story 4.1 | ✓ Covered |
| FR-12 | Epic 4 — Story 4.1 | ✓ Covered |
| FR-13 | Epic 4 — Story 4.1 | ✓ Covered |
| FR-14 | Epic 3 — Story 3.2/3.3 | ✓ Covered |
| FR-15 | Epic 3 — Story 3.4 | ✓ Covered |
| FR-16 | Epic 3 — Story 3.5 | ✓ Covered |
| FR-17 | Epic 3 — Story 3.5 | ✓ Covered |
| FR-18 | Epic 5 — Story 5.1 | ✓ Covered |
| FR-19 | Epic 5 — Story 5.2 | ✓ Covered |
| FR-20 | Epic 5 — Story 5.2 | ✓ Covered |
| FR-21 | Epic 5 — Story 5.3/5.4 | ✓ Covered |
| FR-22 | Epic 5 — Story 5.4 | ✓ Covered |
| FR-23 | Epic 5 — Story 5.1 | ✓ Covered |
| FR-24 | Epic 6 — Story 6.2 | ✓ Covered |
| FR-25 | Epic 6 — Story 6.3 | ✓ Covered |
| FR-26 | Epic 6 — Story 6.4 | ✓ Covered |
| FR-27 | Epic 6 — Story 6.4 | ✓ Covered |
| FR-28 | Epic 6 — Story 6.1 | ✓ Covered |
| FR-29 | Epic 5 — Story 5.3/5.4 | ✓ Covered |
| FR-30 | Epic 6 — Story 6.5 | ✓ Covered |
| FR-31 | Epic 6 — Story 6.5 | ✓ Covered |
| FR-32 | Epic 7 — Story 7.1/7.3 | ✓ Covered |
| FR-33 | Epic 7 — Story 7.2 | ✓ Covered |
| FR-34 | Epic 7 — Story 7.2 | ✓ Covered |
| FR-35 | Epic 8 — Story 8.2 | ✓ Covered |
| FR-36 | Epic 8 — Story 8.2 | ✓ Covered |
| FR-37 | Epic 8 — Story 8.3 | ✓ Covered |
| FR-38 | Epic 8 — Story 8.1 | ✓ Covered |
| FR-39 | Epic 8 — Story 8.4 | ✓ Covered |
| FR-40 | Epic 2 — Story 2.1 (i18n Infrastructure) | ✓ Covered |
| FR-41 | Epic 2 — Story 2.1 | ✓ Covered |
| FR-42 | Epic 2 — Story 2.1 | ✓ Covered |
| FR-43 | Epic 8 — Story 8.3 | ✓ Covered |

### Missing Requirements

None. All 43 FRs are covered.

### Coverage Statistics

- Total PRD FRs: 43
- FRs covered in epics: 43
- Coverage percentage: **100%**

---

### PRD Completeness Assessment

The PRD is complete and well-structured. All 43 FRs have testable consequences. All open questions are resolved. The FR Coverage Map in the Epics document maps every FR to an Epic unambiguously. For **Epic 2**, the relevant FRs are: **FR-4, FR-5, FR-6, FR-7** (Onboarding) and **FR-40, FR-41, FR-42** (Localization) — all are R1 requirements.

---

## UX Alignment Assessment

### UX Document Status

Found — two documents:
- `DESIGN.md` (visual identity, tokens, component specs)
- `EXPERIENCE.md` (information architecture, state patterns, key flows, microcopy)

Both are status: final, dated 2026-06-20.

### Epic 2 UX Coverage

| UX Requirement | Source | Story Coverage | Status |
|---|---|---|---|
| UX-DR20: 3-screen onboarding flow (Intro → Step 1 → Step 2) with gate | EXPERIENCE.md Flow 0, State Patterns, Component Patterns | Stories 2.2, 2.3, 2.4 | ✓ Aligned |
| UX-DR20: Onboarding gate blocks all main routes until setup complete | EXPERIENCE.md "Onboarding — new user gate" state | Story 2.2 | ✓ Aligned |
| UX-DR20: Step indicator (Intro / Step 1 / Step 2) | EXPERIENCE.md Component Patterns "Onboarding step indicator" | Story 2.2 | ✓ Aligned |
| UX-DR19: Microcopy — instrument register, arrow-first deltas, no coaching | EXPERIENCE.md Voice and Tone, microcopy table | Stories 2.2–2.5 | ✓ Aligned |
| UX-DR19: Specific onboarding copy — "Know what your energy costs, every day." | EXPERIENCE.md microcopy table (Onboarding value prop) | Story 2.2 | ✓ Aligned |
| Locale dropdown: top-right of Intro screen (DE ▾ / EN ▾) | EXPERIENCE.md Interaction Primitives | Story 2.2 | ✓ Aligned |
| Locale dropdown: Settings → Language & Region | EXPERIENCE.md IA table | Story 2.5 | ✓ Aligned |
| Intl.NumberFormat / Intl.DateTimeFormat render-time only | Epics FR-41/FR-42, Architecture NFR-3 | Story 2.1 | ✓ Aligned |

### Alignment Issues

⚠️ **Minor documentation inconsistency — Locale persistence storage:**
- `EXPERIENCE.md` (Interaction Primitives, locale dropdown entry) states: *"Persists to browser-local storage."*
- PRD FR-40 states: *"once selected, the override is stored server-side in the user profile."*
- Architecture NFR-3 states: *"The user's locale selection is stored server-side in the user profile."*
- Story 2.1 AC explicitly implements `PUT /api/v1/user/settings` → `Users.LocaleOverride` column.

**Resolution:** The PRD, Architecture, and Story 2.1 are internally consistent. EXPERIENCE.md was authored before the server-side storage decision was finalized (EXPERIENCE.md date: 2026-06-20; architecture date: 2026-06-21). **Story 2.1 is the authoritative specification — implement server-side storage as written.** The EXPERIENCE.md phrase can be treated as stale.

### Warnings

None beyond the above inconsistency, which is already resolved by the story AC.

---

## Epic Quality Review

### Epic 2 — User Value Check

**Title:** "Onboarding & Locale Selection" — user-centric ✓
**Goal:** First-time user completes onboarding and selects locale — clear user outcome ✓
**Standalone value:** Yes — completes the app setup barrier; no main feature accessible otherwise ✓
**Independence:** Builds only on Epic 1 (app shell + auth). Epic 3 builds on Epic 2 (needs Flat to exist). Correct ordering ✓

### Story-by-Story Assessment

#### Story 2.1: i18n Infrastructure & Locale Settings API
**User value:** Dual-purpose (infrastructure + user-visible locale behavior). Acceptable — i18n and the settings API are inseparable: both must exist before any onboarding screen can render correctly.
**Independence:** Completable after Epic 1 only. No forward dependencies ✓
**Sizing:** Medium — 2 backend endpoints + full frontend i18n scaffold. Appropriately bounded ✓
**AC quality:** All ACs are Given/When/Then with specific, testable outcomes ✓
**DB impact:** Adds `Users.LocaleOverride` column via EF Core migration — first and correct place for it ✓
**Verdict:** ✅ Ready for dev

---

#### Story 2.2: Onboarding Gate & Intro Screen
**User value:** Clear — user sees the gate and intro screen ✓
**Independence:** Within-epic dependency on Story 2.1 (`hasFlat` from settings API) — correct sequential ordering ✓
**Sizing:** Medium — routing guard + intro screen + locale dropdown with server persist ✓
**AC quality:** All ACs testable with specific assertions (route interception, tab bar visibility, intro copy, step indicator) ✓
**Verdict:** ✅ Ready for dev

---

#### Story 2.3: Onboarding Step 1 — Flat Name
**User value:** Clear — user names their flat ✓
**Independence:** Within-epic dependency on 2.2 (onboarding flow exists). Correct ✓
**Sizing:** Small — single form step with client state only. No backend call ✓
**AC quality:** Clean BDD — auto-focus, continue disabled until non-empty, client-state hold, back-navigation preserves value, visual spec ✓
**Verdict:** ✅ Ready for dev

---

#### Story 2.4: Onboarding Step 2 — Energy Contract & Completion
**User value:** Highest value in the epic — completes setup, enables the entire product ✓
**Independence:** Correct sequential dependency on 2.2 and 2.3 ✓
**Sizing:** Large but justified — creates foundational Flats + Tariffs entities, the single onboarding endpoint, FluentValidation, and the full Step 2 UI ✓
**AC quality:** Most ACs are precise and testable. Entity field types and FluentValidation rules are explicit ✓

🟠 **Major Issue — Tariffs entity/migration scope ambiguity:**
Story 2.4 writes a Tariffs record (creates the Tariffs table via EF Core migration) but does not specify the full `TariffConfiguration`. The complete Tariff schema — including `ContractStartDate`, `ContractDurationMonths`, `ProviderName`, index `IX_Tariffs_FlatId_EffectiveDate` — is only formally specified in Story 4.1.

A developer implementing Story 2.4 must decide: (a) create the complete Tariffs schema now (anticipating Story 4.1's needs) or (b) create a minimal schema and add columns via a second EF Core migration in Story 4.1.

**Recommendation:** Story 2.4 should explicitly instruct the developer to create the *complete* Tariffs EF Core entity and migration (matching Story 4.1's `TariffConfiguration` spec: all columns including optional ones, and the `IX_Tariffs_FlatId_EffectiveDate` index), since adding columns to an existing table later is a non-breaking but avoidable extra migration step. The AC stating "creates a Tariffs record (EffectiveDate = today as datetimeoffset, all monetary values as decimal)" should be augmented to reference Story 4.1's full schema or inline the complete column list.

**Verdict:** 🔶 Conditionally ready — confirm with developer how Tariffs migration scope is handled before starting

---

#### Story 2.5: Settings — Flat Name, Annual kWh Baseline & Locale
**User value:** Clear — user can refine their setup from Settings ✓
**Independence:** Builds on 2.1 (settings API) and 2.4 (Flat exists). Correct ✓
**Sizing:** Medium — Settings root screen + flat name edit + kWh baseline edit + locale switching ✓
**AC quality:** Generally good. Two gaps identified:

🟡 **Minor Concern 1 — PATCH endpoint URL unspecified:**
The AC says "saving sends a PATCH/PUT to the appropriate settings endpoint" without specifying the endpoint URL or verb. Story 4.1 defines `PATCH /api/v1/flats/{flatId}/tariffs/{tariffId}` for Tariff edits, but there is no equivalent endpoint defined anywhere for `PATCH /api/v1/flats/{flatId}` (flat name and baseline updates). The developer must invent this endpoint. **Recommended:** Clarify that `PATCH /api/v1/flats/{flatId}` accepts `{ name?, annualKwhBaseline?, plannedAnnualSpend? }` and returns HTTP 200 with the updated Flat.

🟡 **Minor Concern 2 — Sign Out behavior unspecified:**
The Settings root AC mentions "Account section shows a Sign Out action" but specifies no endpoint, redirect target, or post-sign-out behavior. SWA Easy Auth provides `/.auth/logout` for sign-out. **Recommended:** Add AC: "Given the user taps Sign Out, When the action fires, Then the browser is redirected to `/.auth/logout` which clears the SWA session and redirects to the OIDC provider logout or the app root."

**Verdict:** 🔶 Conditionally ready — both gaps are minor but should be agreed before implementation to avoid rework

---

### Best Practices Compliance

| Check | Status |
|---|---|
| Epics deliver user value | ✅ Pass |
| Epic can function independently (after Epic 1) | ✅ Pass |
| No forward dependencies to Epic 3+ | ✅ Pass |
| Stories sized appropriately | ✅ Pass |
| DB tables created when first needed | ✅ Pass (Flats + Tariffs in Story 2.4) |
| Acceptance criteria in Given/When/Then | ✅ Pass |
| Traceability to FRs maintained | ✅ Pass |
| No circular dependencies | ✅ Pass |

### Quality Findings Summary

| Severity | Finding | Story |
|---|---|---|
| 🟠 Major | Tariffs EF Core migration scope ambiguous — full schema vs. minimal schema decision needed before implementation | 2.4 |
| 🟡 Minor | PATCH endpoint URL for flat settings unspecified | 2.5 |
| 🟡 Minor | Sign Out behavior and redirect target unspecified | 2.5 |

---

## Summary and Recommendations

### Overall Readiness Status

**CONDITIONALLY READY** — 3 issues found (1 major, 2 minor). No blocking issues. Epic 2 can proceed to development after resolving the Tariffs migration scope question. The two minor Story 2.5 gaps can be resolved during the Story 2.5 kick-off rather than before Story 2.1 begins.

### Issues Requiring Attention

#### Before Story 2.4 Starts

**[MAJOR] Tariffs EF Core migration scope — Story 2.4**

Story 2.4 creates the Tariffs table but does not specify the full schema. Story 4.1 defines the complete `TariffConfiguration` (all columns, FK, index). A developer implementing Story 2.4 needs to know: should the *complete* Tariffs schema be created in 2.4, or a minimal subset?

**Recommendation:** Create the full Tariffs schema in Story 2.4. Full specification from Story 4.1:
- `TariffId` (guid PK), `FlatId` (FK to Flats, cascade delete), `EffectiveDate` (datetimeoffset), `PricePerKwh` (decimal), `MonthlyBaseFee` (decimal), `ProviderName` (nullable nvarchar), `ContractStartDate` (nullable datetimeoffset), `ContractDurationMonths` (nullable int)
- Index: `IX_Tariffs_FlatId_EffectiveDate`
- Zero Data Annotation attributes; all Fluent API

This avoids a second migration in Epic 4 for a table that already exists. Document this decision in the story or in a dev note before implementation begins.

#### Before Story 2.5 Starts

**[MINOR] PATCH endpoint for flat settings — Story 2.5**

Agree on the endpoint contract: `PATCH /api/v1/flats/{flatId}` with body `{ name?, annualKwhBaseline?, plannedAnnualSpend? }` returning HTTP 200 with updated Flat. This endpoint does not conflict with any other story's endpoint definitions.

**[MINOR] Sign Out endpoint — Story 2.5**

The Sign Out action should hit `/.auth/logout` (SWA Easy Auth built-in). The AC should specify: redirect to `/.auth/logout`, which terminates the SWA session. No backend code required — it's a simple anchor link or `window.location.href` assignment. Add this to the Story 2.5 AC before implementation.

### Recommended Next Steps

1. **Immediately:** Confirm Tariffs migration scope decision with the dev team and note it in Story 2.4 (or a shared dev note) before Sprint 2 begins
2. **Before Story 2.5 kick-off:** Agree on `PATCH /api/v1/flats/{flatId}` endpoint contract and add Sign Out AC
3. **Note for developer:** The EXPERIENCE.md locale dropdown entry ("Persists to browser-local storage") is stale — server-side storage via `Users.LocaleOverride` is the implementation target per Story 2.1 ACs and PRD FR-40
4. **Proceed:** Stories 2.1, 2.2, 2.3 are fully ready for dev with no outstanding questions

### Final Note

**Scope:** This assessment is scoped to Epic 2 (Stories 2.1–2.5) in the context of a Sprint 2 readiness check.

Assessment identified **3 issues** (1 major, 2 minor) across 2 stories. 100% FR coverage confirmed. UX alignment is strong with one known stale documentation entry that is already resolved in the story ACs.

**Stories 2.1, 2.2, and 2.3 are fully ready for dev.** Stories 2.4 and 2.5 have small pre-implementation decisions to confirm, both resolvable in a brief team discussion — they are not blockers to starting the sprint.

---
*Assessment performed: 2026-06-28 | Project: energy-tracker | Scope: Epic 2 (Sprint 2 readiness)*
