# SDK Naming Standard

Names describe a type's domain or responsibility. Apply the rules below to new SDK
APIs and use the facade table as a reference for existing subsystems.

## Handwritten Subsystem Facades

Handwritten facades for game subsystems use `Game<DomainName>`, such as
`GameConsole`, `GamePapyrus`, and `GameThread`. Choose a domain noun that describes
the subsystem. Do not append `Service`, `Client`, `Manager`, or `Singleton` to these
facade names. Singleton lifetime and the underlying transport are implementation
details, not naming criteria.

This rule applies to subsystem facades obtained through `context.GetSingleton<T>()`.
It does not require every SDK type to start with `Game`.

## Generated CommonLibF4 Wrappers

Generated wrappers retain CommonLibF4 type names, such as `UI`, `PlayerCamera`, and
`PlayerControls`. They do not receive an additional `Game` prefix or a facade suffix.
The generator removes a leading `RE::` and replaces remaining namespace or nested
type separators (`::`) with underscores (`_`). For example, `RE::UI` becomes `UI`,
and the hypothetical native name `RE::Outer::Inner` becomes `Outer_Inner`.

These rules describe the existing generator behavior; they do not introduce a
generator change.

## Domain Types and Infrastructure

Game entities and values retain descriptive domain names such as `Actor`, `Form`,
and `PapyrusObject`. They do not receive a `Game` prefix merely because they are
part of the SDK.

Infrastructure types use their concrete responsibility, such as `Channel`,
`Serializer`, `Scheduler`, or `Loader`. Role names are appropriate when they explain
what the type does. In particular, the scheduling implementation must have a name
distinct from the public `GameThread` subsystem facade. The existing
`GameThreadScheduler` is that separate implementation, not a subsystem facade.

## Casing and Files

Every type implementing `IGameEvent` ends with `Event`, for example `SaveLoadedEvent`,
`ActorDiedEvent`, or `KeyDownEvent`. This includes private implementation types,
test fixtures, and custom event contracts in examples. Source files for standalone
event types use the same name, such as `SaveLoadedEvent.cs`. Renaming a managed event
type does not change its stable `EventName` or serialized payload.

- Types and methods use `PascalCase`.
- Interfaces use `I` followed by a descriptive contract name.
- Private fields use `_camelCase`.
- Source files match their type names. A separate partial declaration containing a
  singleton factory uses `<TypeName>.Singleton.cs`.
- Generated aggregate files, such as `Singletons.g.cs`, retain their generator-defined
  names.

## Subsystem Facade Names

The following facade names are the current API. Import the listed namespace to
use each type with `context.GetSingleton<T>()`.

| Facade | Namespace |
| --- | --- |
| `GameConsole` | `F4Forge.Net.Console` |
| `GameDiagnostics` | `F4Forge.Net.Diagnostics` |
| `GameObjects` | `F4Forge.Net.Objects` |
| `GameInterception` | `F4Forge.Net.Interception` |
| `GamePapyrus` | `F4Forge.Net.Papyrus` |
| `GameThread` | `F4Forge.Net.Singletons` |

For example, resolve the console and Papyrus facades from a mod context:

```csharp
using F4Forge.Net.Console;
using F4Forge.Net.Papyrus;

var console = context.GetSingleton<GameConsole>();
var papyrus = context.GetSingleton<GamePapyrus>();
```

Facade names do not affect native channel routes, code generation, or runtime
lifetime rules.
