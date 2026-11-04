using F4Forge.Net.Mods;
using F4Forge.Net.Singletons;

namespace F4Forge.Net.UI;

/// <summary>Adds independent, mod-owned entries to the standard pause menu.</summary>
public sealed class GamePauseMenu : IGameSingleton<GamePauseMenu>
{
    private readonly ModContext _context;

    private GamePauseMenu(ModContext context)
    {
        _context = context;
    }

    static GamePauseMenu IGameSingleton<GamePauseMenu>.Create(ModContext context) => new(context);

    /// <summary>Registers a pause menu entry until disposal or mod shutdown.</summary>
    /// <param name="label">Player-visible label.</param>
    /// <param name="onSelected">Asynchronous action tracked by the mod lifetime.</param>
    /// <returns>An owned registration.</returns>
    public IDisposable Register(string label, Func<Task> onSelected)
    {
        using var operation = _context.Lifetime.Enter();
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(onSelected);
        var scope = Guid.NewGuid().ToString("N");
        var subscription = _context.Subscribe<SelectionNotification>("ui.pause.selected", async notification =>
        {
            if (notification.Scope == scope)
            {
                await onSelected().ConfigureAwait(false);
            }
        });
        try
        {
            _context.Messages.Call<bool>("ui.pause.register", new { Scope = scope, Label = label });
            return _context.Lifetime.Own(new Registration(_context, scope, subscription));
        }
        catch
        {
            subscription.Dispose();
            throw;
        }
    }

    private sealed record SelectionNotification(string Scope);

    private sealed class Registration(ModContext context, string scope, IDisposable subscription) : IDisposable
    {
        private int _closed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _closed, 1) != 0)
            {
                return;
            }
            try
            {
                context.CleanupMessages.Call<bool>("ui.pause.release", new { Scope = scope });
            }
            finally
            {
                subscription.Dispose();
            }
        }
    }
}
