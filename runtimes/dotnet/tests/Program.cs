using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using F4Forge.DotNet.Runtime;
using F4Forge.DotNet.Sdk;
using F4Forge.FailureFixture;
using F4Forge.PluginFixture;

internal static unsafe class Program
{
    private static readonly uint[] LogLevels = new uint[8];
    private static int logCount;

    private static int Main()
    {
        if (sizeof(NativeApi) != 160 || sizeof(F4ForgeEndpointDefinition) != 64 || sizeof(F4ForgeStringView) != 16 ||
            sizeof(ManagedBootstrapArgs) != 56)
            return 5;

        delegate* unmanaged[Cdecl]<nint, int> initialize = &Bootstrap.Initialize;
        if (initialize(0) != (int)F4ForgeResult.InvalidArgument)
            return 1;

        NativeApi host = new()
        {
            AbiVersion = 2,
            StructSize = (uint)sizeof(NativeApi),
            ResolveEndpoint = &ResolveEndpoint,
            Invoke = &Invoke,
            Log = &Log
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

        logCount = 0;
        Logger.Trace("trace");
        Logger.Warning("warning");
        Logger.Error("error");
        if (logCount != 3 || LogLevels[0] != 0 || LogLevels[1] != 3 || LogLevels[2] != 4)
            return 15;

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
        var duplicatePath = Path.Combine(pluginDirectory, "Duplicate.dll");
        File.Copy(typeof(FixturePlugin).Assembly.Location, duplicatePath);
        if (loader.Load(duplicatePath) || loader.Count != 1)
            return 12;
        var failurePath = typeof(FailurePlugin).Assembly.Location;
        if (loader.Load(failurePath))
            return 9;

        var manifestRoot = Path.Combine(Path.GetTempPath(), "f4forge-manifest-tests", Guid.NewGuid().ToString("N"));
        var manifestDirectory = Path.Combine(manifestRoot, "ManifestPlugin");
        Directory.CreateDirectory(manifestDirectory);
        var manifestAssembly = Path.Combine(manifestDirectory, "ManifestPlugin.dll");
        File.Copy(typeof(FixturePlugin).Assembly.Location, manifestAssembly);
        File.WriteAllText(Path.Combine(manifestDirectory, "f4forge.plugin.json"),
            "{\"id\":\"fixture.plugin\",\"version\":\"1.0.0\",\"runtime\":\"dotnet\",\"entryAssembly\":\"ManifestPlugin.dll\"}");
        var manifestLoader = new PluginLoader();
        if (manifestLoader.LoadDirectory(manifestRoot) != 1 || !manifestLoader.IsActive("fixture.plugin"))
            return 14;
        manifestLoader.UnloadAll();
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
        var collectiblePath = typeof(FixturePlugin).Assembly.Location;
        var contextReference = LoadAndUnloadContext(collectiblePath);
        for (var attempt = 0; attempt < 3 && contextReference.IsAlive; ++attempt)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        if (contextReference.IsAlive)
            return 13;
        delegate* unmanaged[Cdecl]<void> shutdown = &Bootstrap.Shutdown;
        shutdown();
        return 0;
    }

    private static WeakReference LoadAndUnloadContext(string path)
    {
        var context = new PluginLoadContext(path);
        _ = context.LoadFromAssemblyPath(Path.GetFullPath(path));
        var reference = new WeakReference(context);
        context.Unload();
        return reference;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static ulong ResolveEndpoint(F4ForgeStringView name, uint version) => 1;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Invoke(ulong endpoint, void* request, uint requestSize, void* response, uint responseCapacity, uint* responseSize)
        => (int)F4ForgeResult.Success;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void Log(uint level, F4ForgeStringView message)
    {
        if (logCount < LogLevels.Length) LogLevels[logCount++] = level;
    }

    private sealed class TestPlugin : F4ForgePlugin
    {
        public bool Loaded { get; private set; }
        public override string Id => "test.plugin";
        public override void OnLoad() => Loaded = true;
        public override void OnUnload() => Loaded = false;
    }
}
