# Investigation: API 404 — SWA has no linked backend

> **Folded into `architecture.md`** (2026-07-22 doc consolidation, Epic 9 retro Action Item #2): the SWA Standard-tier requirement is now AD-22b in Infrastructure & Deployment, and the resource table is corrected. This file remains as the historical record.

## Hand-off Brief

1. **What happened.** The SWA (`gray-smoke-096fc4103.7.azurestaticapps.net`) is on the Free tier with an empty `linkedBackends` list; it has no managed API configured, so every `/api/*` request returns 404 — the separately deployed `energytracker-api` Functions app is never reached.
2. **Where the case stands.** Root cause is Confirmed. The API route contract between frontend and backend is correct (`/api/v1/user/settings` on both ends); the only breakage is the missing SWA↔Functions linkage.
3. **What's needed next.** Upgrade the SWA SKU to Standard in `infra/main.bicep`, add a `linkedBackends` sub-resource pointing to `functionsApp`, and re-deploy infrastructure.

## Case Info

| Field            | Value |
| ---------------- | ----- |
| Ticket           | N/A |
| Date opened      | 2026-06-30 |
| Status           | Concluded |
| System           | Azure Static Web Apps (Free) + Azure Functions (linux, isolated worker, .NET 10) |
| Evidence sources | Source code (infra/main.bicep, .github/workflows/, api/, client/src/lib/apiClient.ts), `az staticwebapp show`, `az resource list` |

## Problem Statement

After login, the frontend reports 404 for `https://gray-smoke-096fc4103.7.azurestaticapps.net/api/v1/user/settings`. The question is whether this is an API route mismatch or an infrastructure configuration gap.

## Evidence Inventory

| Source | Status | Notes |
| ------ | ------- | ----- |
| `client/src/lib/apiClient.ts` | Available | `BASE = '/api/v1'` |
| `client/src/features/settings/api/settingsApi.ts` | Available | calls `apiClient.get('/user/settings')` → `/api/v1/user/settings` |
| `api/Features/Settings/GetUserSettingsFunction.cs` | Available | `Route = "v1/user/settings"` → Azure Functions prefix adds `/api/` → `/api/v1/user/settings` |
| `client/staticwebapp.config.json` | Available | Routes `/api/*` to `allowedRoles: anonymous` — no explicit proxy target (SWA handles that via linked backend) |
| `infra/main.bicep` L297–L308 | Available | SWA SKU `Free`; no `linkedBackends` resource |
| `az staticwebapp show energytracker-swa` | Available | `linkedBackends: []`, `sku: Free` |
| `az resource list energytracker-rg` | Available | `energytracker-api` exists, kind `functionapp,linux`, `availabilityState: Normal` |
| `.github/workflows/deploy.yml` | Available | SWA deploy step has NO `api_location`; separate `Azure/functions-action@v1` deploys API to `energytracker-api` |

## Investigation Backlog

| # | Path to Explore | Priority | Status | Notes |
| - | --------------- | -------- | ------ | ----- |
| 1 | Verify API route contract (frontend vs backend) | High | Done | Routes match exactly |
| 2 | Check SWA linked backends via Azure | High | Done | Empty — root cause confirmed |
| 3 | Check Functions app existence and health | High | Done | Exists and Normal; just not linked |
| 4 | Check SWA SKU tier | High | Done | Free — confirms linked-backend limitation |

## Timeline of Events

| Time | Event | Source | Confidence |
| ---- | ----- | ------ | ---------- |
| Build time | Frontend built with `BASE = '/api/v1'`; SWA deploy step uploads static content only (no `api_location`) | `.github/workflows/deploy.yml` | Confirmed |
| Build time | `energytracker-api` Functions app deployed separately via `functions-action@v1` | `.github/workflows/deploy.yml` | Confirmed |
| Runtime | Browser POSTs to SWA `/api/v1/user/settings` | User report | Confirmed |
| Runtime | SWA has no managed API and no linked backend → returns 404 | `az staticwebapp show` | Confirmed |

## Confirmed Findings

### Finding 1: API route contract is in sync

**Evidence:** `client/src/lib/apiClient.ts:1` (`BASE = '/api/v1'`) + `settingsApi.ts:12` (`'/user/settings'`) → full path `/api/v1/user/settings`. `GetUserSettingsFunction.cs:14` (`Route = "v1/user/settings"`) → Functions HTTP trigger registers at `/api/v1/user/settings`.

**Detail:** The 404 is NOT a URL mismatch between frontend and backend.

### Finding 2: SWA has no linked backend and no managed API

**Evidence:** `az staticwebapp show` returns `"linkedBackends": []`. The SWA deploy action in `.github/workflows/deploy.yml` omits `api_location`, so no managed functions are uploaded.

**Detail:** Azure SWA requires either (a) a managed API deployed via `api_location` in the deploy action, or (b) a linked external Functions app. Neither is configured.

### Finding 3: The Functions app exists and is healthy but unlinked

**Evidence:** `az resource list` → `energytracker-api`, `availabilityState: Normal`. Direct call to its URL would work; SWA has no route to it.

### Finding 4: Free tier blocks linked backends

**Evidence:** `infra/main.bicep:300` — `sku: { name: 'Free', tier: 'Free' }`. Azure SWA Free tier only supports managed functions (bundled via `api_location`); linking an external Functions app requires Standard tier.

## Deduced Conclusions

### Deduction 1: Architectural intent was Standard tier with linked backend

**Based on:** Findings 2, 3, 4

**Reasoning:** The Bicep provisions a full, standalone `Microsoft.Web/sites` Functions app with its own consumption plan — not the lightweight managed-functions approach. There is no `api_location` in the deploy workflow. This implies the intent was always to link the external Functions app, not bundle the API with SWA.

**Conclusion:** The Free tier in Bicep is either a cost-saving placeholder or an oversight. The correct fix is to upgrade to Standard tier and add a `linkedBackends` sub-resource.

## Hypothesized Paths

### Hypothesis 1: Frontend uses wrong base URL

**Status:** Refuted

**Theory:** The frontend might call a different path than the backend registers.

**Resolution:** Both resolve to `/api/v1/user/settings`. Refuted by Finding 1.

### Hypothesis 2: SWA managed functions with api_location could fix it on Free tier

**Status:** Open

**Theory:** Add `api_location: ./publish/api` + `skip_api_build: true` to SWA deploy and remove `functions-action` step.

**Would confirm:** SWA managed functions successfully serve .NET 10 isolated worker binaries from a pre-published directory.

**Would refute:** SWA managed functions don't support .NET 10 runtime; deployment fails or 404 persists.

**Note:** Risky — SWA managed functions have historically lagged on .NET runtime support. Standard tier + linked backend (Deduction 1) is safer and aligns with existing architecture.

## Missing Evidence

| Gap | Impact | How to Obtain |
| --- | ------- | ------------- |
| Whether GitHub Action `Azure/functions-action@v1` actually succeeded last run | Would confirm API code is deployed to `energytracker-api` | Check GitHub Actions run logs |

## Final Conclusion

**Confidence: High**

Root cause is the missing SWA↔Functions linkage. The SWA is Free tier with no managed API and no linked backend. Every `/api/*` request returns 404 because SWA has no route to the separately deployed `energytracker-api` Functions app.

## Fix Direction

**Recommended: Upgrade SWA to Standard tier and add linked backend** (Bicep change + re-deploy infra).

In `infra/main.bicep`:

```bicep
// Change SKU from Free to Standard
resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  ...
  sku: { name: 'Standard', tier: 'Standard' }   // was: Free
  ...
}

// Add after the staticWebApp resource:
resource swaLinkedBackend 'Microsoft.Web/staticSites/linkedBackends@2023-12-01' = {
  parent: staticWebApp
  name: 'default'
  properties: {
    backendResourceId: functionsApp.id
    region: location
  }
}
```

No workflow changes required — the separate `functions-action` deploy step stays as-is.

**Status:** Concluded
