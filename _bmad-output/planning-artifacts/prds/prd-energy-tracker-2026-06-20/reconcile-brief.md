# Reconciliation: brief.md vs prd.md

## Source file
brief-energy-tracker-2026-06-20/brief.md

## Gaps found

### 1. "What Makes This Different" — product positioning section is absent
**Source:** The brief has an explicit section "What Makes This Different" articulating four differentiators: hybrid data model (manual + smart plug exports + EU label estimates without any hub/subscription/always-on hardware), cost-first orientation (euros not just kWh), residual-aware honesty (unattributed consumption is explicit, not hidden), and developer-owned self-hosting.
**PRD:** The §1 Vision absorbs some of this language, but the four-point differentiation framing is dissolved into generic prose. "Cost-first" is never stated as a design principle. "Residual-aware" appears as a glossary term and FR-33, but not as a positioning statement. The "no hub, no cloud subscription, no always-on hardware" constraint appears only once in §1 and is not reinforced as a design constraint elsewhere.
**Impact:** Without an explicit positioning section, downstream agents (architect, UX designer) have no anchor for trade-off decisions. When a feature tempts "smarter" automation or cloud integrations, there is no stated principle to push back with. The PRD should carry a short §1.x or sidebar that names these four differentiators as first-class product constraints.

### 2. The "working from home" context and emotional job-to-be-done are weakened
**Source:** The brief opens the problem with "Working from home makes domestic energy consumption a material cost that is difficult to reason about." It names the emotional outcome as a "vague sense that standby draw or specific appliances are wasting money — with no way to confirm or quantify it." The user wants to stop being anxious about something they cannot see.
**PRD:** §2.1 Jobs To Be Done includes the emotional JTBD "Stop being surprised by my annual energy invoice," which is good. But the "working from home" framing — the reason energy is a meaningful cost rather than just background noise — does not appear anywhere in the PRD. The insight "WFH = energy is now a professional cost, not just a utility" is the "why now" signal that makes this product timely.
**Impact:** UX copy, onboarding tone, and insight framing should all speak to someone who works from home and has shifted their relationship to household energy. Without this context, the product risks reading as a generic utility tracker rather than a WFH-era tool. It also weakens the case for why the 60-second mobile flow matters so urgently — the user is going to the basement on a workday morning.

### 3. Release scope reasoning ("why Release 2 is not Release 1") is not articulated
**Source:** The brief's scope section carries implicit reasoning: Release 1 is the "usable replacement for the spreadsheet" — a clear, independent unit of value. Release 2 adds decomposition, which requires a populated flat structure and smart plug history to be meaningful. The sequencing is motivated by standalone value at each stage.
**PRD:** §6 MVP Scope lists what is in each release with FR references, but does not state the rationale for the cut. A reader picking up the PRD cold does not know *why* smart plug import is Release 2, not Release 1 — whether it is effort, risk, dependency, or a deliberate decision to ship sooner with a simpler product.
**Impact:** Without stated rationale, the release boundary looks arbitrary to any agent or stakeholder who reads the PRD without the brief. It also makes the boundary easier to erode ("can we just squeeze the flat structure into R1?"). One sentence per release explaining the value-independence logic would close this gap.

### 4. The "residual shrinks as more plugs are added" progressive coverage model is absent
**Source:** The brief explicitly describes the residual bucket as one that "shrinks as more plugs are added," framing the product as one that progressively improves its picture of consumption over time — not a tool that only works once fully instrumented. The initial state (mostly unattributed) is expected and normal.
**PRD:** FR-33 requires the Residual to always be shown and never suppressed. FR-34 handles the unavailable state. But neither FR, nor the Decomposition description (§4.10), nor the Vision (§1) articulates the *progressive* nature of the model: that adding a new smart plug visibly shrinks the residual and expands the picture. The product experience of "each plug you add tells you something new" is entirely absent.
**Impact:** This framing matters for UX design (the residual should feel like progress, not failure), for onboarding copy (setting expectations about partial coverage), and for insight logic (insights should acknowledge that the residual represents unknowns, not zeros). Without it, the PRD treats decomposition as a binary state rather than a spectrum.

### 5. Tariff comparison as a first-class feature intent is dropped
**Source:** The brief lists "tariff comparison" explicitly in the Executive Summary as a core capability ("enables cost awareness, tariff comparison, and early warning") and includes "Tariff comparison wizard" in the Release 2+ future list.
**PRD:** §5 Non-Goals lists "Tariff comparison wizard" as explicitly out of scope — which is correct for the current releases. However, the PRD never captures the *intent* that tariff comparison is a natural next feature, nor does it ensure the data model is built to support it. The brief's framing that tariff history (stored with effective dates) is the foundation for future comparison is not surfaced as an architectural consideration.
**Impact:** If architecture chooses to implement tariff history in a way that makes comparison calculations awkward (e.g., no period-cost rollup), the opportunity is quietly foreclosed. A one-line note in §10 Open Questions or §9 Assumptions that the tariff history model should remain comparison-friendly would preserve intent without adding scope.

### 6. The vision's native iOS / widget future is scoped but motivation is lost
**Source:** The brief's Vision section is vivid: "A native Swift iOS app follows, adding home screen widgets for at-a-glance daily cost and a quick reading entry shortcut — with iPad and Apple TV as further exploration targets." The motivation is clear: the primary user interaction (meter reading in the basement) is a phone-first moment, and a widget or shortcut reduces friction below even the 60-second target.
**PRD:** §5 Non-Goals correctly excludes a native iOS app. But the Vision (§1) does not mention iOS/widgets at all. The PRD's vision is bounded to the web app's trajectory ("it is built for one person's flat today, but architected to accommodate additional flats and — eventually — additional users"). The directional signal toward native experiences — which shapes architecture decisions (e.g., API design, token handling, widget-compatible data endpoints) — is absent.
**Impact:** An architect reading only the PRD would not know that the API must be clean enough to support a future native client with home screen widget integration. This is a meaningful constraint on API design that the brief surfaces and the PRD drops.
