using F4Forge.Net.Messaging;
using F4Forge.Net.Events;
using F4Forge.Net.Serialization;
using F4Forge.Net.Commands;

namespace F4Forge.Net.Mods;

/// <summary>Services and directory belonging to one mod.</summary>
public sealed class ModContext
{
    internal ModContext(string directory, IMessageChannel messages, IEventChannel events, IDataSerializer serializer)
    {
        Directory = directory;
        Messages = messages;
        Events = events;
        Serializer = serializer;
        Console = new ConsoleService(messages);
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
}
