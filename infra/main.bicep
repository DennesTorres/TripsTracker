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

// ── Computed variables — break circular dependency between Functions and SQL ──
// Azure SQL FQDN is predictable: {serverName}.database.windows.net
// Computing it here avoids Functions depending on SQL outputs while SQL depends
// on Functions outputs (managed identity principal ID).
var sqlServerName = 'sql-tripstracker-${env}-${uniqueSuffix}'
var sqlDatabaseName = 'TripsTracker'
var sqlServerFqdn = '${sqlServerName}.database.windows.net'

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
  }
}

// ── Azure SQL Server + Database ───────────────────────────────────────────────
// Depends on Functions (one-way) for the managed identity principal ID.
module sql 'modules/sql.bicep' = {
  name: 'sql'
  params: {
    location: location
    env: env
    uniqueSuffix: uniqueSuffix
    sqlEntraAdminObjectId: sqlEntraAdminObjectId
    sqlEntraAdminDisplayName: sqlEntraAdminDisplayName
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
    functionAppPrincipalId: functions.outputs.functionAppPrincipalId
  }
}

// ── Azure Static Web App (React frontend) ─────────────────────────────────────
module staticWebApp 'modules/staticwebapp.bicep' = {
  name: 'staticwebapp'
  params: {
    location: location
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
