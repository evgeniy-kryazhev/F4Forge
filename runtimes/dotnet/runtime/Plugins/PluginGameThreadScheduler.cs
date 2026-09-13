using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Runtime.Plugins;

internal sealed class PluginGameThreadScheduler(IGameThreadScheduler scheduler, CancellationToken lifetime)
    : IGameThreadScheduler
{
    public async Task InvokeAsync(Action action, CancellationToken cancellationToken)
    {
        using var linked = Link(cancellationToken);
        await scheduler.InvokeAsync(action, linked.Token).ConfigureAwait(false);
    }

    public async Task<TResult> InvokeAsync<TResult>(Func<TResult> action, CancellationToken cancellationToken)
    {
        using var linked = Link(cancellationToken);
        return await scheduler.InvokeAsync(action, linked.Token).ConfigureAwait(false);
    }

    private CancellationTokenSource Link(CancellationToken cancellationToken) =>
        CancellationTokenSource.CreateLinkedTokenSource(lifetime, cancellationToken);
}
