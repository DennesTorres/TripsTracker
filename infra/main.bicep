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

// ── Log Analytics ─────────────────────────────────────────────────────────────
module logAnalytics 'modules/loganalytics.bicep' = {
  name: 'loganalytics'
  params: {
    location: location
    env: env
  }
}

// ── Azure Functions + Application Insights + Storage ─────────────────────────
// Deployed before SQL so we can capture the managed identity principal ID
module functions 'modules/functions.bicep' = {
  name: 'functions'
  params: {
    location: location
    env: env
    uniqueSuffix: uniqueSuffix
    sqlServerFqdn: sql.outputs.serverFqdn
    sqlDatabaseName: sql.outputs.databaseName
    logAnalyticsWorkspaceId: logAnalytics.outputs.workspaceId
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

// ── Outputs ───────────────────────────────────────────────────────────────────
output functionAppName string = functions.outputs.functionAppName
output functionAppPrincipalId string = functions.outputs.functionAppPrincipalId
output sqlServerFqdn string = sql.outputs.serverFqdn
output sqlDatabaseName string = sql.outputs.databaseName
output staticWebAppHostname string = staticWebApp.outputs.staticWebAppHostname
output staticWebAppDeploymentToken string = staticWebApp.outputs.deploymentToken
