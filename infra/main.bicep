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

@description('Monthly budget limit in USD for Azure Cost Alerts (0 = disabled)')
param monthlyBudgetUsd int = 20

@description('Azure region for Static Web App (must be one of: westus2, centralus, eastus2, westeurope, eastasia)')
param swaLocation string = 'westeurope'

// ── Computed variables — break circular dependencies ──
// Azure SQL FQDN is predictable: {serverName}.database.windows.net
// SWA origin is predictable: https://{name}.azurestaticapps.net
// Computing both here avoids deployment ordering constraints.
var sqlServerName = 'sql-tripstracker-${env}-${uniqueSuffix}'
var sqlDatabaseName = 'TripsTracker'
var sqlServerFqdn = '${sqlServerName}${environment().suffixes.sqlServerHostname}'
var swaName = 'stapp-tripstracker-${env}-${uniqueSuffix}'
var swaOrigin = 'https://${swaName}.azurestaticapps.net'

// ── Log Analytics ─────────────────────────────────────────────────────────────
module logAnalytics 'modules/loganalytics.bicep' = {
  name: 'loganalytics'
  params: {
    location: location
    env: env
  }
}

// ── Azure Functions + Application Insights + Storage ─────────────────────────
// Deployed first so the managed identity principal ID is available for SQL role assignment.
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
    swaOrigin: swaOrigin
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

// ── Azure Static Web App (React frontend) ─────────────────────────────────────
// SWA has limited region support: westus2, centralus, eastus2, westeurope, eastasia
module staticWebApp 'modules/staticwebapp.bicep' = {
  name: 'staticwebapp'
  params: {
    location: swaLocation
    env: env
    uniqueSuffix: uniqueSuffix
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
