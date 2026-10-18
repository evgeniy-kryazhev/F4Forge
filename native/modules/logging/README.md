# Logging module

This feature is added entirely through new files. `module.cpp` registers a native
component; the existing build discovers it under `native/modules/`. Components start
before SDKs and stop after SDKs. No changes to Core routing, interop, `ModContext`, or
the SDK project file are required.

The method `logging.write` accepts `level` (`info`, `warning`, `error`), `source`, and
`message`, and returns `true`. Both request and response use the shared serializer.
The native handler validates requests and writes through REX to the logger initialized
by F4SE. User text is passed as data, never as a format string.

```csharp
using F4Forge.Net.Logging;

// Inside IMod.Start(ModContext context):
context.LogInfo("Mod loaded");
context.LogWarning("Configuration is incomplete");
context.LogError("Could not load a resource");
```

Entries include the mod directory name, for example `[HelloMod] Mod loaded`.
The usual log location is `Documents/My Games/Fallout4/F4SE/F4Forge.Core.log`;
the actual save folder is supplied by F4SE. Logger filtering still applies.
Logging is synchronous and follows the lifetime of the mod's message channel.

For another feature, add its native component under `native/modules/<feature>/`
and a typed SDK extension under `sdk/dotnet/F4Forge.Net/<Feature>/`. Use a unique
method name. Register in Start and remove the registration in Stop. Run `xmake f`
after adding native files. Each feature owns its validation and implementation.

Managed regression tests are in `tests/dotnet/F4Forge.Net.Tests/LoggingTests.cs`.
The standalone `native/tests/logging_tests.cpp` exercises the production handler
with an injected sink; compile it with the Core sources and nlohmann include path.
It does not require a running game or a separate interop export.
