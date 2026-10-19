namespace F4Forge.Net.Mods;

internal sealed class ModLifetime
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<IDisposable> _resources = [];
    private int _active;
    private bool _closed;
    private bool _detaching;
    private readonly Action<string> _reportError;

    internal ModLifetime(Action<string>? reportError = null)
    {
        _reportError = reportError ?? System.Console.Error.WriteLine;
    }

    internal CancellationToken Token => _cancellation.Token;
    internal Task Completion => _drained.Task;

    internal IDisposable Enter()
    {
        var lease = new Lease(this);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            _active++;
        }
        return lease;
    }

    internal IDisposable Own(IDisposable resource)
    {
        lock (_gate)
        {
            if (!_closed)
            {
                _resources.Add(resource);
                return resource;
            }
        }
        resource.Dispose();
        throw new ObjectDisposedException(nameof(ModLifetime));
    }

    // Reserve before invoking user code: shutdown must include the synchronous
    // prefix as well as every continuation of an accepted operation.
    internal Task Run(Func<Task> action)
    {
        var lease = Enter();
        return RunReservedAsync(action, lease);
    }

    private static async Task RunReservedAsync(Func<Task> action, IDisposable lease)
    {
        using (lease)
        {
            await action().ConfigureAwait(false);
        }
    }

    public void Close()
    {
        IDisposable[] resources;
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }
            resources = _resources.ToArray();
            _closed = true;
            _detaching = true;
            _active++;
            _resources.Clear();
        }
        // User cancellation callbacks may do I/O. CancelAsync marks the token
        // immediately without running those callbacks on the engine thread.
        _ = ObserveCancellationAsync(_cancellation.CancelAsync());
        List<Exception>? failures = null;
        foreach (var resource in resources.Reverse())
        {
            try
            {
                resource.Dispose();
            }
            catch (Exception exception)
            {
                (failures ??= []).Add(exception);
            }
        }
        lock (_gate)
        {
            _detaching = false;
            if (_active == 0)
            {
                _drained.TrySetResult();
            }
        }
        if (failures is not null)
        {
            throw new AggregateException("One or more owned resources could not be detached.", failures);
        }
    }

    private async Task ObserveCancellationAsync(Task cancellation)
    {
        try
        {
            await cancellation.ConfigureAwait(false);
        }
        catch (AggregateException exception)
        {
            try
            {
                _reportError($"Cancellation callback failed: {exception}");
            }
            catch
            {
                // A logger cannot prevent other registrations detaching.
            }
        }
        finally
        {
            Leave();
        }
    }

    public void ThrowIfClosed()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
        }
    }

    private void Leave()
    {
        lock (_gate)
        {
            _active--;
            if (_closed && !_detaching && _active == 0)
            {
                _drained.TrySetResult();
            }
        }
    }

    private sealed class Lease(ModLifetime lifetime) : IDisposable
    {
        private ModLifetime? _lifetime = lifetime;

        public void Dispose()
        {
            Interlocked.Exchange(ref _lifetime, null)?.Leave();
        }
    }
}
