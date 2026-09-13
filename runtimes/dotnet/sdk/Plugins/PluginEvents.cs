namespace F4Forge.DotNet.Sdk;

public sealed class PluginEvents
{
    private readonly object _gate = new();
    private readonly string? _pluginId;
    private EventHandler? _gameDataReady;
    private EventHandler? _gameLoaded;
    private EventHandler? _newGame;

    internal PluginEvents(string? pluginId = null) => _pluginId = pluginId;

    public event EventHandler GameDataReady
    {
        add => AddLifecycleHandler(ref _gameDataReady, value);
        remove => RemoveLifecycleHandler(ref _gameDataReady, value);
    }

    public event EventHandler GameLoaded
    {
        add => AddLifecycleHandler(ref _gameLoaded, value);
        remove => RemoveLifecycleHandler(ref _gameLoaded, value);
    }

    public event EventHandler NewGame
    {
        add => AddLifecycleHandler(ref _newGame, value);
        remove => RemoveLifecycleHandler(ref _newGame, value);
    }

    internal void PublishGameDataReady() => PublishLifecycle(ref _gameDataReady, nameof(GameDataReady));

    internal void PublishGameLoaded() => PublishLifecycle(ref _gameLoaded, nameof(GameLoaded));

    internal void PublishNewGame() => PublishLifecycle(ref _newGame, nameof(NewGame));

    private void AddLifecycleHandler(ref EventHandler? handlers, EventHandler value)
    {
        ArgumentNullException.ThrowIfNull(value);
        lock (_gate) handlers += value;
    }

    private void RemoveLifecycleHandler(ref EventHandler? handlers, EventHandler? value)
    {
        if (value == null) return;
        lock (_gate) handlers -= value;
    }

    private void PublishLifecycle(ref EventHandler? handlers, string eventName)
    {
        EventHandler? snapshot;
        lock (_gate) snapshot = handlers;
        if (snapshot == null) return;
        foreach (EventHandler handler in snapshot.GetInvocationList())
        {
            try { handler(this, EventArgs.Empty); }
            catch (Exception exception)
            {
                Logger.Error($"Managed lifecycle handler failed: plugin={_pluginId ?? "unknown"}, " +
                    $"event={eventName}: {exception}");
            }
        }
    }
}
