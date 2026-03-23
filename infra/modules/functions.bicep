@description('Azure region for all resources')
param location string

@description('Environment tag: dev, test, prod')
param env string

@description('Unique suffix for globally unique resource names')
param uniqueSuffix string

@description('Azure SQL Server fully qualified domain name')
param sqlServerFqdn string

@description('Azure SQL database name')
param sqlDatabaseName string

@description('Resource ID of the Log Analytics workspace for diagnostics')
param logAnalyticsWorkspaceId string

var storageAccountName = 'sttripstracker${env}${uniqueSuffix}'
var appServicePlanName = 'asp-tripstracker-${env}'
var functionAppName = 'func-tripstracker-${env}-${uniqueSuffix}'
var appInsightsName = 'appi-tripstracker-${env}'

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  tags: {
    environment: env
    project: 'tripstracker'
  }
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: {
    environment: env
    project: 'tripstracker'
  }
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspaceId
    RetentionInDays: 30
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  tags: {
    environment: env
    project: 'tripstracker'
  }
  kind: 'functionapp'
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  tags: {
    environment: env
    project: 'tripstracker'
  }
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v10.0'
      use32BitWorkerProcess: false
      cors: {
        // SWA origin added after provisioning — placeholder updated per environment
        allowedOrigins: ['https://stapp-tripstracker-${env}-${uniqueSuffix}.azurestaticapps.net']
      }
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'ApplicationInsights__ConnectionString'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'Database__ConnectionString'
          // Managed Identity — no username/password
          value: 'Server=${sqlServerFqdn};Database=${sqlDatabaseName};Authentication=Active Directory Default;TrustServerCertificate=False;Encrypt=True;'
        }
      ]
    }
  }
}

output functionAppName string = functionApp.name
output functionAppPrincipalId string = functionApp.identity.principalId
output appInsightsConnectionString string = appInsights.properties.ConnectionString
