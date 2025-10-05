@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param userPrincipalId string

param tags object = { }

resource TheGrunkGames_mi 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: take('TheGrunkGames_mi-${uniqueString(resourceGroup().id)}', 128)
  location: location
  tags: tags
}

resource TheGrunkGames_acr 'Microsoft.ContainerRegistry/registries@2025-04-01' = {
  name: take('TheGrunkGamesacr${uniqueString(resourceGroup().id)}', 50)
  location: location
  sku: {
    name: 'Basic'
  }
  tags: tags
}

resource TheGrunkGames_acr_TheGrunkGames_mi_AcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(TheGrunkGames_acr.id, TheGrunkGames_mi.id, subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d'))
  properties: {
    principalId: TheGrunkGames_mi.properties.principalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
    principalType: 'ServicePrincipal'
  }
  scope: TheGrunkGames_acr
}

resource TheGrunkGames_law 'Microsoft.OperationalInsights/workspaces@2025-02-01' = {
  name: take('TheGrunkGameslaw-${uniqueString(resourceGroup().id)}', 63)
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
  }
  tags: tags
}

resource TheGrunkGames 'Microsoft.App/managedEnvironments@2025-01-01' = {
  name: take('thegrunkgames${uniqueString(resourceGroup().id)}', 24)
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: TheGrunkGames_law.properties.customerId
        sharedKey: TheGrunkGames_law.listKeys().primarySharedKey
      }
    }
    workloadProfiles: [
      {
        name: 'consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
  tags: tags
}

resource aspireDashboard 'Microsoft.App/managedEnvironments/dotNetComponents@2024-10-02-preview' = {
  name: 'aspire-dashboard'
  properties: {
    componentType: 'AspireDashboard'
  }
  parent: TheGrunkGames
}

output AZURE_LOG_ANALYTICS_WORKSPACE_NAME string = TheGrunkGames_law.name

output AZURE_LOG_ANALYTICS_WORKSPACE_ID string = TheGrunkGames_law.id

output AZURE_CONTAINER_REGISTRY_NAME string = TheGrunkGames_acr.name

output AZURE_CONTAINER_REGISTRY_ENDPOINT string = TheGrunkGames_acr.properties.loginServer

output AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID string = TheGrunkGames_mi.id

output AZURE_CONTAINER_APPS_ENVIRONMENT_NAME string = TheGrunkGames.name

output AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = TheGrunkGames.id

output AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = TheGrunkGames.properties.defaultDomain