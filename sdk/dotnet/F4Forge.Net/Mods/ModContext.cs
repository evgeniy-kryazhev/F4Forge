using F4Forge.Net.Messaging;
using F4Forge.Net.Events;
using F4Forge.Net.Serialization;
using F4Forge.Net.Commands;
using F4Forge.Net.Diagnostics;
using F4Forge.Net.Objects;

namespace F4Forge.Net.Mods;

/// <summary>Services and directory belonging to one mod.</summary>
public sealed class ModContext
{
    internal ModContext(string directory, IMessageChannel messages, IEventChannel events, IDataSerializer serializer, string? modId = null)
    {
        Directory = directory;
        var owner = Path.GetFullPath(modId ?? directory);
        var ownedChannels = new OwnedChannels(owner, messages, events);
        Messages = ownedChannels;
        Events = ownedChannels;
        Serializer = serializer;
        Console = new ConsoleService(Messages);
        Diagnostics = new ModDiagnostics(owner, Messages);
        Objects = new GameObjects(Messages);
    }

    /// <summary>Absolute path to the mod directory.</summary>
    public string Directory { get; }

    /// <summary>Shared channel with registrations owned by this mod.</summary>
    public IMessageChannel Messages { get; }

    /// <summary>Shared events with subscriptions owned by this mod.</summary>
    public IEventChannel Events { get; }

    /// <summary>The same serializer used for messages, events, arguments and results.</summary>
    public IDataSerializer Serializer { get; }

    /// <summary>Game console commands with registrations owned by this mod.</summary>
    public IConsole Console { get; }

    /// <summary>Operation history for this mod and native hook statuses.</summary>
    public ModDiagnostics Diagnostics { get; }

    /// <summary>Typed game objects accessed through the shared message channel.</summary>
    public GameObjects Objects { get; }
}
