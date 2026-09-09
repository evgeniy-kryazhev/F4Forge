namespace F4Forge.DotNet.Sdk;

public sealed class PluginEvents
{
    private readonly object _gate = new();
    private readonly string? _pluginId;
    private KeyDownHandler? _keyDown;
    private LifecycleHandler? _gameDataReady;
    private LifecycleHandler? _gameLoaded;
    private LifecycleHandler? _newGame;

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

    public event LifecycleHandler GameDataReady
    {
        add => AddLifecycleHandler(ref _gameDataReady, value);
        remove => RemoveLifecycleHandler(ref _gameDataReady, value);
    }

    public event LifecycleHandler GameLoaded
    {
        add => AddLifecycleHandler(ref _gameLoaded, value);
        remove => RemoveLifecycleHandler(ref _gameLoaded, value);
    }

    public event LifecycleHandler NewGame
    {
        add => AddLifecycleHandler(ref _newGame, value);
        remove => RemoveLifecycleHandler(ref _newGame, value);
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

    internal void PublishGameDataReady() => PublishLifecycle(ref _gameDataReady, nameof(GameDataReady));

    internal void PublishGameLoaded() => PublishLifecycle(ref _gameLoaded, nameof(GameLoaded));

    internal void PublishNewGame() => PublishLifecycle(ref _newGame, nameof(NewGame));

    private void AddLifecycleHandler(ref LifecycleHandler? handlers, LifecycleHandler value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (_gate) handlers += value;
    }

    private void RemoveLifecycleHandler(ref LifecycleHandler? handlers, LifecycleHandler? value)
    {
        if (value == null) return;
        lock (_gate) handlers -= value;
    }

    private void PublishLifecycle(ref LifecycleHandler? handlers, string eventName)
    {
        LifecycleHandler? snapshot;
        lock (_gate) snapshot = handlers;
        if (snapshot == null) return;
        foreach (LifecycleHandler handler in snapshot.GetInvocationList())
        {
            try { handler(); }
            catch (Exception exception)
            {
                Logger.Error($"Managed lifecycle handler failed: plugin={_pluginId ?? "unknown"}, " +
                    $"event={eventName}: {exception}");
            }
        }
    }
}

public delegate void LifecycleHandler();
