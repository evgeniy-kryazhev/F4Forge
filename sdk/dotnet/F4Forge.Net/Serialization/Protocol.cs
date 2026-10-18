namespace F4Forge.Net.Serialization;

// Property names are the wire schema; a codec changes the encoding, not the schema.
internal sealed record MessageRequest(string Method, object? Arguments);
internal sealed record EventRequest(string Event, object? Data);
internal sealed record ProtocolError(string Code, string Message);
internal sealed record ProtocolResponse<T>(bool Success, T? Value = default, ProtocolError? Error = null);
