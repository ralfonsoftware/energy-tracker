#!/usr/bin/env bash
# Provisions all Azure resources for energy-tracker via Bicep.
# Prerequisites: az login, az bicep installed, energytracker-rg resource group exists.
# Usage: ./infra/deploy.sh <parameters-file>
#   parameters-file: path to a filled-in copy of main.parameters.example.json
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RESOURCE_GROUP="${AZURE_RESOURCE_GROUP:-energytracker-rg}"
PARAMETERS_FILE="${1:-}"

if [[ -z "$PARAMETERS_FILE" ]]; then
  echo "Usage: ./infra/deploy.sh <parameters-file>"
  echo "Copy main.parameters.example.json to main.parameters.json, fill in your values, and re-run:"
  echo "  cp infra/main.parameters.example.json infra/main.parameters.json"
  echo "  # edit infra/main.parameters.json — replace all <...> placeholders"
  echo "  ./infra/deploy.sh infra/main.parameters.json"
  exit 1
fi

if [[ ! -f "$PARAMETERS_FILE" ]]; then
  echo "Parameters file not found: $PARAMETERS_FILE"
  exit 1
fi

if grep -q '<' "$PARAMETERS_FILE"; then
  echo "ERROR: Parameters file still contains unfilled placeholders (<...>)."
  echo "Edit $PARAMETERS_FILE and replace all angle-bracket values before deploying."
  exit 1
fi

SUBSCRIPTION_ID=$(az account show --query id -o tsv 2>/dev/null || true)
if [[ -z "$SUBSCRIPTION_ID" ]]; then
  echo "ERROR: Not logged in to Azure. Run 'az login' first."
  exit 1
fi

if ! az group show --name "$RESOURCE_GROUP" &>/dev/null; then
  echo "ERROR: Resource group '$RESOURCE_GROUP' does not exist."
  echo "Create it first: az group create -n $RESOURCE_GROUP -l westeurope"
  exit 1
fi

echo "=== energy-tracker Azure Infrastructure Provisioning ==="
echo "Resource group : $RESOURCE_GROUP"
echo "Subscription   : $SUBSCRIPTION_ID"
echo "Parameters     : $PARAMETERS_FILE"
echo ""

echo "Deploying Bicep template..."
DEPLOY_OUTPUT=$(az deployment group create \
  --resource-group "$RESOURCE_GROUP" \
  --template-file "${SCRIPT_DIR}/main.bicep" \
  --parameters "@${PARAMETERS_FILE}" \
  --query "properties.outputs" \
  --output json)

echo ""
echo "=== Deployment complete. Outputs: ==="
if command -v python3 &>/dev/null && [[ "$DEPLOY_OUTPUT" != "null" && -n "$DEPLOY_OUTPUT" ]]; then
  echo "$DEPLOY_OUTPUT" | python3 -c "
import sys, json
outputs = json.load(sys.stdin)
if outputs:
    for k, v in outputs.items():
        print(f'  {k}: {v[\"value\"]}')
"
else
  echo "$DEPLOY_OUTPUT"
fi

echo ""
echo "=== Next steps ==="
echo "1. Copy api/local.settings.json.example to api/local.settings.json"
echo "   and fill in the values from the outputs above."
echo ""
echo "2. Grant the managed identity access to SQL (run in Azure SQL as Azure AD admin):"
echo "   Use Azure Portal → SQL Database → Query Editor, or sqlcmd:"
echo "   sqlcmd -S <sqlServerFqdn> -d energytracker-db --authentication-method ActiveDirectoryDefault \\"
echo "     -Q \"CREATE USER [energytracker-identity] FROM EXTERNAL PROVIDER;\""
echo "   sqlcmd -S <sqlServerFqdn> -d energytracker-db --authentication-method ActiveDirectoryDefault \\"
echo "     -Q \"ALTER ROLE db_datareader ADD MEMBER [energytracker-identity];\""
echo "   sqlcmd -S <sqlServerFqdn> -d energytracker-db --authentication-method ActiveDirectoryDefault \\"
echo "     -Q \"ALTER ROLE db_datawriter ADD MEMBER [energytracker-identity];\""
echo "   sqlcmd -S <sqlServerFqdn> -d energytracker-db --authentication-method ActiveDirectoryDefault \\"
echo "     -Q \"ALTER ROLE db_ddladmin ADD MEMBER [energytracker-identity];\""
echo ""
echo "3. Add GitHub repository secrets/variables:"
echo "   - Secret: AZURE_STATIC_WEB_APPS_API_TOKEN"
echo "     Source: Azure Portal → Static Web Apps → <swaName> → Manage deployment token"
echo "   - Variable: AZURE_FUNCTIONS_APP_NAME = <functionsAppName>"
echo "   - Variable: AZURE_RESOURCE_GROUP = $RESOURCE_GROUP"
