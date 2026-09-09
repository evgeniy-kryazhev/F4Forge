namespace F4Forge.DotNet.Runtime;

internal sealed class FrameworkEventRouter : IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];

    public FrameworkEventRouter(NativeHostBridge bridge, Action gameDataReady, Action gameLoaded, Action newGame)
    {
        Add(bridge.SubscribeFramework("framework.game_data_ready", gameDataReady));
        Add(bridge.SubscribeFramework("framework.game_loaded", gameLoaded));
        Add(bridge.SubscribeFramework("framework.new_game", newGame));
    }

    private void Add(IDisposable? subscription)
    {
        if (subscription == null) throw new InvalidOperationException("Framework event endpoint is unavailable.");
        _subscriptions.Add(subscription);
    }

    public void Dispose()
    {
        foreach (var subscription in _subscriptions) subscription.Dispose();
        _subscriptions.Clear();
    }
}
