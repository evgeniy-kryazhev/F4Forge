using F4Forge.Net.Mods;
using F4Forge.Net.Singletons;

namespace F4Forge.Net.Objects;

/// <summary>Reads and updates global variables using load-order-independent identifiers.</summary>
public sealed class GameGlobals : IGameSingleton<GameGlobals>
{
    private readonly ModContext _context;

    private GameGlobals(ModContext context)
    {
        _context = context;
    }

    static GameGlobals IGameSingleton<GameGlobals>.Create(ModContext context) => new(context);

    /// <summary>Reads a global variable on the game thread.</summary>
    /// <param name="plugin">Plugin filename.</param>
    /// <param name="localId">Plugin-local form identifier.</param>
    /// <param name="cancellationToken">Cancels queued work.</param>
    /// <returns>The current value.</returns>
    public Task<float> ReadAsync(string plugin, uint localId, CancellationToken cancellationToken = default) =>
        _context.GetSingleton<GameThread>().RunAsync(() =>
            _context.Messages.Call<float>("forms.global.read", new { Plugin = plugin, LocalId = localId }), cancellationToken);

    /// <summary>Writes a finite global value on the game thread.</summary>
    /// <param name="plugin">Plugin filename.</param>
    /// <param name="localId">Plugin-local form identifier.</param>
    /// <param name="value">New finite value.</param>
    /// <param name="cancellationToken">Cancels queued work.</param>
    /// <returns>Completes after the write.</returns>
    public Task WriteAsync(string plugin, uint localId, float value, CancellationToken cancellationToken = default) =>
        _context.GetSingleton<GameThread>().RunAsync(() =>
        {
            _context.Messages.Call<bool>("forms.global.write", new { Plugin = plugin, LocalId = localId, Value = value });
        }, cancellationToken);
}
