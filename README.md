# F4Forge

F4Forge is an experimental/pre-alpha .NET modding framework for Fallout 4. It provides a C++23 native core, a C ABI for runtime providers, a .NET 10 runtime, a C# SDK, and collectible AssemblyLoadContext-based plugin loading.

## Requirements

- Windows x64
- Visual Studio 2022 or newer with MSVC C++23 support
- .NET SDK 10
- xmake 3.x
- cppcheck for native analysis
- F4SE/CommonLibF4, checked out through submodules

Clone the repository with dependencies:

```powershell
git clone --recurse-submodules https://github.com/evgeniy-kryazhev/F4Forge.git
cd F4Forge
```

## Build

The build script discovers the .NET native host pack automatically when possible:

```powershell
.\scripts\build.ps1
```

Use `-Clean` for a clean output directory or `-DotNetHostDir` to provide an explicit `nethost.h` directory.

The package is written to:

```text
build/F4SE/Plugins/F4Forge.dll
build/F4SE/Plugins/F4Forge/F4Forge.Dotnet.dll
build/F4SE/Plugins/F4Forge/F4Forge.DotNet.Sdk.dll
```

Managed runtime assemblies and configuration are embedded into the native provider resources.

## Tests and Analysis

```powershell
.\scripts\test.ps1
.\scripts\cppcheck.ps1
```

The test script builds native and managed targets, runs native executables, runs managed tests, and verifies `dotnet format`.

## Plugin Manifest

Plugins may include `f4forge.plugin.json`:

```json
{
  "id": "example.plugin",
  "version": "1.0.0",
  "minimumF4ForgeVersion": "0.1.0",
  "runtime": "dotnet",
  "entryAssembly": "Example.Plugin.dll",
  "dependencies": [],
  "providedCapabilities": []
}
```

The loader validates framework compatibility, rejects malformed manifests and path traversal, checks dependencies, detects cycles, and loads dependencies before dependents.

## C# Plugin

```csharp
using F4Forge.DotNet.Sdk;

public sealed class ExamplePlugin : F4ForgePlugin
{
    public override string Id => "example.plugin";

    public override void OnLoad(F4ForgePluginContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
    }
}
```

Resources registered with `context.Track` are disposed during unload. Plugin dispatch is quiesced before its collectible AssemblyLoadContext is unloaded.

## ABI and Threading Policy

The current public host ABI is version 2. C ABI structures use fixed-width fields and explicit `structSize` checks. The runtime-provider ABI remains version 1 because its binary contract has not changed.

Synchronous APIs never perform implicit cross-thread marshalling. `GAME_ONLY` calls from a worker thread return `F4FORGE_RESULT_WRONG_THREAD`. Explicit operation APIs such as `InvokeAsync` and `EmitAsync` provide scheduling and operation handles.

Async operations copy caller buffers, use generation-based handles, support cancellation, and never enqueue raw pointers to unloadable plugin code. A target dispatch lease is acquired only immediately before execution.

## Hot Reload

The framework prevents new dispatch leases after an owner enters quiescing and waits for active leases before unload. Pending operations can be cancelled; running native callbacks are not forcefully interrupted.

Plugins must not retain unmanaged threads, static references, or external callbacks after unload. The framework tracks resources explicitly registered through `F4ForgePluginContext`; arbitrary plugin-created roots remain the plugin's responsibility.

## License

F4Forge is released under the MIT License. See `LICENSE`. Third-party dependencies retain their own licenses.
