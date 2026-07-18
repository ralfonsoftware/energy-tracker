---
title: 'PR Preview Environments (Frontend-Only) for Azure Static Web Apps'
type: 'chore'
created: '2026-07-18'
status: 'done'
context: []
baseline_commit: '6f563de5f058dff5ca5a8c741197a696463e87c9'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `.github/workflows/azure-static-web-apps.yml` only deploys to production on push to `main`. Reviewers currently have no way to see a PR's frontend changes live without pulling the branch locally.

**Approach:** Add a `deploy_preview` job that builds and deploys `client/` to Azure SWA's PR preview slot on `opened`/`synchronize`/`reopened`, plus a `close_preview` job that tears the slot down when the PR closes. This is **frontend-only** — Azure does not support linked/BYO Functions backends in SWA PR preview environments, so `/api/*` calls in the preview will 404. This is a visual/UX review tool, not a functional-testing environment. Also flip `infra/main.bicep`'s `stagingEnvironmentPolicy` from `'Disabled'` to `'Enabled'` — the prerequisite for previews to exist at all — since Ralf explicitly asked for that file to be edited too, though he still applies the infra deploy himself.

## Boundaries & Constraints

**Always:**
- Preview job only builds/deploys `client/` (via `Azure/static-web-apps-deploy@v1`, `action: upload`); never touches `api/` publish, EF migrations, or `Azure/functions-action@v1`.
- `deploy_preview` gated behind `needs: test`, same gate as the existing production `deploy` job.
- Reuse existing secrets only: `secrets.AZURE_STATIC_WEB_APPS_API_TOKEN`, `secrets.GITHUB_TOKEN`. No new secrets/vars, no Azure OIDC login step (not needed for this action).
- `pull_request:` trigger must add `types: [opened, synchronize, reopened, closed]` (currently unset = defaults to opened/synchronize/reopened only) so `closed` events reach `close_preview`.
- Guard the existing `test` job so it does not needlessly re-run on `closed` PR events.
- `infra/main.bicep`'s `staticWebApp` resource: change only the single `stagingEnvironmentPolicy: 'Disabled'` line to `'Enabled'` — no other property on that resource changes.

**Ask First:** If achieving this requires any new GitHub secret, repo variable, or workflow permission beyond `contents: read` / `pull-requests: write`, halt and ask before adding it.

**Never:**
- Do not deploy or link the Functions backend to preview environments (unsupported by Azure for BYO backends).
- Do not run `az deployment` / any deploy script against live Azure to apply the Bicep change — edit the file only; Ralf applies it himself, per standing convention.
- Do not modify the existing `test` job's test-execution steps or the production `deploy` job's logic — extend the file, don't restructure it.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| PR opened/synced/reopened against `main` | `pull_request` event, `test` job passes | `deploy_preview` builds `client/`, deploys to PR's preview slot; Azure bot comments the preview URL on the PR | If `test` fails, `deploy_preview` is skipped (via `needs`); if build/deploy step fails, job fails, no preview created |
| PR closed (merged or abandoned) | `pull_request` `closed` event | `close_preview` runs `action: close`, deleting the preview environment; `test`/`deploy_preview` do not run | N/A |
| Ralf hasn't yet applied the Bicep change to live Azure | Any preview deploy attempt before infra redeploy | Azure rejects the preview deploy until the `stagingEnvironmentPolicy` change is actually deployed | Documented as a known prerequisite in Verification; this spec only edits the file |

</frozen-after-approval>

## Code Map

- `.github/workflows/azure-static-web-apps.yml` — add two jobs, adjust `pull_request` trigger `types`, guard `test` job's `if`.
- `infra/main.bicep:306` — `staticWebApp` resource; flip `stagingEnvironmentPolicy` to `'Enabled'`.

## Tasks & Acceptance

**Execution:**
- [x] `.github/workflows/azure-static-web-apps.yml` -- add `types: [opened, synchronize, reopened, closed]` under the existing `pull_request:` trigger -- required so `closed` events reach the new cleanup job.
- [x] `.github/workflows/azure-static-web-apps.yml` -- add `if: github.event_name != 'pull_request' || github.event.action != 'closed'` to the `test` job -- avoids a wasted full test run when a PR is merely closed/merged.
- [x] `.github/workflows/azure-static-web-apps.yml` -- add `deploy_preview` job: `needs: test`; `if: github.event_name == 'pull_request' && github.event.action != 'closed'`; checkout, setup Node 22, `npm ci` + `npm run build` in `client/`, then `Azure/static-web-apps-deploy@v1` with `action: upload`, `app_location: client/dist`, `skip_app_build: true`, using the existing `AZURE_STATIC_WEB_APPS_API_TOKEN` secret -- mirrors the existing production frontend-deploy step, scoped to PR context.
- [x] `.github/workflows/azure-static-web-apps.yml` -- add `close_preview` job: `if: github.event_name == 'pull_request' && github.event.action == 'closed'`; single step using `Azure/static-web-apps-deploy@v1` with `action: close` and the same token -- tears down the preview slot.
- [x] `infra/main.bicep` -- change `stagingEnvironmentPolicy: 'Disabled'` to `stagingEnvironmentPolicy: 'Enabled'` on the `staticWebApp` resource (line 306) -- required prerequisite for any preview environment to be created; file edit only, no deploy run.

**Acceptance Criteria:**
- Given a PR is opened against `main` with only `client/` changes, when `test` passes, then `deploy_preview` runs and the workflow run shows a successful `Azure/static-web-apps-deploy@v1` upload step scoped to the PR.
- Given a PR is closed (merged or not), when the workflow runs, then `close_preview` runs `action: close` and neither `test` nor `deploy_preview` execute.
- Given the existing production `push`-triggered `deploy` job, when this change is applied, then its steps and conditions remain byte-for-byte unchanged.

## Spec Change Log

## Design Notes

`Azure/static-web-apps-deploy@v1` infers upload vs. close targeting (which PR/slot) from the `pull_request` event context automatically — no `deployment_environment` parameter needed. The action also posts the preview URL as a PR comment itself via `repo_token`; no extra comment step required.

## Verification

**Commands:**
- `actionlint .github/workflows/azure-static-web-apps.yml` -- expected: no syntax errors (if `actionlint` is available; otherwise GitHub's own workflow syntax check on push suffices).

**Manual checks (if no CLI):**
- Ralf applies the Bicep change to live Azure himself (his standing infra-deploy convention) before the workflow change can produce a working preview.
- Open a small test PR after both changes are live and confirm: (1) `deploy_preview` job appears and succeeds, (2) a bot comment with a preview URL appears on the PR, (3) closing the PR triggers `close_preview` and the environment disappears from the Azure Portal's *Environments* tab.

## Suggested Review Order

**Infra prerequisite**

- The gate that makes previews possible at all — must be applied to live Azure before the workflow below does anything.
  [`main.bicep:306`](../../infra/main.bicep#L306)

**Trigger wiring**

- Adds `closed` to the PR event types so the cleanup job below can fire.
  [`azure-static-web-apps.yml:11`](../../.github/workflows/azure-static-web-apps.yml#L11)

- Guards the existing test job from needlessly re-running on a `closed` PR event.
  [`azure-static-web-apps.yml:19`](../../.github/workflows/azure-static-web-apps.yml#L19)

**Preview deploy**

- New job: frontend-only build + upload to the PR's preview slot, gated behind `needs: test`.
  [`azure-static-web-apps.yml:132`](../../.github/workflows/azure-static-web-apps.yml#L132)

- Concurrency group prevents a stale build racing a newer push or a fast close.
  [`azure-static-web-apps.yml:135`](../../.github/workflows/azure-static-web-apps.yml#L135)

**Preview cleanup**

- Tears the preview slot down on PR close; mirrors the same concurrency group as the deploy job.
  [`azure-static-web-apps.yml:174`](../../.github/workflows/azure-static-web-apps.yml#L174)
