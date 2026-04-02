using './gameservice.module.bicep'

param gameservice_containerimage = '{{ .Image }}'
param gameservice_containerport = '{{ targetPortOrDefault 8080 }}'
param thegrunkgames_outputs_azure_container_apps_environment_default_domain = '{{ .Env.THEGRUNKGAMES_AZURE_CONTAINER_APPS_ENVIRONMENT_DEFAULT_DOMAIN }}'
param thegrunkgames_outputs_azure_container_apps_environment_id = '{{ .Env.THEGRUNKGAMES_AZURE_CONTAINER_APPS_ENVIRONMENT_ID }}'
param thegrunkgames_outputs_azure_container_registry_endpoint = '{{ .Env.THEGRUNKGAMES_AZURE_CONTAINER_REGISTRY_ENDPOINT }}'
param thegrunkgames_outputs_azure_container_registry_managed_identity_id = '{{ .Env.THEGRUNKGAMES_AZURE_CONTAINER_REGISTRY_MANAGED_IDENTITY_ID }}'
