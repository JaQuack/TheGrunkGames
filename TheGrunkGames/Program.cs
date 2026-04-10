using Azure.Data.Tables;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scalar.AspNetCore;
using System;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using TheGrunkGames.Hubs;
using TheGrunkGames.Services;

namespace TheGrunkGames
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            var builder = WebApplication.CreateBuilder(args);
            builder.AddServiceDefaults();
            builder.AddMongoDBClient("thegrunkgames");

            var archiveConnectionString = builder.Configuration.GetConnectionString("archiveTables");
            if (!string.IsNullOrEmpty(archiveConnectionString))
                builder.AddAzureTableServiceClient("archiveTables");

            builder.Services.AddProblemDetails();
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All);
                    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                });
            builder.Services.AddSingleton<MatchmakingService>();
            builder.Services.AddSingleton<ITournamentArchiveService>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<TournamentArchiveService>>();
                var tableServiceClient = sp.GetService<TableServiceClient>();
                return tableServiceClient != null
                    ? new TournamentArchiveService(tableServiceClient, logger)
                    : new TournamentArchiveService(logger);
            });
            builder.Services.AddSingleton<IStorageService, StorageService>();
            builder.Services.AddSingleton<IGameService, GameService>();
            builder.Services.AddSignalR();
            builder.Services.AddOpenApi();

            var app = builder.Build();

            var gameService = app.Services.GetRequiredService<IGameService>();
            await gameService.InitializeAsync();

            app.MapDefaultEndpoints();

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.MapOpenApi();
                app.MapScalarApiReference();
            }
            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseCors((cors) => 
            {
                cors.AllowAnyHeader();
                cors.AllowAnyMethod();
                cors.AllowAnyOrigin();

            });

            app.MapControllers();
            app.MapGet("/", () => Results.Redirect("/scalar/v1"));
            app.MapHub<TournamentHub>("/hubs/tournament");

            await app.RunAsync();
        }
    }
}
