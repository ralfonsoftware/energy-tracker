# Investigation: EventGrid webhook validation 401 during infra deploy

> **Folded into `architecture.md`** (2026-07-22 doc consolidation, Epic 9 retro Action Item #2): the Standard-tier SWA linked-backend auto-Easy-Auth side effect and the `excludedPaths`/`dependsOn` fix pattern are now AD-12a in Authentication & Security. This file remains as the historical record.

## Hand-off Brief

1. **What happened.** The `blob-created-to-processimport` Event Grid subscription's webhook validation is rejected with 401 — **not** because of the `blobs_extension` system key (confirmed live and valid), but because **App Service Authentication (Easy Auth) is enabled directly on the `energytracker-api` Function App** and requires a bearer token for every request, including Event Grid's unauthenticated validation POST to `/runtime/webhooks/blobs`.
2. **Where the case stands.** Root cause Confirmed by directly replaying Event Grid's validation request with `curl` against the live endpoint: got `401` with `WWW-Authenticate: Bearer realm="energytracker-api.azurewebsites.net"`, and confirmed via `az rest` that `authsettingsV2.properties.globalValidation.requireAuthentication = true` with the `azureStaticWebApps` identity provider enabled. This is the automatic side effect Azure applies when a Function App is linked as a Standard-tier SWA's custom backend (`swaLinkedBackend`, `infra/main.bicep:316-323`) — it is **not** declared anywhere in `main.bicep` itself, so it's configuration drift from the SWA-linked-backend feature, not from this repo's IaC.
3. **What's needed next.** Add `/runtime/webhooks/blobs` (or a scoped `/runtime/webhooks/*`) to the Function App's `authsettingsV2.globalValidation.excludedPaths`, ideally codified in Bicep so it survives redeploys. See Recommended Next Steps. **(Original Outcome-3 timing-race hypothesis below is superseded — see Follow-up.)**

## Case Info

| Field            | Value                                                                      |
| ---------------- | -------------------------------------------------------------------------- |
| Ticket           | N/A — reported directly by user                                            |
| Date opened      | 2026-07-06                                                                 |
| Status           | Concluded — fix deployed and verified                                     |
| System           | Azure (energytracker-rg), GitHub Actions, Bicep (`infra/main.bicep`)       |
| Evidence sources | GitHub Actions run logs (`gh run view`), `infra/main.bicep`, `infra/deploy.sh`, git history |

## Problem Statement

User reported: "the deployment of the Bicep changes of story 6.0 failed related to EventGrid" with:
```
{
  "code": "InvalidRequest",
  "message": "Webhook endpoint validation failed for .../eventSubscriptions/blob-created-to-processimport. Status: Failed, StatusCode: , Response: The remote server returned an error: (401) Unauthorized."
}
```
Link: https://github.com/ralfonsoftware/energy-tracker/actions/runs/28789129389

**Premise correction:** the EventGrid resources were not added in story 6.0 — they were added in story 6.1 ("Import Pipeline Infrastructure", commit `8bb1b5a`). Story 6.0 was pre-epic-6 hardening and did not touch `infra/main.bicep`'s EventGrid section. Confirmed via `git log --oneline -- infra/main.bicep`.

## Evidence Inventory

| Source   | Status    | Notes     |
| -------- | --------- | --------- |
| GitHub Actions run 28789129389 ("Deploy Azure Infrastructure") | Available | Fetched via `gh run view --log-failed`; contains the exact ARM error and timestamps |
| GitHub Actions run 28788885548 ("Azure Static Web Apps CI/CD", push to main) | Available | Fetched via `gh run view --log`; contains the preceding code deploy for the same commit |
| `infra/main.bicep` (lines 386-425)   | Available | EventGrid system topic + subscription resource, with an explicit dev-note comment describing this exact failure mode |
| `infra/deploy.sh` (lines 94-104)     | Available | Explicit "TWO-PHASE deploy" instructions for this exact scenario |
| git history (`git log`, `git show 8bb1b5a --stat`) | Available | Confirms EventGrid resources were introduced in story 6.1, not 6.0 |
| Functions host runtime logs (App Insights / Kudu) | Missing | Would show exact host cold-start / trigger-sync timestamp; not needed — timeline from Actions logs is sufficient |

## Timeline of Events

| Time (UTC)  | Event               | Source                | Confidence |
| ----------- | ------------------- | ---------------------- | ---------- |
| 2026-07-06T11:35:42Z | PR build for `feature/story-6-1-import-pipeline-infrastructure` (test only, no deploy) | `gh run list` (run 28788620110) | Confirmed |
| 2026-07-06T11:40:49Z | Story 6.1 merged to `main`; "Azure Static Web Apps CI/CD" push-triggered run starts | `gh run list` (run 28788885548) | Confirmed |
| 2026-07-06T11:44:47Z | "Deploy Azure Functions app" step starts (Kudu/OneDeploy, Flex Consumption) | run 28788885548 log | Confirmed |
| 2026-07-06T11:45:22Z | "Deploy Azure Infrastructure" workflow manually dispatched — only ~4.5 min after the code push, and *while the Functions code deploy step was still running* | `gh run list` (run 28789129389) | Confirmed |
| 2026-07-06T11:45:45Z | `az deployment group create` begins evaluating `infra/main.bicep`, including `functionAppHost.listKeys().systemKeys.blobs_extension` | run 28789129389 log | Confirmed |
| 2026-07-06T11:46:01Z | "Successfully deployed web package to Function App" — code deploy finishes | run 28788885548 log | Confirmed |
| 2026-07-06T11:46:56Z | ARM deployment fails: EventGrid webhook validation 401 for `blob-created-to-processimport` | run 28789129389 log | Confirmed |

## Confirmed Findings

### Finding 1: EventGrid subscription's webhook URL embeds a system key generated only after the host starts with the new trigger

**Evidence:** `infra/main.bicep:407-415`:
```bicep
resource importBlobEventSubscription 'Microsoft.EventGrid/systemTopics/eventSubscriptions@2023-12-15-preview' = {
  ...
  properties: {
    destination: {
      endpointType: 'WebHook'
      properties: {
        endpointUrl: 'https://${functionsApp.properties.defaultHostName}/runtime/webhooks/blobs?functionName=Host.Functions.ProcessImport&code=${functionAppHost.listKeys().systemKeys.blobs_extension}'
      }
    }
    ...
```
And the dev-note comment directly above it, `infra/main.bicep:386-392`, written by the story 6.1 author, explicitly predicts this failure mode: the `blobs_extension` system key "is generated lazily on first host start with that trigger registered," and this resource pair "can only be deployed successfully AFTER ProcessImportFunction's code ... has been deployed and the host has started at least once."

### Finding 2: `infra/deploy.sh` codifies a mandatory two-phase deploy for exactly this reason

**Evidence:** `infra/deploy.sh:94-104` — printed as post-deploy guidance: deploy app code first via normal CI/CD, confirm the host started, *then* re-run `infra/deploy.sh` for the Event Grid resources. It explicitly warns: "Running step (b) before step (a) risks `listKeys()` returning a stale/empty `blobs_extension` value or the deployment failing outright."

### Finding 3: The infra deploy was dispatched before the code deploy had finished, not merely too soon after it

**Evidence:** Run 28789129389 log — Bicep template evaluation starts at `2026-07-06T11:45:45Z`. Run 28788885548 log — "Successfully deployed web package to Function App" at `2026-07-06T11:46:01Z`, i.e. **16 seconds after** the infra deployment had already started evaluating `listKeys()`. The EventGrid webhook validation failure itself is timestamped `2026-07-06T11:46:56Z`, after the code finished deploying, but that only means the *package* was in place by then — Flex Consumption still needs the host to actually cold-start, load the `Microsoft.Azure.Functions.Worker.Extensions` blob-trigger extension, and register the trigger before `blobs_extension` exists/updates. There's no evidence in these logs that a cold start + trigger-sync had completed by 11:46:56.

## Deduced Conclusions

### Deduction 1: Root cause is the two-phase deploy race, not a code or Bicep defect

**Based on:** Findings 1, 2, 3 and the Timeline.

**Reasoning:** The story author already anticipated and documented this exact race condition when writing story 6.1, and provided the correct operational sequence (code deploy → confirm host started → infra deploy). The timeline shows that sequence was attempted (code pushed at 11:40, infra dispatched at 11:45) but the gap was too short: the infra deployment began evaluating `listKeys()` *before* the code deployment step even finished uploading the package, let alone before the Flex Consumption host had cold-started and registered the new blob-trigger extension. `listKeys()` on `functionAppHost` therefore returned an empty, stale, or not-yet-provisioned `blobs_extension` value, producing an invalid/unauthorized webhook URL that Event Grid's synchronous validation handshake correctly rejected with 401.

**Conclusion:** This is an operational sequencing issue on this specific deploy attempt, not a defect in `main.bicep` or the application code. Re-running the infra deploy now (code has long since finished deploying, and the Function App has almost certainly cold-started from unrelated traffic/health checks since) should succeed without any code changes.

## Hypothesized Paths

### Hypothesis 1: Function App requires an explicit "wake-up" call, not just elapsed time, before `blobs_extension` exists

**Status:** Open

**Theory:** Flex Consumption apps scale to zero when idle. Even minutes after a successful code deploy, the host may not have cold-started at all if nothing has invoked it (Kudu deployment completion doesn't itself invoke the worker process). If so, a fixed wait time is not a reliable fix — an explicit warm-up request (e.g., hitting any HTTP-triggered function, or `az functionapp restart`) between phase (a) and (b) would be more robust than "wait N minutes."

**Supporting indicators:** Flex Consumption's documented scale-to-zero behavior; the dev note's wording "host has started at least once" (an event, not a duration).

**Would confirm:** Checking Application Insights / Kudu logs for `energytracker-api` to see the actual first cold-start timestamp after the 11:40 deploy, and whether it occurred before or after 11:46:56.

**Would refute:** Evidence that OneDeploy/Kudu deployment on Flex Consumption always triggers an implicit host restart as part of deployment completion (in which case elapsed time alone, past the ~15s gap observed here, would be sufficient).

**Resolution:** Not pursued — out of scope for fixing this specific failed deployment; relevant only if the same race recurs after allowing more elapsed time.

## Missing Evidence

| Gap              | Impact                               | How to Obtain   |
| ---------------- | ------------------------------------ | --------------- |
| Functions host cold-start / trigger-sync timestamp for `energytracker-api` around 2026-07-06T11:40-11:47 | Would fully confirm Hypothesis 1 and pin down exactly how long the safe wait needs to be | Application Insights `requests`/`traces` for the Function App, or Kudu deployment log at the URL printed in the run 28788885548 log (`https://energytracker-api.scm.azurewebsites.net/api/deployments/ab641214-.../log`) |

## Source Code Trace

| Element       | Detail                                      |
| ------------- | -------------------------------------------- |
| Error origin  | ARM/EventGrid platform-side webhook validation handshake against `infra/main.bicep:414`'s generated `endpointUrl` |
| Trigger       | `az deployment group create` evaluating `importBlobEventSubscription` (infra/main.bicep:407) during the manually-dispatched "Deploy Azure Infrastructure" workflow |
| Condition     | `functionAppHost.listKeys().systemKeys.blobs_extension` (infra/main.bicep:414) evaluated before the Functions host had cold-started with the new blob-trigger extension registered |
| Related files | `infra/main.bicep:386-425` (EventGrid resources + dev notes), `infra/deploy.sh:94-104` (two-phase deploy instructions), `.github/workflows/azure-static-web-apps.yml` (code deploy pipeline), `.github/workflows/deploy-infrastructure.yml` (infra deploy pipeline, manually dispatched) |

## Conclusion

**Confidence:** High

Root cause is Confirmed: the "Deploy Azure Infrastructure" workflow was manually dispatched only ~4.5 minutes after the story 6.1 code merge, and its Bicep evaluation of `listKeys().systemKeys.blobs_extension` began *before* the corresponding "Deploy Azure Functions app" step had even finished uploading the new package — let alone before the Flex Consumption host had cold-started and registered the `ProcessImport` blob trigger. This is precisely the two-phase-deploy race that the story 6.1 author already documented in both `infra/main.bicep`'s dev-note comment and `infra/deploy.sh`'s printed guidance. No code or Bicep defect is implicated.

Side note: the user's framing attributed this to "story 6.0" — evidence shows the EventGrid resources were introduced in story 6.1 (commit `8bb1b5a`), not 6.0.

## Recommended Next Steps

### Fix direction

No code/Bicep change required. Operational fix: simply re-run the "Deploy Azure Infrastructure" GitHub Actions workflow now — the code deploy from 11:40 has long since completed and the Function App has had ample time (and likely traffic) to cold-start since. Re-running `az deployment group create` will re-evaluate `listKeys()` and should now pick up a valid `blobs_extension` key.

If this is likely to recur (e.g., a future story adds another Event-Grid-triggered function), consider tightening the operational guard documented in `infra/deploy.sh`: replace "wait a bit, then re-run" with an explicit warm-up step (e.g., `az functionapp restart` or an HTTP ping against the Function App) between code deploy and infra deploy, addressing Hypothesis 1 above.

### Diagnostic

If a re-run also fails with the same 401, pull the Kudu deployment log (link was printed in the 11:40 run) and Application Insights host-start traces to check whether `blobs_extension` exists at all yet — that would point to a deeper issue (e.g., the extension bundle failing to load) rather than a timing race.

## Reproduction Plan

1. From `main`, manually dispatch "Deploy Azure Infrastructure" (`.github/workflows/deploy-infrastructure.yml`) with no developer IPs.
2. Expect: deployment succeeds now, since the Functions app has been running with `ProcessImport`'s EventGrid-sourced blob trigger since 2026-07-06T11:46:01Z (or later, once actually cold-started).
3. Confirm in the Portal: `energytrackerstorage-import-egst/blob-created-to-processimport` subscription shows `Provisioning succeeded`.

## Side Findings

- The "Deploy Azure Infrastructure" workflow is `workflow_dispatch`-only with no minimum-delay guard or dependency on the code-deploy workflow completing — nothing currently prevents a human from re-triggering this race by dispatching it too soon after a code push that introduces a new Event-Grid-triggered function. (Note: superseded as the actual blocker for this failure — see Follow-up below — but still worth guarding against, since it's a real secondary risk documented by the story author.)

## Follow-up: 2026-07-06

### New Evidence

- User re-ran the "Deploy Azure Infrastructure" workflow (re-run-failed-jobs on the same run). Job attempt ran `2026-07-06T12:02:27Z`–`12:03:59Z` — **~17 minutes** after the original code push (`11:40:49Z`) and ~16 minutes after the first failure. Identical error reproduced verbatim.
- Direct `az rest POST .../host/default/listkeys` against the live Function App (well after the second failed attempt) returned a populated, valid `systemKeys.blobs_extension` value — the key demonstrably exists and is not stale.
- `az role assignment list` on the storage account confirmed all four RBAC roles from `main.bicep` (Storage Blob Data Contributor, Storage Queue Data Contributor, Storage Account Contributor, Storage Blob Data Owner) are already assigned to the managed identity — ruling out the user's RBAC-on-storage hypothesis.
- Replayed Event Grid's validation POST directly via `curl` against `https://energytracker-api.azurewebsites.net/runtime/webhooks/blobs?functionName=Host.Functions.ProcessImport&code=<live blobs_extension key>`:
  ```
  HTTP/1.1 401 Unauthorized
  WWW-Authenticate: Bearer realm="energytracker-api.azurewebsites.net"
  ```
  A `WWW-Authenticate: Bearer` challenge is characteristic of App Service Authentication (Easy Auth), not a Functions system-key rejection (a bad/missing `code` normally yields a plain 401 with no `WWW-Authenticate: Bearer` platform challenge).
- `az webapp auth show` / `az rest GET .../config/authsettingsV2` on `energytracker-api` confirmed: `enabled: true`, `globalValidation.requireAuthentication: true`, `unauthenticatedClientAction: RedirectToLoginPage`, with identity provider `azureStaticWebApps` enabled (`registration.clientId: gray-smoke-096fc4103.7.azurestaticapps.net` — this SWA's own hostname).
- `grep -n "authsettings" infra/main.bicep` — no matches. This Authentication config is **not declared anywhere in this repo's Bicep**.

### Additional Findings

#### Finding 4: App Service Authentication (Easy Auth) is enabled directly on the Function App and blocks Event Grid's unauthenticated webhook validation

**Evidence:** `az rest GET .../sites/energytracker-api/config/authsettingsV2` → `properties.globalValidation.requireAuthentication = true`, `identityProviders.azureStaticWebApps.enabled = true`. Reproduced live via `curl` (see New Evidence) — 401 with `WWW-Authenticate: Bearer`, occurring *before* the Functions runtime would ever evaluate the `code=` system-key query parameter.

**Detail:** Linking a standalone ("bring your own") Function App as the custom backend of a **Standard-tier** Azure Static Web App (`infra/main.bicep:316-323`, `swaLinkedBackend`) causes Azure to automatically provision Authentication V2 on the Function App with the `azureStaticWebApps` identity provider and `requireAuthentication: true`. This is by design — it prevents the linked backend from being called directly, bypassing the SWA's own auth/routing. It is **not** something manually configured via Portal by a person on this project; it's an automatic consequence of the linked-backend feature, and it isn't reflected anywhere in `main.bicep`.

The side effect: **every** request to the Function App's raw hostname now requires a bearer token — including Event Grid's synchronous `SubscriptionValidationEvent` POST to `/runtime/webhooks/blobs`, which (per the WebHook + system-key mechanism, confirmed as the only supported approach for Flex Consumption blob triggers via Microsoft Learn docs) carries no bearer token, only the `code=` query parameter. Easy Auth's global gate intercepts and rejects it at the platform level, before the Functions host's own key check ever runs.

### Updated Hypotheses

#### Hypothesis 1 (original): Timing race between code deploy and infra deploy — `blobs_extension` not yet generated

**Status:** Refuted (as the blocking cause for this failure)

**Resolution:** The rerun at 12:02–12:03 (17 min after code push, ample time for host cold-start) produced an identical error, and a live `listKeys()` call moments later confirmed `blobs_extension` already has a valid value. The key's existence/validity is not what's blocking the deployment. The original dev-note-documented two-phase-deploy concern is real and still worth respecting operationally, but it is not what's causing *this* failure — Finding 4 (Easy Auth) is the actual blocker and would reproduce this 401 on every attempt, at any elapsed time, until fixed.

#### Hypothesis 2 (new): RBAC gap on managed identity (user's suggestion)

**Status:** Refuted

**Theory:** User suggested a missing RBAC role on the managed identity used for deployment could cause the 401.

**Would confirm:** A missing role assignment on the storage account, Function App, or resource group relevant to `listKeys()` or Event Grid subscription creation.

**Would refute:** All expected role assignments present and deployment reaching as far as invoking the webhook validation (i.e., getting *past* any RBAC-gated ARM operations).

**Resolution:** `az role assignment list` confirmed all four expected storage roles are assigned to the deploying/runtime identity. The deployment also clearly has enough RBAC to create the Event Grid system topic and reach the webhook-validation step at all (an RBAC failure on the deploying identity would surface as an ARM authorization error before ever attempting the webhook call). The 401 is a Function-App-side Easy Auth rejection, unrelated to the deploying identity's Azure RBAC roles.

### Backlog Changes

| # | Path to Explore | Priority | Status | Notes |
| - | --------------- | -------- | ------ | ----- |
| 2 | Confirm whether `authsettingsV2` was auto-provisioned at `swaLinkedBackend` creation time vs. manually toggled later | Low | Open | Not required to fix the immediate issue; useful only if the team wants to understand exactly when this drift was introduced (check SWA/Function App activity log around when `swaLinkedBackend` was first deployed) |
| 3 | Decide whether to codify `authsettingsV2` (with the exclusion) in `main.bicep` so it survives resource recreation | Medium | Open | See Recommended Next Steps below |

### Updated Conclusion

**Confidence:** High

Root cause is Confirmed: App Service Authentication (Easy Auth), automatically enabled on the `energytracker-api` Function App as a side effect of linking it as the Standard-tier SWA's custom backend, requires a bearer token on all requests — including Event Grid's unauthenticated webhook-validation POST to `/runtime/webhooks/blobs`. This is unrelated to RBAC on the managed identity (verified sufficient) and unrelated to the `blobs_extension` system key (verified valid and present). The original timing-race hypothesis is refuted as the cause of this specific, persistent failure.

**Fix direction:** Exclude the blob-trigger webhook path from Easy Auth's global validation so Event Grid can reach it unauthenticated, while every other route on the Function App stays protected. Concretely, add to the Function App's Authentication V2 settings:
```
globalValidation.excludedPaths: ["/runtime/webhooks/blobs"]
```
This should be added as an explicit `Microsoft.Web/sites/config@2023-12-01` (`authsettingsV2`) resource in `infra/main.bicep`, both to fix the immediate failure and to prevent the same drift from silently reappearing if these resources are ever recreated (this setting currently exists only as an out-of-band side effect, not as IaC).

This is a Bicep/infra change — per this project's ownership convention, Ralf deploys infra changes himself; happy to draft the Bicep addition for review, but it won't be applied to live Azure or pushed without explicit go-ahead.

**Diagnostic (if the excluded-path fix doesn't fully resolve it):** Re-run the same `curl` replication after the fix lands; expect a 200 with the echoed `validationCode` instead of 401. If still 401, capture the response body/headers again to see if a different auth layer (e.g., IP restrictions) is now surfacing.

## Follow-up: 2026-07-06 (#2)

### New Evidence

- Ralf deployed the `authsettingsV2` Bicep addition (run 28791390326). Deployment **still failed with the identical 401** on `blob-created-to-processimport`.
- `az rest GET .../config/authsettingsV2` immediately after the failed run confirmed `globalValidation.excludedPaths: ["/runtime/webhooks/blobs"]` **is live** — the auth-settings resource itself deployed successfully; only the EventGrid subscription resource in the same run failed.
- `az resource show` on the event subscription: `ResourceNotFound` — ARM did not leave a partially-created subscription behind (consistent with incremental deployment: earlier-succeeding resources, like `functionAppAuthSettings`, persist even though the overall deployment is marked Failed).
- Replayed the exact `curl` validation request again, ~8 minutes after the failed deployment finished: **`200 OK`**, body `{"validationResponse":"test-code-123"}`. The exclusion demonstrably works at the runtime level right now.

### Additional Findings

#### Finding 5: `importBlobEventSubscription` had no explicit dependency on `functionAppAuthSettings`, so ARM had no ordering guarantee between them

**Evidence:** `infra/main.bicep` (pre-fix) — `functionAppAuthSettings` (the new `authsettingsV2` resource) and `importBlobEventSubscription` were declared as independent top-level resources with no `dependsOn` edge between them, and no data-flow reference from one to the other that Bicep could use to infer an implicit dependency. `importBlobEventSubscription`'s only implicit dependency is on `functionAppHost` (via `listKeys()`), unrelated to the auth settings.

**Detail:** Without an explicit dependency, ARM is free to deploy independent resources in parallel or in an unspecified order. In this run, Event Grid's synchronous webhook-validation POST (triggered as part of creating `importBlobEventSubscription`) evidently reached the Function App either before the `authsettingsV2` PUT had been applied, or before an Easy-Auth-config change had fully propagated to the running platform auth middleware — so the request was still rejected with 401 at that moment, even though the exclusion was correct and (per the immediate post-failure `curl` test) fully functional within minutes.

### Updated Hypotheses

#### Hypothesis 3: `excludedPaths` fix is functionally correct but was applied too late in this specific deployment run

**Status:** Confirmed

**Would confirm:** `excludedPaths` present and live post-deployment, and a direct replay of the webhook validation succeeding once retested.

**Would refute:** The exclusion still failing on direct replay well after the deployment finished (would indicate a config or path-matching problem instead).

**Resolution:** Confirmed — direct `curl` replay ~8 minutes post-failure returned `200 OK` with the correct validation echo. The fix itself is correct; the failure was purely an ordering/propagation race within that single deployment run between `functionAppAuthSettings` and `importBlobEventSubscription`.

### Backlog Changes

| # | Path to Explore | Priority | Status | Notes |
| - | --------------- | -------- | ------ | ----- |
| 4 | Add explicit `dependsOn: [functionAppAuthSettings]` to `importBlobEventSubscription` | High | Done | Applied in `infra/main.bicep`; `az bicep build` validated clean |

### Updated Conclusion

**Confidence:** High

The Easy Auth exclusion (Finding 4 / Follow-up #1) is the correct and complete fix for the underlying 401 — confirmed working via direct webhook replay. The second deployment failure was a distinct, narrower bug introduced by the fix itself: `importBlobEventSubscription` wasn't ordered to wait for `functionAppAuthSettings`, so ARM could (and did) attempt Event Grid's webhook validation before the auth exclusion had taken effect. Adding an explicit `dependsOn: [functionAppAuthSettings]` on `importBlobEventSubscription` closes that gap. No further hypotheses open.

### Reproduction Plan (updated)

1. Re-run "Deploy Azure Infrastructure" from `main` with the `dependsOn` fix in place.
2. Expect: `functionAppAuthSettings` deploys (no-op if already applied from the prior attempt), then `importBlobEventSubscription` deploys only after it, and Event Grid's validation now succeeds against the already-excluded path.
3. Confirm in the Portal or via `az resource show` on the event subscription: `provisioningState: Succeeded`.

## Case Closed: 2026-07-06

Redeploy succeeded. Verified via `az resource show` on `blob-created-to-processimport`: `provisioningState: Succeeded`. Two fixes were required in total:

1. **`functionAppAuthSettings`** (`authsettingsV2` on the Function App) — excludes `/runtime/webhooks/blobs` from the Easy Auth gate that Azure auto-provisions for Standard-tier SWA linked backends (Finding 4).
2. **`dependsOn: [functionAppAuthSettings]`** on `importBlobEventSubscription` — guarantees the exclusion is live before Event Grid's webhook validation handshake runs (Finding 5).

No open hypotheses or missing evidence remain.
