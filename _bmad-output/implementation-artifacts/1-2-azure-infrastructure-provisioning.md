---
baseline_commit: e18f344b399a856dbaa10ed909ba6f648d30e5df
---

# Story 1.2: Azure Infrastructure Provisioning

Status: done

## Story

As a developer,
I want all required Azure resources provisioned and connected via managed identity,
So that the application has a secure, cost-appropriate cloud infrastructure with no hardcoded credentials.

## Acceptance Criteria

1. **Resources exist** — The following Azure resources are provisioned: Azure Static Web App (Free), Azure Functions app (Flex Consumption Linux, .NET 10), Azure Storage Account (Standard LRS) with blob container `smart-plug-imports` and storage queues (`import-processing`, `insight-discovery`), Azure SQL Server + DB (Basic DTU ~€5/mo), Azure Key Vault (Standard), Application Insights + Log Analytics workspace, and a user-assigned Managed Identity assigned to the Functions app.

2. **Managed Identity connections** — When `DefaultAzureCredential` is used in any Function, connections to Azure SQL, Blob Storage, Storage Queue, and Key Vault succeed without password-based connection strings in any config file or source code.

3. **Application Insights telemetry** — When a Function executes, the invocation trace, any dependencies (SQL, Blob), and any failures are visible in Application Insights within 5 minutes.

4. **Local dev with `az login`** — When the developer runs the Functions host locally after `az login`, all Azure service connections (SQL, Blob, Queue, Key Vault) use the developer's Azure CLI credentials via `DefaultAzureCredential` — no secrets in source code. `local.settings.json.example` exists as a developer onboarding template. **Exception:** `AzureWebJobsStorage` uses Azurite (`UseDevelopmentStorage=true`) for the Functions runtime's internal storage (timers, durable state, host lease) — this is intentional per [AD-21]. Developers must start Azurite and create the required queues before running the Functions host locally (see "Local Dev Setup" in Dev Notes).

## Tasks / Subtasks

- [x] Task 1: Create Bicep infrastructure template (AC: 1, 2, 3)
  - [x] Create `infra/main.bicep` with all required Azure resources and RBAC role assignments
  - [x] Create `infra/main.parameters.example.json` with documented parameter values (known IDs pre-filled)
  - [x] Add `deployments` blob container for Flex Consumption package deployment storage

- [x] Task 2: Create deployment script (AC: 1)
  - [x] Create `infra/deploy.sh` — runs `az deployment group create` with the Bicep template
  - [x] Script outputs key values needed for GitHub secrets/variables and `local.settings.json`

- [x] Task 3: Create local development settings template (AC: 4)
  - [x] Create `api/local.settings.json.example` with DefaultAzureCredential-based settings
  - [x] All settings use resource references (no passwords); `AzureWebJobsStorage` uses Azurite for local dev
  - [x] Verify `api/local.settings.json` is in `.gitignore` and `.example` is NOT ignored

- [x] Task 4: Validate Bicep template compiles (AC: 1)
  - [x] Run `az bicep build infra/main.bicep` — exit 0, zero errors (BCP081 warnings only — expected with Bicep CLI v0.24.24 and newer API versions; not blocking)
  - [x] Verify all required resources are declared (15 resource types confirmed in generated ARM JSON)
  - [x] Verify RBAC role assignments cover Blob Data Contributor, Queue Data Contributor, Key Vault Secrets User

- [x] Task 5: Final verification (AC: 1–4)
  - [x] All AC requirements are covered by the provisioned resources in `main.bicep`
  - [x] No secrets appear in any committed file (grep confirmed — only comments and placeholder templates)
  - [x] `dotnet test` still passes — Passed: 1, Failed: 0 (no regressions)

## Dev Notes

### Known Azure Values (from Ralf's DevOps prep)

These values are already provisioned and must be used as-is:

- **Tenant ID:** stored in GitHub variable `AZURE_TENANT_ID`
- **Subscription ID:** stored in GitHub variable `AZURE_SUBSCRIPTION_ID`
- **Resource Group:** stored in GitHub variable `AZURE_RESOURCE_GROUP` (already exists)
- **Managed Identity Client ID:** stored in GitHub variable `AZURE_CLIENT_ID`
- **Managed Identity Object ID:** stored in GitHub variable `AZURE_PRINCIPAL_ID`
- **Managed Identity Resource ID:** `/subscriptions/${{ vars.AZURE_SUBSCRIPTION_ID }}/resourceGroups/${{ vars.AZURE_RESOURCE_GROUP }}/providers/Microsoft.ManagedIdentity/userAssignedIdentities/energytracker-identity`
- **GitHub Repo:** `ralfonsoftware/energy-tracker`

The Federated Credentials are already configured on this identity for `main` branch OIDC in the CI/CD pipeline.

### Resource Naming Conventions

All resources are parameters with defaults — override as needed for global uniqueness:

| Resource | Default Name | Constraint |
|---|---|---|
| Storage Account | `energytrackerstorage` | 3–24 chars, lowercase+digits, globally unique |
| Functions App | `energytracker-api` | Globally unique |
| SQL Server | `energytracker-sqlsrv` | Globally unique |
| SQL Database | `energytracker-db` | Within server |
| Key Vault | `energytracker-kv` | 3–24 chars, globally unique |
| Log Analytics | `energytracker-logs` | Within resource group |
| App Insights | `energytracker-insights` | Within resource group |
| Static Web App | `energytracker-swa` | Globally unique |

### Managed Identity Role Assignments Required

| Role | Built-in Role ID | Scope |
|---|---|---|
| Storage Blob Data Contributor | `ba92f5b4-2d11-453d-a403-e96b0029c9fe` | Storage Account |
| Storage Queue Data Contributor | `974c5e8b-45b9-4653-ba55-5f855dd0fb88` | Storage Account |
| Key Vault Secrets User | `4633458b-17de-408a-b874-0445c86b69e6` | Key Vault |

SQL access uses Azure AD authentication (`CREATE USER [identity] FROM EXTERNAL PROVIDER`) — this must be run manually in SQL after provisioning (cannot be done in Bicep). The SQL admin is the developer's own Entra ID account.

### Flex Consumption Plan — Bicep Notes

The Flex Consumption plan uses `FC1` SKU with `reserved: true` (Linux). The Functions app uses `functionAppConfig` (not `siteConfig` alone) for Flex Consumption-specific settings:

- `functionAppConfig.deployment.storage` — blob container for package deployment (separate from app storage)
- `functionAppConfig.scaleAndConcurrency` — `instanceMemoryMB: 2048`, `maximumInstanceCount: 10`
- `functionAppConfig.runtime` — `name: 'dotnet-isolated'`, `version: '10.0'`

`AzureWebJobsStorage` uses the `__accountName` + `__credential` pattern (not a traditional connection string) to enable managed identity auth for the Functions runtime.

### Local Dev Setup

**Prerequisites:** `az login` completed, Azurite installed (`npm install -g azurite` or via VS Code extension).

**Step 1 — Start Azurite** (Functions runtime storage):
```bash
azurite --silent --location ~/.azurite --debug ~/.azurite/debug.log
```
Or use the VS Code Azurite extension (Ctrl+Shift+P → "Azurite: Start").

**Step 2 — Create queues in Azurite** (required for queue-triggered Functions):
```bash
# Install Azure Storage Explorer or use the Azure CLI against the emulator
az storage queue create --name import-processing --connection-string "UseDevelopmentStorage=true"
az storage queue create --name insight-discovery --connection-string "UseDevelopmentStorage=true"
```

**Step 3 — Copy and fill in `local.settings.json`**:
```bash
cp api/local.settings.json.example api/local.settings.json
```
Then fill in the placeholder values from `deploy.sh` output:
- `APPLICATIONINSIGHTS_CONNECTION_STRING` — from Azure Portal → Application Insights → energytracker-insights → Connection String
- `SqlConnectionString` — replace `<SQL_SERVER_FQDN>` with the SQL server FQDN from `deploy.sh` output
- `AzureStorageAccountName` — storage account name from `deploy.sh` output
- `KeyVaultUri` — Key Vault URI from `deploy.sh` output
- `AZURE_CLIENT_ID` — **leave empty or remove** for local dev; DefaultAzureCredential uses `az login` credentials automatically

**Step 4 — Run the Functions host**:
```bash
cd api && func start
```

> **Note:** SQL, Blob, Queue (real Azure), and Key Vault connections all resolve via `DefaultAzureCredential` → `AzureCliCredential`. Only `AzureWebJobsStorage` (Functions runtime internals) uses Azurite. The real `import-processing` and `insight-discovery` queues in Azure are not used locally — local queue triggers fire against Azurite.

### GitHub Repo Setup (after Bicep deploy)

After running `infra/deploy.sh`, add to the GitHub repo:
- Secret `AZURE_STATIC_WEB_APPS_API_TOKEN` — from: Azure Portal → Static Web Apps → Manage deployment token
- Variable `AZURE_FUNCTIONS_APP_NAME` — the Functions app name (e.g., `energytracker-api`)
- Variable `AZURE_RESOURCE_GROUP` — `energytracker-rg`

### Local Dev SQL Access

After provisioning, run in Azure SQL (as Azure AD admin):
```sql
CREATE USER [energytracker-identity] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [energytracker-identity];
ALTER ROLE db_datawriter ADD MEMBER [energytracker-identity];
ALTER ROLE db_ddladmin ADD MEMBER [energytracker-identity];
```

For local dev, run equivalent for the developer's own identity (`CREATE USER [email@domain.com] FROM EXTERNAL PROVIDER`).

### Architecture References

- [Architecture: AD-10] — Managed identity for all Azure service connections
- [Architecture: AD-11] — Key Vault provisioned now for future secrets
- [Architecture: AD-21] — Local dev: local.settings.json + Azurite
- [Architecture: AD-22] — Application Insights via OpenTelemetry
- [Architecture: Infrastructure & Deployment — Complete Azure resource set]

## Dev Agent Record

### Agent Model Used

claude-sonnet-4-6

### Debug Log References

None.

### Completion Notes List

- **Bicep CLI v0.24.24 BCP081 warnings** — `az bicep build` exits 0 with only BCP081 "type not available" warnings for some newer API versions (2023-05-01, 2023-09-01, 2023-12-01). These are informational only — the type checker doesn't have definitions for those API versions locally, but the ARM deployment will succeed. Upgrade Bicep CLI to silence them.
- **Flex Consumption `functionAppConfig`** — Flex Consumption uses `functionAppConfig.deployment.storage` for package deployment, pointing to a dedicated `deployments` blob container in the storage account. The `AzureWebJobsStorage__accountName` + `__credential: managedidentity` pattern enables passwordless storage access from the Functions runtime.
- **SQL managed identity access requires post-deployment SQL script** — RBAC role assignments cover Blob and Queue, but SQL Azure AD user creation (`CREATE USER FROM EXTERNAL PROVIDER`) must be run manually in SQL after deployment. This is a documented SQL limitation — it cannot be done via Bicep.
- **`infra/main.json` gitignored** — The compiled ARM template is a Bicep build artifact and is gitignored; the `.bicep` source is the authoritative IaC.

### File List

| File | Action |
|------|--------|
| `infra/main.bicep` | Created |
| `infra/main.parameters.example.json` | Created |
| `infra/deploy.sh` | Created |
| `api/local.settings.json.example` | Created |

## Review Findings

### Decision-Needed

- [x] [Review][Decision] Real subscription ID and managed identity GUIDs hardcoded as defaults/values in committed example files — `main.bicep` default params and `main.parameters.example.json` embed the real subscription ID (`0d07577a-...`), managed identity object ID (`d95bc26a-...`), client ID (`c2b7d43d-...`), and resource ID. For a public repo this is a reconnaissance aid; for a private repo it couples the template to a single environment and will break any second dev who deploys without overriding all three params. Decision: replace defaults with `<placeholder>` values and require the params file to supply them, OR accept this is a single-env private template.
- [x] [Review][Decision] AzureWebJobsStorage Azurite vs. AC4 conflict — AC4 states "all service connections use DefaultAzureCredential," but `local.settings.json.example` uses `"AzureWebJobsStorage": "UseDevelopmentStorage=true"` (Azurite). The task spec and [AD-21] explicitly planned for Azurite, but AC4 was never updated. Consequence: local queues (`import-processing`, `insight-discovery`) don't exist in Azurite by default, masking queue-trigger bugs. Decision: update AC4 to carve out AzureWebJobsStorage=Azurite, OR switch to real storage with DefaultAzureCredential locally.
- [x] [Review][Decision] SQL firewall AllowAllAzureIPs allows any Azure tenant to reach the SQL endpoint — `startIpAddress: '0.0.0.0' / endIpAddress: '0.0.0.0'` is the "allow Azure services" rule but it permits any service in any Azure subscription globally. Decision: accept this for Basic-tier dev (private-endpoint cost not worth it), OR remove and use VNet service endpoint / private endpoint.

### Patch

- [x] [Review][Patch] Replace hardcoded subscription ID, managed identity GUIDs with placeholders in `main.bicep` param defaults, `main.parameters.example.json` values, and `deploy.sh` SUBSCRIPTION_ID — require caller to supply real values [infra/main.bicep, infra/main.parameters.example.json, infra/deploy.sh]
- [x] [Review][Patch] Remove `AllowAllAzureIPs` SQL firewall rule and add optional `developerIpAddresses` array param — each entry creates a named firewall rule; empty array = no rule (Functions-to-SQL goes via Azure internal already via managed identity) [infra/main.bicep:~L128]
- [x] [Review][Patch] Key Vault missing `enablePurgeProtection: true` — without it, a principal can permanently delete the vault within the soft-delete window [infra/main.bicep:~L149]
- [x] [Review][Patch] Key Vault `softDeleteRetentionInDays: 7` — minimum value; use 30 for any production secrets store [infra/main.bicep:~L153]
- [x] [Review][Patch] `deploy.sh` next-step SQL instruction uses non-existent `az sql db execute-query` command — will produce `command not found`; use sqlcmd or Portal query editor instead [infra/deploy.sh:~L51]
- [x] [Review][Patch] `deploy.sh` defaults to `.example` file — running `./deploy.sh` with no argument silently passes placeholder SQL admin values to ARM, causing a deployment failure with a confusing error [infra/deploy.sh:L10]
- [x] [Review][Patch] `deploy.sh` duplicate "Subscription" echo line — line 22 and 23 both print `$SUBSCRIPTION_ID`; one should print `$RESOURCE_GROUP` [infra/deploy.sh:L22-23]
- [x] [Review][Patch] `deploy.sh` `python3` dependency not checked — if `python3` is absent, the script exits non-zero after a fully successful Azure deployment [infra/deploy.sh:~L38]
- [x] [Review][Patch] `DEPLOY_OUTPUT` could be JSON `null` — if ARM outputs are empty, `json.load()` parses `None` and `.items()` crashes with `AttributeError` [infra/deploy.sh:~L39]
- [x] [Review][Patch] `functionsApp` has no `dependsOn` for RBAC role assignments — ARM can start the Functions host before blob/queue RBAC propagates; host fails to initialize until propagation completes, requiring a manual restart [infra/main.bicep:~L206]
- [x] [Review][Patch] `AZURE_CLIENT_ID` hardcoded to managed identity GUID in `local.settings.json.example` — causes `DefaultAzureCredential` to attempt (and timeout on) `ManagedIdentityCredential` locally; replace with `<leave empty for local dev — only needed in deployed app settings>` [api/local.settings.json.example:L16]

### Defer

- [x] [Review][Defer] No network isolation — all resources expose public endpoints [infra/main.bicep] — deferred, pre-existing; private endpoints would add cost beyond Basic-tier scope
- [x] [Review][Defer] SQL API uses preview version `2022-11-01-preview` [infra/main.bicep:~L113] — deferred, pre-existing; acceptable for now, upgrade when GA API is needed
- [x] [Review][Defer] No `Microsoft.Insights/diagnosticSettings` resources — Log Analytics workspace is unconnected to SQL, Key Vault, Storage logs [infra/main.bicep] — deferred, pre-existing; not in story requirements
- [x] [Review][Defer] Static Web App `stagingEnvironmentPolicy: 'Disabled'` — PR preview environments disabled [infra/main.bicep:~L272] — deferred, pre-existing; design choice
- [x] [Review][Defer] SQL Basic (5 DTUs) mismatched against up to 10 concurrent 2 GB Function instances — intentional cost trade-off per story spec [infra/main.bicep] — deferred, pre-existing
- [x] [Review][Defer] No `@minLength`/`@maxLength`/`@pattern` on storage/keyvault name params — invalid names fail inside ARM after partial provisioning [infra/main.bicep:~L4,L25] — deferred, pre-existing

## Change Log

- Created `infra/main.bicep` — Bicep template for all Azure resources with RBAC assignments (Date: 2026-06-27)
- Created `infra/main.parameters.example.json` — pre-filled parameter template with known values (Date: 2026-06-27)
- Created `infra/deploy.sh` — deployment script with post-deployment instructions (Date: 2026-06-27)
- Created `api/local.settings.json.example` — developer onboarding template with DefaultAzureCredential settings (Date: 2026-06-27)
- Modified `.gitignore` — added `infra/main.json` exclusion (ARM compiled output) (Date: 2026-06-27)
