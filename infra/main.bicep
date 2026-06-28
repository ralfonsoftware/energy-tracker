@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Storage account name — must be globally unique, 3–24 chars, lowercase+digits')
param storageAccountName string = 'energytrackerstorage'

@description('Azure Functions app name — must be globally unique')
param functionsAppName string = 'energytracker-api'

@description('Azure SQL Server name — must be globally unique')
param sqlServerName string = 'energytracker-sqlsrv'

@description('Azure SQL Database name')
param sqlDatabaseName string = 'energytracker-db'

@description('Azure Key Vault name — must be globally unique, 3–24 chars')
param keyVaultName string = 'energytracker-kv'

@description('Log Analytics workspace name')
param logAnalyticsName string = 'energytracker-logs'

@description('Application Insights name')
param appInsightsName string = 'energytracker-insights'

@description('Azure Static Web App name')
param swaName string = 'energytracker-swa'

@description('Azure region for the Static Web App — SWA is not available in all regions (e.g. not in germanywestcentral). Defaults to westeurope.')
param swaLocation string = 'westeurope'

@description('Object ID of the user-assigned managed identity (for RBAC assignments)')
param managedIdentityObjectId string

@description('Client ID of the user-assigned managed identity (for Functions app config)')
param managedIdentityClientId string

@description('Full resource ID of the user-assigned managed identity')
param managedIdentityResourceId string

@description('Developer IP addresses allowed to reach the SQL server — each entry creates a named firewall rule. Leave empty to allow no direct developer access (Functions-to-SQL uses managed identity internally).')
param developerIpAddresses array = []

@description('Azure AD UPN of the SQL admin (developer Entra ID account)')
param sqlAdminLogin string

@description('Object ID of the SQL admin Entra ID account')
param sqlAdminObjectId string

// ── Derived names ──────────────────────────────────────────────────────────────
var functionsPlanName = '${functionsAppName}-plan'
var blobContainerName = 'smart-plug-imports'
var deploymentsContainerName = 'deployments'
var importQueueName = 'import-processing'
var insightQueueName = 'insight-discovery'

// ── Built-in role definition IDs ───────────────────────────────────────────────
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var storageQueueDataContributorRoleId = '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

// ── Log Analytics Workspace ────────────────────────────────────────────────────
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// ── Application Insights ───────────────────────────────────────────────────────
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// ── Storage Account ────────────────────────────────────────────────────────────
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
  }
}

resource blobServices 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

// Blob container for smart plug file uploads
resource smartPlugImportsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobServices
  name: blobContainerName
  properties: {
    publicAccess: 'None'
  }
}

// Blob container for Flex Consumption package deployment
resource deploymentsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobServices
  name: deploymentsContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource queueServices 'Microsoft.Storage/storageAccounts/queueServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

// Queue for smart plug import processing
resource importQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  parent: queueServices
  name: importQueueName
}

// Queue for nightly insight discovery signals
resource insightQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  parent: queueServices
  name: insightQueueName
}

// ── Azure SQL ──────────────────────────────────────────────────────────────────
resource sqlServer 'Microsoft.Sql/servers@2022-11-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: sqlAdminLogin
      sid: sqlAdminObjectId
      tenantId: subscription().tenantId
    }
  }
}

// Per-developer IP allowlist — only created when developerIpAddresses param is non-empty.
// Functions-to-SQL traffic goes via Azure internal routing and needs no firewall rule.
resource sqlFirewallDeveloperIps 'Microsoft.Sql/servers/firewallRules@2022-11-01-preview' = [for (ip, i) in developerIpAddresses: {
  parent: sqlServer
  name: 'developer-${i}'
  properties: {
    startIpAddress: ip
    endIpAddress: ip
  }
}]

// Allows GitHub-hosted runners (which are Azure VMs) to reach SQL during CI migrations.
// This rule name is special: Azure recognises 0.0.0.0→0.0.0.0 as "Allow Azure services".
resource sqlFirewallAllowAzureServices 'Microsoft.Sql/servers/firewallRules@2022-11-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2022-11-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
    capacity: 5
  }
  properties: {
    maxSizeBytes: 2147483648
  }
}

// ── Azure Key Vault ────────────────────────────────────────────────────────────
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: { family: 'A', name: 'standard' }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 30
    enablePurgeProtection: true
  }
}

// ── Functions Hosting Plan (Flex Consumption — Linux) ─────────────────────────
resource functionsPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: functionsPlanName
  location: location
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true
  }
}

// ── Azure Functions App ────────────────────────────────────────────────────────
resource functionsApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionsAppName
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentityResourceId}': {}
    }
  }
  properties: {
    serverFarmId: functionsPlan.id
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storageAccount.properties.primaryEndpoints.blob}${deploymentsContainerName}'
          authentication: {
            type: 'UserAssignedIdentity'
            userAssignedIdentityResourceId: managedIdentityResourceId
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: 10
        instanceMemoryMB: 2048
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
    }
    siteConfig: {
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        // AzureWebJobsStorage via managed identity (accountName pattern)
        {
          name: 'AzureWebJobsStorage__accountName'
          value: storageAccount.name
        }
        {
          name: 'AzureWebJobsStorage__credential'
          value: 'managedidentity'
        }
        {
          name: 'AzureWebJobsStorage__clientId'
          value: managedIdentityClientId
        }
        // Tell DefaultAzureCredential which managed identity to use
        {
          name: 'AZURE_CLIENT_ID'
          value: managedIdentityClientId
        }
        // SQL connection string — no password; uses Active Directory Default
        {
          name: 'SqlConnectionString'
          value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabaseName};Authentication=Active Directory Default;TrustServerCertificate=False;Encrypt=True;'
        }
        // Storage account name for blob/queue SDK clients
        {
          name: 'AzureStorageAccountName'
          value: storageAccount.name
        }
        // Key Vault URI for future secrets
        {
          name: 'KeyVaultUri'
          value: keyVault.properties.vaultUri
        }
      ]
    }
  }
  dependsOn: [
    deploymentsContainer
    blobDataContributorAssignment
    queueDataContributorAssignment
    keyVaultSecretsUserAssignment
  ]
}

// ── Azure Static Web App ───────────────────────────────────────────────────────
resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: swaName
  location: swaLocation
  sku: { name: 'Free', tier: 'Free' }
  properties: {
    stagingEnvironmentPolicy: 'Disabled'
    allowConfigFileUpdates: true
    buildProperties: {
      skipGithubActionWorkflowGeneration: true
    }
  }
}

// ── RBAC: Managed Identity → Storage Blob Data Contributor ────────────────────
resource blobDataContributorAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, managedIdentityObjectId, storageBlobDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: managedIdentityObjectId
    principalType: 'ServicePrincipal'
  }
}

// ── RBAC: Managed Identity → Storage Queue Data Contributor ───────────────────
resource queueDataContributorAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, managedIdentityObjectId, storageQueueDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageQueueDataContributorRoleId)
    principalId: managedIdentityObjectId
    principalType: 'ServicePrincipal'
  }
}

// ── RBAC: Managed Identity → Key Vault Secrets User ───────────────────────────
resource keyVaultSecretsUserAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, managedIdentityObjectId, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
    principalId: managedIdentityObjectId
    principalType: 'ServicePrincipal'
  }
}

// ── Outputs ────────────────────────────────────────────────────────────────────
@description('Storage account name (needed for local.settings.json)')
output storageAccountName string = storageAccount.name

@description('Storage account blob endpoint')
output storageAccountBlobEndpoint string = storageAccount.properties.primaryEndpoints.blob

@description('Functions app name (set as AZURE_FUNCTIONS_APP_NAME GitHub variable)')
output functionsAppName string = functionsApp.name

@description('SQL Server FQDN (for local.settings.json SqlConnectionString)')
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName

@description('SQL Database name')
output sqlDatabaseName string = sqlDatabase.name

@description('Key Vault URI (for local.settings.json KeyVaultUri)')
output keyVaultUri string = keyVault.properties.vaultUri

@description('Application Insights connection string (for local.settings.json)')
output appInsightsConnectionString string = appInsights.properties.ConnectionString

@description('Static Web App default hostname')
output staticWebAppHostname string = staticWebApp.properties.defaultHostname

@description('Static Web App name (for retrieving deployment token)')
output staticWebAppName string = staticWebApp.name
