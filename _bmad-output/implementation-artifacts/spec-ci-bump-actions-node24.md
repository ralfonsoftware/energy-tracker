---
title: 'CI: Bump GitHub Actions to Node.js 24-native majors'
type: 'chore'
created: '2026-07-29'
status: 'done'
route: 'one-shot'
baseline_commit: 'c8805f8c3260de23de39ed701eb3ca5797ab1cde'
---

## Intent

**Problem:** GitHub Actions CI runs were emitting a deprecation warning — `actions/checkout@v4`, `actions/setup-node@v4`, and `actions/setup-dotnet@v4` all target the deprecated Node.js 20 runtime and were being force-run on Node.js 24 by GitHub's runners.

**Approach:** Bump each pinned action to its current latest major (`checkout@v7`, `setup-node@v7`, `setup-dotnet@v6`), confirmed via each tag's `action.yml` to declare `using: node24` natively. Applied consistently across every occurrence in both workflow files, including `deploy-infrastructure.yml`'s lone `checkout@v4`, which wasn't named in the original warning but uses the same deprecated pin and would hit the same warning on its next run.

## Suggested Review Order

- Entry point — first CI job's action pins, representative of the fix applied identically at every other call site.
  [`azure-static-web-apps.yml:23`](../../.github/workflows/azure-static-web-apps.yml#L23)
  [`azure-static-web-apps.yml:27`](../../.github/workflows/azure-static-web-apps.yml#L27)
  [`azure-static-web-apps.yml:44`](../../.github/workflows/azure-static-web-apps.yml#L44)

- Same three pins repeated in the `deploy` job.
  [`azure-static-web-apps.yml:59`](../../.github/workflows/azure-static-web-apps.yml#L59)
  [`azure-static-web-apps.yml:74`](../../.github/workflows/azure-static-web-apps.yml#L74)
  [`azure-static-web-apps.yml:92`](../../.github/workflows/azure-static-web-apps.yml#L92)

- Same pins in the `deploy_preview` job (checkout + setup-node only; no .NET step here).
  [`azure-static-web-apps.yml:143`](../../.github/workflows/azure-static-web-apps.yml#L143)
  [`azure-static-web-apps.yml:149`](../../.github/workflows/azure-static-web-apps.yml#L149)

- Peripheral — the one `checkout@v4` in the infrastructure-deploy workflow, bumped for consistency even though this workflow only runs on manual dispatch.
  [`deploy-infrastructure.yml:24`](../../.github/workflows/deploy-infrastructure.yml#L24)
