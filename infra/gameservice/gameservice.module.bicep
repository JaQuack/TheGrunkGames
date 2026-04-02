@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param thegrunkgames_outputs_azure_container_apps_environment_default_domain string

param thegrunkgames_outputs_azure_container_apps_environment_id string

param thegrunkgames_outputs_azure_container_registry_endpoint string

param thegrunkgames_outputs_azure_container_registry_managed_identity_id string

param gameservice_containerimage string

param gameservice_containerport string

resource gameservice 'Microsoft.App/containerApps@2025-02-02-preview' = {
  name: 'gameservice'
  location: location
  properties: {
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: int(gameservice_containerport)
        transport: 'http'
      }
      registries: [
        {
          server: thegrunkgames_outputs_azure_container_registry_endpoint
          identity: thegrunkgames_outputs_azure_container_registry_managed_identity_id
        }
      ]
      runtime: {
        dotnet: {
          autoConfigureDataProtection: true
        }
      }
    }
    environmentId: thegrunkgames_outputs_azure_container_apps_environment_id
    template: {
      containers: [
        {
          image: gameservice_containerimage
          name: 'gameservice'
          env: [
            {
              name: 'OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EXCEPTION_LOG_ATTRIBUTES'
              value: 'true'
            }
            {
              name: 'OTEL_DOTNET_EXPERIMENTAL_OTLP_EMIT_EVENT_LOG_ATTRIBUTES'
              value: 'true'
            }
            {
              name: 'OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY'
              value: 'in_memory'
            }
            {
              name: 'ASPNETCORE_FORWARDEDHEADERS_ENABLED'
              value: 'true'
            }
            {
              name: 'HTTP_PORTS'
              value: gameservice_containerport
            }
            {
              name: 'services__blazorapp__http__0'
              value: 'http://blazorapp.${thegrunkgames_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'services__blazorapp__https__0'
              value: 'https://blazorapp.${thegrunkgames_outputs_azure_container_apps_environment_default_domain}'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
      }
    }
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${thegrunkgames_outputs_azure_container_registry_managed_identity_id}': { }
    }
  }
}