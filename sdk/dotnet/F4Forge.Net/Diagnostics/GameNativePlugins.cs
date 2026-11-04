using F4Forge.Net.Mods;
using F4Forge.Net.Singletons;

namespace F4Forge.Net.Diagnostics;

/// <summary>Inspects native DLL dependencies already loaded into the game process.</summary>
public sealed class GameNativePlugins : IGameSingleton<GameNativePlugins>
{
    private readonly ModContext _context;

    private GameNativePlugins(ModContext context)
    {
        _context = context;
    }

    static GameNativePlugins IGameSingleton<GameNativePlugins>.Create(ModContext context) => new(context);

    /// <summary>Checks a loaded module by filename without loading it.</summary>
    /// <param name="name">DLL filename, without directory components.</param>
    /// <returns>Whether that native module is loaded.</returns>
    public bool IsLoaded(string name) => _context.Messages.Call<bool>("runtime.native_plugin_loaded", new { Name = name });
}
