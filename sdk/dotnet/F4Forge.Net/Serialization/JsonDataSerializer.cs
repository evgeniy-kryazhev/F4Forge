using System.Text.Json;

namespace F4Forge.Net.Serialization;

/// <summary>The default UTF-8 JSON wire format.</summary>
public sealed class JsonDataSerializer : IDataSerializer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public string Format => "json";

    /// <inheritdoc />
    public byte[] Serialize<T>(T value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, Options);
    }

    /// <inheritdoc />
    public T Deserialize<T>(ReadOnlySpan<byte> data)
    {
        return JsonSerializer.Deserialize<T>(data, Options)!;
    }
}
