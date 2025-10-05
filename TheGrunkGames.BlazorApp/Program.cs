using TheGrunkGames.BlazorApp.Components;
using Microsoft.Extensions.ServiceDiscovery.Http;
using Microsoft.Extensions.Http;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace TheGrunkGames.BlazorApp;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.AddServiceDefaults();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
        });

        builder.Services.AddHttpClient<GameServiceClient>(_ => _.BaseAddress = new Uri("https+http://gameservice"));

        builder.Services.AddSingleton(sp =>
        {
            var connection = new HubConnectionBuilder()
                .WithUrl("https+http://gameservice/hubs/tournament", options =>
                {
                    options.HttpMessageHandlerFactory = _ =>
                        sp.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler();
                })
                .WithAutomaticReconnect()
                .Build();

            return new TournamentHubConnection(connection);
        });

        var oidcAuthority = builder.Configuration["Authentication:Authority"];
        var authEnabled = !string.IsNullOrEmpty(oidcAuthority);

        if (authEnabled)
        {
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie()
            .AddOpenIdConnect(options =>
            {
                options.Authority = oidcAuthority;
                options.ClientId = builder.Configuration["Authentication:ClientId"];
                options.ClientSecret = builder.Configuration["Authentication:ClientSecret"];
                options.ResponseType = OpenIdConnectResponseType.Code;
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.Scope.Add("openid");
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.MapInboundClaims = false;
            });

            var adminEmails = builder.Configuration.GetSection("Authentication:AdminEmails")
                .Get<string[]>() ?? [];

            builder.Services.AddAuthorizationBuilder()
                .AddPolicy("Admin", policy =>
                {
                    if (adminEmails.Length > 0)
                        policy.RequireClaim("email", adminEmails);
                    else
                        policy.RequireAuthenticatedUser();
                });
        }
        else
        {
            builder.Services.AddAuthorizationBuilder()
                .AddPolicy("Admin", policy => policy.RequireAssertion(_ => true));
        }

        builder.Services.AddCascadingAuthenticationState();

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        var app = builder.Build();

        app.MapDefaultEndpoints();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        if (authEnabled)
        {
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapGet("/auth/login", (string? returnUrl) =>
                TypedResults.Challenge(
                    new AuthenticationProperties { RedirectUri = returnUrl ?? "/admin/tournament" },
                    [OpenIdConnectDefaults.AuthenticationScheme]));

            app.MapGet("/auth/logout", async (HttpContext context) =>
            {
                await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme,
                    new AuthenticationProperties { RedirectUri = "/" });
            });
        }

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.Run();
    }
}
