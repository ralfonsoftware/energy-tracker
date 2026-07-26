---
title: 'react-router-dom Dependabot Security Update — Investigate and Patch Bump'
type: 'chore'
created: '2026-07-26'
status: 'done'
route: 'one-shot'
context: []
---

# react-router-dom Dependabot Security Update — Investigate and Patch Bump

## Intent

**Problem:** A Dependabot security-update job for `react-router-dom` (GHSA-qwww-vcr4-c8h2, react-router CSRF bypass, `>=7.12.0 <8.3.0`) failed because npm's resolver can't satisfy a patched `react-router` without a major-version downgrade of `react-router-dom` — no `react-router-dom` release yet depends on the patched `react-router@8.3.0` line.

**Approach:** Confirmed via `npm view`/`npm audit` that no real fix exists on the registry today, and confirmed via grep that this app uses zero `unstable_` RSC router APIs (the only exploitable surface per the advisory), so the app isn't affected. Bumped `react-router-dom` to its latest available patch (7.18.0 → 7.18.1, a non-security CJS-`main`-field fix) and dismissed the GitHub Dependabot alert as `not_used` with a comment explaining why, so it stops re-triggering failed update PRs. Logged the still-open CVE and a prototyped-but-rejected `overrides` alternative in `deferred-work.md` for future revisit.

## Suggested Review Order

- Confirms the only functional change is a patch-level version bump, nothing else touched.
  [`package.json:28`](../../client/package.json#L28)

- Lockfile regenerated with `npm install react-router-dom@7.18.1` against the CI-pinned npm version (11.17.0); diff is version-only.
  [`package-lock.json`](../../client/package-lock.json)

- Documents the deliberately-left-open CVE, why it's safe to leave open, and a rejected stronger-fix alternative (`overrides` to `react-router@8.3.0`, blocked by a Node engine mismatch) to revisit later.
  [`deferred-work.md`](../implementation-artifacts/deferred-work.md)
