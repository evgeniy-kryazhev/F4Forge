using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using F4Forge.DotNet.Runtime;
using F4Forge.DotNet.Runtime.Interop;
using F4Forge.DotNet.Runtime.Plugins;
using F4Forge.DotNet.Sdk;
using F4Forge.FailureFixture;
using F4Forge.PluginFixture;

internal static unsafe class Program
{
    private static readonly uint[] LogLevels = new uint[8];
    private static int logCount;

    internal static int Run()
    {
        if (sizeof(NativeApi) != 168 || sizeof(F4ForgeEndpointDefinition) != 64 || sizeof(F4ForgeStringView) != 16 ||
            sizeof(ManagedBootstrapArgs) != 56)
            return 5;
        if (sizeof(F4ForgeKeyEventData) != 28 ||
            Marshal.OffsetOf<F4ForgeKeyEventData>(nameof(F4ForgeKeyEventData.StructSize)) != 0 ||
            Marshal.OffsetOf<F4ForgeKeyEventData>(nameof(F4ForgeKeyEventData.DeviceType)) != 4 ||
            Marshal.OffsetOf<F4ForgeKeyEventData>(nameof(F4ForgeKeyEventData.KeyCode)) != 8 ||
            Marshal.OffsetOf<F4ForgeKeyEventData>(nameof(F4ForgeKeyEventData.IsDown)) != 12 ||
            Marshal.OffsetOf<F4ForgeKeyEventData>(nameof(F4ForgeKeyEventData.IsRepeat)) != 16 ||
            Marshal.OffsetOf<F4ForgeKeyEventData>(nameof(F4ForgeKeyEventData.HeldSeconds)) != 20 ||
            Marshal.OffsetOf<F4ForgeKeyEventData>(nameof(F4ForgeKeyEventData.IsMenu)) != 24 ||
            (uint)Key.Unknown != 0 || (uint)Key.A != 0x41 || (uint)Key.F12 != 0x7B ||
            (uint)Key.Apostrophe != 0xDE)
            return 30;
        if (Marshal.OffsetOf<NativeApi>(nameof(NativeApi.AbiVersion)) != 0 ||
            Marshal.OffsetOf<NativeApi>(nameof(NativeApi.StructSize)) != 4 ||
            Marshal.OffsetOf<NativeApi>(nameof(NativeApi.ResolveEndpoint)) != 8 ||
            Marshal.OffsetOf<NativeApi>(nameof(NativeApi.Invoke)) != 16 ||
            Marshal.OffsetOf<NativeApi>(nameof(NativeApi.InvokeAsync)) != 104 ||
            Marshal.OffsetOf<NativeApi>(nameof(NativeApi.ReleaseOperation)) != 152 ||
            Marshal.OffsetOf<NativeApi>(nameof(NativeApi.WaitModuleQuiescence)) != 160 ||
            Marshal.OffsetOf<ManagedBootstrapArgs>(nameof(ManagedBootstrapArgs.Host)) != 8 ||
            Marshal.OffsetOf<ManagedBootstrapArgs>(nameof(ManagedBootstrapArgs.Runtime)) != 48 ||
            Marshal.OffsetOf<F4ForgeEndpointDefinition>(nameof(F4ForgeEndpointDefinition.Name)) != 32 ||
            Marshal.OffsetOf<F4ForgeEndpointDefinition>(nameof(F4ForgeEndpointDefinition.Thunk)) != 48 ||
            Marshal.OffsetOf<F4ForgeEndpointDefinition>(nameof(F4ForgeEndpointDefinition.Context)) != 56)
            return 27;

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
        host.AbiVersion = 1;
        if (initialize((nint)(&bootstrapArgs)) != (int)F4ForgeResult.InvalidAbiVersion)
            return 28;
        host.AbiVersion = 2;
        host.StructSize = 159;
        if (initialize((nint)(&bootstrapArgs)) != (int)F4ForgeResult.InvalidStructSize)
            return 29;
        host.StructSize = (uint)sizeof(NativeApi);
        if (initialize((nint)(&bootstrapArgs)) != (int)F4ForgeResult.Success)
            return 11;

        logCount = 0;
        Logger.Trace("trace");
        Logger.Warning("warning");
        Logger.Error("error");
        if (logCount != 3 || LogLevels[0] != 0 || LogLevels[1] != 3 || LogLevels[2] != 4)
            return 15;

        var input = new InputEvents("test.plugin");
        var firstHandlerCalls = 0;
        var lastHandlerCalls = 0;
        EventHandler<KeyDownEventArgs> firstHandler = (_, _) => ++firstHandlerCalls;
        EventHandler<KeyDownEventArgs> lastHandler = (_, _) => ++lastHandlerCalls;
        input.KeyDown += firstHandler;
        input.KeyDown += (_, _) => throw new InvalidOperationException("key handler failure");
        input.KeyDown += lastHandler;
        input.PublishKeyDown(new KeyDownEventArgs(InputDevice.Keyboard, Key.A, false, 0, false));
        if (firstHandlerCalls != 1 || lastHandlerCalls != 1) return 31;
        input.KeyDown -= firstHandler;
        input.KeyDown -= lastHandler;
        input.PublishKeyDown(new KeyDownEventArgs(InputDevice.Keyboard, Key.A, false, 0, false));
        if (firstHandlerCalls != 1 || lastHandlerCalls != 1) return 32;

        var otherInput = new InputEvents("other.plugin");
        var otherHandlerCalls = 0;
        otherInput.KeyDown += (_, _) => ++otherHandlerCalls;
        input.PublishKeyDown(new KeyDownEventArgs(InputDevice.Keyboard, Key.B, false, 0, false));
        if (otherHandlerCalls != 0) return 34;
        otherInput.PublishKeyDown(new KeyDownEventArgs(InputDevice.Keyboard, Key.B, false, 0, false));
        if (otherHandlerCalls != 1) return 35;

        var events = new PluginEvents("test.plugin");
        var lifecycleCalls = 0;
        events.GameDataReady += (_, _) => ++lifecycleCalls;
        events.GameLoaded += (_, _) => throw new InvalidOperationException("lifecycle handler failure");
        events.GameLoaded += (_, _) => ++lifecycleCalls;
        events.NewGame += (_, _) => ++lifecycleCalls;
        events.PublishGameDataReady();
        events.PublishGameLoaded();
        events.PublishNewGame();
        if (lifecycleCalls != 3) return 33;

        var plugin = new TestPlugin();
        if (plugin.Id != "test.plugin")
            return 2;

        plugin.OnLoad();
        if (!plugin.Loaded)
            return 3;

        var pluginDirectory = Path.Combine(Path.GetTempPath(), "f4forge-plugin-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pluginDirectory);
        var copiedFixture = Path.Combine(pluginDirectory, "Fixture.dll");
        File.Copy(typeof(FixturePlugin).Assembly.Location, copiedFixture);
        var loader = new PluginLoader(2, allowManifestlessPlugins: true);
        if (loader.LoadDirectory(pluginDirectory) != 1 || loader.Count != 1 || !loader.IsActive("fixture.plugin"))
            return 6;
        var manifestOnlyLoader = new PluginLoader();
        if (manifestOnlyLoader.LoadDirectory(pluginDirectory) != 0)
            return 26;
        var quarantineLoader = new PluginLoader();
        if (quarantineLoader.Load(copiedFixture) == false)
            return 21;
        var callbackEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCallback = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatchTask = Task.Run(() => quarantineLoader.Dispatch(_ =>
        {
            callbackEntered.TrySetResult(true);
            releaseCallback.Task.GetAwaiter().GetResult();
        }));
        if (!callbackEntered.Task.Wait(TimeSpan.FromSeconds(1))) return 22;
        var unloadTask = Task.Run(quarantineLoader.UnloadAll);
        if (!unloadTask.Wait(TimeSpan.FromSeconds(1)) ||
            !quarantineLoader.IsQuarantined("fixture.plugin")) return 23;
        releaseCallback.TrySetResult(true);
        if (!dispatchTask.Wait(TimeSpan.FromSeconds(1)) ||
            !SpinWait.SpinUntil(() => !quarantineLoader.IsQuarantined("fixture.plugin"), 1000)) return 24;
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

        var incompatibleRoot = Path.Combine(Path.GetTempPath(), "f4forge-incompatible-tests", Guid.NewGuid().ToString("N"));
        var incompatibleDirectory = Path.Combine(incompatibleRoot, "IncompatiblePlugin");
        Directory.CreateDirectory(incompatibleDirectory);
        File.Copy(typeof(FixturePlugin).Assembly.Location, Path.Combine(incompatibleDirectory, "Plugin.dll"));
        File.WriteAllText(Path.Combine(incompatibleDirectory, "f4forge.plugin.json"),
            "{\"id\":\"fixture.plugin\",\"entryAssembly\":\"Plugin.dll\",\"minimumF4ForgeVersion\":\"99.0.0\"}");
        if (new PluginLoader().LoadDirectory(incompatibleRoot) != 0)
            return 16;

        var malformedRoot = Path.Combine(Path.GetTempPath(), "f4forge-malformed-tests", Guid.NewGuid().ToString("N"));
        var malformedDirectory = Path.Combine(malformedRoot, "MalformedPlugin");
        Directory.CreateDirectory(malformedDirectory);
        File.Copy(typeof(FixturePlugin).Assembly.Location, Path.Combine(malformedDirectory, "Plugin.dll"));
        File.WriteAllText(Path.Combine(malformedDirectory, "f4forge.plugin.json"),
            "{\"id\":\"fixture.plugin\",\"entryAssembly\":\"Plugin.dll\",\"minimumF4ForgeVersion\":\"not-a-version\"}");
        if (new PluginLoader().LoadDirectory(malformedRoot) != 0)
            return 17;

        var missingDependencyRoot = Path.Combine(Path.GetTempPath(), "f4forge-dependency-tests", Guid.NewGuid().ToString("N"));
        var missingDependencyDirectory = Path.Combine(missingDependencyRoot, "DependentPlugin");
        Directory.CreateDirectory(missingDependencyDirectory);
        File.Copy(typeof(FixturePlugin).Assembly.Location, Path.Combine(missingDependencyDirectory, "Plugin.dll"));
        File.WriteAllText(Path.Combine(missingDependencyDirectory, "f4forge.plugin.json"),
            "{\"id\":\"fixture.plugin\",\"entryAssembly\":\"Plugin.dll\",\"dependencies\":[\"missing.plugin\"]}");
        if (new PluginLoader().LoadDirectory(missingDependencyRoot) != 0)
            return 18;

        var activationRoot = Path.Combine(Path.GetTempPath(), "f4forge-activation-tests", Guid.NewGuid().ToString("N"));
        var failedDirectory = Path.Combine(activationRoot, "Failed");
        var dependentDirectory = Path.Combine(activationRoot, "Dependent");
        Directory.CreateDirectory(failedDirectory);
        Directory.CreateDirectory(dependentDirectory);
        File.Copy(typeof(FailurePlugin).Assembly.Location, Path.Combine(failedDirectory, "Failure.dll"));
        File.Copy(typeof(FixturePlugin).Assembly.Location, Path.Combine(dependentDirectory, "Dependent.dll"));
        File.WriteAllText(Path.Combine(failedDirectory, "f4forge.plugin.json"),
            "{\"id\":\"failure.plugin\",\"entryAssembly\":\"Failure.dll\"}");
        File.WriteAllText(Path.Combine(dependentDirectory, "f4forge.plugin.json"),
            "{\"id\":\"dependent.plugin\",\"entryAssembly\":\"Dependent.dll\",\"dependencies\":[\"failure.plugin\"]}");
        var activationLoader = new PluginLoader();
        if (activationLoader.LoadDirectory(activationRoot) != 0 || activationLoader.Count != 0 ||
            activationLoader.IsActive("dependent.plugin"))
            return 19;

        var duplicateRoot = Path.Combine(Path.GetTempPath(), "f4forge-duplicate-manifest-tests", Guid.NewGuid().ToString("N"));
        var duplicateOne = Path.Combine(duplicateRoot, "One");
        var duplicateTwo = Path.Combine(duplicateRoot, "Two");
        Directory.CreateDirectory(duplicateOne);
        Directory.CreateDirectory(duplicateTwo);
        File.Copy(typeof(FixturePlugin).Assembly.Location, Path.Combine(duplicateOne, "One.dll"));
        File.Copy(typeof(FixturePlugin).Assembly.Location, Path.Combine(duplicateTwo, "Two.dll"));
        File.WriteAllText(Path.Combine(duplicateOne, "f4forge.plugin.json"),
            "{\"id\":\"duplicate.plugin\",\"entryAssembly\":\"One.dll\"}");
        File.WriteAllText(Path.Combine(duplicateTwo, "f4forge.plugin.json"),
            "{\"id\":\"duplicate.plugin\",\"entryAssembly\":\"Two.dll\"}");
        if (new PluginLoader().LoadDirectory(duplicateRoot) != 0)
            return 20;

        if (!loader.Reload("fixture.plugin") || loader.Count != 1)
            return 7;
        loader.Dispatch(_ => throw new InvalidOperationException("callback failure"));
        loader.Dispatch(_ => throw new InvalidOperationException("callback failure"));
        if (loader.IsActive("fixture.plugin"))
            return 10;
        if (!loader.Reload("fixture.plugin") || !loader.IsActive("fixture.plugin"))
            return 25;
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
    }
}
