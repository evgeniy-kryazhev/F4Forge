using F4Forge.Net.Messaging;

namespace F4Forge.Net.Diagnostics;

/// <summary>Bounded diagnostic history for one mod and the host's native hook statuses.</summary>
public sealed class ModDiagnostics
{
    private readonly IMessageChannel _messages;

    internal ModDiagnostics(string modId, IMessageChannel messages)
    {
        ModId = modId;
        _messages = messages;
    }

    /// <summary>The mod DLL path, independent of its containing directory.</summary>
    public string ModId { get; }

    /// <summary>Returns this mod's newest operations first; history is bounded to 256 operations across the host.</summary>
    public IReadOnlyList<DiagnosticRecord> GetOperations(int maximum = 64)
    {
        if (maximum is < 1 or > 256)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum));
        }
        return _messages.Call<DiagnosticRecord[]>("diagnostics.operations", new { ModId, Maximum = maximum });
    }

    /// <summary>Returns statuses for hooks that register with the native diagnostics service.</summary>
    public IReadOnlyList<HookStatus> GetHooks()
    {
        return _messages.Call<HookStatus[]>("diagnostics.hooks", null);
    }
}
