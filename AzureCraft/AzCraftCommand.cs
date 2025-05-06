using System.Text.Json;

namespace AzureCraft
{
    public record AzCraftCommand
    {
        public string CommandName { get; set; }
        public JsonElement[] Arguments { get; set; }
    }
}
