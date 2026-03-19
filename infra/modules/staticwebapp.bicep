@description('Azure region for all resources')
param location string

@description('Environment tag: dev, test, prod')
param env string

@description('Unique suffix for globally unique resource names')
param uniqueSuffix string

var staticWebAppName = 'stapp-tripstracker-${env}-${uniqueSuffix}'
var skuName = env == 'prod' ? 'Standard' : 'Free'

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staticWebAppName
  location: location
  tags: {
    environment: env
    project: 'tripstracker'
  }
  sku: {
    name: skuName
    tier: skuName
  }
  properties: {
    // GitHub Actions deployment token is retrieved post-deploy and stored as a secret
    buildProperties: {
      skipGithubActionWorkflowGeneration: true
    }
  }
}

output staticWebAppName string = staticWebApp.name
output staticWebAppHostname string = staticWebApp.properties.defaultHostname
output deploymentToken string = staticWebApp.listSecrets().properties.apiKey
