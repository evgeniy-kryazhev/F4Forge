using F4Forge.Net.Messaging;

namespace F4Forge.Net.Mods;

/// <summary>Services and directory belonging to one mod.</summary>
public sealed class ModContext
{
    internal ModContext(string directory, IMessageChannel messages)
    {
        Directory = directory;
        Messages = messages;
    }

    /// <summary>Absolute path to the mod directory.</summary>
    public string Directory { get; }

    /// <summary>Shared channel with registrations owned by this mod.</summary>
    public IMessageChannel Messages { get; }
}
