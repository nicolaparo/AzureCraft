using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AzureCraft
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Configuration.AddUserSecrets<Program>();

            var subscriptionId = builder.Configuration["AZURE_SUBSCRIPTION_ID"]!;
            var resourceGroupName = builder.Configuration["AZURE_RESOURCE_GROUP_NAME"]!;

            var settings = new AzureSettings()
            {
                SubscriptionId = subscriptionId,
                ResourceGroupName = resourceGroupName
            };

            builder.Services.AddSingleton(settings);

            builder.Services.AddSingleton<MinecraftServerDownloader>();
            builder.Services.AddSingleton(sp => ActivatorUtilities.CreateInstance<MinecraftServer>(sp, "server.jar"));
            builder.Services.AddSingleton<AzureCraftServerBridge>();
            builder.Services.AddSingleton<PowerShellService>();
            builder.Services.AddSingleton<AzureCraftService>();

            builder.Services.AddHostedService(sp => sp.GetRequiredService<MinecraftServer>());
            builder.Services.AddHostedService(sp => sp.GetRequiredService<AzureCraftServerBridge>());
            builder.Services.AddHostedService(sp => sp.GetRequiredService<AzureCraftService>());

            var app = builder.Build();

            await app.RunAsync();
        }
    }
}
