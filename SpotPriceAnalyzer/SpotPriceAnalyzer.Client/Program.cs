using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace SpotPriceAnalyzer.Client
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.Services.AddBlazorBootstrap();

            await builder.Build().RunAsync();
        }
    }
}
