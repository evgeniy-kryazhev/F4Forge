namespace F4Forge.Net.Messaging;

/// <summary>An error returned by the shared message channel.</summary>
public sealed class MessageException(string code, string message) : Exception(message)
{
    /// <summary>Machine-readable error code returned by the host.</summary>
    public string Code { get; } = code;
}
