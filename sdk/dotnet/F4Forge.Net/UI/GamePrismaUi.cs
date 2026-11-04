using F4Forge.Net.Mods;
using F4Forge.Net.Singletons;

namespace F4Forge.Net.UI;

/// <summary>Creates mod-owned HTML views through the optional PrismaUI_F4 API.</summary>
public sealed class GamePrismaUi : IGameSingleton<GamePrismaUi>
{
    private readonly ModContext _context;

    private GamePrismaUi(ModContext context)
    {
        _context = context;
    }

    static GamePrismaUi IGameSingleton<GamePrismaUi>.Create(ModContext context) => new(context);

    /// <summary>Checks whether PrismaUI_F4 API v2 is installed.</summary>
    /// <param name="cancellationToken">Cancels the queued check.</param>
    /// <returns>Whether a compatible API is available.</returns>
    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        _context.GetSingleton<GameThread>().RunAsync(() =>
            _context.Messages.Call<bool>("prisma.available", null), cancellationToken);

    /// <summary>Creates a hidden view using a path relative to PrismaUI_F4/views.</summary>
    /// <param name="path">Unique relative HTML path.</param>
    /// <param name="onMessage">Receives messages sent using window.f4forge(string).</param>
    /// <param name="cancellationToken">Cancels pending creation.</param>
    /// <returns>A mod-owned view; wait for readiness before sending data.</returns>
    public async Task<PrismaView> CreateAsync(string path, Action<string> onMessage,
        CancellationToken cancellationToken = default)
    {
        using var operation = _context.Lifetime.Enter();
        var view = new PrismaView(_context, onMessage);
        try
        {
            await view.CreateAsync(path, cancellationToken).ConfigureAwait(false);
            _context.Lifetime.Own(view);
            return view;
        }
        catch
        {
            view.Dispose();
            throw;
        }
    }
}
