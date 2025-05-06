using Microsoft.Extensions.Logging;

namespace AzureCraft
{
    public class MinecraftServerDownloader(ILogger<MinecraftServerDownloader> logger)
    {
        public async Task DownloadMinecraftServerAsync(string fileName = "server.jar")
        {
            var uri = new Uri("https://piston-data.mojang.com/v1/objects/e6ec2f64e6080b9b5d9b471b291c33cc7f509733/server.jar");
            using var client = new HttpClient();
            var response = await client.GetAsync(uri);

            if (response.IsSuccessStatusCode)
                using (var fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None))
                    await response.Content.CopyToAsync(fileStream);
            else
                logger.LogError("Failed to download the Minecraft server jar file. Status code: {StatusCode}", response.StatusCode);
        }
    }
}
