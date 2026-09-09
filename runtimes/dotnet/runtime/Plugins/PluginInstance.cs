using F4Forge.DotNet.Sdk;
using F4Forge.DotNet.Runtime.Interop;

namespace F4Forge.DotNet.Runtime.Plugins;

internal enum PluginState
{
    Loading, Active, Quiescing, Disabled, Quarantined, Unloaded
}

internal sealed unsafe class PluginInstance
{
    private static readonly TimeSpan QuiescenceTimeout = TimeSpan.FromMilliseconds(100);
    internal static readonly AsyncLocal<int> DispatchDepth = new();

    private readonly object _lifecycleGate = new();
    private readonly TaskCompletionSource<bool> _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _inFlight;
    private PluginState _state = PluginState.Loading;
    private int _failures;
    private bool _teardownStarted;
    private bool _unloadStarted;
    private NativeHostBridge? _bridge;

    public PluginInstance(string path, PluginLoadContext context, F4ForgePlugin plugin, string id,
        PluginResourceScope scope, NativeApi* host, ulong runtime)
    {
        Path = path;
        Context = context;
        Plugin = plugin;
        Id = id;
        Scope = scope;
        var bridge = host == null ? null : new NativeHostBridge(host, runtime, id);
        _bridge = bridge;
        if (bridge != null)
        {
            scope.Add(bridge);
            bridge.SetFinalizationCallback(FinalizeAfterNativeQuiescence);
            ContextInfo = new F4ForgePluginContext(
                bridge.Module, scope.Add, bridge, scope.CancellationToken, new PluginEvents(id));
        }
        else
        {
            ContextInfo = new F4ForgePluginContext(
                F4Forge.DotNet.Sdk.ModuleHandle.Invalid, scope.Add, null, scope.CancellationToken,
                new PluginEvents(id));
        }
    }

    public string Path { get; }
    public PluginLoadContext Context { get; }
    public F4ForgePlugin Plugin { get; }
    public string Id { get; }
    public PluginResourceScope Scope { get; }
    public F4ForgePluginContext ContextInfo { get; }
    public PluginState State { get { lock (_lifecycleGate) return _state; } }

    public bool Activate()
    {
        lock (_lifecycleGate)
        {
            if (_state != PluginState.Loading) return false;
            _state = PluginState.Active;
            return true;
        }
    }

    public bool TryAcquireDispatchLease(out IDisposable? lease)
    {
        lock (_lifecycleGate)
        {
            if (_state != PluginState.Active)
            {
                lease = null;
                return false;
            }
            ++_inFlight;
            lease = new DispatchLease(this);
            return true;
        }
    }

    public void ResetFailures()
    {
        lock (_lifecycleGate) _failures = 0;
    }

    public int RecordFailure(int failureThreshold)
    {
        lock (_lifecycleGate)
        {
            ++_failures;
            if (_failures >= failureThreshold) _state = PluginState.Disabled;
            return _failures;
        }
    }

    public bool Stop()
    {
        bool finalize;
        bool wait;
        lock (_lifecycleGate)
        {
            if (_state == PluginState.Unloaded) return true;
            if (_state == PluginState.Quiescing || _state == PluginState.Quarantined)
            {
                finalize = false;
                wait = DispatchDepth.Value == 0;
            }
            else
            {
                _state = PluginState.Quiescing;
                finalize = _inFlight == 0;
                wait = !finalize && DispatchDepth.Value == 0;
            }
        }
        if (!wait && !finalize) return false;
        if (finalize)
        {
            FinalizeStop();
            return State == PluginState.Unloaded;
        }
        return WaitForStop();
    }

    private bool WaitForStop()
    {
        if (_stopped.Task.Wait(QuiescenceTimeout)) return true;
        lock (_lifecycleGate)
        {
            if (_state != PluginState.Unloaded) _state = PluginState.Quarantined;
        }
        return false;
    }

    private void FinalizeStop()
    {
        lock (_lifecycleGate)
        {
            if (_teardownStarted || _state == PluginState.Unloaded) return;
            _teardownStarted = true;
        }
        Scope.Dispose();
        if (_bridge != null && !_bridge.IsQuiesced)
        {
            lock (_lifecycleGate) _state = PluginState.Quarantined;
            return;
        }
        FinalizeAfterNativeQuiescence();
    }

    private void FinalizeAfterNativeQuiescence()
    {
        lock (_lifecycleGate)
        {
            if (_unloadStarted) return;
            _unloadStarted = true;
            _state = PluginState.Unloaded;
        }
        Context.Unload();
        _stopped.TrySetResult(true);
    }

    private void ReleaseDispatchLease()
    {
        bool finalize;
        lock (_lifecycleGate)
        {
            --_inFlight;
            finalize = _inFlight == 0 && _state is PluginState.Quiescing or PluginState.Quarantined;
        }
        if (finalize) FinalizeStop();
    }

    private sealed class DispatchLease : IDisposable
    {
        private PluginInstance? _instance;

        public DispatchLease(PluginInstance instance) => _instance = instance;

        public void Dispose() => Interlocked.Exchange(ref _instance, null)?.ReleaseDispatchLease();
    }
}
