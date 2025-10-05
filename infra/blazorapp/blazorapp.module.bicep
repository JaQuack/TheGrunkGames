@description('The location for the resource(s) to be deployed.')
param location string = resourceGroup().location

param thegrunkgames_outputs_azure_container_apps_environment_default_domain string

param thegrunkgames_outputs_azure_container_apps_environment_id string

param thegrunkgames_outputs_azure_container_registry_endpoint string

param thegrunkgames_outputs_azure_container_registry_managed_identity_id string

param blazorapp_containerimage string

param blazorapp_containerport string

param certificateName string

param customDomain string

resource blazorapp 'Microsoft.App/containerApps@2025-02-02-preview' = {
  name: 'blazorapp'
  location: location
  properties: {
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: int(blazorapp_containerport)
        transport: 'http'
        customDomains: [
          {
            name: customDomain
            bindingType: (certificateName != '') ? 'SniEnabled' : 'Disabled'
            certificateId: (certificateName != '') ? '${thegrunkgames_outputs_azure_container_apps_environment_id}/managedCertificates/${certificateName}' : null
          }
        ]
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
          image: blazorapp_containerimage
          name: 'blazorapp'
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
              value: blazorapp_containerport
            }
            {
              name: 'services__gameservice__http__0'
              value: 'http://gameservice.${thegrunkgames_outputs_azure_container_apps_environment_default_domain}'
            }
            {
              name: 'services__gameservice__https__0'
              value: 'https://gameservice.${thegrunkgames_outputs_azure_container_apps_environment_default_domain}'
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