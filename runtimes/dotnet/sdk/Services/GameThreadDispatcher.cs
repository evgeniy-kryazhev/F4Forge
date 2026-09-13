namespace F4Forge.DotNet.Sdk;

public sealed class GameThreadDispatcher
{
    private readonly IGameThreadScheduler? _scheduler;

    internal GameThreadDispatcher(IGameThreadScheduler? scheduler) => _scheduler = scheduler;

    public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return _scheduler?.InvokeAsync(action, cancellationToken) ??
            Task.FromException(new InvalidOperationException("The game-thread scheduler is unavailable."));
    }

    public Task<TResult> InvokeAsync<TResult>(Func<TResult> action, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return _scheduler?.InvokeAsync(action, cancellationToken) ??
            Task.FromException<TResult>(new InvalidOperationException("The game-thread scheduler is unavailable."));
    }
}

internal interface IGameThreadScheduler
{
    Task InvokeAsync(Action action, CancellationToken cancellationToken);
    Task<TResult> InvokeAsync<TResult>(Func<TResult> action, CancellationToken cancellationToken);
}
