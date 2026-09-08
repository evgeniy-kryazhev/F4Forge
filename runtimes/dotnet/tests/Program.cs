using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using F4Forge.DotNet.Runtime;
using F4Forge.DotNet.Sdk;
using F4Forge.FailureFixture;
using F4Forge.PluginFixture;

internal static unsafe class Program
{
    private static int Main()
    {
        if (sizeof(NativeApi) != 104 || sizeof(F4ForgeEndpointDefinition) != 64 || sizeof(F4ForgeStringView) != 16 ||
            sizeof(ManagedBootstrapArgs) != 56)
            return 5;

        delegate* unmanaged[Cdecl]<nint, int> initialize = &Bootstrap.Initialize;
        if (initialize(0) != (int)F4ForgeResult.InvalidArgument)
            return 1;

        NativeApi host = new()
        {
            AbiVersion = 1,
            StructSize = (uint)sizeof(NativeApi),
            ResolveEndpoint = &ResolveEndpoint,
            Invoke = &Invoke
        };
        ManagedBootstrapArgs bootstrapArgs = new()
        {
            AbiVersion = 1,
            StructSize = (uint)sizeof(ManagedBootstrapArgs),
            Host = &host,
            Runtime = 1
        };
        if (initialize((nint)(&bootstrapArgs)) != (int)F4ForgeResult.Success)
            return 11;

        var plugin = new TestPlugin();
        if (plugin.Id != "test.plugin")
            return 2;

        plugin.OnLoad();
        if (!plugin.Loaded)
            return 3;

        plugin.OnUnload();
        if (plugin.Loaded)
            return 4;

        var pluginDirectory = Path.Combine(Path.GetTempPath(), "f4forge-plugin-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pluginDirectory);
        var copiedFixture = Path.Combine(pluginDirectory, "Fixture.dll");
        File.Copy(typeof(FixturePlugin).Assembly.Location, copiedFixture);
        var loader = new PluginLoader(2);
        if (loader.LoadDirectory(pluginDirectory) != 1 || loader.Count != 1 || !loader.IsActive("fixture.plugin"))
            return 6;
        var failurePath = typeof(FailurePlugin).Assembly.Location;
        if (loader.Load(failurePath))
            return 9;
        if (!loader.Reload("fixture.plugin") || loader.Count != 1)
            return 7;
        loader.Dispatch(_ => throw new InvalidOperationException("callback failure"));
        loader.Dispatch(_ => throw new InvalidOperationException("callback failure"));
        if (loader.IsActive("fixture.plugin"))
            return 10;
        loader.UnloadAll();
        if (loader.Count != 0)
            return 8;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        return 0;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static ulong ResolveEndpoint(F4ForgeStringView name, uint version) => 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Invoke(ulong endpoint, void* request, uint requestSize, void* response, uint responseCapacity, uint* responseSize)
        => (int)F4ForgeResult.Success;

    private sealed class TestPlugin : F4ForgePlugin
    {
        public bool Loaded { get; private set; }
        public override string Id => "test.plugin";
        public override void OnLoad() => Loaded = true;
        public override void OnUnload() => Loaded = false;
    }
}
