using F4Forge.Net.Mods;
using F4Forge.Net.Singletons;

namespace F4Forge.Net.Input;

/// <summary>Reads input context and controller state without consuming engine input.</summary>
public sealed class GameInput : IGameSingleton<GameInput>
{
    private readonly ModContext _context;

    private GameInput(ModContext context)
    {
        _context = context;
    }

    static GameInput IGameSingleton<GameInput>.Create(ModContext context) => new(context);

    /// <summary>Reads pause and modifier state on the game thread.</summary>
    /// <param name="cancellationToken">Cancels queued work.</param>
    /// <returns>A copied input context.</returns>
    public Task<InputSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
        _context.GetSingleton<GameThread>().RunAsync(() =>
            _context.Messages.Call<InputSnapshot>("input.state", null), cancellationToken);

    /// <summary>Reads a controller without requiring the game thread.</summary>
    /// <param name="index">Controller index from zero through three.</param>
    /// <returns>A copied state, including disconnected state.</returns>
    public ControllerSnapshot ReadController(int index = 0) =>
        _context.Messages.Call<ControllerSnapshot>("input.controller", new { Index = index });
}
