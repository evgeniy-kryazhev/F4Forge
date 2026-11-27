# CommonLibF4 bindings

Building F4Forge from source requires the Windows x64 LLVM SDK, including `include/clang-c/Index.h`, `lib/libclang.lib`, `bin/libclang.dll` and Clang's resource headers. LLVM 21.1.8 was used with the pinned CommonLibF4 submodule. Set `LLVM_ROOT` to the SDK directory or configure XMake with `xmake f --llvm_dir="C:\Program Files\LLVM"`. LLVM is a build-time dependency. XMake links libffi into the native host.

`xmake build F4Forge.Bindings` regenerates the catalog. Building the host or the managed SDK also runs this target. Outputs are in `build/generated/bindings`:

- `AutoRegistry.cpp` registers typed C++ thunks in the host's method registry.
- `AutoEventSinks.h` projects CommonLibF4 event fields for the native sinks.
- `Bindings.g.cs` exposes typed wrappers in `F4Forge.Bindings` and event payloads in `F4Forge.Events`.
- `API.md` lists call names, return types, event names, payload fields and omitted members.

The input is the umbrella header `RE/Fallout.h`. The parser visits public inline and virtual methods of non-template classes and structs declared directly in namespace `RE`. Supported wire types include bool, signed and unsigned 32-bit integers, float, enums with a supported 32-bit underlying type, void returns, and borrowed pointers or reference arguments to supported RE records.

For example, a plugin holding a live `RE::NiPoint3*` as `nint point` can call either `Bridge.Invoke<float>("NiPoint3.SqrLength", point)` or `Bindings.NiPoint3.SqrLength(point)`. The full signature `RE::NiPoint3::SqrLength() const` stays valid when overloads are added. Overloaded short names carry a signature suffix.

Instance calls require the address of the **declaring C++ subobject**. Multiple inheritance can give a base subobject a different address from the complete object. The bridge checks argument tags, counts and required non-null references. The caller is responsible for native object type, lifetime and game-thread requirements.

## Subscribing to game events

The parser discovers public static methods and namespace functions named `GetEventSource()` returning a pointer to `RE::BSTEventSource<T>` or `RE::BSTGlobalEvent::EventSource<T>`. F4Forge attaches a typed sink when F4SE signals that game data is ready. Plugins can subscribe in `IPlugin.Start` and keep the returned `IDisposable` for the desired lifetime.

```csharp
var subscription = Bridge.On<Events.TESDeathEvent>(Events.TESDeathEvent.Name, (sender, args) =>
{
    if (args.Dead)
    {
        Console.WriteLine($"Actor 0x{args.ActorDying:X} died.");
    }
});
```

Handlers execute synchronously on the engine thread that raised the event. The SDK copies scalar field values before invoking a handler. The sender is the source address. Pointer and `NiPointer<T>` fields expose borrowed addresses as `nint`; retaining a payload does not extend an engine object's lifetime. Sources with no projected fields still publish typed notifications. Custom plugin events continue to use `Bridge.Publish<T>` and `Bridge.On<T>`.
