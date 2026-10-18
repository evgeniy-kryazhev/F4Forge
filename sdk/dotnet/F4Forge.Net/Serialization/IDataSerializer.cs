namespace F4Forge.Net.Serialization;

/// <summary>Encodes all message and event data. Implementations must be thread-safe.</summary>
public interface IDataSerializer
{
    /// <summary>Wire format identifier shared with the native implementation.</summary>
    string Format { get; }

    /// <summary>Serializes a value, including protocol envelopes, to owned bytes.</summary>
    /// <param name="value">Value to serialize.</param>
    /// <returns>The encoded value.</returns>
    byte[] Serialize<T>(T value);

    /// <summary>Deserializes a value without retaining borrowed input memory.</summary>
    /// <param name="data">Encoded bytes.</param>
    /// <returns>A value independent of the input buffer.</returns>
    T Deserialize<T>(ReadOnlySpan<byte> data);
}
