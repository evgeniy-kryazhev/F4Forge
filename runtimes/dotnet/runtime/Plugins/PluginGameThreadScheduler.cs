using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Runtime.Plugins;

internal sealed class PluginGameThreadScheduler(
    IGameThreadScheduler scheduler, PluginInstance owner, CancellationToken lifetime)
    : IGameThreadScheduler
{
    public async Task InvokeAsync(Action action, CancellationToken cancellationToken)
    {
        using var linked = Link(cancellationToken);
        await scheduler.InvokeAsync(() => Execute(() =>
        {
            action();
            return true;
        }, linked.Token), linked.Token).ConfigureAwait(false);
    }

    public async Task<TResult> InvokeAsync<TResult>(Func<TResult> action, CancellationToken cancellationToken)
    {
        using var linked = Link(cancellationToken);
        return await scheduler.InvokeAsync(() => Execute(action, linked.Token), linked.Token).ConfigureAwait(false);
    }

    private TResult Execute<TResult>(Func<TResult> action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!owner.TryAcquireDispatchLease(out var lease))
        {
            throw new OperationCanceledException("The plugin is no longer active.", cancellationToken);
        }

        using (lease)
        {
            // Self-unload must defer teardown until this delegate releases its lease.
            ++PluginInstance.DispatchDepth.Value;
            try
            {
                return action();
            }
            finally
            {
                --PluginInstance.DispatchDepth.Value;
            }
        }
    }

    private CancellationTokenSource Link(CancellationToken cancellationToken) =>
        CancellationTokenSource.CreateLinkedTokenSource(lifetime, cancellationToken);
}
