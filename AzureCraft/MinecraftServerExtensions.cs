using AzureCraft.Nbt;

namespace AzureCraft
{
    public static class MinecraftServerExtensions
    {
        public static async Task ExecuteAsync(this MinecraftServer server, string command)
        {
            try
            {
                await (await server.GetRconClientAsync()).SendCommandAsync(command);
                //await Task.Delay(100);
            }
            catch (Exception e)
            {

            }
        }
        public static async Task ExecuteVoidAsync(this MinecraftServer server, string command)
        {
            try
            {
                await (await server.GetRconClientAsync()).SendCommandVoidAsync(command);
                //await Task.Delay(100);
            }
            catch (Exception e)
            {

            }
        }

        public static async Task SayAsync(this MinecraftServer server, string message)
        {
            await server.ExecuteVoidAsync($"say {message}");
        }

        public static async Task SummonAsync(this MinecraftServer server, string entity, string position, string? nbt = null)
        {
            if (nbt is null)
                await server.ExecuteVoidAsync($"summon {entity} {position}");
            else
                await server.ExecuteVoidAsync($"summon {entity} {position} {nbt}");
        }
        public static async Task SummonAsync(this MinecraftServer server, string entity, MinecraftPosition position, object nbt = null)
        {
            var snbt = nbt is null ? null : NbtSerializer.SerializeToSnbt(nbt);
            await server.SummonAsync(entity, position.ToString(), snbt);
        }

        public static async Task FillAsync(this MinecraftServer server, MinecraftPosition from, MinecraftPosition to, string block, object nbt = null)
        {
            var snbt = nbt is null ? null : NbtSerializer.SerializeToSnbt(nbt);
            await server.ExecuteVoidAsync($"fill {from} {to} {block}{snbt}");
        }

        public static async Task SetBlockAsync(this MinecraftServer server, MinecraftPosition position, string block, object nbt = null)
        {
            var snbt = nbt is null ? null : NbtSerializer.SerializeToSnbt(nbt);
            await server.ExecuteVoidAsync($"setblock {position} {block}{snbt}");
        }

        public static async Task KillAsync(this MinecraftServer server, string entitySelector)
        {
            await server.ExecuteVoidAsync($"kill {entitySelector}");
            await Task.Delay(500);
        }
    }

}