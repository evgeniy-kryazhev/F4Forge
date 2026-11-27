# Working on F4Forge

F4Forge is an F4SE host for C# plugins in Fallout 4. The native host uses C++23 and CommonLibF4. The managed SDK targets .NET 10 on Windows x64.

## Extending the SDK

- For a plugin-facing feature, first look up the method or event in `build/generated/bindings/API.md`. Implement the convenience API in C# using the generated `Bindings` methods and `Events` payloads. For example, `GameEvents.OnGameLoaded` wraps `Events.TESLoadGameEvent`.
- Match each SDK namespace to its feature folder under `sdk/src/`. For example, `Input/InputEvents.cs` belongs to `F4Forge.Input` and `Plugins/IPlugin.cs` belongs to `F4Forge.Plugins`. Use `F4Forge.Bridge` for the managed bridge in `sdk/src/bridge/` and its generated `Bindings` and `Events` types. Use *plugin* for a managed extension and `PluginLoader` for its loader.
- Preserve direct access for plugin authors through `Bindings`, `Bridge.Invoke<T>`, and `Bridge.On<T>`. Callbacks run synchronously on the notifying thread. Native addresses in payloads are borrowed.
- If the catalog lacks a required operation, check the pinned CommonLibF4 headers and the generator's discovery rules. Explain the gap when the request is scoped to the SDK. When extending coverage, implement the rule in `tools/HeaderParser/` so libclang generates the native registration and matching C# declarations together. Treat files under `build/generated/bindings/` as build outputs and `API.md` as reference documentation.

## Code readability

Use descriptive names, conventional formatting, and explicit control flow and error handling in every language. Give each logical step its own line. Use concise comments to explain non-obvious decisions, lifetime requirements, or invariants.

## Building and testing

Run commands from the repository root. Choose the check that exercises the changed layer.

| Task | Command |
| --- | --- |
| Regenerate the CommonLibF4 catalog | `xmake build F4Forge.Bindings` |
| Run managed tests | `dotnet test F4Forge.slnx -c Release` |
| Build and exercise the native host with a C# test plugin | `xmake build F4Forge.Hosting.Tests` followed by `xmake run F4Forge.Hosting.Tests` |
| Build the distributable host | `xmake build F4Forge` |
| Build the example plugin | `dotnet build examples/HelloWorld/HelloWorld.csproj -c Release` |

## Deployment

Install `build/dist/F4Forge.dll` in `Data/F4SE/Plugins/`. Install managed plugins in `Data/F4Forge/` or its subdirectories. The host embeds the managed SDK. When a plugin starts using a newly generated type, deploy the corresponding host build with it. Check startup, plugin count, and plugin messages in `Data/F4SE/Plugins/F4Forge/Net.log`.
