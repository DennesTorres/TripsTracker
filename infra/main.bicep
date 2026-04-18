// TripsTracker — Main Bicep orchestration
// Deploy with:
//   az deployment group create \
//     --resource-group rg-tripstracker-{env} \
//     --template-file infra/main.bicep \
//     --parameters infra/{env}.bicepparam

targetScope = 'resourceGroup'

@description('Azure region for all resources')
param location string = resourceGroup().location

@description('Environment: dev, test, or prod')
@allowed(['dev', 'test', 'prod'])
param env string

@description('Short unique suffix for globally unique names (4-6 chars, lowercase alphanumeric)')
param uniqueSuffix string

@description('Object ID of the Azure AD group to set as SQL Entra admin')
param sqlEntraAdminObjectId string

@description('Display name of the SQL Entra admin group')
param sqlEntraAdminDisplayName string

@description('User-Agent header value for Nominatim geocoding API requests')
param nominatimUserAgent string

@description('OIDC authority URL for JWT validation')
param authAuthority string = 'https://login.microsoftonline.com/common/v2.0'

@description('Application ID URI for JWT audience validation (api://{clientId})')
param authAudience string

@description('Monthly budget limit in USD for Azure Cost Alerts (0 = disabled)')
param monthlyBudgetUsd int = 20

@description('Azure region for Static Web App (must be one of: westus2, centralus, eastus2, westeurope, eastasia)')
param swaLocation string = 'westeurope'

// ── Computed variables ────────────────────────────────────────────────────────
// Azure SQL FQDN is predictable from the naming convention.
// SWA hostname is NOT predictable — Azure assigns a random subdomain.
// Functions CORS uses the actual SWA hostname from the SWA module output.
var sqlServerName = 'sql-tripstracker-${env}-${uniqueSuffix}'
var sqlDatabaseName = 'TripsTracker'
var sqlServerFqdn = '${sqlServerName}${environment().suffixes.sqlServerHostname}'

// ── Log Analytics ─────────────────────────────────────────────────────────────
module logAnalytics 'modules/loganalytics.bicep' = {
  name: 'loganalytics'
  params: {
    location: location
    env: env
  }
}

// ── Azure Static Web App (React frontend) ─────────────────────────────────────
// Deployed before Functions so its actual hostname is available for CORS config.
// SWA has limited region support: westus2, centralus, eastus2, westeurope, eastasia
module staticWebApp 'modules/staticwebapp.bicep' = {
  name: 'staticwebapp'
  params: {
    location: swaLocation
    env: env
    uniqueSuffix: uniqueSuffix
  }
}

// ── Azure Functions + Application Insights + Storage ─────────────────────────
module functions 'modules/functions.bicep' = {
  name: 'functions'
  params: {
    location: location
    env: env
    uniqueSuffix: uniqueSuffix
    sqlServerFqdn: sqlServerFqdn
    sqlDatabaseName: sqlDatabaseName
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
    nominatimUserAgent: nominatimUserAgent
    swaOrigin: 'https://${staticWebApp.outputs.staticWebAppHostname}'
    authAuthority: authAuthority
    authAudience: authAudience
  }
}

// ── Azure SQL Server + Database ───────────────────────────────────────────────
module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    location: location
    env: env
    uniqueSuffix: uniqueSuffix
    sqlEntraAdminObjectId: sqlEntraAdminObjectId
    sqlEntraAdminDisplayName: sqlEntraAdminDisplayName
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
  }
}

// ── Azure Cost Budget Alert ───────────────────────────────────────────────────
module budget 'modules/budget.bicep' = if (monthlyBudgetUsd > 0) {
  name: 'budget'
  params: {
    env: env
    monthlyBudgetUsd: monthlyBudgetUsd
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────
output functionAppName string = functions.outputs.functionAppName
output functionAppPrincipalId string = functions.outputs.functionAppPrincipalId
output sqlServerFqdn string = sqlServerFqdn
output sqlDatabaseName string = sqlDatabaseName
output staticWebAppHostname string = staticWebApp.outputs.staticWebAppHostname
output staticWebAppDeploymentToken string = staticWebApp.outputs.deploymentToken
output sqlServerIdentityPrincipalId string = sql.outputs.sqlServerIdentityPrincipalId
