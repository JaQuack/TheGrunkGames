using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;
using TheGrunkGames.Hubs;
using TheGrunkGames.Services;

namespace TheGrunkGames
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.AddServiceDefaults();
            builder.AddMongoDBClient("thegrunkgames");

            builder.Services.AddProblemDetails();
            builder.Services.AddControllers();
            builder.Services.AddSingleton<MatchmakingService>();
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
            }
            app.MapOpenApi();
            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseCors((cors) => 
            {
                cors.AllowAnyHeader();
                cors.AllowAnyMethod();
                cors.AllowAnyOrigin();

            });

            app.MapControllers();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");
            app.MapHub<TournamentHub>("/hubs/tournament");

            await app.RunAsync();
        }
    }
}
