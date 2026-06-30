# Implementation Artifacts Index

Story implementation specs and project tracking for the energy-tracker app.

## Epic 1 — Project Foundation & Authenticated App Shell

- **[1-1-monorepo-scaffold-and-cicd-pipeline.md](./1-1-monorepo-scaffold-and-cicd-pipeline.md)** - Story 1.1: monorepo scaffold and GitHub Actions CI/CD
- **[1-2-azure-infrastructure-provisioning.md](./1-2-azure-infrastructure-provisioning.md)** - Story 1.2: Azure SWA, Function App, PostgreSQL provisioning
- **[1-3-database-schema-users-table-and-ef-core-migration-baseline.md](./1-3-database-schema-users-table-and-ef-core-migration-baseline.md)** - Story 1.3: Users table schema and EF Core migration baseline
- **[1-4-swa-easy-auth-and-tenantresolver-middleware.md](./1-4-swa-easy-auth-and-tenantresolver-middleware.md)** - Story 1.4: SWA Easy Auth integration and tenant resolver middleware
- **[1-5-app-shell-design-system-tokens-routing-and-navigation.md](./1-5-app-shell-design-system-tokens-routing-and-navigation.md)** - Story 1.5: React app shell with design tokens, routing, and bottom nav

## Epic 2 — Onboarding & Locale Selection

- **[2-1-i18n-infrastructure-and-locale-settings-api.md](./2-1-i18n-infrastructure-and-locale-settings-api.md)** - Story 2.1: i18n setup, Flat entity, and locale settings API
- **[2-2-onboarding-gate-and-intro-screen.md](./2-2-onboarding-gate-and-intro-screen.md)** - Story 2.2: Onboarding gate logic and intro/locale switcher screen
- **[2-3-onboarding-step-1-flat-name.md](./2-3-onboarding-step-1-flat-name.md)** - Story 2.3: Onboarding step 1 — flat name input
- **[2-4-onboarding-step-2-energy-contract-and-completion.md](./2-4-onboarding-step-2-energy-contract-and-completion.md)** - Story 2.4: Onboarding step 2 — energy contract and completion
- **[2-5-settings-flat-name-annual-kwh-baseline-and-locale.md](./2-5-settings-flat-name-annual-kwh-baseline-and-locale.md)** - Story 2.5: Settings screen — flat name, kWh baseline, locale, sign-out

## Tracking & Meta

- **[deferred-work.md](./deferred-work.md)** - Items deferred from code reviews for later resolution
- **[epic-1-retro-2026-06-28.md](./epic-1-retro-2026-06-28.md)** - Epic 1 retrospective notes from 2026-06-28
- **[sprint-status.yaml](./sprint-status.yaml)** - Machine-readable sprint progress tracking file

## Investigations

- **[investigations/api-404-swa-investigation.md](./investigations/api-404-swa-investigation.md)** - Root cause analysis for API 404 on SWA (no linked backend)
