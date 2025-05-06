using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AzureCraft
{
    public class AzureCraftServerBridge : BackgroundService, IDisposable
    {
        private readonly MinecraftServer minecraftServer;
        private readonly PowerShellService powerShellService;
        private readonly AzureCraftService azCraftCommandHandler;
        private readonly ILogger<AzureCraftServerBridge> logger;

        public AzureCraftServerBridge(MinecraftServer minecraftServer
            , PowerShellService powerShellService
            , AzureCraftService azCraftCommandHandler
            , ILogger<AzureCraftServerBridge> logger)
        {
            minecraftServer.OnMessageReceivedAsync += OnMessageReceivedAsync;
            minecraftServer.OnErrorReceivedAsync += OnErrorReceivedAsync;
            this.minecraftServer = minecraftServer;
            this.powerShellService = powerShellService;
            this.azCraftCommandHandler = azCraftCommandHandler;
            this.logger = logger;
        }

        override protected Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;

        public override void Dispose()
        {
            GC.SuppressFinalize(this);
            minecraftServer.OnMessageReceivedAsync -= OnMessageReceivedAsync;
            minecraftServer.OnErrorReceivedAsync -= OnErrorReceivedAsync;
        }

        private async Task OnMessageReceivedAsync(string rawmessage)
        {
            try
            {
                // message format is [18:41:02] [Server thread/INFO]: TerrazzoSgabello joined the game
                // lets parse the message:

                var regex = new Regex(@"\[(\d{2}:\d{2}:\d{2})\] \[.*?\]: (.+)");
                var match = regex.Match(rawmessage);
                if (!match.Success)
                {
                    logger.LogWarning("Failed to parse message: {Message}", rawmessage);
                    return;
                }

                var time = match.Groups[1].Value;
                var message = match.Groups[2].Value;

                if (message.Contains("joined the game"))
                {
                    var playerName = message.Split(' ')[0];
                    await OnPlayerJoinAsync(playerName);
                }

                if (message.Contains("left the game"))
                {
                    var playerName = message.Split(' ')[0];
                    await OnPlayerLeaveAsync(playerName);
                }

                // lets parse an azcraft command:
                // [18:45:20] [Server thread/INFO]: <TerrazzoSgabello> AzCraft:


                if (message.Contains("AzCraft:"))
                {
                    var playerName = message.Split(' ')[0];
                    var command = message.Split(["AzCraft:"], StringSplitOptions.None)[1].Trim();
                    await OnAzCraftCommandReceivedAsync(command);
                }

                if (message.Contains("pwsh:"))
                {
                    var playerName = message.Split(' ')[0];
                    var command = message.Split(["pwsh:"], StringSplitOptions.None)[1].Trim();
                    var result = await powerShellService.ExecuteCommandAsync(command);
                    logger.LogInformation("PowerShell command result: {Result}", result);
                    var client = await minecraftServer.GetRconClientAsync();

                    foreach (var line in result.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
                        await client.SendCommandAsync($"say {line}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process message: {Message}", rawmessage);
            }
        }
        private Task OnErrorReceivedAsync(string error) => Task.CompletedTask;

        private async Task OnPlayerJoinAsync(string playerName)
        {
            //var client = await minecraftServer.GetRconClientAsync();
            //await client.SendCommandAsync($"op {playerName}");
        }
        private Task OnPlayerLeaveAsync(string playerName) => Task.CompletedTask;
        private async Task OnAzCraftCommandReceivedAsync(string serializedCommand)
        {
            try
            {
                AzCraftCommand? azCraftCommand = null;
                try
                {
                    azCraftCommand = JsonSerializer.Deserialize<AzCraftCommand>(serializedCommand);
                }
                catch
                {
                    azCraftCommand = new() { CommandName = serializedCommand };
                }

                if (azCraftCommand is null)
                {
                    logger.LogError("Failed to deserialize AzCraft command: {Command}", serializedCommand);
                    return;
                }

                await azCraftCommandHandler.ExecuteAzCraftCommandAsync(azCraftCommand);
            }
            catch (Exception ex)
            {

                logger.LogError(ex, "Failed to execute AzCraft command: {Command}", serializedCommand);
            }
        }
    }
}
