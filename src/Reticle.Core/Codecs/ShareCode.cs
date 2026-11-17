using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Reticle.Core.Model;
using Reticle.Core.Serialization;

namespace Reticle.Core.Codecs;

/// <summary>Compact share code: <c>RTCL1.</c> + base64url(Brotli(UTF-8 JSON)) without padding.</summary>
public static class ShareCode
{
    public const string Prefix = "RTCL1.";
    private const int MaxDecompressedBytes = 1024 * 1024;

    // Same options as Json, but properties equal to their type's default are omitted
    // (the deserializer restores them), which keeps the code short.
    private static readonly JsonSerializerOptions CompactOptions = new(Json.Options)
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { OmitDefaultValues } },
    };

    private static void OmitDefaultValues(JsonTypeInfo info)
    {
        if (info.Kind != JsonTypeInfoKind.Object || info.Type.IsAbstract || info.Type.GetConstructor(Type.EmptyTypes) is null)
            return;
        var defaults = Activator.CreateInstance(info.Type);
        foreach (var prop in info.Properties)
        {
            if (prop.Get is not { } get || prop.Set is null) continue;
            var defaultValue = get(defaults!);
            prop.ShouldSerialize = (_, value) => !Equals(value, defaultValue);
        }
    }

    public static string Encode(CrosshairDoc doc)
    {
        var stripped = doc with { Id = "", CreatedAt = DateTimeOffset.UnixEpoch, UpdatedAt = DateTimeOffset.UnixEpoch };
        var json = JsonSerializer.SerializeToUtf8Bytes(stripped, typeof(CrosshairDoc), CompactOptions);

        var buffer = new byte[BrotliEncoder.GetMaxCompressedLength(json.Length)];
        if (!BrotliEncoder.TryCompress(json, buffer, out var written, quality: 11, window: 22))
            throw new InvalidOperationException("Brotli compression failed.");

        return Prefix + ToBase64Url(buffer.AsSpan(0, written));
    }

    /// <summary>Never throws; failures are reported through <see cref="ImportResult.Error"/>.</summary>
    public static ImportResult Decode(string code)
    {
        const string notACode = "Not a Reticle share code.";
        const string damaged = "Share code is damaged or incomplete.";

        var text = code?.Trim();
        if (string.IsNullOrEmpty(text) || !text.StartsWith(Prefix, StringComparison.Ordinal))
            return new ImportResult(null, notACode);

        try
        {
            var compressed = FromBase64Url(text.AsSpan(Prefix.Length));
            using var input = new MemoryStream(compressed);
            using var brotli = new BrotliStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int n;
            while ((n = brotli.Read(chunk, 0, chunk.Length)) > 0)
            {
                if (output.Length + n > MaxDecompressedBytes)
                    return new ImportResult(null, "Share code is too large.");
                output.Write(chunk, 0, n);
            }

            var json = new UTF8Encoding(false, true).GetString(output.GetBuffer(), 0, (int)output.Length);
            var doc = Json.Deserialize<CrosshairDoc>(json);
            if (doc.Layers is null || doc.Layers.Any(l => l is null))
                return new ImportResult(null, damaged);

            var now = DateTimeOffset.UtcNow;
            return new ImportResult(doc with { Id = Guid.NewGuid().ToString("N"), CreatedAt = now, UpdatedAt = now }, null);
        }
        catch (NotSupportedException)
        {
            return new ImportResult(null, "Share code uses an unsupported version.");
        }
        catch (Exception ex) when (ex is FormatException or JsonException or InvalidDataException
                                       or ArgumentException or DecoderFallbackException or InvalidOperationException
                                       or IOException)
        {
            return new ImportResult(null, damaged);
        }
    }

    private static string ToBase64Url(ReadOnlySpan<byte> data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(ReadOnlySpan<char> s)
    {
        var b = s.ToString().Replace('-', '+').Replace('_', '/');
        b = (b.Length % 4) switch { 2 => b + "==", 3 => b + "=", 1 => throw new FormatException(), _ => b };
        return Convert.FromBase64String(b);
    }
}
