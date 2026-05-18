@description('Azure region for all resources')
param location string

@description('Environment tag: dev, test, prod')
param env string

var workspaceName = 'log-tripstracker-${env}'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: workspaceName
  location: location
  tags: {
    environment: env
    project: 'tripstracker'
  }
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

output workspaceId string = logAnalytics.id
output workspaceName string = logAnalytics.name
