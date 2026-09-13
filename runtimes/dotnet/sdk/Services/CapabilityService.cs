namespace F4Forge.DotNet.Sdk;

public sealed class CapabilityService
{
    private readonly IPluginHostBridge? _bridge;

    internal CapabilityService(IPluginHostBridge? bridge) => _bridge = bridge;

    public uint Query(string id, uint minimumVersion = 1)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        return _bridge?.QueryCapability(id, minimumVersion) ?? 0;
    }
}
