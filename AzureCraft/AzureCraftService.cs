using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics.Eventing.Reader;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace AzureCraft
{
    public class AzureCraftService(AzureSettings settings,
        MinecraftServer minecraftServer,
        ILogger<AzureCraftService> logger) : BackgroundService
    {
        private readonly ArmClient client = new ArmClient(new DefaultAzureCredential(), settings.SubscriptionId);
        private readonly ResourceIdentifier resourceGroupId = ResourceGroupResource.CreateResourceIdentifier(settings.SubscriptionId, settings.ResourceGroupName);

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask;
        }

        public async Task ExecuteAzCraftCommandAsync(AzCraftCommand command)
        {
            var commandName = command.CommandName;

            if (command.Arguments is null)
                command.Arguments = [];

            // look for the method annotated with the same name as the command
            var method = GetType().GetMethods()
                .FirstOrDefault(m => m.GetCustomAttribute<AzCraftCommandAttribute>()?.CommandName == commandName);

            if (method is null)
                throw new AzCraftCommandException($"Command {commandName} not found");

            var parameters = method.GetParameters();

            if (parameters.Length < (command.Arguments.Length))
                throw new AzCraftCommandException($"Command {commandName} has {parameters.Length} parameters, but {command.Arguments.Length} were provided");

            var args = new object[parameters.Length];

            foreach (var (index, (value, parameter)) in command.Arguments.Zip(parameters).Index())
                args[index] = value.Deserialize(parameter.ParameterType)!;

            var result = method.Invoke(this, args);

            if (result is Task task)
                await task;
        }

        [AzCraftCommand("hello")]
        public async Task HelloAsync(string name)
        {
            logger.LogInformation($"Hello {name}!");
        }

        [AzCraftCommand("list-resources")]
        public async Task ListResourcesAsync()
        {
            var resources = client.GetResourceGroupResource(resourceGroupId)
                .GetGenericResourcesAsync();

            await foreach (var resource in resources)
            {
                await minecraftServer.SayAsync(resource.Data.Name);
                logger.LogInformation($"Resource: {resource.Data.Name} ({resource.Data.ResourceType})");
            }
        }

        [AzCraftCommand("build")]
        public async Task BuildAsync()
        {
            var resources = client.GetResourceGroupResource(resourceGroupId)
            .GetGenericResourcesAsync();

            await minecraftServer.KillAsync("@e[type=!player]");
            await Task.Delay(1500);
            await minecraftServer.KillAsync("@e[type=!player]");
            await Task.Delay(1500);

            var index = 0;
            await foreach (var resource in resources)
            {
                var cellX = index % 5;
                var cellZ = index / 5;

                var cellSize = 4;

                var y = -60;
                var x = cellX * cellSize;
                var z = cellZ * cellSize;

                await minecraftServer.FillAsync(new(x, y, z), new(x + cellSize, y + 10, z + cellSize), MinecraftItemId.Air);
                await minecraftServer.FillAsync(new(x, y, z), new(x + cellSize, y, z + cellSize), MinecraftItemId.GrassBlock);

                y++;

                await minecraftServer.FillAsync(new(x, y, z), new(x + cellSize, y, z + cellSize), MinecraftItemId.OakFence);
                await minecraftServer.FillAsync(new(x + 1, y, z + 1), new(x + cellSize - 1, y, z + cellSize - 1), MinecraftItemId.Air);

                // determine resource type
                var resourceType = resource.Data.ResourceType;

                // check if it is a webapp
                if (resourceType == WebSiteResource.ResourceType)
                {
                    var webSite = client.GetWebSiteResource(resource.Id);
                    if (!webSite.HasData)
                        webSite = (await webSite.GetAsync()).Value;

                    var block = webSite.Data.State switch
                    {
                        "Running" => MinecraftItemId.GreenConcrete,
                        "Stopped" => MinecraftItemId.RedConcrete,
                        _ => MinecraftItemId.GrayConcrete,
                    };

                    await minecraftServer.FillAsync(new(x + 1, y, z + 1), new(x + cellSize - 1, y, z + cellSize - 1), block);

                    await minecraftServer.SetBlockAsync(new(x + 1, y + 1, z + 1), MinecraftItemId.CommandBlock, new
                    {
                        Command = $"say pwsh:az webapp stop -n \"{webSite.Data.Name}\" -g {webSite.Data.ResourceGroup}"
                    });
                    await minecraftServer.SetBlockAsync(new(x + 1, y + 2, z + 1), MinecraftItemId.CrimsonButton);

                    await minecraftServer.SetBlockAsync(new(x + cellSize - 1, y + 1, z + 1), MinecraftItemId.CommandBlock, new
                    {
                        Command = $"say pwsh:az webapp start -n \"{webSite.Data.Name}\" -g {webSite.Data.ResourceGroup}"
                    });
                    await minecraftServer.SetBlockAsync(new(x + cellSize - 1, y + 2, z + 1), MinecraftItemId.WarpedButton);
                }

                y++;

                await minecraftServer.SummonAsync("chicken", new MinecraftPosition()
                {
                    X = x + 2,
                    Y = y,
                    Z = z + 2
                }, new { NoAI = true, CustomName = new[] { new { text = resource.Data.Name } } });
                logger.LogInformation($"Resource: {resource.Data.Name} ({resource.Data.ResourceType})");

                index++;
            }
        }
    }
}
