using F4Forge.DotNet.Runtime;
using F4Forge.DotNet.Sdk;

internal static unsafe class Program
{
    private static int Main()
    {
        if (sizeof(NativeApi) != 104 || sizeof(F4ForgeEndpointDefinition) != 64 || sizeof(F4ForgeStringView) != 16)
            return 5;

        delegate* unmanaged[Cdecl]<nint, int> initialize = &Bootstrap.Initialize;
        if (initialize(0) != (int)F4ForgeResult.InvalidArgument)
            return 1;

        var plugin = new TestPlugin();
        if (plugin.Id != "test.plugin")
            return 2;

        plugin.OnLoad();
        if (!plugin.Loaded)
            return 3;

        plugin.OnUnload();
        return plugin.Loaded ? 4 : 0;
    }

    private sealed class TestPlugin : F4ForgePlugin
    {
        public bool Loaded { get; private set; }
        public override string Id => "test.plugin";
        public override void OnLoad() => Loaded = true;
        public override void OnUnload() => Loaded = false;
    }
}
