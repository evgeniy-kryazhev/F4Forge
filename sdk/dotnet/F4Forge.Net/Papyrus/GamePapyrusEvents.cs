using F4Forge.Net.Mods;
using F4Forge.Net.Singletons;

namespace F4Forge.Net.Papyrus;

/// <summary>Dispatches external F4SE events to registered Papyrus scripts.</summary>
public sealed class GamePapyrusEvents : IGameSingleton<GamePapyrusEvents>
{
    private readonly ModContext _context;

    private GamePapyrusEvents(ModContext context)
    {
        _context = context;
    }

    static GamePapyrusEvents IGameSingleton<GamePapyrusEvents>.Create(ModContext context) => new(context);

    /// <summary>Copies primitive arguments and schedules the registered Papyrus callbacks.</summary>
    /// <param name="name">Event name used by RegisterForExternalEvent.</param>
    /// <param name="arguments">Ordered primitive arguments.</param>
    /// <param name="cancellationToken">Cancels pending dispatch.</param>
    /// <returns>Completes when the VM has been asked to dispatch; script execution is asynchronous.</returns>
    public Task SendExternalAsync(string name, object?[] arguments, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _context.GetSingleton<GameThread>().RunAsync(() =>
        {
            _context.Messages.Call<bool>("papyrus.external.send", new { Name = name, Arguments = arguments });
        }, cancellationToken);
    }
}
