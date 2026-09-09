namespace F4Forge.DotNet.Sdk;

public sealed class PluginEvents
{
    private readonly object _gate = new();
    private readonly string? _pluginId;
    private KeyDownHandler? _keyDown;

    internal PluginEvents(string? pluginId = null) => _pluginId = pluginId;

    public event KeyDownHandler KeyDown
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

    public event KeyDownHandler OnKeyDownEvent
    {
        add => KeyDown += value;
        remove => KeyDown -= value;
    }

    internal void PublishKeyDown(KeyDownEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        KeyDownHandler? handlers;
        lock (_gate) handlers = _keyDown;
        if (handlers == null) return;
        foreach (KeyDownHandler handler in handlers.GetInvocationList())
        {
            try { handler(args); }
            catch (Exception exception)
            {
                Logger.Error($"Managed key handler failed: plugin={_pluginId ?? "unknown"}: {exception}");
            }
        }
    }
}
