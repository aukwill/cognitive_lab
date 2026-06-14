using System.Text.Json;

namespace DocumentDistiller.Core.Serialization;

internal static class JsonDefaults
{
    public static JsonSerializerOptions Options { get; } =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
}
