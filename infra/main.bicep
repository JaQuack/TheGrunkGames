targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the environment that can be used as part of naming resource convention, the name of the resource group for your application will use this name, prefixed with rg-')
param environmentName string

@minLength(1)
@description('The location used for all deployed resources')
param location string

@description('Id of the user or app to assign application roles')
param principalId string = ''

param certificateName string = ''
param customDomain string

var tags = {
  'azd-env-name': environmentName
}

resource rg 'Microsoft.Resources/resourceGroups@2022-09-01' = {
  name: 'rg-${environmentName}'
  location: location
  tags: tags
}

module TheGrunkGames 'TheGrunkGames/TheGrunkGames.module.bicep' = {
  name: 'TheGrunkGames'
  scope: rg
  params: {
    location: location
    userPrincipalId: principalId
  }
}
output AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = TheGrunkGames.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN
output AZURE_CONTAINER_REGISTRY_ENDPOINT string = TheGrunkGames.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output THEGRUNKGAMES_AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN string = TheGrunkGames.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN
output THEGRUNKGAMES_AZURE_CONTAINER_APPS_ENVIRONMENT_ID string = TheGrunkGames.outputs.AZURE_CONTAINER_APPS_ENVIRONMENT_ID
output THEGRUNKGAMES_AZURE_CONTAINER_REGISTRY_ENDPOINT string = TheGrunkGames.outputs.AZURE_CONTAINER_REGISTRY_ENDPOINT
output THEGRUNKGAMES_AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID string = TheGrunkGames.outputs.AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID
output AZURE_CERTIFICATE_NAME string = certificateName
