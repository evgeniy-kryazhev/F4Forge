using F4Forge.Net.Events;
using F4Forge.Net.Messaging;
using F4Forge.Net.Mods;

namespace F4Forge.Net.Threading;

/// <summary>Runs synchronous operations on the engine thread without blocking a publisher.</summary>
public sealed class GameThreadScheduler
{
    private readonly IMessageChannel _messages;
    private readonly IEventChannel _events;
    private readonly ModLifetime _lifetime;

    internal GameThreadScheduler(IMessageChannel messages, IEventChannel events, ModLifetime lifetime)
    {
        _messages = messages;
        _events = events;
        _lifetime = lifetime;
    }

    /// <summary>Runs a synchronous action immediately on the game thread or queues it through F4SE.</summary>
    /// <param name="action">Synchronous engine operation; do not pass an async lambda.</param>
    /// <param name="cancellationToken">Cancels work that has not started.</param>
    /// <returns>A task observing execution, cancellation, and exceptions.</returns>
    public Task RunAsync(Action action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return RunAsync(() =>
        {
            action();
            return true;
        }, cancellationToken);
    }

    /// <summary>Runs a synchronous function on the game thread and returns its result.</summary>
    /// <typeparam name="T">Copied result type.</typeparam>
    /// <param name="action">Synchronous function; engine object validity is checked when it executes.</param>
    /// <param name="cancellationToken">Cancels work that has not started.</param>
    /// <returns>The operation's result or failure.</returns>
    public Task<T> RunAsync<T>(Func<T> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var invocation = _lifetime.Enter();
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<T>(cancellationToken);
        }
        if (_messages.Call<bool>("threading.is_game_thread", null))
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                _lifetime.ThrowIfClosed();
                return Task.FromResult(action());
            }
            catch (Exception exception)
            {
                return Task.FromException<T>(exception);
            }
        }
        var pending = new Pending<T>(action, cancellationToken, _lifetime.Token);
        try
        {
            pending.Subscription = _events.Subscribe<ExecutionEvent>(execution =>
            {
                if (execution.TaskId == pending.Id && _messages.Call<bool>("threading.is_game_thread", null))
                {
                    pending.Execute();
                }
            });
            var tracked = _lifetime.Run(async () =>
            {
                try
                {
                    await pending.Task.ConfigureAwait(false);
                }
                catch
                {
                    // The returned task owns the failure; the lifetime only drains.
                }
                finally
                {
                    pending.Dispose();
                }
            });
            _messages.Call<bool>("threading.enqueue", new { TaskId = pending.Id });
            return CompleteAsync(pending.Task, tracked);
        }
        catch (Exception exception)
        {
            pending.Fail(exception);
            pending.Dispose();
            return pending.Task;
        }
    }

    private static async Task<T> CompleteAsync<T>(Task<T> result, Task tracked)
    {
        try
        {
            return await result.ConfigureAwait(false);
        }
        finally
        {
            await tracked.ConfigureAwait(false);
        }
    }

    private sealed record ExecutionEvent(string TaskId) : IGameEvent
    {
        public static string EventName => "threading.execute";
    }

    private sealed class Pending<T> : IDisposable
    {
        private readonly object _gate = new();
        private readonly TaskCompletionSource<T> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource _cancellation;
        private readonly CancellationTokenRegistration _registration;
        private Func<T>? _action;
        private bool _started;
        private int _disposed;

        internal Pending(Func<T> action, CancellationToken cancellation, CancellationToken shutdown)
        {
            _action = action;
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation, shutdown);
            _registration = _cancellation.Token.Register(Cancel);
        }

        internal string Id { get; } = Guid.NewGuid().ToString("N");
        internal Task<T> Task => _result.Task;
        internal IDisposable? Subscription { get; set; }

        private void Cancel()
        {
            lock (_gate)
            {
                if (_started)
                {
                    return;
                }
                _action = null;
                _result.TrySetCanceled(_cancellation.Token);
            }
        }

        internal void Execute()
        {
            Func<T>? action;
            lock (_gate)
            {
                if (_started || _result.Task.IsCompleted)
                {
                    return;
                }
                _started = true;
                action = _action;
                _action = null;
            }
            try
            {
                _result.TrySetResult(action!());
            }
            catch (Exception exception)
            {
                _result.TrySetException(exception);
            }
        }

        internal void Fail(Exception exception)
        {
            lock (_gate)
            {
                if (!_started)
                {
                    _action = null;
                    _result.TrySetException(exception);
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }
            Subscription?.Dispose();
            _registration.Dispose();
            _cancellation.Dispose();
        }
    }
}
