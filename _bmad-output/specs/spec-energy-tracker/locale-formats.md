# Locale Formats

Canonical rendering contract for CAP-12. Defines the exact output format for numbers, dates, times, and currency per supported locale. All data is stored and transmitted locale-neutrally (ISO 8601 dates with timezone, decimal-point numbers, currency as fixed-decimal values); locale formatting is applied only at render time.

---

## Supported Locales

### de-DE (German)

| Element | Format | Example |
|---|---|---|
| Decimal separator | Comma | `1,27` |
| Thousands separator | Period | `1.234,56` |
| Currency | Symbol after value, comma decimal | `1,27 €` |
| Date | dd.mm.yyyy | `12.04.2026` |
| Time | 24-hour, HH:mm | `14:12` |

### en-US (English — United States)

| Element | Format | Example |
|---|---|---|
| Decimal separator | Period | `1.27` |
| Thousands separator | Comma | `1,234.56` |
| Currency | Symbol before value, period decimal | `$1.27` |
| Date | mm/dd/yyyy | `04/12/2026` |
| Time | 12-hour, h:mm AM/PM | `2:12 PM` |

---

## Currency handling

- Currency follows the user's locale (€ for de-DE, $ for en-US).
- Tariff entry and all cost figures use the currency of the user's configured locale.
- Currency amounts are stored as fixed-decimal values (equivalent to C# `decimal`) to ensure precision; no floating-point representation of monetary values anywhere in the data layer.
- Currency symbol and formatting pattern are derived from the locale at render time; no hardcoded currency strings in application code.

---

## Storage and transfer contract

- **Dates/times:** ISO 8601 with explicit timezone offset (e.g. `2026-04-12T14:12:00+02:00`). All scheduled jobs operate in UTC. All datetime values stored and transferred with timezone information.
- **Numbers:** stored as decimal-point values (machine representation), formatted per locale only at render time.
- **Currency:** stored as fixed-decimal (no floating-point); locale determines symbol and separators at render time.
