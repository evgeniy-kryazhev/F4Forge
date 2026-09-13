# F4Forge ABI

The normative declarations are [`native/abi/f4forge_abi.h`](../native/abi/f4forge_abi.h) and [`native/abi/f4forge_runtime_abi.h`](../native/abi/f4forge_runtime_abi.h). Their C++ `static_assert`s and the managed [`AbiContractTests`](../runtimes/dotnet/tests/AbiContractTests.cs) enforce the x64 layouts below.

## Contract

- Host ABI version: `3`.
- Runtime-provider ABI version: `2`.
- Target: Windows x64.
- Public structures use C linkage, fixed-width integer fields and `F4FORGE_CALL` (`__cdecl` with MSVC).
- Handles are opaque unsigned 64-bit generation/index values. Zero is invalid.
- String and byte views contain a pointer and byte length; strings are UTF-8 and need not be NUL terminated.
- There is no host ABI v2 compatibility shim.

Compatible additions within an ABI version are append-only. Consumers validate `abiVersion` and `structSize` before reading fields and use `F4FORGE_HAS_FIELD` for optional tails.

## Host binding and isolation

`F4ForgeGetHostApi` returns a 24-byte binding by value:

```c
typedef struct F4ForgeHostBinding {
    uint32_t abiVersion;             /* offset 0 */
    uint32_t structSize;             /* offset 4 */
    const F4ForgeHostApi* api;       /* offset 8 */
    void* context;                   /* offset 16 */
} F4ForgeHostBinding;

F4ForgeHostBinding F4FORGE_CALL F4ForgeGetHostApi(void);
```

The binding couples an immutable function table with one host instance. Every `F4ForgeHostApi` function receives that binding's `context` as its first argument; callers must not substitute a global or another binding's context. This makes the API table stateless and routes all mutable state through the composition-root-created `F4ForgeHost` instance, so independent hosts used by integrations and tests remain isolated.

The binding and table are borrowed. Keep the DLL and owning host alive through every call. `F4ForgeGetHostApi` does not initialize F4SE or runtime providers. User callbacks retained by endpoint/event registration have their separate callback context at the position declared by their callback typedef; the context-first rule above applies to host-table calls.

The current `F4ForgeHostApi` is 168 bytes. Bytes `0..159` are the required managed-host prefix; `waitModuleQuiescence` at offset 160 is usable only when `structSize >= 168` and its pointer is non-null.

## Layouts

All offsets are decimal bytes on Windows x64.

| Structure | Size | Field offsets |
|---|---:|---|
| `F4ForgeStringView`, `F4ForgeByteView` | 16 | pointer `0`, length `8` |
| `F4ForgeHostBinding` | 24 | `abiVersion 0`, `structSize 4`, `api 8`, `context 16` |
| `F4ForgeHostApi` | 168 | header `0,4`; callbacks start at `8` and advance by 8 through `waitModuleQuiescence 160` |
| `F4ForgeEndpointDefinition` | 64 | header/kind/version/flags/thread/sizes `0..28`; `name 32`, `thunk 48`, `context 56` |
| `F4ForgeRuntimeInfo` | 48 | `abiVersion 0`, `structSize 4`, `id 8`, `name 24`, `providerVersion 40` |
| `F4ForgeRuntimeInitializeParams` | 72 | `abiVersion 0`, `structSize 4`, `host 8`, `runtime 32`, `pluginDirectory 40`, `configDirectory 56` |
| `F4ForgeManagedBootstrapArgs` | 72 | `abiVersion 0`, `structSize 4`, `host 8`, `pluginDirectory 32`, `configDirectory 48`, `runtime 64` |
| `F4ForgeRuntimeProvider` | 40 | `abiVersion 0`, `structSize 4`, `info 8`, `initialize 16`, `shutdown 24`, `executeTask 32` |
| `F4ForgeRuntimeTask` | 16 | `runtime 0`, `taskHandle 8` |
| `F4ForgeKeyEventData` | 28 | `structSize 0`, `deviceType 4`, `keyCode 8`, `isDown 12`, `isRepeat 16`, `heldSeconds 20`, `isMenu 24` |

Runtime-provider ABI v2 bootstraps a provider with `F4ForgeRuntimeInitializeParams`, including the complete host binding. The .NET native provider then passes the reordered `F4ForgeManagedBootstrapArgs` to managed `Bootstrap.Initialize`. Both are 72 bytes but have different post-binding field order; they are not interchangeable. Providers expose `F4ForgeDescribeRuntime`, and the provider, info, initialize params and managed bootstrap args all carry runtime-provider ABI version `2`.

## Scheduling

`queueTask(hostContext, runtime, taskHandle)` transfers only numeric values. The managed runtime owns the `taskHandle -> delegate/completion/cancellation` map; no delegate or caller-owned pointer crosses the ABI.

```text
context.GameThread.InvokeAsync(delegate, token)
  -> managed scheduler stores taskHandle -> work item
  -> host.queueTask(context, runtime, taskHandle)
  -> native runtime admission + game-thread scheduler post
  -> runtime provider executeTask(runtime, taskHandle)
  -> managed scheduler removes work item
  -> delegate executes and completes its Task
```

Scheduling ownership and shutdown rules:

- Managed code owns a work item until exactly one execution, cancellation, native rejection, or scheduler disposal removes it.
- Cancellation removes a pending managed work item and completes its `Task`; an already posted native numeric task may later become a harmless no-op.
- Native `queueTask` admits only the matching active runtime generation. Admission is held while posting so shutdown cannot pass the enqueue boundary concurrently.
- The native scheduler owns its copied `{runtime, taskHandle}` record after a successful post and cleans it after execution or scheduler cancellation.
- Immediately before calling the provider, `RuntimeManager::ExecuteTask` revalidates the generation and active state and holds a task user through `executeTask`.
- Quiescing rejects admission, waits for admission/execution users, and makes queued stale-generation work inert. Managed disposal rejects new work and cancels every remaining work item.

Async endpoint operations similarly copy request/payload bytes, use generation-checked operation handles, support cancellation and acquire a module dispatch lease only immediately before callback execution. Quiescing prevents new leases; cancellation does not forcefully interrupt a callback already running.

## Event flow

Native framework/input subscriptions enter one runtime-owned router and are dispatched to isolated plugin contexts:

```text
native event endpoint
  -> NativeHostBridge subscription callback
  -> managed event router (payload validation/conversion)
  -> PluginDispatcher snapshot + active dispatch lease
  -> plugin's PluginEvents/InputEvents instance
  -> plugin handler
```

Each plugin has its own collectible `PluginLoadContext`, plugin instance, `F4ForgePluginContext`, event collections, resource scope, cancellation token and module bridge. The runtime composition root shares discovery/dependency services, native event routers, registry/dispatcher/unloader and the runtime task scheduler; `PluginGameThreadScheduler` binds shared scheduling to each plugin's lifetime token.

## Ownership and lifetime

Input views transfer no ownership. Endpoint names are copied when registered; callback and callback-context pointers are retained according to endpoint/module lifetime. Async request and payload bytes are copied before deferred execution. Provider metadata must remain valid for the provider's advertised lifetime.

Built-in native framework/input handlers have process lifetime in the F4SE integration. They are registered once and intentionally remain installed until process teardown because F4SE has no exit-process message. Explicit `F4ForgeHost::Shutdown()` exists for integrations and tests; managed plugin resources are disposed during runtime shutdown/reload.

## Input event v1

The native host publishes game-thread endpoint `input.key.down`, version `1`, with the 28-byte `F4ForgeKeyEventData`. It includes keyboard button-down events only. Autorepeat sets `isRepeat`; unknown codes map to `Key.Unknown`. `isMenu` is true for the main-menu sink and false for gameplay, and one physical path is never published to both. Managed handlers are synchronous on the game thread and must not block. Mouse, gamepad, key-up, suppression and additional input contexts are not part of v1.
