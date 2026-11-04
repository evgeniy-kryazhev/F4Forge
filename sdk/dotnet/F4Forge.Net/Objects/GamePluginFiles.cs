using F4Forge.Net.Mods;
using F4Forge.Net.Singletons;

namespace F4Forge.Net.Objects;

/// <summary>Queries active data plugins, including light plugins.</summary>
public sealed class GamePluginFiles : IGameSingleton<GamePluginFiles>
{
    private readonly ModContext _context;

    private GamePluginFiles(ModContext context)
    {
        _context = context;
    }

    static GamePluginFiles IGameSingleton<GamePluginFiles>.Create(ModContext context) => new(context);

    /// <summary>Checks whether an ESP, ESM, or ESL is active in the current load order.</summary>
    /// <param name="name">Plugin filename.</param>
    /// <param name="cancellationToken">Cancels queued work.</param>
    /// <returns>Whether the plugin is loaded.</returns>
    public Task<bool> IsActiveAsync(string name, CancellationToken cancellationToken = default) =>
        _context.GetSingleton<GameThread>().RunAsync(() =>
            _context.Messages.Call<bool>("forms.plugin_active", new { Name = name }), cancellationToken);
}
