# Smart Plug Export Formats

Canonical format reference for CAP-6 (smart plug file import). Documents the exact structure of Eve Home and Meross exports as observed from sample files. The parser must handle both formats and produce a unified consumption timeline in daily kWh.

---

## Eve Home — Excel (.xlsx)

**File naming:** user-chosen export name; no enforced pattern. Example: `2026-06-20_Steckdose_HiFi_Gesamtverbrauch.xlsx`

**Sheet:** single sheet, always named `Gesamtverbrauch`.

**Structure:**

| Row | Column A | Column B |
|-----|----------|----------|
| 1 | `Gerät: {device_name}` | _(empty)_ |
| 2 | `Raum: {room_name}` | _(empty)_ |
| 3 | `Zuhause: {home_name}` | _(empty)_ |
| 4 | `Datum` _(header)_ | `Gesamtverbrauch (Wh)` _(header)_ |
| 5+ | `datetime` | `float` |

**Data rows (row 5 onward):**
- **Column A (`Datum`):** Python `datetime` with date and time; ~10-minute sampling interval.
- **Column B (`Gesamtverbrauch (Wh)`):** Energy consumed in that ~10-minute interval, in **Wh** (watt-hours). Values are small floats (e.g. `1.08`, `0.82`). Not cumulative — each row is independent.
- **Order:** reverse chronological (newest row first).

**Metadata extraction:**
- Device name: strip `"Gerät: "` prefix from cell A1.
- Room name: strip `"Raum: "` prefix from cell A2. (Informational; the user assigns the plug to a power point via the flat structure, overriding this.)
- Home name: strip `"Zuhause: "` prefix from cell A3. (Ignore for import; single-flat scope in v1/v2.)

**Aggregation to daily kWh:**
Sum all interval Wh values for a calendar day (local date of the `Datum` timestamp), then divide by 1000.

**Edge cases to handle:**
- Multiple exports for the same plug covering overlapping periods: deduplicate by timestamp before aggregating.
- Rows with `None` / empty values: skip.
- Timezone: timestamps appear to be local time; treat as-is (no UTC conversion).

---

## Meross — CSV (.csv)

**File naming:** enforced pattern: `Power Monitor Day Data - {device_name} - {YYYYMMDD}.csv`
- Device name is encoded in the filename only; there is no in-file device metadata.
- Example: `Power Monitor Day Data - Schreibtisch - 20260620.csv`

**Encoding:** UTF-8, may include a BOM (`﻿`). Parser must strip BOM.

**Delimiter quirk:** fields are tab-separated, but the value column is prefixed with a literal comma and trailing whitespace. Effective structure per line: `{date}\t,{value}\t`

**Structure:**

| Column (as parsed after stripping `\t,` quirk) | Content |
|---|---|
| `Date` | `YYYY-MM-DD` string |
| `Power Consumption-(kWh)` | `float`, daily kWh consumed |

**Header row:** `Date\t,Power Consumption-(kWh)\t` — strip the `\t,` to normalize column names before parsing.

**Data rows:**
- **Date:** `YYYY-MM-DD`, forward chronological order.
- **Power Consumption-(kWh):** daily aggregate in **kWh** — no further aggregation needed.
- Values may be `0.000` for days the device was unplugged or powered off.

**Metadata extraction:**
- Device name: parse from filename using pattern `Power Monitor Day Data - (.+) - \d{8}\.csv`. No room information available.

**Edge cases to handle:**
- BOM on first byte: strip before parsing.
- Trailing whitespace/tab on each line: strip before splitting.
- Empty rows at end of file: skip.

---

## Unified timeline contract

Both formats must be normalized to this structure before reconciliation with the main meter:

```
{
  "plug_id": str,          // user-assigned ID from flat structure
  "device_name": str,      // from file metadata / filename
  "date": date,            // calendar date (local)
  "kwh": float             // energy consumed on that date
}
```

Gap detection: if a plug's timeline has missing dates within its active period (first date → last date in the export), those gaps are flagged and projected from the plug's recent trend (linear interpolation over the gap, capped at the per-day average of the 7 days before the gap).
