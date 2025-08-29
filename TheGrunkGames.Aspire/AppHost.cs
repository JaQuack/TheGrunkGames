using Aspire.Hosting;
using Aspire.Hosting.Publishing;

#pragma warning disable ASPIREACADOMAINS001

var builder = DistributedApplication.CreateBuilder(args);

var customDomain = builder.AddParameter("customDomain");
var certificateName = builder.AddParameter("certificateName", value: "", publishValueAsDefault: true);

builder.AddAzureContainerAppEnvironment("TheGrunkGames");

var api = builder.AddProject<Projects.TheGrunkGames>("gameservice")
    .WithExternalHttpEndpoints();


var blazorApp = builder.AddProject<Projects.TheGrunkGames_BlazorApp>("blazorapp")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .PublishAsAzureContainerApp((infra, app) =>
    {
        app.ConfigureCustomDomain(customDomain, certificateName);
    });

api.WithReference(blazorApp);

builder.Build().Run();
