using F4Forge.Net.Mods;
using F4Forge.Net.Singletons;

namespace F4Forge.Net.UI;

/// <summary>A revocable Prisma view with callbacks scoped to its owning mod.</summary>
public sealed class PrismaView : IDisposable
{
    private readonly ModContext _context;
    private readonly string _scope = Guid.NewGuid().ToString("N");
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly IDisposable _readiness;
    private readonly IDisposable _messages;
    private int _closed;
    private bool _created;

    internal PrismaView(ModContext context, Action<string> onMessage)
    {
        _context = context;
        _readiness = context.Subscribe<ReadyNotification>("prisma.ready", notification =>
        {
            if (notification.Scope == _scope)
            {
                if (!_ready.TrySetResult() && Volatile.Read(ref _closed) == 0)
                {
                    Reloaded?.Invoke();
                }
            }
        });
        _messages = context.Subscribe<MessageNotification>("prisma.message", notification =>
        {
            if (notification.Scope == _scope && Volatile.Read(ref _closed) == 0)
            {
                onMessage(notification.Message);
            }
        });
    }

    /// <summary>Raised on the game thread when an already-ready page reloads its DOM.</summary>
    public event Action? Reloaded;

    internal Task CreateAsync(string path, CancellationToken cancellationToken) =>
        _context.GetSingleton<GameThread>().RunAsync(() =>
        {
            _context.Messages.Call<bool>("prisma.create", new { Scope = _scope, Path = path });
            _created = true;
        }, cancellationToken);

    /// <summary>Waits up to 30 seconds for the page to load.</summary>
    /// <param name="cancellationToken">Cancels waiting.</param>
    /// <returns>Completes when the DOM is ready.</returns>
    public Task WaitUntilReadyAsync(CancellationToken cancellationToken = default) =>
        _ready.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);

    /// <summary>Shows the view, captures input, and pauses gameplay.</summary>
    /// <returns>Completes after focus is acquired.</returns>
    public Task ShowAsync() => UpdateAsync("show", null);

    /// <summary>Hides the view and releases its input focus.</summary>
    /// <returns>Completes after focus is released.</returns>
    public Task HideAsync() => UpdateAsync("hide", null);

    /// <summary>Sends an application message to window.f4forgeReceive.</summary>
    /// <param name="message">Application-defined UTF-8-compatible text.</param>
    /// <returns>Completes after the renderer accepts the message.</returns>
    public Task SendAsync(string message) => UpdateAsync("send", message);

    private Task UpdateAsync(string operation, string? message)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
        return _context.GetSingleton<GameThread>().RunAsync(() =>
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _closed) != 0, this);
            _context.Messages.Call<bool>("prisma.update", new { Scope = _scope, Operation = operation, Message = message });
        });
    }

    /// <summary>Detaches callbacks synchronously and schedules native view destruction.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
        {
            return;
        }
        _ready.TrySetCanceled();
        Reloaded = null;
        try
        {
            if (_created)
            {
                _context.CleanupMessages.Call<bool>("prisma.release", new { Scope = _scope });
            }
        }
        finally
        {
            _messages.Dispose();
            _readiness.Dispose();
        }
    }

    private sealed record ReadyNotification(string Scope);
    private sealed record MessageNotification(string Scope, string Message);
}
