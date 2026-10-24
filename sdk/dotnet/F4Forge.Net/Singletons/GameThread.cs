using F4Forge.Net.Mods;
using F4Forge.Net.Threading;

namespace F4Forge.Net.Singletons;

/// <summary>Runs synchronous operations on the game thread for one mod context.</summary>
public sealed class GameThread : IGameSingleton<GameThread>
{
    private readonly GameThreadScheduler _scheduler;

    private GameThread(ModContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _scheduler = new GameThreadScheduler(context.Messages, context.Events, context.Lifetime);
    }

    static GameThread IGameSingleton<GameThread>.Create(ModContext context)
    {
        return new GameThread(context);
    }

    /// <summary>Runs a synchronous action on the game thread or queues it through F4SE.</summary>
    /// <param name="action">Synchronous engine operation; do not pass an async lambda.</param>
    /// <param name="cancellationToken">Cancels work that has not started.</param>
    /// <returns>A task observing execution, cancellation, and exceptions.</returns>
    public Task RunAsync(Action action, CancellationToken cancellationToken = default)
    {
        return _scheduler.RunAsync(action, cancellationToken);
    }

    /// <summary>Runs a synchronous function on the game thread and returns its result.</summary>
    /// <typeparam name="TResult">Copied result type.</typeparam>
    /// <param name="action">Synchronous engine operation; do not pass an async lambda.</param>
    /// <param name="cancellationToken">Cancels work that has not started.</param>
    /// <returns>The operation's result or failure.</returns>
    public Task<TResult> RunAsync<TResult>(Func<TResult> action, CancellationToken cancellationToken = default)
    {
        return _scheduler.RunAsync(action, cancellationToken);
    }
}
