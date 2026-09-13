using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Runtime.Plugins;

internal sealed class PluginDispatcher(PluginInstanceRegistry registry, int failureThreshold)
{
    public void Dispatch(Action<F4ForgePlugin> callback)
    {
        foreach (var instance in registry.Snapshot())
        {
            if (!instance.TryAcquireDispatchLease(out var lease)) continue;
            using (lease)
            try
            {
                ++PluginInstance.DispatchDepth.Value;
                callback(instance.Plugin);
                instance.ResetFailures();
            }
            catch (Exception exception)
            {
                var failures = instance.RecordFailure(failureThreshold);
                Logger.Error($"Managed plugin callback failed: plugin={instance.Id}, " +
                    $"exception={exception.GetType().FullName}, failures={failures}, " +
                    $"threshold={failureThreshold}: {exception}");
                if (failures >= failureThreshold)
                    Logger.Warning($"Managed plugin disabled: plugin={instance.Id}, threshold={failureThreshold}.");
            }
            finally { --PluginInstance.DispatchDepth.Value; }
        }
    }

    public void DispatchInput(KeyDownEventArgs args) => DispatchContext(context => context.Input.PublishKeyDown(args));

    public void DispatchLifecycle(Action<PluginEvents> callback) => DispatchContext(context => callback(context.Events));

    private void DispatchContext(Action<F4ForgePluginContext> callback)
    {
        foreach (var instance in registry.Snapshot())
        {
            if (!instance.TryAcquireDispatchLease(out var lease)) continue;
            using (lease)
            try { callback(instance.ContextInfo); }
            catch (Exception exception)
            {
                Logger.Error($"Managed plugin context callback failed: plugin={instance.Id}: {exception}");
            }
        }
    }
}
