# Reconciliation: SPEC + Companions vs prd.md

## Source files
- SPEC.md
- smart-plug-formats.md
- locale-formats.md

---

## Gaps found

### 1. Eve Home timestamps are local time — no UTC conversion
**Source (smart-plug-formats.md):** "Timezone: timestamps appear to be local time; treat as-is (no UTC conversion)." This is an explicit parsing rule: the `Datum` column values must NOT be interpreted as UTC or converted.
**PRD:** FR-24 does not mention timezone handling for Eve Home timestamps at all. The NFR-3 and Constraint both mandate ISO 8601 with timezone offset for stored datetimes, which could mislead an implementer into applying UTC normalization at parse time, breaking daily-boundary aggregation.
**Impact:** If an implementer applies UTC conversion during aggregation, days will be split at midnight UTC rather than local midnight, corrupting daily kWh totals and reconciliation figures. The omission is a real implementation risk, especially given that NFR-3 actively pushes toward UTC everywhere else.

---

### 2. Eve Home home name (cell A3) must be extracted and then explicitly ignored
**Source (smart-plug-formats.md):** Row 3 / cell A3 contains `Zuhause: {home_name}`. The companion specifies: "strip `'Zuhause: '` prefix from cell A3. (Ignore for import; single-flat scope in v1/v2.)" The extract-then-ignore rule matters because the parser must not error if the cell is present, and must not use the value for flat assignment.
**PRD:** FR-24 lists device name (A1) and room name (A2), but makes no mention of cell A3 / home name. A parser implementation built solely from FR-24 may either break on the unexpected third metadata row or, worse, attempt to use it for flat matching.
**Impact:** Parser correctness and future-proofing risk. If the parser errors on A3 content, all Eve Home imports fail. If it silently uses A3 for flat scoping, it introduces an incorrect cross-flat assignment path.

---

### 3. Meross: zero-value rows (device unplugged) are valid data, not gaps
**Source (smart-plug-formats.md):** "Values may be `0.000` for days the device was unplugged or powered off." This is a positive assertion: `0.000` is a legitimate daily kWh entry, not a missing day requiring interpolation.
**PRD:** FR-25 and FR-26 together do not distinguish between a missing date (a gap requiring interpolation) and a `0.000` value row (a present, valid zero-consumption day). Gap detection logic in FR-26 operates on "missing dates"; an implementer without the companion may incorrectly flag `0.000` Meross rows as gaps.
**Impact:** Incorrect interpolation of legitimate zero-consumption days inflates attributed kWh for those days, breaks the ±0.1 kWh reconciliation tolerance (FR-27/SM-3), and generates spurious gap notifications to the user.

---

### 4. Locale-formats.md currency placement and time format are not reproduced in the PRD
**Source (locale-formats.md):** Specifies symbol-after-value with a space for de-DE (`1,27 €`), symbol-before-value for en-US (`$1.27`); 24-hour `HH:mm` for de-DE, 12-hour `h:mm AM/PM` for en-US.
**PRD:** FR-41 says "formatted according to the active Locale's conventions as defined in `locale-formats.md`" and its only testable consequence is "renders with the correct symbol, separators, and precision." The symbol position (before vs. after the amount), the space between amount and symbol in de-DE, and the 12-hour/24-hour distinction for time are not stated anywhere in the PRD itself.
**Impact:** Downstream agents (UX designer, developer) consuming only the PRD lack the format contract needed to build and test correct rendering. Without these specifics the testable consequences in FR-41 cannot be evaluated without also reading the companion, defeating the PRD's purpose as a self-contained requirements document. Story acceptance criteria will be underspecified.

---

### 5. CAP-1 performance success criterion requires three confirmed test entries — absent from PRD
**Source (SPEC.md, CAP-1):** "server-side processing time (from request received to response dispatched, client network excluded) is within the time budget; confirmed across three test entries."
**PRD:** NFR-1 defines the ≤2s Tier 1 budget and FR-8 references "the performance budget defined in §NFR-1," but the validation requirement — three independently confirmed entries — is not stated anywhere in the PRD.
**Impact:** Without the three-entry confirmation requirement, a single favorable timing result could be accepted as passing. The SPEC's intent is to guard against a lucky outlier; the PRD's omission weakens the test bar for SM-1 and FR-8.

---

### 6. Annual kWh Baseline serves two distinct purposes; PRD only documents one
**Source (SPEC.md, CAP-11):** The annual kWh estimate is stated to serve two roles: (a) "a baseline for invoice deviation hints" and (b) the budget pressure alert in CAP-10 ("rolling projections exceed the user's planned annual spend").
**PRD:** FR-37 (budget pressure alert) captures role (b). FR-5 and FR-7 describe storing the Annual kWh Baseline. However, "invoice deviation hints" — role (a), a distinct UX signal separate from a budget alert — is never described as a feature or consequence in the PRD. The word "invoice" appears only in the Vision section as a goal, not as a driven feature requirement.
**Impact:** If no FR covers invoice deviation hints, neither the UX designer nor the developer will build them. The SPEC's CAP-11 success criterion ("the annual kWh baseline is used in CAP-10 invoice deviation logic") requires this path to be implemented and testable. The gap means an entire category of insight is silently dropped.

---

### 7. Unified timeline internal contract (plug_id linkage to flat structure) is unspecified
**Source (smart-plug-formats.md):** The unified timeline contract specifies `plug_id` as a "user-assigned ID from flat structure," making the link from parsed smart plug data back to the flat structure an explicit architectural requirement. Gap detection scope is also defined here: "first date → last date in the export" (not the flat's full history).
**PRD:** FR-24/FR-25 describe parsing to "a daily kWh timeline per plug" but do not specify how a parsed plug is linked to a Power Point in the Flat Structure, nor that `plug_id` is user-assigned rather than derived from file metadata. FR-26 correctly scopes gap detection to the export's active range, but the internal contract connecting file imports to Flat Structure via a user-assigned identifier is absent.
**Impact:** Without this linkage specified, the architect may design the import-to-structure association differently (e.g., by matching device name strings from the file), which would break for devices with identical names in different rooms and would not survive a device rename. Downstream epics and stories for smart plug import will lack the data-model constraint needed to implement or test the association correctly.
