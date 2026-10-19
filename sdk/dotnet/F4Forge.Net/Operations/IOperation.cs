using F4Forge.Net.Messaging;

namespace F4Forge.Net.Operations;

/// <summary>Module-owned method metadata and arguments for the shared transport.</summary>
public interface IOperation
{
    /// <summary>The method exposed by the native module.</summary>
    string MethodName { get; }

    /// <summary>Wire data passed to the serializer; operation metadata is not serialized.</summary>
    object? Arguments { get; }

    /// <summary>Validates arguments and ownership before entering the transport.</summary>
    /// <param name="messages">The mod-owned channel executing this operation.</param>
    void Validate(IMessageChannel messages)
    {
    }
}
