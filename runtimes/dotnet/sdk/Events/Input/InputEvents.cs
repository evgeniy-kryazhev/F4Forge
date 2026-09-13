namespace F4Forge.DotNet.Sdk;

public sealed class InputEvents
{
    private readonly object _gate = new();
    private readonly string? _pluginId;
    private EventHandler<KeyDownEventArgs>? _keyDown;

    internal InputEvents(string? pluginId = null) => _pluginId = pluginId;

    public event EventHandler<KeyDownEventArgs> KeyDown
    {
        add
        {
            ArgumentNullException.ThrowIfNull(value);
            lock (_gate) _keyDown += value;
        }
        remove
        {
            if (value == null) return;
            lock (_gate) _keyDown -= value;
        }
    }

    internal void PublishKeyDown(KeyDownEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        EventHandler<KeyDownEventArgs>? handlers;
        lock (_gate) handlers = _keyDown;
        if (handlers == null) return;
        foreach (EventHandler<KeyDownEventArgs> handler in handlers.GetInvocationList())
        {
            try { handler(this, args); }
            catch (Exception exception)
            {
                Logger.Error($"Managed key handler failed: plugin={_pluginId ?? "unknown"}: {exception}");
            }
        }
    }
}
