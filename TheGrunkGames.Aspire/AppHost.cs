using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.TheGrunkGames>("gameservice");

var blazorApp = builder.AddProject<Projects.TheGrunkGames_BlazorApp>("blazorapp").WithReference(api);

api.WithReference(blazorApp);

builder.Build().Run();
