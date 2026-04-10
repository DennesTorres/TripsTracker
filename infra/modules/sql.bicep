@description('Azure region for all resources')
param location string

@description('Environment tag: dev, test, prod')
param env string

@description('Unique suffix for globally unique resource names')
param uniqueSuffix string

@description('Object ID of the Azure AD group or managed identity to set as SQL Entra admin')
param sqlEntraAdminObjectId string

@description('Display name of the SQL Entra admin')
param sqlEntraAdminDisplayName string

@description('Resource ID of the Log Analytics workspace for diagnostics')
param logAnalyticsWorkspaceId string

// Note: managed identity database role assignment (db_datareader/db_datawriter/db_ddladmin)
// must be done via T-SQL post-deployment — see issue #145. Bicep cannot execute T-SQL.

var serverName = 'sql-tripstracker-${env}-${uniqueSuffix}'
var databaseName = 'TripsTracker'
var skuName = env == 'prod' ? 'S1' : 'Basic'
var skuTier = env == 'prod' ? 'Standard' : 'Basic'
var skuCapacity = env == 'prod' ? 20 : 5

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: serverName
  location: location
  tags: {
    environment: env
    project: 'tripstracker'
  }
  properties: {
    // Entra-only authentication — no SQL username/password
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: sqlEntraAdminDisplayName
      principalType: 'Group'
      sid: sqlEntraAdminObjectId
      tenantId: subscription().tenantId
    }
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource allowAzureServices 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource database 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: databaseName
  location: location
  tags: {
    environment: env
    project: 'tripstracker'
  }
  sku: {
    name: skuName
    tier: skuTier
    capacity: skuCapacity
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
  }
}

resource auditingSettings 'Microsoft.Sql/servers/auditingSettings@2023-08-01-preview' = {
  parent: sqlServer
  name: 'default'
  properties: {
    state: 'Enabled'
    isAzureMonitorTargetEnabled: true
  }
}

resource diagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'diag-${databaseName}'
  scope: database
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        categoryGroup: 'allLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'Basic'
        enabled: true
      }
    ]
  }
}

output serverName string = sqlServer.name
output serverFqdn string = sqlServer.properties.fullyQualifiedDomainName
output databaseName string = database.name
