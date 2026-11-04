using F4Forge.Net.Mods;
using F4Forge.Net.Singletons;

namespace F4Forge.Net.UI;

/// <summary>Accesses open Scaleform movies using copied values rather than native pointers.</summary>
public sealed class GameScaleform : IGameSingleton<GameScaleform>
{
    private readonly ModContext _context;

    private GameScaleform(ModContext context)
    {
        _context = context;
    }

    static GameScaleform IGameSingleton<GameScaleform>.Create(ModContext context) => new(context);

    /// <summary>Reads a movie variable on the game thread.</summary>
    /// <typeparam name="T">Copied primitive or array result.</typeparam>
    /// <param name="menu">Engine menu name.</param>
    /// <param name="path">ActionScript variable path.</param>
    /// <param name="cancellationToken">Cancels queued work.</param>
    /// <returns>The copied value.</returns>
    public Task<T> ReadAsync<T>(string menu, string path, CancellationToken cancellationToken = default) =>
        _context.GetSingleton<GameThread>().RunAsync(() =>
            _context.Messages.Call<T>("ui.movie.read", new { Menu = menu, Path = path }), cancellationToken);

    /// <summary>Writes a movie variable on the game thread.</summary>
    /// <param name="menu">Engine menu name.</param>
    /// <param name="path">ActionScript variable path.</param>
    /// <param name="value">Copied primitive or array.</param>
    /// <param name="cancellationToken">Cancels queued work.</param>
    /// <returns>Completes after the write.</returns>
    public Task WriteAsync(string menu, string path, object? value, CancellationToken cancellationToken = default) =>
        _context.GetSingleton<GameThread>().RunAsync(() =>
        {
            _context.Messages.Call<bool>("ui.movie.write", new { Menu = menu, Path = path, Value = value });
        }, cancellationToken);

    /// <summary>Invokes a movie function on the game thread.</summary>
    /// <typeparam name="T">Copied result type, including nullable types for void functions.</typeparam>
    /// <param name="menu">Engine menu name.</param>
    /// <param name="path">ActionScript function path.</param>
    /// <param name="arguments">Copied primitive and array arguments.</param>
    /// <param name="cancellationToken">Cancels queued work.</param>
    /// <returns>The copied return value.</returns>
    public Task<T> InvokeAsync<T>(string menu, string path, object?[] arguments,
        CancellationToken cancellationToken = default) =>
        _context.GetSingleton<GameThread>().RunAsync(() =>
            _context.Messages.Call<T>("ui.movie.invoke", new { Menu = menu, Path = path, Arguments = arguments }), cancellationToken);

    /// <summary>Invokes a movie function while discarding its return value.</summary>
    /// <param name="menu">Engine menu name.</param>
    /// <param name="path">ActionScript function path.</param>
    /// <param name="arguments">Copied primitive and array arguments.</param>
    /// <param name="cancellationToken">Cancels queued work.</param>
    /// <returns>Completes after invocation.</returns>
    public Task InvokeAsync(string menu, string path, object?[] arguments, CancellationToken cancellationToken = default) =>
        _context.GetSingleton<GameThread>().RunAsync(() =>
        {
            _context.Messages.Call<bool>("ui.movie.invoke", new { Menu = menu, Path = path, Arguments = arguments, DiscardResult = true });
        }, cancellationToken);
}
