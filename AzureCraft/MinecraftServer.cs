using Azure.ResourceManager.AppService;
using AzureCraft.Nbt;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace AzureCraft
{
    public class MinecraftServer : BackgroundService
    {
        private Process? process = null;
        private readonly string serverDirectory;
        private readonly string fileName;
        private readonly MinecraftServerDownloader downloader;
        private readonly ILogger<MinecraftServer> logger;

        public event Func<string?, Task>? OnMessageReceivedAsync;
        public event Func<string?, Task>? OnErrorReceivedAsync;

        private async Task EnsureEulaAcceptedAsync()
        {
            var eulaFilePath = Path.Combine(serverDirectory, "eula.txt");
            if (!File.Exists(eulaFilePath))
            {
                await File.WriteAllTextAsync(eulaFilePath, "eula=true");
                logger.LogInformation("EULA accepted. Created eula.txt file.");
            }
        }

        public async Task<IDictionary<string, string>> ReadServerPropertiesAsync()
        {
            var propertiesFilePath = Path.Combine(serverDirectory, "server.properties");
            if (!File.Exists(propertiesFilePath))
                return new Dictionary<string, string>();

            var properties = new Dictionary<string, string>();
            await foreach (var line in File.ReadLinesAsync(propertiesFilePath))
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;
                var parts = line.Split('=');
                if (parts.Length == 2)
                    properties[parts[0].Trim()] = parts[1].Trim();
            }
            return properties;
        }
        public async Task WriteServerPropertiesAsync(IDictionary<string, string> properties)
        {
            var propertiesFilePath = Path.Combine(serverDirectory, "server.properties");
            using (var writer = new StreamWriter(propertiesFilePath))
            {
                foreach (var kvp in properties)
                    await writer.WriteLineAsync($"{kvp.Key}={kvp.Value}");
            }
        }

        public async Task ConfigureServerPropertiesAsync()
        {
            var properties = await ReadServerPropertiesAsync();

            properties["level-type"] = "flat";
            properties["allow-command-block"] = "true";
            properties["enable-command-block"] = "true";
            properties["spawn-monsters"] = "false";
            properties["generate-structures"] = "false";
            properties["spawn-npcs"] = "false";
            properties["spawn-animals"] = "false";
            properties["difficulty"] = "peaceful";
            properties["gamemode"] = "creative";

            // enable rcon
            properties["enable-rcon"] = "true";
            properties["rcon.password"] = "your_password_here";

            await WriteServerPropertiesAsync(properties);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!File.Exists(Path.Combine(serverDirectory, fileName)))
            {
                await downloader.DownloadMinecraftServerAsync(Path.Combine(serverDirectory, fileName));
                await EnsureEulaAcceptedAsync();
                await ConfigureServerPropertiesAsync();
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "java",
                Arguments = $"-jar {fileName} nogui",
                WorkingDirectory = serverDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            process = new Process { StartInfo = processStartInfo };
            process.OutputDataReceived += (sender, e) =>
            {
                logger.LogInformation(e.Data);
                OnMessageReceivedAsync?.Invoke(e.Data)?.Wait();
            };
            process.ErrorDataReceived += (sender, e) =>
            {
                logger.LogError(e.Data);
                OnErrorReceivedAsync?.Invoke(e.Data)?.Wait();
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            stoppingToken.Register(() => process?.StandardInput.WriteLine("stop"));

            await process.WaitForExitAsync();

            process = null;
        }

        private readonly Lazy<Task<MinecraftCommandSender>>? rconClient;

        public MinecraftServer(string serverJarFileName, MinecraftServerDownloader downloader, ILogger<MinecraftServer> logger)
        {
            this.downloader = downloader;
            this.logger = logger;
            serverDirectory = Path.GetDirectoryName(serverJarFileName);
            fileName = Path.GetFileName(serverJarFileName);
            rconClient = new(async () =>
            {
                var properties = await ReadServerPropertiesAsync();
                if (properties.TryGetValue("rcon.password", out var password))
                {
                    var client = new MinecraftCommandSender(this, "localhost", 25575, password);
                    return client;
                }
                else
                {
                    throw new Exception("RCON password not found in server.properties.");
                }
            });
        }

        public async Task<MinecraftCommandSender> GetRconClientAsync()
        {
            return await rconClient.Value;
        }

        public class MinecraftCommandSender(MinecraftServer server, string address, int port, string secret)
        {
            private MinecraftRconClient client;

            public async Task SendCommandVoidAsync(string command) => await server.process.StandardInput.WriteLineAsync(command);

            public async Task<RconMessageResponse> SendCommandAsync(string command)
            {
                for (var i = 0; i < 3; i++)
                {
                    if (client is null)
                    {
                        client = new MinecraftRconClient(address, port);
                        if (!await client.AuthenticateAsync(secret))
                            throw new Exception("Failed to authenticate with RCON.");
                    }

                    try
                    {
                        return await client.SendCommandAsync(command);
                    }
                    catch (Exception ex)
                    {
                        client.Dispose();
                        client = null;
                    }
                    await Task.Delay((i + 1) * 1000);
                }

                throw new Exception("Failed to send command after 3 attempts.");
            }
        }
    }

    public class MinecraftBlock
    {
        public string Name { get; set; }
        public Dictionary<string, object>? Nbt { get; set; }

        public override string ToString()
        {
            return Nbt is null ? Name : $"{Name}{NbtSerializer.SerializeToSnbt(Nbt)}";
        }
    }
}