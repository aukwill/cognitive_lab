using System.Text.Json;
using System.Text.Json.Serialization;

namespace CognitiveRuntime.Core.Experiments.Dungeons;

internal static class DungeonJson
{
    public static readonly JsonSerializerOptions Options = new(
        JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, Options);
}
