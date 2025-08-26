using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
namespace TheGrunkGames.Frontend;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        builder.AddServiceDefaults();

        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");



        builder.Services.AddScoped<GameApiService>();
        builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("https+http://gameservice") });

        await builder.Build().RunAsync();
    }
}
