using F4Forge.Net.Messaging;
using F4Forge.Net.Events;
using F4Forge.Net.Serialization;
using F4Forge.Net.Diagnostics;

namespace F4Forge.Net.Mods;

/// <summary>Directory, shared channels, serializer, and lifetime belonging to one mod.</summary>
public sealed class ModContext
{
    internal ModContext(string directory, IMessageChannel messages, IEventChannel events, IDataSerializer serializer,
        string? modId = null, ModLifetime? lifetime = null)
    {
        Directory = directory;
        ModId = Path.GetFullPath(modId ?? directory);
        Lifetime = lifetime ?? new ModLifetime();
        CleanupMessages = (messages as ScopedChannel)?.Transport ?? messages;
        var ownedChannels = new OwnedChannels(ModId, messages, events, Lifetime);
        Messages = ownedChannels;
        Events = ownedChannels;
        Serializer = serializer;
    }

    internal ModLifetime Lifetime { get; }
    internal string ModId { get; }
    // Detach must bypass closed mod channels while the native host is still alive.
    internal IMessageChannel CleanupMessages { get; }

    /// <summary>Absolute path to the mod directory.</summary>
    public string Directory { get; }

    /// <summary>Shared channel with registrations owned by this mod.</summary>
    public IMessageChannel Messages { get; }

    /// <summary>Shared events with subscriptions owned by this mod.</summary>
    public IEventChannel Events { get; }

    /// <summary>The same serializer used for messages, events, arguments and results.</summary>
    public IDataSerializer Serializer { get; }
}
