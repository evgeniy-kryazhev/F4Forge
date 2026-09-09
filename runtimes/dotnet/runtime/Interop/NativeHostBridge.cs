using System.Text;
using F4Forge.DotNet.Sdk;
using SdkModuleHandle = F4Forge.DotNet.Sdk.ModuleHandle;

namespace F4Forge.DotNet.Runtime.Interop;

internal unsafe sealed partial class NativeHostBridge : IPluginHostBridge, IDisposable
{
    private readonly NativeApi* _api;
    private readonly SdkModuleHandle _module;
    private readonly List<CallbackRegistration> _registrations = [];
    private readonly TaskCompletionSource<bool> _quiesced =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Action? _finalized;
    private bool _disposed;
    private int _finalizationStarted;

    public NativeHostBridge(NativeApi* api, ulong runtime, string id)
    {
        _api = api;
        if (api == null || api->RegisterModule == null) return;
        var bytes = Encoding.UTF8.GetBytes(id);
        fixed (byte* text = bytes)
        {
            ulong module = 0;
            var result = api->RegisterModule(runtime,
                new F4ForgeStringView { Data = text, Length = (uint)bytes.Length }, 1, &module);
            if (result != (int)F4ForgeResult.Success)
                throw new InvalidOperationException($"Native module registration failed: {result}");
            _module = new SdkModuleHandle(module);
        }
    }

    public SdkModuleHandle Module => _module;

    public EndpointHandle ResolveEndpoint(string name, uint version)
    {
        if (_api == null || _api->ResolveEndpoint == null) return default;
        var bytes = Encoding.UTF8.GetBytes(name);
        fixed (byte* text = bytes)
            return new EndpointHandle(_api->ResolveEndpoint(
                new F4ForgeStringView { Data = text, Length = (uint)bytes.Length }, version));
    }

    public F4ForgeResult Invoke(EndpointHandle endpoint, ReadOnlySpan<byte> request, Span<byte> response,
        out uint responseSize)
    {
        responseSize = 0;
        if (_api == null || _api->Invoke == null) return F4ForgeResult.InactiveRuntime;
        fixed (byte* requestPointer = request)
        fixed (byte* responsePointer = response)
        fixed (uint* size = &responseSize)
            return (F4ForgeResult)_api->Invoke(endpoint.Value,
                request.IsEmpty ? null : requestPointer, (uint)request.Length,
                response.IsEmpty ? null : responsePointer, (uint)response.Length, size);
    }

    public uint QueryCapability(string id, uint minimumVersion)
    {
        if (_api == null || _api->QueryCapability == null) return 0;
        var bytes = Encoding.UTF8.GetBytes(id);
        fixed (byte* text = bytes)
            return _api->QueryCapability(
                new F4ForgeStringView { Data = text, Length = (uint)bytes.Length }, minimumVersion);
    }
}
