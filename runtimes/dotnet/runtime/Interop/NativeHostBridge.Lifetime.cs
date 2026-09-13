using F4Forge.DotNet.Sdk;

namespace F4Forge.DotNet.Runtime.Interop;

internal unsafe sealed partial class NativeHostBridge
{
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var registration in _registrations) registration.Dispose();
        if (_api == null || !_module.IsValid || _api->UnregisterModule == null) {
            FinalizeCallbackStates();
            return;
        }
        _api->UnregisterModule(_context, _module.Value);
        if (!HasModuleQuiescenceApi) return;
        _ = Task.Run(() => {
            var quiescence = (F4ForgeResult)_api->WaitModuleQuiescence(_context, _module.Value, uint.MaxValue);
            if (quiescence is F4ForgeResult.Success or F4ForgeResult.InvalidHandle)
                FinalizeCallbackStates();
        });
    }

    public bool IsQuiesced => _quiesced.Task.IsCompletedSuccessfully;
    private bool HasModuleQuiescenceApi => _api != null && _api->StructSize >= 168 && _api->WaitModuleQuiescence != null;

    public void SetFinalizationCallback(Action callback)
    {
        _finalized = callback;
        if (IsQuiesced) callback();
    }

    private void FinalizeCallbackStates()
    {
        if (Interlocked.Exchange(ref _finalizationStarted, 1) != 0) return;
        foreach (var registration in _registrations) registration.Retire();
        _quiesced.TrySetResult(true);
        _finalized?.Invoke();
    }
}
