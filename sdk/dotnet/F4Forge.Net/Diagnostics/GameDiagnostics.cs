using F4Forge.Net.Diagnostics.Queries;
using F4Forge.Net.Messaging;
using F4Forge.Net.Operations;

namespace F4Forge.Net.Diagnostics;

/// <summary>Bounded diagnostic history for one mod and the host's native hook statuses.</summary>
public sealed partial class GameDiagnostics
{
    private readonly IMessageChannel _messages;

    internal GameDiagnostics(string modId, IMessageChannel messages)
    {
        ModId = modId;
        _messages = messages;
    }

    /// <summary>The mod DLL path, independent of its containing directory.</summary>
    public string ModId { get; }

    /// <summary>Returns this mod's newest operations first; history is bounded to 256 operations across the host.</summary>
    public IReadOnlyList<DiagnosticRecord> GetOperations(int maximum = 64)
    {
        return _messages.Query(new GetDiagnosticOperations(ModId, maximum));
    }

    /// <summary>Returns statuses for hooks that register with the native diagnostics service.</summary>
    public IReadOnlyList<HookStatus> GetHooks()
    {
        return _messages.Query(new GetHookStatuses());
    }
}
