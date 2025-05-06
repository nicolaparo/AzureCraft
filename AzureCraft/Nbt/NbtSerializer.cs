using AzureCraft.Nbt.Tags;
using System.Text.Json;

namespace AzureCraft.Nbt
{
    public class NbtSerializer
    {
        public static NbtTag Deserialize(string snbt) => SnbtParser.Parse(snbt, false);
        public static object Deserialize(string snbt, Type targetType) => Deserialize(snbt).ToJson().Deserialize(targetType);
        public static T Deserialize<T>(string snbt) => Deserialize(snbt).ToJson().Deserialize<T>();

        public static NbtTag Serialize<T>(T value, string tagName = null)
        {
            var json = JsonSerializer.Serialize(value);
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);
            return SerializeJson(jsonElement, tagName);
        }
        public static NbtTag SerializeJson(JsonElement value, string tagName = null)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                var tag = tagName is null ? new NbtCompound() : new NbtCompound(tagName);
                foreach (var property in value.EnumerateObject())
                {
                    var childTag = SerializeJson(property.Value, property.Name);
                    tag.Add(childTag);
                }
                return tag;
            }
            else if (value.ValueKind == JsonValueKind.Array)
            {
                var tag = tagName is null ? new NbtList() : new NbtList(tagName);
                foreach (var item in value.EnumerateArray())
                {
                    var childTag = SerializeJson(item);
                    tag.Add(childTag);
                }
                return tag;
            }
            else
            {
                return value.ValueKind switch
                {
                    JsonValueKind.True => new NbtByte(tagName, 1),
                    JsonValueKind.False => new NbtByte(tagName, 0),
                    JsonValueKind.Number => new NbtDouble(tagName, value.GetDouble()),
                    _ => new NbtString(tagName, value.ToString())
                };
            }

        }

        public static string SerializeToSnbt(NbtTag tag, SnbtOptions? options = null)
        {
            return tag.ToSnbt(options ?? SnbtOptions.Default);
        }
        public static string SerializeToSnbt(object value, SnbtOptions? options = null)
        {
            var tag = Serialize(value);
            return tag.ToSnbt(options ?? SnbtOptions.Default);
        }
    }
}
