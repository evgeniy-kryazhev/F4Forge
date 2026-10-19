using F4Forge.Net.Diagnostics;
using F4Forge.Net.Mods;

namespace F4Forge.Net.Events;

internal sealed class AsyncNotification<T> : IDisposable
{
    private readonly ModLifetime _delivery = new();
    private readonly ModLifetime _owner;
    private Func<T, CancellationToken, Task>? _handler;
    private readonly string? _modId;
    private readonly string _name;
    private readonly Action<DiagnosticRecord> _record;
    private readonly Action<string> _report;
    private readonly CancellationTokenRegistration _shutdown;
    private IDisposable? _subscription;

    internal AsyncNotification(ModLifetime owner, string name, Func<T, CancellationToken, Task> handler,
        Func<Action<T>, IDisposable> subscribe, Action<DiagnosticRecord> record, Action<string> report)
    {
        _owner = owner;
        _handler = handler;
        _modId = DiagnosticTrace.CurrentOwner;
        _name = name;
        _record = record;
        _report = report;
        _shutdown = owner.Token.Register(_delivery.Close);
        try
        {
            // Cancellation callbacks registered by the mod also belong to its
            // lifetime, even when the notification itself has already finished.
            _ = owner.Run(() => _delivery.Completion);
            _subscription = subscribe(Invoke);
        }
        catch
        {
            _shutdown.Dispose();
            _delivery.Close();
            throw;
        }
    }

    private void Invoke(T value)
    {
        var handler = Volatile.Read(ref _handler);
        if (handler is null)
        {
            return;
        }
        try
        {
            // Both lifetimes reserve the callback before its synchronous prefix.
            // DeliverAsync observes failures before the owner releases its lease.
            _ = _owner.Run(() => DeliverAsync(value, handler));
        }
        catch (ObjectDisposedException)
        {
            // Unsubscription won the race against a captured publisher snapshot.
        }
    }

    private async Task DeliverAsync(T value, Func<T, CancellationToken, Task> handler)
    {
        try
        {
            await _delivery.Run(() => DiagnosticTrace.RunAsync(_modId, "event.handle.async", _name, _record,
                () => handler(value, _delivery.Token), _delivery.Token)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_delivery.Token.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (_delivery.Token.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            try
            {
                _report($"Mod '{_modId}', async event '{_name}': {exception}");
            }
            catch
            {
                // Error reporting must not create an unobserved task failure.
            }
        }
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _handler, null);
        _delivery.Close();
        Interlocked.Exchange(ref _subscription, null)?.Dispose();
        _shutdown.Dispose();
    }
}
