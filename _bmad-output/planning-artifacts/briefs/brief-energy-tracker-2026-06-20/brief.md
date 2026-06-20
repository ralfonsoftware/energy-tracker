---
title: "Product Brief: energy-tracker"
status: final
created: 2026-06-20
updated: 2026-06-20
version: 2
---

# Product Brief: energy-tracker

## Executive Summary

energy-tracker is a personal energy monitoring web application that replaces the spreadsheet-based workflow of manually recording main meter readings with a purpose-built, mobile-friendly experience. It ingests readings from a flat's main power meter, calculates key usage metrics, and progressively breaks down total consumption to individual rooms and devices using data exported from smart plugs and EU energy label estimates.

The app translates raw kWh figures into euros using the user's configured tariff — enabling cost awareness, tariff comparison, and early warning when monthly spend trends toward an unexpectedly high annual invoice. It is built for a developer-owner running it on Azure, starting as a single-user app with architecture open to future multi-tenant use.

## The Problem

Working from home makes domestic energy consumption a material cost that is difficult to reason about without the right tools. The current approach — manual meter readings logged in a spreadsheet — works for capturing the raw total, but breaks down in three ways:

- **Mobile UX is poor.** Reading the meter in the basement and entering the value on a phone via a spreadsheet is friction-heavy.
- **No intelligence.** The spreadsheet holds numbers but delivers no trends, no anomaly detection, no cost projection.
- **No decomposition.** The main meter gives one blob of consumption. Without attribution to rooms, plugs, and devices, there is no actionable signal about where to save.

The result is a yearly energy invoice that can surprise — and a vague sense that standby draw or specific appliances are wasting money — with no way to confirm or quantify it.

## Who This Serves

**Primary: the developer-owner living alone or with flatmates, working from home.** Energy is a meaningful monthly cost. They are technically comfortable, motivated to understand their consumption, and willing to spend a few minutes per week entering readings and uploading exports. They want actionable signals, not raw data dashboards.

**Secondary (future): other households.** The architecture supports additional users managing their own flat or house. Multi-tenancy is not in scope for v1 but must not be designed out.

## What Makes This Different

This is not a general-purpose smart home platform. It is narrow, honest, and built around the constraints of a typical European flat: one main meter you read by hand, a mix of smart plug brands with incompatible export formats, and appliances you cannot instrument but know the label rating for. The key differentiators:

- **Hybrid data model.** Combines manual main meter readings, smart plug file exports (multi-brand), and EU energy label estimates into a single coherent picture — without requiring any hub, cloud subscription, or always-on hardware.
- **Cost-first.** Tariff is a first-class input. Every metric surfaces in euros, not just kWh. Tariff comparison is a built-in feature.
- **Residual-aware.** The app does not pretend full coverage. Unattributed consumption is explicit, not hidden — and shrinks as more plugs are added.
- **Developer-owned.** Self-hosted on Azure, no third-party subscription, full data ownership.

## The Solution

**Layer 1 — Main meter tracking.** The user manually enters readings from the flat's main meter. The app calculates daily and weekly average consumption in kWh and euros, detects unusual spikes, and visualizes trends over time. Tariff configuration (€/kWh + fixed monthly fee) makes every figure immediately meaningful in money terms.

**Layer 2 — Smart plug attribution.** The user uploads periodic exports from Eve Home smart plugs (Excel) and Meross smart plugs (CSV). The app parses these into a unified timeline and reconciles them against the main meter totals. A configurable flat structure (flat → rooms → power points → devices) maps each smart plug to a physical location, allowing total consumption to be decomposed as far as smart plug coverage allows. Devices known only by their EU energy label rating contribute an estimated baseline. Everything unattributed — induction hob, ad-hoc appliances — rolls into an "uncategorized" residual bucket.

**Layer 3 — Actionable insights.** With decomposed data, the app surfaces: devices with disproportionate standby draw (easy wins), high-consumption appliances where replacement offers a quantifiable payback, and budget signals when rolling averages suggest the annual total will exceed expectation.

## Success Criteria

- Main meter reading entry takes under 60 seconds on a mobile browser.
- Daily and weekly kWh and cost averages are visible immediately after entry.
- After uploading smart plug exports, consumption is attributed to rooms and devices without manual data wrangling.
- The app identifies at least one actionable insight (standby offender, high-cost device, budget alert) within the first month of real use.
- Annual invoice amount is predictable within 10% from rolling monthly data before the invoice arrives.
- Tariff changes are entered with an effective date; historical cost figures reflect the tariff that was active in each period.

## Scope

### Release 1 — Core Tracking

The first release delivers a usable replacement for the spreadsheet: meter reading entry with a mobile-first UX, KPI dashboard, cost translation, and spike detection.

**In:**
- Responsive web app (mobile-first UX)
- Manual main meter reading log with date/time
- KPI dashboard: daily average kWh, weekly average kWh, daily cost, weekly cost, projected monthly cost
- Spike detection and visualisation of consumption trends
- Tariff configuration: €/kWh + fixed monthly fee, stored with effective date (costs are period-locked — past periods keep the tariff active at the time)
- Secured access (authentication required)
- Hosted on Azure (Static Web App frontend, .NET Azure Functions backend)

**Out of Release 1:**
- Smart plug import, flat structure, device attribution, EU label entry, decomposition view (all in Release 2)
- Native iOS app
- Multi-user / multi-flat management UI
- Real-time monitoring
- Export / reporting

### Release 2 — Consumption Decomposition

The second release layers in smart plug data and the flat structure model, enabling the total consumption figure to be broken down to individual rooms and devices.

**In:**
- Smart plug data import: Eve Home (Excel) and Meross (CSV)
- Gap-filling: when smart plug exports don't cover a full period, missing data is projected from the current trend
- Flat structure definition: flat → rooms → power points → devices
- Device attribution: assign smart plug to a power point/device location
- EU energy label device entry: rated wattage → estimated baseline contribution for uninstrumented devices
- Consumption decomposition view: attributed vs. residual breakdown by room and device
- Insight layer: high standby draw flagging, high-consumption replacement candidates, budget pressure alerts

**Out of Release 2 (future):**
- Native Swift iOS app (targeting iPhone, potentially iPad and Apple TV)
- Direct smart plug API integration (Eve, Meross, Home Assistant) — removes file export step
- Tariff comparison wizard
- Multi-tenant hosted version

## Vision

energy-tracker becomes the single source of truth for household energy use. The web app gains a growing device library pre-populated with EU energy label data and richer visualisations. A native Swift iOS app follows, adding home screen widgets for at-a-glance daily cost and a quick reading entry shortcut — with iPad and Apple TV as further exploration targets. The architecture allows other households to run their own instance or use a hosted multi-tenant version.
