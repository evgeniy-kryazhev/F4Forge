# Migrating from host ABI v2 to v3

F4Forge is pre-alpha and ABI v3 is intentionally breaking. There is no v2 compatibility shim: consumers and runtime providers must migrate together.

## Acquire a binding

`F4ForgeGetHostApi` no longer returns a process-global table pointer. It returns `F4ForgeHostBinding` by value:

```c
F4ForgeHostBinding host = F4ForgeGetHostApi();
```

Validate `host.abiVersion == 3`, `host.structSize >= 24`, `host.api != NULL`, the table's version/required size, and required callbacks. Pass `host.context` as the first argument to every host-table call:

```c
F4ForgeEndpointHandle endpoint =
    host.api->resolveEndpoint(host.context, name, 1);
```

Do not cache a global context or mix `api` and `context` from different bindings. State now belongs to the host instance behind the binding, enabling isolated hosts and composition roots.

## Update callback signatures

Every `F4ForgeHostApi` function pointer now starts with `void* hostContext`. Add it before all previous parameters and forward the binding context. Endpoint/event/interceptor callback typedefs retain their independently supplied callback contexts exactly as declared in the v3 header.

The task call is now:

```c
F4ForgeResult queueTask(void* hostContext,
                        F4ForgeRuntimeHandle runtime,
                        uint64_t taskHandle);
```

Runtime-provider ABI v2 changes `executeTask` from a `F4ForgeRuntimeTask*` to value arguments:

```c
void executeTask(F4ForgeRuntimeHandle runtime, uint64_t taskHandle);
```

Keep delegate/task state behind the numeric identifier. Remove it exactly once on execution, cancellation or rejection, and cancel pending work during shutdown. Expect admission to fail once a runtime quiesces and stale-generation queued tasks not to execute.

## Update provider bootstrap layouts

Require `F4FORGE_RUNTIME_PROVIDER_ABI_VERSION == 2`. On x64:

- `F4ForgeRuntimeInitializeParams` is 72 bytes: host binding at `8`, runtime at `32`, plugin directory at `40`, config directory at `56`.
- `F4ForgeManagedBootstrapArgs` is 72 bytes: host binding at `8`, plugin directory at `32`, config directory at `48`, runtime at `64`.
- `F4ForgeRuntimeProvider` remains 40 bytes; `executeTask` is at offset `32`.

The two 72-byte bootstrap structures have different field order. Validate `abiVersion` and `structSize`, then validate the nested 24-byte host binding and host-table prefix before dereferencing callbacks.

## Update managed plugins

- Replace `context.Events.KeyDown` with `context.Input.KeyDown`.
- Event handlers use `EventHandler` and `EventHandler<KeyDownEventArgs>`.
- Use `context.GameThread.InvokeAsync(action, cancellationToken)` for explicit game-thread dispatch.
- Prefer `context.Endpoints` and `context.Capabilities`; forwarding methods remain on `F4ForgePluginContext` during the pre-alpha transition.
- Treat `F4ForgePluginContext` as the plugin's public lifetime boundary: it exposes `Module`, `CancellationToken`, `Events`, `Input`, `Endpoints`, `Capabilities`, `GameThread` and `Track`.

The managed runtime is now decomposed into discovery/dependency resolution, assembly activation, instance registry, dispatch, unload, native event routing and task scheduling. Each plugin receives isolated context/event/resource objects and a collectible load context rather than sharing mutable SDK context state.
