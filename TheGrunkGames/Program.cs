using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using TheGrunkGames.Services;

namespace TheGrunkGames
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.AddServiceDefaults();

            builder.Services.AddControllers();
            builder.Services.AddSingleton<GameService>();
            builder.Services.AddSingleton<StorageService>();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "TheGrunkGames", Version = "v1" });
            });

            builder.Services.AddMvc();
            builder.Services.AddMvcCore();
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    var clients = builder.Configuration.GetServiceEndpoints("front");  // Get the http and https endpoints for the client known by resource name as "blazor" in the AppHost.
                                                                                       // var clients = builder.Configuration.GetServiceEndpoints("blazor1", "blazor2"); // This overload does the same thing for multiple clients.
                                                                                       // var clients = builder.Configuration.GetServiceEndpoint("blazor", "http"); // This overload gets a single named endpoint for a single resource. In this case, the "http" endpoint for the "blazor" resource.

                    policy.WithOrigins(clients); // Add the clients as allowed origins for cross origin resource sharing.
                    policy.AllowAnyMethod();
                    policy.WithHeaders("X-Requested-With");
                });
            });


            var app = builder.Build();

            app.MapDefaultEndpoints();

            if (builder.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "TheGrunkGames v1"));
            }
            app.UseHttpsRedirection();
            app.UseRouting();

            app.MapControllers();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");


            app.Run();
        }
    }
}
