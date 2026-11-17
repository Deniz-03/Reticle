using System.Text.Json;
using System.Text.Json.Serialization;
using Reticle.Core.Model;

namespace Reticle.Core.Serialization;

public static class Json
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var o = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            AllowOutOfOrderMetadataProperties = true,
        };
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return o;
    }

    public static string Serialize<T>(T value)
    {
        // Serialize layers via the base type so the "type" discriminator is always written.
        var type = value is Layer ? typeof(Layer) : typeof(T);
        return JsonSerializer.Serialize(value, type, Options);
    }

    /// <exception cref="NotSupportedException">The document or settings schema is not 1.</exception>
    public static T Deserialize<T>(string json)
    {
        var value = JsonSerializer.Deserialize<T>(json, Options)
            ?? throw new JsonException("JSON document is null.");
        switch (value)
        {
            case CrosshairDoc { Schema: not CrosshairDoc.CurrentSchema } d:
                throw new NotSupportedException($"Unsupported crosshair schema {d.Schema}.");
            case Settings { Schema: not Settings.CurrentSchema } s:
                throw new NotSupportedException($"Unsupported settings schema {s.Schema}.");
        }
        return value;
    }
}
