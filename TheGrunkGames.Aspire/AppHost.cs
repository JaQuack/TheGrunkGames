using Aspire.Hosting;
using Aspire.Hosting.Publishing;

#pragma warning disable ASPIREACADOMAINS001

var builder = DistributedApplication.CreateBuilder(args);

var mongo = builder.AddMongoDB("mongodb")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume("thegrunkgames-mongo-data");
var mongoDb = mongo.AddDatabase("thegrunkgames");

var archiveTables = builder.AddConnectionString("archiveTables");

var api = builder.AddProject<Projects.TheGrunkGames>("gameservice")
    .WithExternalHttpEndpoints()
    .WithReference(mongoDb)
    .WithReference(archiveTables)
    .WaitFor(mongoDb);


var blazorApp = builder.AddProject<Projects.TheGrunkGames_BlazorApp>("blazorapp")
    .WithExternalHttpEndpoints()
    .WithReference(api);

api.WithReference(blazorApp);

builder.Build().Run();
