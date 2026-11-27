# F4Forge

F4Forge loads .NET plugins when F4SE starts Fallout 4 on Windows x64.

## Write a plugin

Target .NET 10 and reference `F4Forge.Net.dll` from the SDK build
(`sdk/dotnet/F4Forge.Net/bin/Release/net10.0/`). F4Forge embeds this assembly
for use at runtime. Each plugin DLL must contain one
public, concrete `IMod` class with a public parameterless constructor. The loader
calls `Initialize` once and passes the absolute path to the folder containing the DLL.

```csharp
using System.IO;
using F4Forge.Net.Mods;

public sealed class MyPlugin : IMod
{
    public void Initialize(string directory)
    {
        File.WriteAllText(Path.Combine(directory, "loaded.txt"), "Plugin initialized.");
    }
}
```

Place the DLL and its private dependencies together under `Data/F4ForgeNet/`.
The loader searches subfolders recursively, initializes plugins in path order,
and keeps their instances alive for the game session. Errors from an individual
plugin are recorded in `Data/F4SE/Plugins/F4Forge/Net.log` and discovery continues.
Plugins built for the previous SDK must be rebuilt against this interface.

## Build and test

Use MSVC with C++23 support, xmake 3.1 or later, and the .NET 10 x64 SDK.

```powershell
git submodule update --init --recursive
xmake f -m releasedbg -y
xmake -j 2
xmake run F4Forge.Hosting.Tests
dotnet test tests/dotnet/F4Forge.Net.Tests/F4Forge.Net.Tests.csproj -c Release
```

The native hosting test installs only `F4Forge.dll` and calls its exported F4SE
entry point with a test interface. It starts the real CLR and checks that an
external plugin initializes from a nested directory using a private dependency.

## Install

Players need F4SE and the .NET 10 x64 runtime. For a manual install, copy
`build/dist/F4Forge.dll` to `Data/F4SE/Plugins/`. For Mod Organizer 2, run
`./build-mo2.ps1` and install the archive from `build/packages/`. When upgrading,
remove the old `F4Forge.Core.dll` so F4SE starts the loader only once.

```text
Data/
  F4SE/Plugins/
    F4Forge.dll
  F4ForgeNet/
    MyPlugin/
      MyPlugin.dll
```
